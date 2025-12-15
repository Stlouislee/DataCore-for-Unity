using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NumSharp;
using AroAro.DataCore.Events;
using AroAro.DataCore.Events;

namespace AroAro.DataCore.Tabular
{
    public sealed class TabularData : IDataSet
    {
        private readonly Dictionary<string, IColumn> _columns = new(StringComparer.Ordinal);

        public TabularData(string name)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name required", nameof(name)) : name;
        }

        public string Name { get; }
        public DataSetKind Kind => DataSetKind.Tabular;

        public int RowCount { get; private set; }

        public IReadOnlyCollection<string> ColumnNames => _columns.Keys;

        public IDataSet WithName(string name)
        {
            var clone = CloneShallow(name);
            return clone;
        }

        public TabularData CloneShallow(string newName)
        {
            var t = new TabularData(newName)
            {
                RowCount = RowCount
            };
            foreach (var kv in _columns)
                t._columns[kv.Key] = kv.Value.Clone();
            return t;
        }

        public void AddNumericColumn(string name, NDArray data)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Column name required", nameof(name));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.ndim != 1) throw new ArgumentException("Only 1D arrays supported", nameof(data));

            var len = (int)data.size;
            EnsureRowCountCompatible(len);
            _columns[name] = new NumericColumn(name, data.copy());
            if (RowCount == 0) RowCount = len;

