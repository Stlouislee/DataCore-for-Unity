using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly Dictionary<string, NDArray> _numericData = new();
        private readonly Dictionary<string, string[]> _stringData = new();
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
                    copy.AddNumericColumn(colName, numData.Clone() as NDArray);
                }
                else if (colType == ColumnType.String && _stringData.TryGetValue(colName, out var strData))
                {
                    copy.AddStringColumn(colName, strData.ToArray());
                }
            }
            return copy;
        }

        #endregion

        #region ITabularDataset Implementation

        public int RowCount => _rowCount;
        public int ColumnCount => _columnNames.Count;
        public IReadOnlyCollection<string> ColumnNames => _columnNames.AsReadOnly();

        public void AddNumericColumn(string name, double[] data)
        {
            AddNumericColumn(name, np.array(data));
        }

        public void AddNumericColumn(string name, NDArray data)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            
            if (_numericData.ContainsKey(name) || _stringData.ContainsKey(name))
                throw new ArgumentException($"Column '{name}' already exists");

            int newRowCount = (int)data.shape[0];
            if (_columnNames.Count > 0 && newRowCount != _rowCount)
                throw new ArgumentException($"Row count mismatch: expected {_rowCount}, got {newRowCount}");

            _columnNames.Add(name);
            _columnTypes[name] = ColumnType.Numeric;
            _numericData[name] = data;
            _rowCount = newRowCount;
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

        public NDArray GetNumericColumn(string name)
        {
            if (!_numericData.TryGetValue(name, out var data))
                throw new KeyNotFoundException($"Numeric column '{name}' not found");
            return data;
        }

        public string[] GetStringColumn(string name)
        {
            if (!_stringData.TryGetValue(name, out var data))
                throw new KeyNotFoundException($"String column '{name}' not found");
            return data;
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
                    if (kvp.Value is double or int or float or long)
                    {
                        _columnTypes[kvp.Key] = ColumnType.Numeric;
                        _numericData[kvp.Key] = np.array(new double[] { Convert.ToDouble(kvp.Value) });
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

            // Add to existing columns
            foreach (var colName in _columnNames)
            {
                var value = values.TryGetValue(colName, out var v) ? v : null;
                var type = _columnTypes[colName];

                if (type == ColumnType.Numeric)
                {
                    var currentData = _numericData[colName].ToArray<double>();
                    var newData = new double[currentData.Length + 1];
                    Array.Copy(currentData, newData, currentData.Length);
                    newData[currentData.Length] = value != null ? Convert.ToDouble(value) : 0.0;
                    _numericData[colName] = np.array(newData);
                }
                else
                {
                    var currentData = _stringData[colName];
                    var newData = new string[currentData.Length + 1];
                    Array.Copy(currentData, newData, currentData.Length);
                    newData[currentData.Length] = value?.ToString() ?? "";
                    _stringData[colName] = newData;
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
                {
                    var data = _numericData[kvp.Key].ToArray<double>();
                    data[rowIndex] = Convert.ToDouble(kvp.Value);
                    _numericData[kvp.Key] = np.array(data);
                }
                else
                {
                    var data = _stringData[kvp.Key];
                    data[rowIndex] = kvp.Value?.ToString() ?? "";
                }
            }
            return true;
        }

        public bool DeleteRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rowCount)
                return false;

            foreach (var colName in _columnNames)
            {
                var type = _columnTypes[colName];
                if (type == ColumnType.Numeric)
                {
                    var data = _numericData[colName].ToArray<double>().ToList();
                    data.RemoveAt(rowIndex);
                    _numericData[colName] = np.array(data.ToArray());
                }
                else
                {
                    var data = _stringData[colName].ToList();
                    data.RemoveAt(rowIndex);
                    _stringData[colName] = data.ToArray();
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
                {
                    row[colName] = _numericData[colName].GetDouble(rowIndex);
                }
                else
                {
                    row[colName] = _stringData[colName][rowIndex];
                }
            }
            return row;
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
            var count = _rowCount;
            foreach (var colName in _columnNames.ToList())
            {
                if (_numericData.ContainsKey(colName))
                    _numericData[colName] = np.array(new double[0]);
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

        public double Sum(string columnName)
        {
            var data = GetNumericColumn(columnName);
            return data.ToArray<double>().Sum();
        }

        public double Average(string columnName)
        {
            var data = GetNumericColumn(columnName);
            return data.ToArray<double>().Average();
        }

        public double Min(string columnName)
        {
            var data = GetNumericColumn(columnName);
            return data.ToArray<double>().Min();
        }

        public double Max(string columnName)
        {
            var data = GetNumericColumn(columnName);
            return data.ToArray<double>().Max();
        }

        public void ImportFromCsv(string csvContent, bool hasHeader = true, char delimiter = ',')
        {
            var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return;

            string[] headers;
            int dataStartIndex;

            if (hasHeader)
            {
                headers = lines[0].Split(delimiter);
                dataStartIndex = 1;
            }
            else
            {
                var firstRow = lines[0].Split(delimiter);
                headers = Enumerable.Range(0, firstRow.Length).Select(i => $"Column{i}").ToArray();
                dataStartIndex = 0;
            }

            // Detect column types from first data row
            var firstDataRow = lines[dataStartIndex].Split(delimiter);
            var isNumeric = new bool[headers.Length];
            
            for (int i = 0; i < headers.Length; i++)
            {
                isNumeric[i] = double.TryParse(firstDataRow[i], out _);
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

            for (int row = dataStartIndex; row < lines.Length; row++)
            {
                var values = lines[row].Split(delimiter);
                for (int col = 0; col < headers.Length && col < values.Length; col++)
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
            var sb = new StringBuilder();
            
            // Header
            sb.AppendLine(string.Join(delimiter.ToString(), _columnNames));

            // Data
            for (int i = 0; i < _rowCount; i++)
            {
                var values = new List<string>();
                foreach (var colName in _columnNames)
                {
                    var type = _columnTypes[colName];
                    if (type == ColumnType.Numeric)
                        values.Add(_numericData[colName].GetDouble(i).ToString());
                    else
                        values.Add(_stringData[colName][i]);
                }
                sb.AppendLine(string.Join(delimiter.ToString(), values));
            }

            return sb.ToString();
        }

        public string ExportToCsv(char delimiter, bool includeHeader)
        {
            var sb = new StringBuilder();
            
            // Header
            if (includeHeader)
            {
                sb.AppendLine(string.Join(delimiter.ToString(), _columnNames));
            }

            // Data
            for (int i = 0; i < _rowCount; i++)
            {
                var values = new List<string>();
                foreach (var colName in _columnNames)
                {
                    var type = _columnTypes[colName];
                    if (type == ColumnType.Numeric)
                        values.Add(_numericData[colName].GetDouble(i).ToString());
                    else
                        values.Add(_stringData[colName][i]);
                }
                sb.AppendLine(string.Join(delimiter.ToString(), values));
            }

            return sb.ToString();
        }

        public double Mean(string columnName)
        {
            return Average(columnName);
        }

        public double Std(string columnName)
        {
            var data = GetNumericColumn(columnName).ToArray<double>();
            if (data.Length == 0) return 0;
            
            var mean = data.Average();
            var sumSquares = data.Sum(x => (x - mean) * (x - mean));
            return Math.Sqrt(sumSquares / data.Length);
        }

        public int[] Where(string column, QueryOp op, object value)
        {
            var indices = new List<int>();
            for (int i = 0; i < _rowCount; i++)
            {
                var row = GetRow(i);
                if (!row.TryGetValue(column, out var cellValue))
                    continue;

                bool match = op switch
                {
                    QueryOp.Eq => Equals(cellValue, value),
                    QueryOp.Neq => !Equals(cellValue, value),
                    QueryOp.Gt => Convert.ToDouble(cellValue) > Convert.ToDouble(value),
                    QueryOp.Gte => Convert.ToDouble(cellValue) >= Convert.ToDouble(value),
                    QueryOp.Lt => Convert.ToDouble(cellValue) < Convert.ToDouble(value),
                    QueryOp.Lte => Convert.ToDouble(cellValue) <= Convert.ToDouble(value),
                    QueryOp.Contains => cellValue?.ToString().Contains(value?.ToString() ?? "") ?? false,
                    QueryOp.StartsWith => cellValue?.ToString().StartsWith(value?.ToString() ?? "") ?? false,
                    QueryOp.EndsWith => cellValue?.ToString().EndsWith(value?.ToString() ?? "") ?? false,
                    _ => false
                };

                if (match)
                    indices.Add(i);
            }
            return indices.ToArray();
        }

        public void CreateIndex(string columnName)
        {
            // 内存实现中索引不必要，但为了接口兼容提供空实现
            // No-op for in-memory implementation
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
                    QueryOp.Neq => WhereNotEquals(column, value),
                    QueryOp.Gt => WhereGreaterThan(column, Convert.ToDouble(value)),
                    QueryOp.Gte => WhereGreaterThanOrEqual(column, Convert.ToDouble(value)),
                    QueryOp.Lt => WhereLessThan(column, Convert.ToDouble(value)),
                    QueryOp.Lte => WhereLessThanOrEqual(column, Convert.ToDouble(value)),
                    QueryOp.Contains => WhereContains(column, value?.ToString() ?? ""),
                    QueryOp.StartsWith => WhereStartsWith(column, value?.ToString() ?? ""),
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
                var indices = new List<int>();
                for (int i = 0; i < _source.RowCount; i++)
                {
                    var row = _source.GetRow(i);
                    if (_filters.All(f => f(row)))
                        indices.Add(i);
                }
                return indices.ToArray();
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
                    result = _orderDescending
                        ? result.OrderByDescending(x => x.row[_orderByColumn])
                        : result.OrderBy(x => x.row[_orderByColumn]);
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
                for (int i = 0; i < _source.RowCount; i++)
                {
                    var row = _source.GetRow(i);
                    if (_filters.All(f => f(row)))
                        return true;
                }
                return false;
            }

            public Dictionary<string, object> FirstOrDefault()
            {
                for (int i = 0; i < _source.RowCount; i++)
                {
                    var row = _source.GetRow(i);
                    if (_filters.All(f => f(row)))
                    {
                        if (_selectedColumns != null && _selectedColumns.Count > 0)
                        {
                            return row.Where(kvp => _selectedColumns.Contains(kvp.Key))
                                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                        }
                        return row;
                    }
                }
                return null;
            }

            public double Sum(string column)
            {
                double sum = 0;
                for (int i = 0; i < _source.RowCount; i++)
                {
                    var row = _source.GetRow(i);
                    if (_filters.All(f => f(row)) && row.TryGetValue(column, out var value))
                    {
                        sum += Convert.ToDouble(value);
                    }
                }
                return sum;
            }

            public double Average(string column)
            {
                double sum = 0;
                int count = 0;
                for (int i = 0; i < _source.RowCount; i++)
                {
                    var row = _source.GetRow(i);
                    if (_filters.All(f => f(row)) && row.TryGetValue(column, out var value))
                    {
                        sum += Convert.ToDouble(value);
                        count++;
                    }
                }
                return count > 0 ? sum / count : 0;
            }

            public double Max(string column)
            {
                double max = double.MinValue;
                bool found = false;
                for (int i = 0; i < _source.RowCount; i++)
                {
                    var row = _source.GetRow(i);
                    if (_filters.All(f => f(row)) && row.TryGetValue(column, out var value))
                    {
                        var val = Convert.ToDouble(value);
                        if (!found || val > max)
                        {
                            max = val;
                            found = true;
                        }
                    }
                }
                return found ? max : 0;
            }

            public double Min(string column)
            {
                double min = double.MaxValue;
                bool found = false;
                for (int i = 0; i < _source.RowCount; i++)
                {
                    var row = _source.GetRow(i);
                    if (_filters.All(f => f(row)) && row.TryGetValue(column, out var value))
                    {
                        var val = Convert.ToDouble(value);
                        if (!found || val < min)
                        {
                            min = val;
                            found = true;
                        }
                    }
                }
                return found ? min : 0;
            }
        }
    }
}
