using System;
using System.Collections.Generic;
using System.Globalization;
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
        private readonly object _lock = new object();
        private bool _disposed;
        private int _pendingMetadataUpdates;
        private const int MetadataUpdateBatchSize = 100; // Batch metadata updates

        internal LiteDbTabularDataset(LiteDatabase database, TabularMetadata metadata)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _rows = _database.GetCollection<TabularRow>($"tabular_{metadata.Id}");
            _rows.EnsureIndex(r => r.RowIndex, true);
        }

        /// <summary>
        /// Check if the database is still accessible
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LiteDbTabularDataset), $"Tabular dataset '{_metadata.Name}' has been disposed");
        }

        /// <summary>
        /// Mark this dataset as disposed (called by parent store)
        /// </summary>
        internal void MarkDisposed()
        {
            _disposed = true;
        }

        /// <summary>
        /// Force flush any pending metadata updates
        /// </summary>
        public void FlushMetadata()
        {
            lock (_lock)
            {
                if (_pendingMetadataUpdates > 0)
                {
                    ForceUpdateMetadata();
                    _pendingMetadataUpdates = 0;
                }
            }
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

        public int RowCount
        {
            get
            {
                ThrowIfDisposed();
                return _metadata.RowCount;
            }
        }
        
        public int ColumnCount
        {
            get
            {
                ThrowIfDisposed();
                return _metadata.Columns.Count;
            }
        }
        
        public IReadOnlyCollection<string> ColumnNames
        {
            get
            {
                ThrowIfDisposed();
                return _metadata.Columns.Select(c => c.Name).ToList().AsReadOnly();
            }
        }

        #endregion

        #region 列操作

        public void AddNumericColumn(string name, double[] data)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Column name is required", nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            lock (_lock)
            {
                ValidateColumnLength(data.Length);
                EnsureColumn(name, "Numeric");

                if (_metadata.RowCount == 0)
                {
                    // 批量创建行 - 使用 InsertBulk 提速并保证事务安全性
                    var newRows = new List<TabularRow>(data.Length);
                    for (int i = 0; i < data.Length; i++)
                    {
                        var row = new TabularRow 
                        { 
                            RowIndex = i, 
                            Data = new BsonDocument { { name, new BsonValue(data[i]) } } 
                        };
                        newRows.Add(row);
                    }
                    _rows.InsertBulk(newRows);
                    _metadata.RowCount = data.Length;
                }
                else
                {
                    // 更新现有行
                    // 注意：LiteDB 没有 UpdateBulk，所以我们只能查询出来修改后再 Update
                    // 对于大型数据集，这仍然比较慢，但为了保持逻辑简单暂时如此
                    var existingRows = _rows.FindAll().OrderBy(r => r.RowIndex).ToList();
                    for (int i = 0; i < Math.Min(existingRows.Count, data.Length); i++)
                    {
                        existingRows[i].Data[name] = new BsonValue(data[i]);
                        _rows.Update(existingRows[i]);
                    }
                }

                ForceUpdateMetadata(); // Force update after bulk operation
            }
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
            ThrowIfDisposed();
            
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Column name is required", nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            lock (_lock)
            {
                ValidateColumnLength(data.Length);
                EnsureColumn(name, "String");

                if (_metadata.RowCount == 0)
                {
                    // 批量创建行 - 使用 InsertBulk
                    var newRows = new List<TabularRow>(data.Length);
                    for (int i = 0; i < data.Length; i++)
                    {
                        var row = new TabularRow 
                        { 
                            RowIndex = i, 
                            Data = new BsonDocument { { name, new BsonValue(data[i]) } } 
                        };
                        newRows.Add(row);
                    }
                    _rows.InsertBulk(newRows);
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

                ForceUpdateMetadata(); // Force update after bulk operation
            }
        }

        public bool RemoveColumn(string name)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                var col = _metadata.Columns.FirstOrDefault(c => c.Name == name);
                if (col == null) return false;

                _metadata.Columns.Remove(col);

                foreach (var row in _rows.FindAll())
                {
                    row.Data.Remove(name);
                    _rows.Update(row);
                }

                ForceUpdateMetadata();
                return true;
            }
        }

        public bool HasColumn(string name)
        {
            ThrowIfDisposed();
            return _metadata.Columns.Any(c => c.Name == name);
        }

        public NDArray GetNumericColumn(string name)
        {
            ThrowIfDisposed();
            if (!HasColumn(name))
                throw new KeyNotFoundException($"Column '{name}' not found");

            var data = _rows.FindAll().OrderBy(r => r.RowIndex)
                .Select(r => r.Data.TryGetValue(name, out var v) && !v.IsNull ? v.AsDouble : 0.0)
                .ToArray();

            return np.array(data);
        }

        public string[] GetStringColumn(string name)
        {
            ThrowIfDisposed();
            
            if (!HasColumn(name))
                throw new KeyNotFoundException($"Column '{name}' not found");

            return _rows.FindAll().OrderBy(r => r.RowIndex)
                .Select(r => r.Data.TryGetValue(name, out var v) && !v.IsNull ? v.AsString : null)
                .ToArray();
        }

        public ColumnType GetColumnType(string name)
        {
            ThrowIfDisposed();
            
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
            ThrowIfDisposed();
            
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            lock (_lock)
            {
                var row = new TabularRow
                {
                    RowIndex = _metadata.RowCount,
                    Data = ConvertToBsonDocument(values)
                };

                _rows.Insert(row);
                _metadata.RowCount++;
                UpdateMetadata();
            }
        }

        public int AddRows(IEnumerable<IDictionary<string, object>> rows)
        {
            ThrowIfDisposed();
            
            lock (_lock)
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
                    ForceUpdateMetadata(); // Force update after bulk operation
                }

                return count;
            }
        }

        public bool UpdateRow(int rowIndex, IDictionary<string, object> values)
        {
            ThrowIfDisposed();
            
            if (rowIndex < 0 || rowIndex >= _metadata.RowCount)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            lock (_lock)
            {
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
        }

        public bool DeleteRow(int rowIndex)
        {
            ThrowIfDisposed();
            
            if (rowIndex < 0 || rowIndex >= _metadata.RowCount)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            lock (_lock)
            {
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
                ForceUpdateMetadata();
                return true;
            }
        }

        public Dictionary<string, object> GetRow(int rowIndex)
        {
            ThrowIfDisposed();
            
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
            ThrowIfDisposed();
            
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
            ThrowIfDisposed();
            
            lock (_lock)
            {
                int count = _metadata.RowCount;
                _rows.DeleteAll();
                _metadata.RowCount = 0;
                ForceUpdateMetadata();
                return count;
            }
        }

        #endregion

        #region 查询

        public ITabularQuery Query()
        {
            ThrowIfDisposed();
            return new LiteDbTabularQuery(this);
        }

        public int[] Where(string column, QueryOp op, object value)
        {
            ThrowIfDisposed();
            return Query().Where(column, op, value).ToRowIndices();
        }

        #endregion

        #region CSV 导入导出

        public void ImportFromCsv(string csvContent, bool hasHeader = true, char delimiter = ',')
        {
            if (string.IsNullOrEmpty(csvContent))
                throw new ArgumentException("CSV content cannot be null or empty", nameof(csvContent));

            // NOTE: This method is designed for fast/atomic initial imports.
            // It builds complete row documents and does a single InsertBulk.
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

            // Normalize header count
            for (int i = headers.Count; i < columnCount; i++)
                headers.Add($"Column{i}");

            // Overwrite existing content (rows + column schema)
            _rows.DeleteAll();
            _metadata.Columns.Clear();
            _metadata.RowCount = 0;

            var columnTypes = new string[columnCount];
            for (int col = 0; col < columnCount; col++)
            {
                columnTypes[col] = DetermineColumnType(dataRows, col);
                EnsureColumn(headers[col], columnTypes[col]);
            }

            // Build row documents and insert once
            var newRows = new List<TabularRow>(dataRows.Count);
            for (int rowIndex = 0; rowIndex < dataRows.Count; rowIndex++)
            {
                var rowValues = dataRows[rowIndex];
                var doc = new BsonDocument();

                for (int col = 0; col < columnCount; col++)
                {
                    var colName = headers[col];
                    var raw = col < rowValues.Count ? rowValues[col] : "";

                    if (columnTypes[col] == "Numeric")
                    {
                        if (string.IsNullOrWhiteSpace(raw))
                        {
                            doc[colName] = new BsonValue(double.NaN);
                        }
                        else if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                        {
                            doc[colName] = new BsonValue(d);
                        }
                        else
                        {
                            doc[colName] = new BsonValue(double.NaN);
                        }
                    }
                    else
                    {
                        doc[colName] = new BsonValue(raw ?? string.Empty);
                    }
                }

                newRows.Add(new TabularRow { RowIndex = rowIndex, Data = doc });
            }

            _rows.InsertBulk(newRows);
            _metadata.RowCount = dataRows.Count;
            UpdateMetadata();
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

        /// <summary>
        /// Batched metadata update - only writes to DB after threshold or on ForceUpdateMetadata
        /// </summary>
        private void UpdateMetadata()
        {
            _metadata.ModifiedAt = DateTime.UtcNow;
            _pendingMetadataUpdates++;
            
            if (_pendingMetadataUpdates >= MetadataUpdateBatchSize)
            {
                ForceUpdateMetadata();
                _pendingMetadataUpdates = 0;
            }
        }

        /// <summary>
        /// Force write metadata to database immediately
        /// </summary>
        private void ForceUpdateMetadata()
        {
            try
            {
                _metadata.ModifiedAt = DateTime.UtcNow;
                var meta = _database.GetCollection<TabularMetadata>("tabular_meta");
                meta.Update(_metadata);
                _pendingMetadataUpdates = 0;
            }
            catch (ObjectDisposedException)
            {
                // Database was disposed, mark self as disposed
                _disposed = true;
                throw;
            }
            catch (LiteException ex) when (ex.Message.Contains("disposed"))
            {
                _disposed = true;
                throw new ObjectDisposedException(nameof(LiteDbTabularDataset), "Database has been disposed", ex);
            }
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
                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) numericCount++;
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
