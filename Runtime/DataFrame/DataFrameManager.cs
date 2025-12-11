using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataCore.Monitoring;
using DataCore.Platform;
using DataCore.Serialization;
using Microsoft.Data.Analysis;
using NumSharp;

namespace DataCore.DataFrame
{
    /// <summary>
    /// Manages DataFrame datasets with versioning, persistence, and memory optimization
    /// </summary>
    public class DataFrameManager : IDisposable
    {
        private readonly Dictionary<string, DataFrameDataset> _datasets;
        private readonly Dictionary<string, List<DataFrameVersion>> _versionHistory;
        private readonly IFileSystem _fileSystem;
        private readonly ISerializer<Microsoft.Data.Analysis.DataFrame> _serializer;
        private readonly MemoryUsageTracker _memoryTracker;
        private readonly object _lock = new object();
        
        private bool _disposed;
        
        /// <summary>
        /// Maximum memory usage in bytes (default: 1GB)
        /// </summary>
        public long MaxMemoryUsage { get; set; } = 1024 * 1024 * 1024; // 1GB
        
        /// <summary>
        /// Memory usage warning threshold (default: 80% of MaxMemoryUsage)
        /// </summary>
        public long MemoryWarningThreshold => (long)(MaxMemoryUsage * 0.8);
        
        /// <summary>
        /// Current memory usage in bytes
        /// </summary>
        public long CurrentMemoryUsage => _memoryTracker.TotalMemoryUsage;
        
        /// <summary>
        /// Number of datasets currently loaded
        /// </summary>
        public int DatasetCount => _datasets.Count;
        
        /// <summary>
        /// Event fired when memory usage exceeds warning threshold
        /// </summary>
        public event Action<long> OnMemoryWarning;
        
        /// <summary>
        /// Event fired when a dataset is loaded
        /// </summary>
        public event Action<string, DataFrameMetadata> OnDatasetLoaded;
        
        /// <summary>
        /// Event fired when a dataset is saved
        /// </summary>
        public event Action<string, DataFrameMetadata> OnDatasetSaved;
        
        public DataFrameManager(IFileSystem fileSystem = null, ISerializer<Microsoft.Data.Analysis.DataFrame> serializer = null)
        {
            _datasets = new Dictionary<string, DataFrameDataset>();
            _versionHistory = new Dictionary<string, List<DataFrameVersion>>();
            _fileSystem = fileSystem ?? FileSystemFactory.GetFileSystem();
            _serializer = serializer ?? new DataFrameCsvSerializer();
            _memoryTracker = new MemoryUsageTracker();
        }
        
        /// <summary>
        /// Get a DataFrame dataset by name
        /// </summary>
        public Microsoft.Data.Analysis.DataFrame Get(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            lock (_lock)
            {
                if (!_datasets.TryGetValue(name, out var dataset))
                {
                    throw new KeyNotFoundException($"DataFrame '{name}' not found");
                }
                
                dataset.LastAccessTime = DateTime.UtcNow;
                dataset.AccessCount++;
                return dataset.Data;
            }
        }
        
        /// <summary>
        /// Get a specific row from a DataFrame
        /// </summary>
        public DataFrameRow GetRow(string name, long rowIndex)
        {
            var df = Get(name);
            return df.Rows[rowIndex];
        }
        
        /// <summary>
        /// Get a specific column from a DataFrame
        /// </summary>
        public DataFrameColumn GetColumn(string name, string columnName)
        {
            var df = Get(name);
            return df[columnName];
        }
        
        /// <summary>
        /// Get a specific cell value
        /// </summary>
        public object GetValue(string name, long rowIndex, string columnName)
        {
            var df = Get(name);
            return df[columnName][rowIndex];
        }
        
