using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiteDB;
using NumSharp;

namespace AroAro.DataCore.LiteDb
{
    /// <summary>
    /// LiteDB 表格数据集实现
    /// </summary>
    public sealed class LiteDbTabularDataset : ITabularDataset
    {
        private readonly LiteDatabase _database;
        private readonly TabularMetadata _metadata;
        private readonly ILiteCollection<TabularRow> _rows;

        internal LiteDbTabularDataset(LiteDatabase database, TabularMetadata metadata)
        {
            _database = database;
            _metadata = metadata;
            _rows = _database.GetCollection<TabularRow>($"tabular_{metadata.Id}");
            _rows.EnsureIndex(r => r.RowIndex, true);
        }

        #region IDataSet 实现

        public string Name => _metadata.Name;
        public DataSetKind Kind => DataSetKind.Tabular;
        public string Id => _metadata.DatasetId;

        public IDataSet WithName(string name)
        {
            throw new NotSupportedException("Use IDataStore.CreateTabular and copy data instead");
        }

        #endregion

        #region ITabularDataset 实现

        public int RowCount => _metadata.RowCount;
        public int ColumnCount => _metadata.Columns.Count;
        public IReadOnlyCollection<string> ColumnNames => _metadata.Columns.Select(c => c.Name).ToList().AsReadOnly();

        #endregion

        #region 列操作

        public void AddNumericColumn(string name, double[] data)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Column name is required", nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            ValidateColumnLength(data.Length);
            EnsureColumn(name, "Numeric");

            if (_metadata.RowCount == 0)
            {
                // 创建行
                for (int i = 0; i < data.Length; i++)
                {
                    var row = new TabularRow { RowIndex = i, Data = new BsonDocument { { name, new BsonValue(data[i]) } } };
                    _rows.Insert(row);
                }
                _metadata.RowCount = data.Length;
            }
            else
            {
                // 更新现有行
                var existingRows = _rows.FindAll().OrderBy(r => r.RowIndex).ToList();
                for (int i = 0; i < Math.Min(existingRows.Count, data.Length); i++)
                {
                    existingRows[i].Data[name] = new BsonValue(data[i]);
                    _rows.Update(existingRows[i]);
                }
            }

            UpdateMetadata();
        }

        public void AddNumericColumn(string name, NDArray data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.ndim != 1)
                throw new ArgumentException("Only 1D arrays supported", nameof(data));

