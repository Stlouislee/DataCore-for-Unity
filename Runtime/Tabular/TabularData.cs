using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NumSharp;

namespace AroAro.DataCore.Tabular
{
    /// <summary>
    /// 内存中的表格数据类 - 用于Session内部的DataFrame操作转换
    /// 这是一个轻量级的内存实现，不支持持久化
    /// </summary>
    public class TabularData : ITabularDataset
    {
        private readonly string _name;
        private readonly string _id;
        private readonly List<string> _columnNames = new();
        private readonly Dictionary<string, ColumnType> _columnTypes = new();
        private readonly Dictionary<string, double[]> _numericData = new();
        private readonly Dictionary<string, string[]> _stringData = new();
        private readonly HashSet<string> _indexedColumns = new();
        private int _rowCount = 0;

        public TabularData(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _id = Guid.NewGuid().ToString("N");
        }

        #region IDataSet Implementation

        public string Name => _name;
        public DataSetKind Kind => DataSetKind.Tabular;
        public string Id => _id;

        public IDataSet WithName(string newName)
        {
            var copy = new TabularData(newName);
            foreach (var colName in _columnNames)
            {
                var colType = _columnTypes[colName];
                if (colType == ColumnType.Numeric && _numericData.TryGetValue(colName, out var numData))
                {
                    copy.AddNumericColumn(colName, (double[])numData.Clone());
                }
                else if (colType == ColumnType.String && _stringData.TryGetValue(colName, out var strData))
                {
                    copy.AddStringColumn(colName, (string[])strData.Clone());
                }
            }
            return copy;
        }

        #endregion

        #region ITabularDataset Implementation

        public int RowCount => _rowCount;
        public int ColumnCount => _columnNames.Count;
        public IReadOnlyCollection<string> ColumnNames => _columnNames.AsReadOnly();

        public void AddColumn(string name, string type, bool indexed = false)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            if (_numericData.ContainsKey(name) || _stringData.ContainsKey(name))
                return; // Already exists, no-op

            _columnNames.Add(name);
            var colType = type switch
            {
                "Numeric" => ColumnType.Numeric,
                "String" => ColumnType.String,
                "Boolean" => ColumnType.Boolean,
                "DateTime" => ColumnType.DateTime,
                _ => ColumnType.String
            };
            _columnTypes[name] = colType;

            if (colType == ColumnType.Numeric)
                _numericData[name] = new double[_rowCount];
            else
                _stringData[name] = new string[_rowCount];

            if (indexed)
                _indexedColumns.Add(name);
        }

        public void AddNumericColumn(string name, double[] data)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (_numericData.ContainsKey(name) || _stringData.ContainsKey(name))
                throw new ArgumentException($"Column '{name}' already exists");

            if (_columnNames.Count > 0 && data.Length != _rowCount)
                throw new ArgumentException($"Row count mismatch: expected {_rowCount}, got {data.Length}");