        /// <summary>
        /// Set a DataFrame dataset
        /// </summary>
        public void Set(string name, Microsoft.Data.Analysis.DataFrame data, Dictionary<string, object> properties = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
                
            lock (_lock)
            {
                var metadata = new DataFrameMetadata(name)
                {
                    RowCount = data.Rows.Count,
                    ColumnCount = data.Columns.Count,
                    ColumnNames = data.Columns.Select(c => c.Name).ToList(),
                    ColumnTypes = data.Columns.Select(c => c.DataType.Name).ToList(),
                    Properties = properties ?? new Dictionary<string, object>()
                };
                
                var dataset = new DataFrameDataset
                {
                    Data = data,
                    Metadata = metadata,
                    LastAccessTime = DateTime.UtcNow,
                    AccessCount = 0
                };
                
                // Check if dataset already exists
                if (_datasets.ContainsKey(name))
                {
                    // Remove old dataset from memory tracking
                    var oldDataset = _datasets[name];
                    _memoryTracker.ReleaseMemory(oldDataset.Metadata.GetEstimatedSizeInBytes());
                }
                
                _datasets[name] = dataset;
                _memoryTracker.AllocateMemory(metadata.GetEstimatedSizeInBytes());
                
                // Add to version history
                if (!_versionHistory.ContainsKey(name))
                {
                    _versionHistory[name] = new List<DataFrameVersion>();
                }
                
                var version = new DataFrameVersion
                {
                    VersionNumber = metadata.Version,
                    Description = "Dataset created/updated",
                    RowCount = data.Rows.Count
                };
                
                _versionHistory[name].Add(version);
                
                CheckMemoryUsage();
            }
        }
        
        /// <summary>
        /// Add a column to a DataFrame
        /// </summary>
        public void AddColumn(string name, DataFrameColumn column)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
            if (column == null)
                throw new ArgumentNullException(nameof(column));
                
            lock (_lock)
            {
                var df = Get(name);
                df.Columns.Add(column);
                
                // Update metadata
                _datasets[name].Metadata.ColumnNames.Add(column.Name);
                _datasets[name].Metadata.ColumnTypes.Add(column.DataType.Name);
                _datasets[name].Metadata.Touch();
            }
        }
        
        /// <summary>
        /// Remove a column from a DataFrame
        /// </summary>
        public void RemoveColumn(string name, string columnName)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
            if (string.IsNullOrEmpty(columnName))
                throw new ArgumentException("Column name cannot be null or empty", nameof(columnName));
                