            // 触发数据集修改事件
            DataCoreEventManager.RaiseDatasetModified(this, "AddNumericColumn", new { name, length = len });
        }

        public void AddStringColumn(string name, string[] data)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Column name required", nameof(name));
            if (data == null) throw new ArgumentNullException(nameof(data));

            EnsureRowCountCompatible(data.Length);
            _columns[name] = new StringColumn(name, (string[])data.Clone());
            if (RowCount == 0) RowCount = data.Length;

            // 触发数据集修改事件
            DataCoreEventManager.RaiseDatasetModified(this, "AddStringColumn", new { name, length = data.Length });
        }

        public bool RemoveColumn(string name)
        {
            var removed = _columns.Remove(name);
            if (removed)
            {
                DataCoreEventManager.RaiseDatasetModified(this, "RemoveColumn", name);
            }
            return removed;
        }

        public bool HasColumn(string name) => _columns.ContainsKey(name);

        public NDArray GetNumericColumn(string name)
        {
            if (!_columns.TryGetValue(name, out var col)) throw new KeyNotFoundException($"Column not found: {name}");
            if (col is not NumericColumn n) throw new InvalidOperationException($"Column '{name}' is not numeric");
            return n.Data;
        }

        public string[] GetStringColumn(string name)
        {
            if (!_columns.TryGetValue(name, out var col)) throw new KeyNotFoundException($"Column not found: {name}");
            if (col is not StringColumn s) throw new InvalidOperationException($"Column '{name}' is not string");
            return s.Data;
        }

        public void AddRow(IDictionary<string, object> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            if (_columns.Count == 0)
                throw new InvalidOperationException("Define columns before adding rows");

            foreach (var col in _columns.Values)
                col.Append(values.TryGetValue(col.Name, out var v) ? v : null);

            RowCount += 1;

            DataCoreEventManager.RaiseDatasetModified(this, "AddRow", new { RowIndex = RowCount - 1 });
        }

        public void UpdateRow(int rowIndex, IDictionary<string, object> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (rowIndex < 0 || rowIndex >= RowCount) throw new ArgumentOutOfRangeException(nameof(rowIndex));

            foreach (var kv in values)
            {
                if (!_columns.TryGetValue(kv.Key, out var col))
                    throw new KeyNotFoundException($"Column not found: {kv.Key}");
                col.Set(rowIndex, kv.Value);
            }

            DataCoreEventManager.RaiseDatasetModified(this, "UpdateRow", new { RowIndex = rowIndex });
        }

        public void DeleteRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= RowCount) throw new ArgumentOutOfRangeException(nameof(rowIndex));
            foreach (var col in _columns.Values)
                col.RemoveAt(rowIndex);
            RowCount -= 1;

            DataCoreEventManager.RaiseDatasetModified(this, "DeleteRow", new { RowIndex = rowIndex });
        }

        public TabularQuery Query() => new TabularQuery(this);

        /// <summary>
        /// 从 CSV 字符串导入数据
        /// </summary>
        /// <param name="csvContent">CSV 内容</param>
        /// <param name="hasHeader">第一行是否为列名</param>
        /// <param name="delimiter">分隔符，默认为逗号</param>
        public void ImportFromCsv(string csvContent, bool hasHeader = true, char delimiter = ',')
        {
            if (string.IsNullOrEmpty(csvContent))
                throw new ArgumentException("CSV content cannot be null or empty", nameof(csvContent));

            var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return;

            // 解析列名
            var headerLine = hasHeader ? lines[0] : null;
            var dataStartIndex = hasHeader ? 1 : 0;

            // 批量解析所有数据行
            var dataRows = new List<List<string>>();
            for (int i = dataStartIndex; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var values = ParseCsvLine(line, delimiter);
                dataRows.Add(values);
            }

            if (dataRows.Count == 0)
                return;

            // 确定列数和列名
            int columnCount = dataRows.Max(row => row.Count);
            var headers = new List<string>();

            if (headerLine != null)
            {
                headers = ParseCsvLine(headerLine, delimiter);
                // 确保列数一致
                columnCount = Math.Max(columnCount, headers.Count);
            }
            else
            {
                for (int i = 0; i < columnCount; i++)
                {
                    headers.Add($"Column{i}");
                }
            }

            // 批量确定列类型
            var columnTypes = new ColumnType[columnCount];
            for (int col = 0; col < columnCount; col++)
            {
                columnTypes[col] = DetermineColumnTypeBatch(dataRows, col);
            }

            // 批量创建列数据
            var numericData = new Dictionary<string, List<double>>();
            var stringData = new Dictionary<string, List<string>>();

            for (int col = 0; col < columnCount; col++)
            {
                var columnName = col < headers.Count ? headers[col] : $"Column{col}";
                
                if (columnTypes[col] == ColumnType.Numeric)
                {
                    numericData[columnName] = new List<double>();
                }
                else
                {
                    stringData[columnName] = new List<string>();
                }
            }

            // 批量填充数据
            foreach (var row in dataRows)
            {
                for (int col = 0; col < columnCount; col++)
                {
                    var columnName = col < headers.Count ? headers[col] : $"Column{col}";
                    var value = col < row.Count ? row[col] : "";

                    if (columnTypes[col] == ColumnType.Numeric)
                    {
                        if (double.TryParse(value, out double numValue))
                            numericData[columnName].Add(numValue);
                        else
                            numericData[columnName].Add(0.0);
                    }
                    else
                    {
                        stringData[columnName].Add(value ?? "");
                    }
                }
            }

            // 批量添加列
            foreach (var kv in numericData)
            {
                AddNumericColumn(kv.Key, np.array(kv.Value.ToArray()));
            }
            foreach (var kv in stringData)
            {
                AddStringColumn(kv.Key, kv.Value.ToArray());
            }
        }

        /// <summary>
        /// 从 CSV 文件导入数据
        /// </summary>
        /// <param name="filePath">CSV 文件路径</param>
        /// <param name="hasHeader">第一行是否为列名</param>
        /// <param name="delimiter">分隔符，默认为逗号</param>
        /// <param name="useFastMode">是否使用快速模式（适用于简单CSV文件）</param>
        public void ImportFromCsvFile(string filePath, bool hasHeader = true, char delimiter = ',', bool useFastMode = false)
        {
            if (!System.IO.File.Exists(filePath))
                throw new FileNotFoundException($"CSV file not found: {filePath}");

            // 对于大文件，使用流式读取
            var fileInfo = new System.IO.FileInfo(filePath);
            if (fileInfo.Length > 1024 * 1024) // 大于1MB的文件
            {
                ImportFromCsvFileStream(filePath, hasHeader, delimiter, useFastMode);
            }
            else
            {
                var content = System.IO.File.ReadAllText(filePath);
                ImportFromCsv(content, hasHeader, delimiter);
            }
        }

        /// <summary>
        /// 使用流式处理导入大CSV文件
        /// </summary>
        private void ImportFromCsvFileStream(string filePath, bool hasHeader, char delimiter, bool useFastMode)
        {
            var dataRows = new List<List<string>>();
            var headers = new List<string>();

            using (var reader = new System.IO.StreamReader(filePath))
            {
                string line;
                bool isFirstLine = true;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    List<string> values;
                    if (useFastMode)
                    {
                        values = ParseCsvLineFast(line, delimiter);
                    }
                    else
                    {
                        values = ParseCsvLine(line, delimiter);
                    }
                    
                    if (isFirstLine && hasHeader)
                    {
                        headers = values;
                        isFirstLine = false;
                        continue;
                    }

                    dataRows.Add(values);
                    
                    // 限制内存使用，分批处理
                    if (dataRows.Count >= 10000)
                    {
                        ProcessBatchData(dataRows, headers, hasHeader);
                        dataRows.Clear();
                    }
                }

                // 处理剩余数据
                if (dataRows.Count > 0)
                {
                    ProcessBatchData(dataRows, headers, hasHeader);
                }
            }
        }

        private void ProcessBatchData(List<List<string>> dataRows, List<string> headers, bool hasHeader)
        {
            if (dataRows.Count == 0)
                return;

            int columnCount = dataRows.Max(row => row.Count);
            
            if (!hasHeader || headers.Count == 0)
            {
                headers.Clear();
                for (int i = 0; i < columnCount; i++)
                {
                    headers.Add($"Column{i}");
                }
            }

            // 批量确定列类型（仅对当前批次）
            var columnTypes = new ColumnType[columnCount];
            for (int col = 0; col < columnCount; col++)
            {
                columnTypes[col] = DetermineColumnTypeBatch(dataRows, col);
            }

            // 批量创建列数据
            var numericData = new Dictionary<string, List<double>>();
            var stringData = new Dictionary<string, List<string>>();

            for (int col = 0; col < columnCount; col++)
            {
                var columnName = col < headers.Count ? headers[col] : $"Column{col}";
                
                if (columnTypes[col] == ColumnType.Numeric)
                {
                    if (!numericData.ContainsKey(columnName))
                        numericData[columnName] = new List<double>();
                }
                else
                {
                    if (!stringData.ContainsKey(columnName))
                        stringData[columnName] = new List<string>();
                }
            }

            // 批量填充数据
            foreach (var row in dataRows)
            {
                for (int col = 0; col < columnCount; col++)
                {
                    var columnName = col < headers.Count ? headers[col] : $"Column{col}";
                    var value = col < row.Count ? row[col] : "";

                    if (columnTypes[col] == ColumnType.Numeric)
                    {
                        if (double.TryParse(value, out double numValue))
                            numericData[columnName].Add(numValue);
                        else
                            numericData[columnName].Add(0.0);
                    }
                    else
                    {
                        stringData[columnName].Add(value ?? "");
                    }
                }
            }

            // 批量添加或更新列
            foreach (var kv in numericData)
            {
                if (_columns.ContainsKey(kv.Key))
                {
                    var existingColumn = _columns[kv.Key] as NumericColumn;
                    if (existingColumn != null)
                    {
                        // 合并数据
                        var combinedData = existingColumn.Data.Data<double>().Concat(kv.Value).ToArray();
                        _columns[kv.Key] = new NumericColumn(kv.Key, np.array(combinedData));
                    }
                }
                else
                {
                    AddNumericColumn(kv.Key, np.array(kv.Value.ToArray()));
                }
            }
            
            foreach (var kv in stringData)
            {
                if (_columns.ContainsKey(kv.Key))
                {
                    var existingColumn = _columns[kv.Key] as StringColumn;
                    if (existingColumn != null)
                    {
                        // 合并数据
                        var combinedData = existingColumn.Data.Concat(kv.Value).ToArray();
                        _columns[kv.Key] = new StringColumn(kv.Key, combinedData);
                    }
                }
                else
                {
                    AddStringColumn(kv.Key, kv.Value.ToArray());
                }
            }

            // 更新行数
            RowCount += dataRows.Count;
        }

        private enum ColumnType
        {
            Numeric,
            String
        }

        private ColumnType DetermineColumnType(string[] lines, int startIndex, int columnIndex, char delimiter)
        {
            for (int i = startIndex; i < Math.Min(startIndex + 100, lines.Length); i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var values = ParseCsvLine(line, delimiter);
                if (columnIndex >= values.Count)
                    continue;

                var value = values[columnIndex];
                if (string.IsNullOrEmpty(value))
                    continue;

                if (double.TryParse(value, out _))
                    return ColumnType.Numeric;
            }
            return ColumnType.String;
        }

        private ColumnType DetermineColumnTypeBatch(List<List<string>> dataRows, int columnIndex)
        {
            int numericCount = 0;
            int totalCount = 0;

            foreach (var row in dataRows)
            {
                if (columnIndex >= row.Count)
                    continue;

                var value = row[columnIndex];
                if (string.IsNullOrEmpty(value))
                    continue;

                totalCount++;
                if (double.TryParse(value, out _))
                    numericCount++;

                // 如果前100行中有超过70%是数字，就认为是数字列
                if (totalCount >= 100)
                    break;
            }

            return totalCount > 0 && numericCount * 1.0 / totalCount > 0.7 ? ColumnType.Numeric : ColumnType.String;
        }

        private List<string> ParseCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            // 预分配容量，减少内存分配
            sb.EnsureCapacity(line.Length / 10);

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // 转义的双引号
                        sb.Append('"');
                        i++; // 跳过下一个引号
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

        /// <summary>
        /// 高性能CSV解析方法，适用于简单CSV（无引号转义）
        /// </summary>
        private List<string> ParseCsvLineFast(string line, char delimiter)
        {
            var result = new List<string>();
            int start = 0;
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(line.Substring(start, i - start));
                    start = i + 1;
                }
            }

            // 添加最后一个字段
            if (start < line.Length)
            {
                result.Add(line.Substring(start));
            }
            else if (start == line.Length)
            {
                result.Add(""); // 空字段
            }

            return result;
        }

        private void EnsureRowCountCompatible(int incomingLength)
        {
            if (RowCount == 0) return;
            if (incomingLength != RowCount)
                throw new InvalidOperationException($"Column length {incomingLength} does not match RowCount {RowCount}");
        }

        internal IColumn GetColumnInternal(string name)
        {
            if (!_columns.TryGetValue(name, out var col))
                throw new KeyNotFoundException($"Column not found: {name}");
            return col;
        }

        internal IEnumerable<IColumn> ColumnsInternal => _columns.Values;
    }

    internal interface IColumn
    {
        string Name { get; }
        ColumnKind Kind { get; }
        int Length { get; }
        IColumn Clone();
        void Append(object value);
        void Set(int index, object value);
        void RemoveAt(int index);
    }

    internal enum ColumnKind
    {
        Numeric = 1,
        String = 2,
    }

    internal sealed class NumericColumn : IColumn
    {
        public NumericColumn(string name, NDArray data)
        {
            Name = name;
            Data = data;
        }

        public string Name { get; }
        public ColumnKind Kind => ColumnKind.Numeric;
        public NDArray Data { get; private set; }
        public int Length => (int)Data.size;

        public IColumn Clone() => new NumericColumn(Name, Data.copy());

        public void Append(object value)
        {
            // Keep numeric as double for widest compatibility.
            var v = value == null ? double.NaN : Convert.ToDouble(value);
            var current = Data.astype(np.float64);
            var single = np.array(new[] { v });
            Data = np.concatenate(new[] { current, single });
        }

        public void Set(int index, object value)
        {
            var v = value == null ? double.NaN : Convert.ToDouble(value);
            Data.SetDouble(v, index);
        }

        public void RemoveAt(int index)
        {
            var current = Data.astype(np.float64);
            var len = (int)current.size;
            var next = new double[len - 1];
            var write = 0;
            for (var i = 0; i < len; i++)
            {
                if (i == index) continue;
                next[write++] = Convert.ToDouble((object)current.GetValue(i));
            }
            Data = np.array(next);
        }
    }

    internal sealed class StringColumn : IColumn
    {
        public StringColumn(string name, string[] data)
        {
            Name = name;
            Data = data;
        }

        public string Name { get; }
        public ColumnKind Kind => ColumnKind.String;
        public string[] Data { get; private set; }
        public int Length => Data.Length;

        public IColumn Clone() => new StringColumn(Name, (string[])Data.Clone());

        public void Append(object value)
        {
            var s = value?.ToString();
            var next = new string[Data.Length + 1];
            Array.Copy(Data, next, Data.Length);
            next[^1] = s;
            Data = next;
        }

        public void Set(int index, object value)
        {
            Data[index] = value?.ToString();
        }

        public void RemoveAt(int index)
        {
            var next = new string[Data.Length - 1];
            if (index > 0) Array.Copy(Data, 0, next, 0, index);
            if (index < Data.Length - 1) Array.Copy(Data, index + 1, next, index, Data.Length - index - 1);
            Data = next;
        }
    }
}