            AddNumericColumn(name, data.ToArray<double>());
        }

        public void AddStringColumn(string name, string[] data)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Column name is required", nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            ValidateColumnLength(data.Length);
            EnsureColumn(name, "String");

            if (_metadata.RowCount == 0)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    var row = new TabularRow { RowIndex = i, Data = new BsonDocument { { name, new BsonValue(data[i]) } } };
                    _rows.Insert(row);
                }
                _metadata.RowCount = data.Length;
            }
            else
            {
                var existingRows = _rows.FindAll().OrderBy(r => r.RowIndex).ToList();
                for (int i = 0; i < Math.Min(existingRows.Count, data.Length); i++)
                {
                    existingRows[i].Data[name] = new BsonValue(data[i]);
                    _rows.Update(existingRows[i]);
                }
            }

            UpdateMetadata();
        }

        public bool RemoveColumn(string name)
        {
            var col = _metadata.Columns.FirstOrDefault(c => c.Name == name);
            if (col == null) return false;

            _metadata.Columns.Remove(col);

            foreach (var row in _rows.FindAll())
            {
                row.Data.Remove(name);
                _rows.Update(row);
            }

            UpdateMetadata();
            return true;
        }

        public bool HasColumn(string name) => _metadata.Columns.Any(c => c.Name == name);

        public NDArray GetNumericColumn(string name)
        {
            if (!HasColumn(name))
                throw new KeyNotFoundException($"Column '{name}' not found");

            var data = _rows.FindAll().OrderBy(r => r.RowIndex)
                .Select(r => r.Data.TryGetValue(name, out var v) && !v.IsNull ? v.AsDouble : 0.0)
                .ToArray();

            return np.array(data);
        }

        public string[] GetStringColumn(string name)
        {
            if (!HasColumn(name))
                throw new KeyNotFoundException($"Column '{name}' not found");

            return _rows.FindAll().OrderBy(r => r.RowIndex)
                .Select(r => r.Data.TryGetValue(name, out var v) && !v.IsNull ? v.AsString : null)
                .ToArray();
        }

        public ColumnType GetColumnType(string name)
        {
            var col = _metadata.Columns.FirstOrDefault(c => c.Name == name);
            if (col == null) return ColumnType.Unknown;

            return col.Type switch
            {
                "Numeric" => ColumnType.Numeric,
                "String" => ColumnType.String,
                "Boolean" => ColumnType.Boolean,
                "DateTime" => ColumnType.DateTime,
                _ => ColumnType.Unknown
            };
        }

        #endregion

        #region 行操作

        public void AddRow(IDictionary<string, object> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            var row = new TabularRow
            {
                RowIndex = _metadata.RowCount,
                Data = ConvertToBsonDocument(values)
            };

            _rows.Insert(row);
            _metadata.RowCount++;
            UpdateMetadata();
        }

        public int AddRows(IEnumerable<IDictionary<string, object>> rows)
        {
            int count = 0;
            int startIndex = _metadata.RowCount;

            var rowDocs = rows.Select((values, i) => new TabularRow
            {
                RowIndex = startIndex + i,
                Data = ConvertToBsonDocument(values)
            }).ToList();

            if (rowDocs.Count > 0)
            {
                _rows.InsertBulk(rowDocs);
                _metadata.RowCount += rowDocs.Count;
                count = rowDocs.Count;
                UpdateMetadata();
            }

            return count;
        }

        public bool UpdateRow(int rowIndex, IDictionary<string, object> values)
        {
            if (rowIndex < 0 || rowIndex >= _metadata.RowCount)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            var row = _rows.FindOne(r => r.RowIndex == rowIndex);
            if (row == null) return false;

            foreach (var kv in values)
            {
                row.Data[kv.Key] = ConvertToBsonValue(kv.Value);
            }

            _rows.Update(row);
            UpdateMetadata();
            return true;
        }

        public bool DeleteRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _metadata.RowCount)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            var row = _rows.FindOne(r => r.RowIndex == rowIndex);
            if (row == null) return false;

            _rows.Delete(row.Id);

            // 更新后续行索引
            var subsequentRows = _rows.Find(r => r.RowIndex > rowIndex).ToList();
            foreach (var r in subsequentRows)
            {
                r.RowIndex--;
                _rows.Update(r);
            }

            _metadata.RowCount--;
            UpdateMetadata();
            return true;
        }

        public Dictionary<string, object> GetRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _metadata.RowCount)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            var row = _rows.FindOne(r => r.RowIndex == rowIndex);
            if (row == null) return null;

            var result = new Dictionary<string, object>();
            foreach (var kv in row.Data)
            {
                result[kv.Key] = ConvertFromBsonValue(kv.Value);
            }
            return result;
        }

        public IEnumerable<Dictionary<string, object>> GetRows(int startIndex, int count)
        {
            var rows = _rows.Find(r => r.RowIndex >= startIndex && r.RowIndex < startIndex + count)
                .OrderBy(r => r.RowIndex);

            foreach (var row in rows)
            {
                var result = new Dictionary<string, object>();
                foreach (var kv in row.Data)
                {
                    result[kv.Key] = ConvertFromBsonValue(kv.Value);
                }
                yield return result;
            }
        }

        public int Clear()
        {
            int count = _metadata.RowCount;
            _rows.DeleteAll();
            _metadata.RowCount = 0;
            UpdateMetadata();
            return count;
        }

        #endregion

        #region 查询

        public ITabularQuery Query() => new LiteDbTabularQuery(this);

        public int[] Where(string column, QueryOp op, object value)
        {
            return Query().Where(column, op, value).ToRowIndices();
        }

        #endregion

        #region CSV 导入导出

        public void ImportFromCsv(string csvContent, bool hasHeader = true, char delimiter = ',')
        {
            if (string.IsNullOrEmpty(csvContent))
                throw new ArgumentException("CSV content cannot be null or empty", nameof(csvContent));

            var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return;

            var headers = new List<string>();
            int dataStartIndex = 0;

            if (hasHeader)
            {
                headers = ParseCsvLine(lines[0], delimiter);
                dataStartIndex = 1;
            }

            var dataRows = new List<List<string>>();
            for (int i = dataStartIndex; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    dataRows.Add(ParseCsvLine(lines[i], delimiter));
            }

            if (dataRows.Count == 0) return;

            int columnCount = dataRows.Max(r => r.Count);
            if (!hasHeader)
            {
                for (int i = 0; i < columnCount; i++)
                    headers.Add($"Column{i}");
            }

            var columnTypes = new string[columnCount];
            for (int col = 0; col < columnCount; col++)
            {
                columnTypes[col] = DetermineColumnType(dataRows, col);
            }

            for (int col = 0; col < columnCount; col++)
            {
                var colName = col < headers.Count ? headers[col] : $"Column{col}";

                if (columnTypes[col] == "Numeric")
                {
                    var data = dataRows.Select(row =>
                        col < row.Count && double.TryParse(row[col], out var d) ? d : 0.0
                    ).ToArray();
                    AddNumericColumn(colName, data);
                }
                else
                {
                    var data = dataRows.Select(row =>
                        col < row.Count ? row[col] : ""
                    ).ToArray();
                    AddStringColumn(colName, data);
                }
            }
        }

        public string ExportToCsv(char delimiter = ',', bool includeHeader = true)
        {
            var sb = new StringBuilder();
            var columns = ColumnNames.ToList();

            if (includeHeader)
            {
                sb.AppendLine(string.Join(delimiter.ToString(), columns.Select(EscapeCsvField)));
            }

            foreach (var row in _rows.FindAll().OrderBy(r => r.RowIndex))
            {
                var values = columns.Select(col =>
                {
                    row.Data.TryGetValue(col, out var value);
                    return EscapeCsvField(value?.ToString() ?? "");
                });
                sb.AppendLine(string.Join(delimiter.ToString(), values));
            }

            return sb.ToString();
        }

        #endregion

        #region 统计函数

        public double Sum(string column) => GetNumericColumn(column).sum().GetDouble(0);
        public double Mean(string column) => GetNumericColumn(column).mean().GetDouble(0);
        public double Max(string column) => GetNumericColumn(column).max().GetDouble(0);
        public double Min(string column) => GetNumericColumn(column).min().GetDouble(0);
        public double Std(string column) => GetNumericColumn(column).std().GetDouble(0);

        #endregion

        #region 索引

        public void CreateIndex(string columnName)
        {
            _rows.EnsureIndex($"Data.{columnName}");
        }

        #endregion

        #region 内部方法

        internal IEnumerable<TabularRow> GetAllRowsInternal()
        {
            return _rows.FindAll().OrderBy(r => r.RowIndex);
        }

        private void EnsureColumn(string name, string type)
        {
            if (!_metadata.Columns.Any(c => c.Name == name))
            {
                _metadata.Columns.Add(new ColumnMeta
                {
                    Name = name,
                    Type = type,
                    Index = _metadata.Columns.Count
                });
            }
        }

        private void ValidateColumnLength(int length)
        {
            if (_metadata.RowCount > 0 && length != _metadata.RowCount)
                throw new InvalidOperationException($"Column length {length} does not match existing row count {_metadata.RowCount}");
        }

        private void UpdateMetadata()
        {
            _metadata.ModifiedAt = DateTime.UtcNow;
            var meta = _database.GetCollection<TabularMetadata>("tabular_meta");
            meta.Update(_metadata);
        }

        private BsonDocument ConvertToBsonDocument(IDictionary<string, object> values)
        {
            var doc = new BsonDocument();
            foreach (var kv in values)
            {
                doc[kv.Key] = ConvertToBsonValue(kv.Value);
            }
            return doc;
        }

        private BsonValue ConvertToBsonValue(object value)
        {
            if (value == null) return BsonValue.Null;
            return value switch
            {
                double d => new BsonValue(d),
                float f => new BsonValue(f),
                int i => new BsonValue(i),
                long l => new BsonValue(l),
                bool b => new BsonValue(b),
                DateTime dt => new BsonValue(dt),
                string s => new BsonValue(s),
                _ => new BsonValue(value.ToString())
            };
        }

        private object ConvertFromBsonValue(BsonValue value)
        {
            if (value.IsNull) return null;
            if (value.IsDouble) return value.AsDouble;
            if (value.IsInt32) return value.AsInt32;
            if (value.IsInt64) return value.AsInt64;
            if (value.IsBoolean) return value.AsBoolean;
            if (value.IsDateTime) return value.AsDateTime;
            if (value.IsString) return value.AsString;
            return value.ToString();
        }

        private List<string> ParseCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else inQuotes = !inQuotes;
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(c);
            }
            result.Add(sb.ToString());
            return result;
        }

        private string DetermineColumnType(List<List<string>> dataRows, int columnIndex)
        {
            int numericCount = 0, totalCount = 0;
            foreach (var row in dataRows.Take(100))
            {
                if (columnIndex >= row.Count) continue;
                var value = row[columnIndex];
                if (string.IsNullOrEmpty(value)) continue;
                totalCount++;
                if (double.TryParse(value, out _)) numericCount++;
            }
            return totalCount > 0 && numericCount * 1.0 / totalCount > 0.7 ? "Numeric" : "String";
        }

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }

        #endregion
    }
}