            lock (_lock)
            {
                var df = Get(name);
                df.Columns.Remove(columnName);
                
                // Update metadata
                var index = _datasets[name].Metadata.ColumnNames.IndexOf(columnName);
                if (index >= 0)
                {
                    _datasets[name].Metadata.ColumnNames.RemoveAt(index);
                    _datasets[name].Metadata.ColumnTypes.RemoveAt(index);
                    _datasets[name].Metadata.Touch();
                }
            }
        }
        
        /// <summary>
        /// Rename a column
        /// </summary>
        public void RenameColumn(string name, string oldName, string newName)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
            if (string.IsNullOrEmpty(oldName))
                throw new ArgumentException("Old column name cannot be null or empty", nameof(oldName));
            if (string.IsNullOrEmpty(newName))
                throw new ArgumentException("New column name cannot be null or empty", nameof(newName));
                
            lock (_lock)
            {
                var df = Get(name);
                var column = df[oldName];
                column.SetName(newName);
                
                // Update metadata
                var index = _datasets[name].Metadata.ColumnNames.IndexOf(oldName);
                if (index >= 0)
                {
                    _datasets[name].Metadata.ColumnNames[index] = newName;
                    _datasets[name].Metadata.Touch();
                }
            }
        }
        
        /// <summary>
        /// Filter rows based on a condition
        /// </summary>
        public Microsoft.Data.Analysis.DataFrame Filter(string name, DataFrameColumn condition)
        {
            var df = Get(name);
            return df.Filter(condition);
        }
        
        /// <summary>
        /// Sort DataFrame by column
        /// </summary>
        public Microsoft.Data.Analysis.DataFrame OrderBy(string name, string columnName, bool ascending = true)
        {
            var df = Get(name);
            return df.OrderBy(columnName);
        }
        
        /// <summary>
        /// Remove duplicate rows
        /// </summary>
        public Microsoft.Data.Analysis.DataFrame DropDuplicates(string name, string[] columnNames = null)
        {
            var df = Get(name);
            if (columnNames == null)
                return df.DropDuplicates();
            return df.DropDuplicates(columnNames);
        }
        
        /// <summary>
        /// Sample rows from DataFrame
        /// </summary>
        public Microsoft.Data.Analysis.DataFrame Sample(string name, int count, int? seed = null)
        {
            var df = Get(name);
            return df.Sample(count, seed);
        }
        
        /// <summary>
        /// Group by columns and aggregate
        /// </summary>
        public Microsoft.Data.Analysis.DataFrame GroupBy(string name, string[] groupByColumns, Dictionary<string, string> aggregations)
        {
            var df = Get(name);
            var grouped = df.GroupBy(groupByColumns);
            
            // Apply aggregations
            var result = grouped;
            foreach (var agg in aggregations)
            {
                result = result.Aggregate(agg.Key, agg.Value);
            }
            
            return result;
        }
        
        /// <summary>
        /// Join two DataFrames
        /// </summary>
        public Microsoft.Data.Analysis.DataFrame Join(string leftName, string rightName, string leftColumn, string rightColumn, JoinAlgorithm joinType = JoinAlgorithm.Inner)
        {
            var leftDf = Get(leftName);
            var rightDf = Get(rightName);
            return leftDf.Join(rightDf, leftColumn, rightColumn, joinType);
        }
        
        /// <summary>
        /// Check if a dataset exists
        /// </summary>
        public bool Contains(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
                
            lock (_lock)
            {
                return _datasets.ContainsKey(name);
            }
        }
        
        /// <summary>
        /// Remove a dataset
        /// </summary>
        public bool Remove(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
                
            lock (_lock)
            {
                if (_datasets.TryGetValue(name, out var dataset))
                {
                    _memoryTracker.ReleaseMemory(dataset.Metadata.GetEstimatedSizeInBytes());
                    _datasets.Remove(name);
                    return true;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Load a DataFrame from CSV file asynchronously
        /// </summary>
        public async Task<Microsoft.Data.Analysis.DataFrame> LoadCsvAsync(string name, string filePath, char separator = ',', bool header = true, int chunkSize = 10000, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            
            try
            {
                var data = await _serializer.DeserializeAsync(filePath, cancellationToken);
                
                Set(name, data, new Dictionary<string, object>
                {
                    ["file_path"] = filePath,
                    ["loaded_time"] = DateTime.UtcNow,
                    ["format"] = "csv"
                });
                
                OnDatasetLoaded?.Invoke(name, _datasets[name].Metadata);
                
                return data;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load DataFrame '{name}' from '{filePath}'", ex);
            }
        }
        
        /// <summary>
        /// Load a DataFrame from JSON file asynchronously
        /// </summary>
        public async Task<Microsoft.Data.Analysis.DataFrame> LoadJsonAsync(string name, string filePath, CancellationToken cancellationToken = default)
        {
            // Implementation would use JSON serializer
            throw new NotImplementedException("JSON loading not yet implemented");
        }
        
        /// <summary>
        /// Save a DataFrame to CSV file asynchronously
        /// </summary>
        public async Task SaveCsvAsync(string name, string filePath, char separator = ',', bool header = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
                
            Microsoft.Data.Analysis.DataFrame data;
            lock (_lock)
            {
                if (!_datasets.TryGetValue(name, out var dataset))
                {
                    throw new KeyNotFoundException($"DataFrame '{name}' not found");
                }
                data = dataset.Data;
            }
            
            try
            {
                await _serializer.SerializeAsync(data, filePath, cancellationToken);
                
                lock (_lock)
                {
                    _datasets[name].Metadata.FilePath = filePath;
                    _datasets[name].Metadata.Touch();
                }
                
                OnDatasetSaved?.Invoke(name, _datasets[name].Metadata);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save DataFrame '{name}' to '{filePath}'", ex);
            }
        }
        
        /// <summary>
        /// Save a DataFrame to JSON file asynchronously
        /// </summary>
        public async Task SaveJsonAsync(string name, string filePath, CancellationToken cancellationToken = default)
        {
            // Implementation would use JSON serializer
            throw new NotImplementedException("JSON saving not yet implemented");
        }
        
        /// <summary>
        /// Save all datasets
        /// </summary>
        public async Task SaveAllAsync(CancellationToken cancellationToken = default)
        {
            string[] datasetNames;
            lock (_lock)
            {
                datasetNames = _datasets.Keys.ToArray();
            }
            
            var tasks = datasetNames.Select(name => SaveCsvAsync(name, $"{name}.csv", cancellationToken: cancellationToken));
            await Task.WhenAll(tasks);
        }
        
        /// <summary>
        /// Get dataset metadata
        /// </summary>
        public DataFrameMetadata GetMetadata(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            lock (_lock)
            {
                if (_datasets.TryGetValue(name, out var dataset))
                {
                    return dataset.Metadata;
                }
                
                throw new KeyNotFoundException($"DataFrame '{name}' not found");
            }
        }
        
        /// <summary>
        /// Get all dataset names
        /// </summary>
        public string[] GetDatasetNames()
        {
            lock (_lock)
            {
                return _datasets.Keys.ToArray();
            }
        }
        
        /// <summary>
        /// Clear all datasets
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                foreach (var dataset in _datasets.Values)
                {
                    _memoryTracker.ReleaseMemory(dataset.Metadata.GetEstimatedSizeInBytes());
                }
                
                _datasets.Clear();
                _versionHistory.Clear();
            }
        }
        
        /// <summary>
        /// Get memory usage statistics
        /// </summary>
        public MemoryUsageReport GetMemoryReport()
        {
            return _memoryTracker.GetReport();
        }
        
        private void CheckMemoryUsage()
        {
            if (CurrentMemoryUsage > MemoryWarningThreshold)
            {
                OnMemoryWarning?.Invoke(CurrentMemoryUsage);
            }
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                Clear();
                _disposed = true;
            }
        }
        
        private class DataFrameDataset
        {
            public Microsoft.Data.Analysis.DataFrame Data { get; set; }
            public DataFrameMetadata Metadata { get; set; }
            public DateTime LastAccessTime { get; set; }
            public int AccessCount { get; set; }
        }
    }
    
    /// <summary>
    /// Metadata for DataFrame datasets
    /// </summary>
    [Serializable]
    public class DataFrameMetadata
    {
        public string Name { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime ModifiedTime { get; set; }
        public long RowCount { get; set; }
        public int ColumnCount { get; set; }
        public List<string> ColumnNames { get; set; } = new List<string>();
        public List<string> ColumnTypes { get; set; } = new List<string>();
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public int Version { get; set; } = 1;
        public string FilePath { get; set; }
        public string Format { get; set; }
        
        public DataFrameMetadata()
        {
            CreatedTime = DateTime.UtcNow;
            ModifiedTime = DateTime.UtcNow;
        }
        
        public DataFrameMetadata(string name) : this()
        {
            Name = name;
        }
        
        public void Touch()
        {
            ModifiedTime = DateTime.UtcNow;
            Version++;
        }
        
        public long GetEstimatedSizeInBytes()
        {
            // Rough estimate: 100 bytes per cell
            return RowCount * ColumnCount * 100;
        }
    }
    
    /// <summary>
    /// Version information for DataFrame datasets
    /// </summary>
    [Serializable]
    public class DataFrameVersion
    {
        public int VersionNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public long RowCount { get; set; }
        
        public DataFrameVersion()
        {
            Timestamp = DateTime.UtcNow;
        }
    }
}