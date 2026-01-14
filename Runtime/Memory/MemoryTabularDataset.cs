using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NumSharp;

namespace AroAro.DataCore.Memory
{
    /// <summary>
    /// 内存表格数据集实现
    /// </summary>
    public sealed class MemoryTabularDataset : ITabularDataset
    {
        private readonly string _name;
        private readonly string _id;
        private readonly Dictionary<string, MemoryColumn> _columns = new(StringComparer.Ordinal);
        private int _rowCount;

        internal MemoryTabularDataset(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _id = Guid.NewGuid().ToString("N");
        }

        #region IDataSet 实现

        public string Name => _name;
        public DataSetKind Kind => DataSetKind.Tabular;
        public string Id => _id;

        public IDataSet WithName(string name)
        {
            var clone = new MemoryTabularDataset(name);
            foreach (var col in _columns.Values)
            {
                clone._columns[col.Name] = col.Clone();
            }
            clone._rowCount = _rowCount;
            return clone;
        }

        #endregion

        #region ITabularDataset 实现

        public int RowCount => _rowCount;
        public int ColumnCount => _columns.Count;
        public IReadOnlyCollection<string> ColumnNames => _columns.Keys.ToList().AsReadOnly();

        #endregion

        #region 列操作

        public void AddNumericColumn(string name, double[] data)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Column name is required", nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            ValidateColumnLength(data.Length);
            _columns[name] = new MemoryColumn(name, ColumnType.Numeric, data.Cast<object>().ToList());
            if (_rowCount == 0) _rowCount = data.Length;
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
            _columns[name] = new MemoryColumn(name, ColumnType.String, data.Cast<object>().ToList());
            if (_rowCount == 0) _rowCount = data.Length;
        }

        public bool RemoveColumn(string name) => _columns.Remove(name);

        public bool HasColumn(string name) => _columns.ContainsKey(name);

        public NDArray GetNumericColumn(string name)
        {
            if (!_columns.TryGetValue(name, out var col))
                throw new KeyNotFoundException($"Column '{name}' not found");
            if (col.Type != ColumnType.Numeric)
                throw new InvalidOperationException($"Column '{name}' is not numeric");

            return np.array(col.Data.Select(v => Convert.ToDouble(v ?? 0.0)).ToArray());
        }

        public string[] GetStringColumn(string name)
        {
            if (!_columns.TryGetValue(name, out var col))
                throw new KeyNotFoundException($"Column '{name}' not found");
            if (col.Type != ColumnType.String)
                throw new InvalidOperationException($"Column '{name}' is not string");

            return col.Data.Select(v => v?.ToString()).ToArray();
        }

        public ColumnType GetColumnType(string name)
        {
            if (!_columns.TryGetValue(name, out var col))
                return ColumnType.Unknown;
            return col.Type;
        }

        #endregion

        #region 行操作

        public void AddRow(IDictionary<string, object> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            foreach (var col in _columns.Values)
            {
                values.TryGetValue(col.Name, out var value);
                col.Data.Add(value);
            }
            _rowCount++;
        }

        public int AddRows(IEnumerable<IDictionary<string, object>> rows)
        {
            int count = 0;
            foreach (var row in rows)
            {
                AddRow(row);
                count++;
            }
            return count;
        }

        public bool UpdateRow(int rowIndex, IDictionary<string, object> values)
        {
            if (rowIndex < 0 || rowIndex >= _rowCount)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            foreach (var kv in values)
            {
                if (_columns.TryGetValue(kv.Key, out var col))
                {
                    col.Data[rowIndex] = kv.Value;
                }
            }
            return true;
        }

        public bool DeleteRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rowCount)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            foreach (var col in _columns.Values)
            {
                col.Data.RemoveAt(rowIndex);
            }
            _rowCount--;
            return true;
        }

        public Dictionary<string, object> GetRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rowCount)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            var result = new Dictionary<string, object>();
            foreach (var col in _columns.Values)
            {
                result[col.Name] = col.Data[rowIndex];
            }
            return result;
        }

        public IEnumerable<Dictionary<string, object>> GetRows(int startIndex, int count)
        {
            for (int i = startIndex; i < Math.Min(startIndex + count, _rowCount); i++)
            {
                yield return GetRow(i);
            }
        }

        public int Clear()
        {
            int count = _rowCount;
            foreach (var col in _columns.Values)
            {
                col.Data.Clear();
            }
            _rowCount = 0;
            return count;
        }

        #endregion

        #region 查询

        public ITabularQuery Query() => new MemoryTabularQuery(this);

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

            // 确定列类型
            var columnTypes = new ColumnType[columnCount];
            for (int col = 0; col < columnCount; col++)
            {
                columnTypes[col] = DetermineColumnType(dataRows, col);
            }

            // 创建列并填充数据
            for (int col = 0; col < columnCount; col++)
            {
                var colName = col < headers.Count ? headers[col] : $"Column{col}";

                if (columnTypes[col] == ColumnType.Numeric)
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

            for (int i = 0; i < _rowCount; i++)
            {
                var row = GetRow(i);
                var values = columns.Select(col =>
                {
                    row.TryGetValue(col, out var value);
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
            // 内存存储不需要显式索引，此方法为接口兼容
        }

        #endregion

        #region 内部方法

        internal MemoryColumn GetColumnInternal(string name)
        {
            if (!_columns.TryGetValue(name, out var col))
                throw new KeyNotFoundException($"Column '{name}' not found");
            return col;
        }

        private void ValidateColumnLength(int length)
        {
            if (_rowCount > 0 && length != _rowCount)
                throw new InvalidOperationException($"Column length {length} does not match existing row count {_rowCount}");
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
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }

        private ColumnType DetermineColumnType(List<List<string>> dataRows, int columnIndex)
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
            return totalCount > 0 && numericCount * 1.0 / totalCount > 0.7 ? ColumnType.Numeric : ColumnType.String;
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

    /// <summary>
    /// 内存列数据
    /// </summary>
    internal class MemoryColumn
    {
        public string Name { get; }
        public ColumnType Type { get; }
        public List<object> Data { get; }

        public MemoryColumn(string name, ColumnType type, List<object> data)
        {
            Name = name;
            Type = type;
            Data = data ?? new List<object>();
        }

        public MemoryColumn Clone()
        {
            return new MemoryColumn(Name, Type, new List<object>(Data));
        }
    }
}