            _columnNames.Add(name);
            _columnTypes[name] = ColumnType.Numeric;
            _numericData[name] = data;
            _rowCount = data.Length;
        }

        [Obsolete("Use AddNumericColumn(name, double[]) instead. Will be removed in v1.0.")]
        public void AddNumericColumn(string name, NDArray data)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            AddNumericColumn(name, data.ToArray<double>());
        }

        public void AddStringColumn(string name, string[] data)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (_numericData.ContainsKey(name) || _stringData.ContainsKey(name))
                throw new ArgumentException($"Column '{name}' already exists");

            if (_columnNames.Count > 0 && data.Length != _rowCount)
                throw new ArgumentException($"Row count mismatch: expected {_rowCount}, got {data.Length}");

            _columnNames.Add(name);
            _columnTypes[name] = ColumnType.String;
            _stringData[name] = data;
            _rowCount = data.Length;
        }

        public bool RemoveColumn(string name)
        {
            if (!_columnNames.Contains(name))
                return false;

            _columnNames.Remove(name);
            _columnTypes.Remove(name);
            _numericData.Remove(name);
            _stringData.Remove(name);

            if (_columnNames.Count == 0)
                _rowCount = 0;

            return true;
        }

        public bool HasColumn(string name) => _columnNames.Contains(name);

        [Obsolete("Use GetNumericColumnRaw(name) instead. Will be removed in v1.0.")]
        public NDArray GetNumericColumn(string name)
        {
            return np.array(GetNumericColumnRaw(name));
        }

        /// <summary>
        /// 获取原始 double[] 数组（无克隆，无包装）
        /// </summary>
        public double[] GetNumericColumnRaw(string name)
        {
            if (!_numericData.TryGetValue(name, out var data))
                throw new KeyNotFoundException($"Numeric column '{name}' not found");
            return data;
        }

        public string[] GetStringColumn(string name)
        {
            if (!_stringData.TryGetValue(name, out var data))
                throw new KeyNotFoundException($"String column '{name}' not found");
            return (string[])data.Clone();
        }

        public ColumnType GetColumnType(string name)
        {
            if (!_columnTypes.TryGetValue(name, out var type))
                throw new KeyNotFoundException($"Column '{name}' not found");
            return type;
        }

        public void AddRow(IDictionary<string, object> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            // Initialize columns if empty
            if (_columnNames.Count == 0)
            {
                foreach (var kvp in values)
                {
                    _columnNames.Add(kvp.Key);
                    if (kvp.Value is IConvertible && kvp.Value is not bool and not char and not string)
                    {
                        _columnTypes[kvp.Key] = ColumnType.Numeric;
                        _numericData[kvp.Key] = new[] { Convert.ToDouble(kvp.Value) };
                    }
                    else
                    {
                        _columnTypes[kvp.Key] = ColumnType.String;
                        _stringData[kvp.Key] = new[] { kvp.Value?.ToString() ?? "" };
                    }
                }
                _rowCount = 1;
                return;
            }

            // Add to existing columns — O(1) amortized per column
            foreach (var colName in _columnNames)
            {
                var value = values.TryGetValue(colName, out var v) ? v : null;
                var type = _columnTypes[colName];

                if (type == ColumnType.Numeric)
                {
                    var data = _numericData[colName];
                    Array.Resize(ref data, data.Length + 1);
                    data[data.Length - 1] = value != null ? Convert.ToDouble(value) : 0.0;
                    _numericData[colName] = data;
                }
                else
                {
                    var data = _stringData[colName];
                    Array.Resize(ref data, data.Length + 1);
                    data[data.Length - 1] = value?.ToString() ?? "";
                    _stringData[colName] = data;
                }
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
                return false;

            foreach (var kvp in values)
            {
                if (!_columnNames.Contains(kvp.Key))
                    continue;

                var type = _columnTypes[kvp.Key];
                if (type == ColumnType.Numeric)
                    _numericData[kvp.Key][rowIndex] = Convert.ToDouble(kvp.Value);
                else
                    _stringData[kvp.Key][rowIndex] = kvp.Value?.ToString() ?? "";
            }
            return true;
        }

        public bool DeleteRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rowCount)
                return false;

            int newLen = _rowCount - 1;
            foreach (var colName in _columnNames)
            {
                var type = _columnTypes[colName];
                if (type == ColumnType.Numeric)
                {
                    var data = _numericData[colName];
                    if (rowIndex < newLen)
                        Array.Copy(data, rowIndex + 1, data, rowIndex, newLen - rowIndex);
                    Array.Resize(ref data, newLen);
                    _numericData[colName] = data;
                }
                else
                {
                    var data = _stringData[colName];
                    if (rowIndex < newLen)
                        Array.Copy(data, rowIndex + 1, data, rowIndex, newLen - rowIndex);
                    Array.Resize(ref data, newLen);
                    _stringData[colName] = data;
                }
            }
            _rowCount--;
            return true;
        }

        public Dictionary<string, object> GetRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rowCount)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            var row = new Dictionary<string, object>();
            foreach (var colName in _columnNames)
            {
                var type = _columnTypes[colName];
                if (type == ColumnType.Numeric)
                    row[colName] = _numericData[colName][rowIndex];
                else
                    row[colName] = _stringData[colName][rowIndex];
            }
            return row;
        }

        public IEnumerable<Dictionary<string, object>> GetRows(int startIndex, int count)
        {
            return Enumerable.Range(startIndex, Math.Min(count, _rowCount - startIndex))
                .Select(GetRow);
        }

        public int Clear()
        {
            var count = _rowCount;
            foreach (var colName in _columnNames.ToList())
            {
                if (_numericData.ContainsKey(colName))
                    _numericData[colName] = Array.Empty<double>();
                if (_stringData.ContainsKey(colName))
                    _stringData[colName] = Array.Empty<string>();
            }
            _rowCount = 0;
            return count;
        }

        public ITabularQuery Query()
        {
            return new InMemoryTabularQuery(this);
        }

        public RawResult ExecuteRaw(string sql, params object[] args)
        {
            throw new NotSupportedException("ExecuteRaw is only supported on LiteDB-backed datasets");
        }

        public double Sum(string columnName)
        {
            return GetNumericColumnRaw(columnName).Sum();
        }

        public double Average(string columnName)
        {
            var data = GetNumericColumnRaw(columnName);
            return data.Length == 0 ? 0 : data.Average();
        }

        public double Min(string columnName)
        {
            var data = GetNumericColumnRaw(columnName);
            return data.Length == 0 ? 0 : data.Min();
        }

        public double Max(string columnName)
        {
            var data = GetNumericColumnRaw(columnName);
            return data.Length == 0 ? 0 : data.Max();
        }

        /// <summary>
        /// Import data from CSV content, replacing all existing columns and data.
        /// </summary>
        /// <remarks>
        /// ⚠️ This method clears all existing columns before importing. Any previous data is lost.
        /// Call this only on a fresh dataset or when you intend to replace all data.
        /// </remarks>
        public void ImportFromCsv(string csvContent, bool hasHeader = true, char delimiter = ',')
        {
            var parsed = AroAro.DataCore.Import.CsvParser.ParseAll(csvContent, delimiter);
            if (parsed.Count == 0) return;

            string[] headers;
            int dataStartIndex;

            if (hasHeader)
            {
                headers = parsed[0].ToArray();
                dataStartIndex = 1;
            }
            else
            {
                headers = Enumerable.Range(0, parsed[0].Count).Select(i => $"Column{i}").ToArray();
                dataStartIndex = 0;
            }

            // Detect column types from first 100 data rows (not just first row)
            int sampleSize = Math.Min(100, parsed.Count - dataStartIndex);
            var isNumeric = new bool[headers.Length];
            
            for (int i = 0; i < headers.Length; i++)
            {
                int numericCount = 0;
                int totalSampled = 0;
                for (int s = 0; s < sampleSize; s++)
                {
                    var row = parsed[dataStartIndex + s];
                    if (i < row.Count && !string.IsNullOrWhiteSpace(row[i]))
                    {
                        totalSampled++;
                        if (double.TryParse(row[i], out _))
                            numericCount++;
                    }
                }
                isNumeric[i] = totalSampled > 0 && (double)numericCount / totalSampled > 0.7;
            }

            // Parse all data
            var numericCols = new Dictionary<int, List<double>>();
            var stringCols = new Dictionary<int, List<string>>();

            for (int i = 0; i < headers.Length; i++)
            {
                if (isNumeric[i])
                    numericCols[i] = new List<double>();
                else
                    stringCols[i] = new List<string>();
            }

            for (int row = dataStartIndex; row < parsed.Count; row++)
            {
                var values = parsed[row];
                for (int col = 0; col < headers.Length && col < values.Count; col++)
                {
                    if (isNumeric[col])
                    {
                        double.TryParse(values[col], out var d);
                        numericCols[col].Add(d);
                    }
                    else
                    {
                        stringCols[col].Add(values[col]);
                    }
                }
            }

            // Add columns
            for (int i = 0; i < headers.Length; i++)
            {
                if (isNumeric[i])
                    AddNumericColumn(headers[i], numericCols[i].ToArray());
                else
                    AddStringColumn(headers[i], stringCols[i].ToArray());
            }
        }

        public string ExportToCsv(char delimiter = ',')
        {
            var header = string.Join(delimiter.ToString(), _columnNames.Select(n => EscapeCsvField(n, delimiter)));
            var rows = Enumerable.Range(0, _rowCount).Select(i =>
                string.Join(delimiter.ToString(), _columnNames.Select(colName =>
                    _columnTypes[colName] == ColumnType.Numeric
                        ? _numericData[colName][i].ToString()
                        : EscapeCsvField(_stringData[colName][i], delimiter))));
            return header + Environment.NewLine + string.Join(Environment.NewLine, rows) + Environment.NewLine;
        }

        public string ExportToCsv(char delimiter, bool includeHeader)
        {
            var lines = new List<string>();

            if (includeHeader)
                lines.Add(string.Join(delimiter.ToString(), _columnNames.Select(n => EscapeCsvField(n, delimiter))));

            lines.AddRange(Enumerable.Range(0, _rowCount).Select(i =>
                string.Join(delimiter.ToString(), _columnNames.Select(colName =>
                    _columnTypes[colName] == ColumnType.Numeric
                        ? _numericData[colName][i].ToString()
                        : EscapeCsvField(_stringData[colName][i], delimiter)))));

            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        private static string EscapeCsvField(string field, char delimiter)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(delimiter) || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }

        public double Mean(string columnName)
        {
            return Average(columnName);
        }

        [Obsolete("Use StdPop() for population std or StdSample() for sample std. Will be removed in v1.0.")]
        public double Std(string columnName)
        {
            return StdSample(columnName);
        }

        /// <summary>
        /// Population standard deviation (N denominator, no Bessel correction).
        /// Use this when the data represents the entire population.
        /// </summary>
        public double StdPop(string columnName)
        {
            var data = GetNumericColumnRaw(columnName);
            if (data.Length == 0) return 0;
            var mean = data.Average();
            var sumSquares = data.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSquares / data.Length);
        }

        /// <summary>
        /// Sample standard deviation (N-1 denominator, with Bessel correction).
        /// Use this when the data is a sample from a larger population.
        /// </summary>
        public double StdSample(string columnName)
        {
            var data = GetNumericColumnRaw(columnName);
            if (data.Length < 2) return 0;
            var mean = data.Average();
            var sumSquares = data.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSquares / (data.Length - 1));
        }

        public int[] Where(string column, QueryOp op, object value)
        {
            if (!_columnNames.Contains(column))
                return Array.Empty<int>();

            var type = _columnTypes[column];

            if (type == ColumnType.Numeric)
            {
                var data = _numericData[column];
                var cmpVal = Convert.ToDouble(value);
                return Enumerable.Range(0, _rowCount)
                    .Where(i => MatchesNumericOp(data[i], op, cmpVal))
                    .ToArray();
            }
            else
            {
                var data = _stringData[column];
                var cmpStr = value?.ToString() ?? "";
                return Enumerable.Range(0, _rowCount)
                    .Where(i => MatchesStringOp(data[i], op, cmpStr))
                    .ToArray();
            }
        }

        private static bool MatchesNumericOp(double val, QueryOp op, double target) => op switch
        {
            QueryOp.Eq => val == target,
            QueryOp.Ne => val != target,
            QueryOp.Gt => val > target,
            QueryOp.Ge => val >= target,
            QueryOp.Lt => val < target,
            QueryOp.Le => val <= target,
            _ => false
        };

        private static bool MatchesStringOp(string val, QueryOp op, string target) => op switch
        {
            QueryOp.Eq => val == target,
            QueryOp.Ne => val != target,
            QueryOp.Contains => val.Contains(target),
            QueryOp.StartsWith => val.StartsWith(target),
            QueryOp.EndsWith => val.EndsWith(target),
            _ => false
        };

        public void CreateIndex(string columnName)
        {
            // 内存实现中索引不必要，但为了接口兼容记录状态
            if (HasColumn(columnName))
                _indexedColumns.Add(columnName);
        }

        public bool IsColumnIndexed(string name)
        {
            return _indexedColumns.Contains(name);
        }

        public void Compact()
        {
            // In-memory implementation has no gaps — DeleteRow already shifts elements.
            // No-op for interface compatibility.
        }

        #endregion

        #region 异步操作

        public Task AddRowAsync(IDictionary<string, object> values, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            AddRow(values);
            return Task.CompletedTask;
        }

        public Task<int> AddRowsAsync(IEnumerable<IDictionary<string, object>> rows, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(AddRows(rows));
        }

        public Task AddNumericColumnAsync(string name, double[] data, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            AddNumericColumn(name, data);
            return Task.CompletedTask;
        }

        public Task AddStringColumnAsync(string name, string[] data, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            AddStringColumn(name, data);
            return Task.CompletedTask;
        }

        public Task<int> ClearAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Clear());
        }

        /// <summary>
        /// 异步执行原生查询（内存实现，直接委托同步方法）
        /// </summary>
        public Task<RawResult> ExecuteRawAsync(string sql, object[] args, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(ExecuteRaw(sql, args));
        }

        /// <summary>
        /// 异步导出为 CSV（内存实现，直接委托同步方法）
        /// </summary>
        public Task<string> ExportToCsvAsync(char delimiter = ',', bool includeHeader = true, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(ExportToCsv(delimiter, includeHeader));
        }

        /// <summary>
        /// 异步从 CSV 导入（内存实现，直接委托同步方法）
        /// </summary>
        public Task ImportFromCsvAsync(string csvContent, bool hasHeader = true, char delimiter = ',', CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            ImportFromCsv(csvContent, hasHeader, delimiter);
            return Task.CompletedTask;
        }

        #endregion

        /// <summary>
        /// 简单的内存查询实现
        /// </summary>
        private class InMemoryTabularQuery : ITabularQuery
        {
            private readonly TabularData _source;
            private List<Func<Dictionary<string, object>, bool>> _filters = new();
            private string _orderByColumn;
            private bool _orderDescending;
            private List<(string Column, bool Descending)> _thenByColumns = new();
            private int _skip;
            private int _take = int.MaxValue;
            private List<string> _selectedColumns;

            public InMemoryTabularQuery(TabularData source)
            {
                _source = source;
            }

            public ITabularQuery WhereEquals(string column, object value)
            {
                _filters.Add(row => row.TryGetValue(column, out var v) && Equals(v, value));
                return this;
            }

            public ITabularQuery WhereNotEquals(string column, object value)
            {
                _filters.Add(row => row.TryGetValue(column, out var v) && !Equals(v, value));
                return this;
            }

            public ITabularQuery WhereGreaterThan(string column, double value)
            {
                _filters.Add(row => row.TryGetValue(column, out var v) && Convert.ToDouble(v) > value);
                return this;
            }

            public ITabularQuery WhereGreaterThanOrEqual(string column, double value)
            {
                _filters.Add(row => row.TryGetValue(column, out var v) && Convert.ToDouble(v) >= value);
                return this;
            }

            public ITabularQuery WhereLessThan(string column, double value)
            {
                _filters.Add(row => row.TryGetValue(column, out var v) && Convert.ToDouble(v) < value);
                return this;
            }

            public ITabularQuery WhereLessThanOrEqual(string column, double value)
            {
                _filters.Add(row => row.TryGetValue(column, out var v) && Convert.ToDouble(v) <= value);
                return this;
            }

            public ITabularQuery WhereBetween(string column, double min, double max)
            {
                _filters.Add(row =>
                {
                    if (!row.TryGetValue(column, out var v)) return false;
                    var val = Convert.ToDouble(v);
                    return val >= min && val <= max;
                });
                return this;
            }

            public ITabularQuery WhereContains(string column, string value)
            {
                _filters.Add(row => row.TryGetValue(column, out var v) && v?.ToString().Contains(value) == true);
                return this;
            }

            public ITabularQuery WhereStartsWith(string column, string value)
            {
                _filters.Add(row => row.TryGetValue(column, out var v) && v?.ToString().StartsWith(value) == true);
                return this;
            }

            public ITabularQuery WhereEndsWith(string column, string value)
            {
                _filters.Add(row => row.TryGetValue(column, out var v) && v?.ToString().EndsWith(value) == true);
                return this;
            }

            public ITabularQuery WhereIn<T>(string column, IEnumerable<T> values)
            {
                var set = new HashSet<object>(values.Cast<object>());
                _filters.Add(row => row.TryGetValue(column, out var v) && set.Contains(v));
                return this;
            }

            public ITabularQuery WhereIsNull(string column)
            {
                _filters.Add(row => !row.TryGetValue(column, out var v) || v == null);
                return this;
            }

            public ITabularQuery WhereIsNotNull(string column)
            {
                _filters.Add(row => row.TryGetValue(column, out var v) && v != null);
                return this;
            }

            public ITabularQuery Where(string column, QueryOp op, object value)
            {
                return op switch
                {
                    QueryOp.Eq => WhereEquals(column, value),
                    QueryOp.Ne => WhereNotEquals(column, value),
                    QueryOp.Gt => WhereGreaterThan(column, Convert.ToDouble(value)),
                    QueryOp.Ge => WhereGreaterThanOrEqual(column, Convert.ToDouble(value)),
                    QueryOp.Lt => WhereLessThan(column, Convert.ToDouble(value)),
                    QueryOp.Le => WhereLessThanOrEqual(column, Convert.ToDouble(value)),
                    QueryOp.Contains => WhereContains(column, value?.ToString() ?? ""),
                    QueryOp.StartsWith => WhereStartsWith(column, value?.ToString() ?? ""),
                    QueryOp.EndsWith => WhereEndsWith(column, value?.ToString() ?? ""),
                    _ => this
                };
            }

            public ITabularQuery OrderBy(string column)
            {
                _orderByColumn = column;
                _orderDescending = false;
                return this;
            }

            public ITabularQuery OrderByDescending(string column)
            {
                _orderByColumn = column;
                _orderDescending = true;
                return this;
            }

            public ITabularQuery ThenBy(string column)
            {
                _thenByColumns.Add((column, false));
                return this;
            }

            public ITabularQuery ThenByDescending(string column)
            {
                _thenByColumns.Add((column, true));
                return this;
            }

            public ITabularQuery Skip(int count)
            {
                _skip = count;
                return this;
            }

            public ITabularQuery Limit(int count)
            {
                _take = count;
                return this;
            }

            public ITabularQuery Page(int pageNumber, int pageSize)
            {
                _skip = pageNumber * pageSize;
                _take = pageSize;
                return this;
            }

            public ITabularQuery Select(params string[] columns)
            {
                _selectedColumns = columns.ToList();
                return this;
            }

            public int[] ToRowIndices()
            {
                return Enumerable.Range(0, _source.RowCount)
                    .Where(i => _filters.All(f => f(_source.GetRow(i))))
                    .ToArray();
            }

            public List<Dictionary<string, object>> ToDictionaries()
            {
                IEnumerable<(int index, Dictionary<string, object> row)> result = 
                    Enumerable.Range(0, _source.RowCount)
                        .Select(i => (i, _source.GetRow(i)));

                foreach (var filter in _filters)
                {
                    result = result.Where(x => filter(x.row));
                }

                if (!string.IsNullOrEmpty(_orderByColumn))
                {
                    var colType = _source.GetColumnType(_orderByColumn);
                    IOrderedEnumerable<(int index, Dictionary<string, object> row)> ordered;
                    if (colType == ColumnType.Numeric)
                    {
                        ordered = _orderDescending
                            ? result.OrderByDescending(x => Convert.ToDouble(x.row[_orderByColumn]))
                            : result.OrderBy(x => Convert.ToDouble(x.row[_orderByColumn]));
                    }
                    else
                    {
                        ordered = _orderDescending
                            ? result.OrderByDescending(x => x.row[_orderByColumn]?.ToString() ?? "")
                            : result.OrderBy(x => x.row[_orderByColumn]?.ToString() ?? "");
                    }

                    // Apply ThenBy/ThenByDescending columns
                    foreach (var (col, desc) in _thenByColumns)
                    {
                        var thenColType = _source.GetColumnType(col);
                        if (thenColType == ColumnType.Numeric)
                        {
                            ordered = desc
                                ? ordered.ThenByDescending(x => Convert.ToDouble(x.row[col]))
                                : ordered.ThenBy(x => Convert.ToDouble(x.row[col]));
                        }
                        else
                        {
                            ordered = desc
                                ? ordered.ThenByDescending(x => x.row[col]?.ToString() ?? "")
                                : ordered.ThenBy(x => x.row[col]?.ToString() ?? "");
                        }
                    }
                    result = ordered;
                }

                var rows = result.Skip(_skip).Take(_take).Select(x => x.row);

                if (_selectedColumns != null && _selectedColumns.Count > 0)
                {
                    rows = rows.Select(r => r.Where(kvp => _selectedColumns.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
                }

                return rows.ToList();
            }

            public int Count()
            {
                IEnumerable<Dictionary<string, object>> result = 
                    Enumerable.Range(0, _source.RowCount)
                        .Select(i => _source.GetRow(i));

                foreach (var filter in _filters)
                {
                    result = result.Where(filter);
                }

                return result.Count();
            }

            public bool Any()
            {
                return Enumerable.Range(0, _source.RowCount)
                    .Any(i => _filters.All(f => f(_source.GetRow(i))));
            }

            public Dictionary<string, object> FirstOrDefault()
            {
                var match = Enumerable.Range(0, _source.RowCount)
                    .Select(i => _source.GetRow(i))
                    .FirstOrDefault(row => _filters.All(f => f(row)));

                if (match == null) return null;
                if (_selectedColumns != null && _selectedColumns.Count > 0)
                {
                    return match.Where(kvp => _selectedColumns.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
                return match;
            }

            public double Sum(string column)
            {
                return Enumerable.Range(0, _source.RowCount)
                    .Select(i => _source.GetRow(i))
                    .Where(row => _filters.All(f => f(row)) && row.ContainsKey(column))
                    .Sum(row => Convert.ToDouble(row[column]));
            }

            public double Average(string column)
            {
                var values = Enumerable.Range(0, _source.RowCount)
                    .Select(i => _source.GetRow(i))
                    .Where(row => _filters.All(f => f(row)) && row.ContainsKey(column))
                    .Select(row => Convert.ToDouble(row[column]));
                return values.Any() ? values.Average() : 0;
            }

            public double Max(string column)
            {
                var values = Enumerable.Range(0, _source.RowCount)
                    .Select(i => _source.GetRow(i))
                    .Where(row => _filters.All(f => f(row)) && row.ContainsKey(column))
                    .Select(row => Convert.ToDouble(row[column]));
                return values.Any() ? values.Max() : 0;
            }

            public double Min(string column)
            {
                var values = Enumerable.Range(0, _source.RowCount)
                    .Select(i => _source.GetRow(i))
                    .Where(row => _filters.All(f => f(row)) && row.ContainsKey(column))
                    .Select(row => Convert.ToDouble(row[column]));
                return values.Any() ? values.Min() : 0;
            }
        }
    }
}
