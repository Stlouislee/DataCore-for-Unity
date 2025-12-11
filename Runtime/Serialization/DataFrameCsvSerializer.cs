using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Analysis;

namespace DataCore.Serialization
{
    /// <summary>
    /// CSV serializer for DataFrame
    /// </summary>
    public class DataFrameCsvSerializer : ISerializer<DataFrame>
    {
        private readonly SerializerConfig _config;
        private readonly char _separator;
        private readonly bool _includeHeader;
        private readonly Encoding _encoding;
        
        public DataFrameCsvSerializer(SerializerConfig config = null, char separator = ',', bool includeHeader = true, Encoding encoding = null)
        {
            _config = config ?? new SerializerConfig { Format = SerializationFormat.Binary };
            _separator = separator;
            _includeHeader = includeHeader;
            _encoding = encoding ?? Encoding.UTF8;
        }
        
        public byte[] Serialize(DataFrame data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream, _encoding))
            {
                WriteDataFrame(writer, data);
                writer.Flush();
                return stream.ToArray();
            }
        }
        
        public DataFrame Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Bytes cannot be null or empty", nameof(bytes));
            
            using (var stream = new MemoryStream(bytes))
            using (var reader = new StreamReader(stream, _encoding))
            {
                return ReadDataFrame(reader);
            }
        }
        
        public async Task SerializeAsync(DataFrame data, string filePath, CancellationToken cancellationToken = default)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            using (var writer = new StreamWriter(stream, _encoding))
            {
                await WriteDataFrameAsync(writer, data, cancellationToken);
            }
        }
        
        public async Task<DataFrame> DeserializeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var reader = new StreamReader(stream, _encoding))
            {
                return await ReadDataFrameAsync(reader, cancellationToken);
            }
        }
        
        private void WriteDataFrame(StreamWriter writer, DataFrame data)
        {
            // Write header
            if (_includeHeader)
            {
                var header = string.Join(_separator.ToString(), data.Columns.Select(c => EscapeValue(c.Name)));
                writer.WriteLine(header);
            }
            
            // Write data rows
            for (long rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
            {
                var rowValues = new List<string>();
                foreach (var column in data.Columns)
                {
                    var value = column[rowIndex];
                    rowValues.Add(EscapeValue(FormatValue(value)));
                }
                
                var row = string.Join(_separator.ToString(), rowValues);
                writer.WriteLine(row);
            }
        }
        
        private async Task WriteDataFrameAsync(StreamWriter writer, DataFrame data, CancellationToken cancellationToken)
        {
            // Write header
            if (_includeHeader)
            {
                var header = string.Join(_separator.ToString(), data.Columns.Select(c => EscapeValue(c.Name)));
                await writer.WriteLineAsync(header);
            }
            
            // Write data rows
            for (long rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var rowValues = new List<string>();
                foreach (var column in data.Columns)
                {
                    var value = column[rowIndex];
                    rowValues.Add(EscapeValue(FormatValue(value)));
                }
                
                var row = string.Join(_separator.ToString(), rowValues);
                await writer.WriteLineAsync(row);
            }
        }
        
        private DataFrame ReadDataFrame(StreamReader reader)
        {
            var lines = new List<string>();
            string line;
            
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
            }
            
            if (lines.Count == 0)
                throw new InvalidDataException("CSV file is empty");
            
            return ParseDataFrame(lines);
        }
        
        private async Task<DataFrame> ReadDataFrameAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            var lines = new List<string>();
            string line;
            
            while ((line = await reader.ReadLineAsync()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lines.Add(line);
            }
            
            if (lines.Count == 0)
                throw new InvalidDataException("CSV file is empty");
            
            return ParseDataFrame(lines);
        }
        
        private DataFrame ParseDataFrame(List<string> lines)
        {
            if (lines.Count == 0)
                throw new InvalidDataException("No data to parse");
            
            // Parse header
            var headerLine = lines[0];
            var columnNames = ParseLine(headerLine);
            
            // Check if first line is actually a header (contains non-numeric values)
            bool hasHeader = _includeHeader || !IsNumericLine(lines[1]);
            
            if (!hasHeader)
            {
                // Generate column names
                columnNames = Enumerable.Range(0, columnNames.Count).Select(i => $"Column{i}").ToList();
            }
            
            // Parse data rows
            var dataRows = hasHeader ? lines.Skip(1).ToList() : lines;
            var columnData = new Dictionary<string, List<object>>();
            
            foreach (var columnName in columnNames)
            {
                columnData[columnName] = new List<object>();
            }
            
            foreach (var row in dataRows)
            {
                var values = ParseLine(row);
                if (values.Count != columnNames.Count)
                    continue; // Skip malformed rows
                
                for (int i = 0; i < columnNames.Count; i++)
                {
                    columnData[columnNames[i]].Add(ParseValue(values[i]));
                }
            }
            
            // Create DataFrame columns
            var columns = new List<DataFrameColumn>();
            foreach (var columnName in columnNames)
            {
                var values = columnData[columnName];
                var column = CreateColumn(columnName, values);
                columns.Add(column);
            }
            
            return new DataFrame(columns);
        }
        
        private List<string> ParseLine(string line)
        {
            var values = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            
            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        current.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == _separator && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }
            
            values.Add(current.ToString());
            return values;
        }
        
        private string EscapeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            
            // Escape quotes
            if (value.Contains('"'))
            {
                value = value.Replace("\"", "\"\"");
            }
            
            // Quote if contains separator, quote, or newline
            if (value.Contains(_separator) || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value}"";
            }
            
            return value;
        }
        
        private string FormatValue(object value)
        {
            if (value == null)
                return "";
            
            return value.ToString();
        }
        
        private object ParseValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            value = value.Trim();
            
            // Try to parse as boolean
            if (bool.TryParse(value, out var boolValue))
                return boolValue;
            
            // Try to parse as integer
            if (int.TryParse(value, out var intValue))
                return intValue;
            
            // Try to parse as long
            if (long.TryParse(value, out var longValue))
                return longValue;
            
            // Try to parse as double
            if (double.TryParse(value, out var doubleValue))
                return doubleValue;
            
            // Return as string
            return value;
        }
        
        private bool IsNumericLine(string line)
        {
            var values = ParseLine(line);
            return values.All(v => double.TryParse(v, out _));
        }
        
        private DataFrameColumn CreateColumn(string name, List<object> values)
        {
            if (values.Count == 0)
                return new StringDataFrameColumn(name, 0);
            
            // Determine column type based on first non-null value
            var sampleValue = values.FirstOrDefault(v => v != null);
            if (sampleValue == null)
                return new StringDataFrameColumn(name, values.Select(v => v?.ToString()));
            
            return sampleValue switch
            {
                bool _ => new BooleanDataFrameColumn(name, values.Select(v => v as bool?)),
                int _ => new Int32DataFrameColumn(name, values.Select(v => v as int?)),
                long _ => new Int64DataFrameColumn(name, values.Select(v => v as long?)),
                double _ => new DoubleDataFrameColumn(name, values.Select(v => v as double?)),
                float _ => new SingleDataFrameColumn(name, values.Select(v => v as float?)),
                _ => new StringDataFrameColumn(name, values.Select(v => v?.ToString()))
            };
        }
    }
}