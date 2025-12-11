using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataCore.Platform;
using DataCore.Serialization;
using NumSharp;

namespace DataCore.Tensor
{
    /// <summary>
    /// Manages tensor datasets (NDArray) with versioning, persistence, and memory optimization
    /// </summary>
    public class TensorDataManager : IDisposable
    {
        private readonly Dictionary<string, TensorDataset> _datasets;
        private readonly Dictionary<string, List<TensorVersion>> _versionHistory;
        private readonly TensorObjectPool _objectPool;
        private readonly IFileSystem _fileSystem;
        private readonly ISerializer<NDArray> _serializer;
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
        public event Action<string, TensorMetadata> OnDatasetLoaded;
        
        /// <summary>
        /// Event fired when a dataset is saved
        /// </summary>
        public event Action<string, TensorMetadata> OnDatasetSaved;
        
        public TensorDataManager(IFileSystem fileSystem = null, ISerializer<NDArray> serializer = null)
        {
            _datasets = new Dictionary<string, TensorDataset>();
            _versionHistory = new Dictionary<string, List<TensorVersion>>();
            _objectPool = new TensorObjectPool();
            _fileSystem = fileSystem ?? FileSystemFactory.GetFileSystem();
            _serializer = serializer ?? new NumpySerializer();
            _memoryTracker = new MemoryUsageTracker();
            
            // Start memory monitoring
            StartMemoryMonitoring();
        }
        
        /// <summary>
        /// Get a tensor dataset by name
        /// </summary>
        public NDArray Get(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            lock (_lock)
            {
                if (!_datasets.TryGetValue(name, out var dataset))
                {
                    throw new KeyNotFoundException($"Dataset '{name}' not found");
                }
                
                dataset.LastAccessTime = DateTime.UtcNow;
                dataset.AccessCount++;
                return dataset.Data;
            }
        }
        
        /// <summary>
        /// Get a slice of a tensor dataset
        /// </summary>
        public NDArray GetSlice(string name, params Slice[] slices)
        {
            var data = Get(name);
            return data[slices];
        }
        
        /// <summary>
        /// Get filtered data from a tensor dataset
        /// </summary>
        public NDArray GetWhere(string name, Func<NDArray, NDArray> condition)
        {
            var data = Get(name);
            return condition(data);
        }
        
        /// <summary>
        /// Set a tensor dataset
        /// </summary>
        public void Set(string name, NDArray data, Dictionary<string, object> properties = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
                
            lock (_lock)
            {
                var metadata = new TensorMetadata(name)
                {
                    Shape = data.shape,
                    DataType = data.dtype.Name,
                    Properties = properties ?? new Dictionary<string, object>()
                };
                
                var dataset = new TensorDataset
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
                    _objectPool.Return(oldDataset.Data);
                }
                
                _datasets[name] = dataset;
                _memoryTracker.AllocateMemory(metadata.GetEstimatedSizeInBytes());
                
                // Add to version history
                if (!_versionHistory.ContainsKey(name))
                {
                    _versionHistory[name] = new List<TensorVersion>();
                }
                
                var version = new TensorVersion
                {
                    VersionNumber = metadata.Version,
                    Description = "Dataset created/updated",
                    Checksum = ComputeChecksum(data)
                };
                
                _versionHistory[name].Add(version);
                
                CheckMemoryUsage();
            }
        }
        
        /// <summary>
        /// Batch get multiple datasets
        /// </summary>
        public Dictionary<string, NDArray> GetBatch(string[] names)
        {
            if (names == null)
                throw new ArgumentNullException(nameof(names));
                
            var result = new Dictionary<string, NDArray>();
            
            lock (_lock)
            {
                foreach (var name in names)
                {
                    if (_datasets.TryGetValue(name, out var dataset))
                    {
                        result[name] = dataset.Data;
                        dataset.LastAccessTime = DateTime.UtcNow;
                        dataset.AccessCount++;
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Batch set multiple datasets
        /// </summary>
        public void SetBatch(Dictionary<string, NDArray> datasets)
        {
            if (datasets == null)
                throw new ArgumentNullException(nameof(datasets));
                
            foreach (var kvp in datasets)
            {
                Set(kvp.Key, kvp.Value);
            }
        }
        
        /// <summary>
        /// Batch update multiple datasets
        /// </summary>
        public void UpdateBatch(Dictionary<string, NDArray> updates)
        {
            SetBatch(updates);
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
                    _objectPool.Return(dataset.Data);
                    _datasets.Remove(name);
                    return true;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Create a snapshot of a dataset
        /// </summary>
        public string CreateSnapshot(string name, string description = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            lock (_lock)
            {
                if (!_datasets.TryGetValue(name, out var dataset))
                {
                    throw new KeyNotFoundException($"Dataset '{name}' not found");
                }
                
                var snapshotName = $"{name}_snapshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                var snapshotData = dataset.Data.copy();
                
                Set(snapshotName, snapshotData, new Dictionary<string, object>
                {
                    ["original_dataset"] = name,
                    ["snapshot_time"] = DateTime.UtcNow,
                    ["description"] = description ?? "Auto-generated snapshot"
                });
                
                return snapshotName;
            }
        }
        
        /// <summary>
        /// Rollback to a previous version
        /// </summary>
        public bool Rollback(string name, int version)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            lock (_lock)
            {
                if (!_versionHistory.ContainsKey(name))
                    return false;
                    
                var versions = _versionHistory[name];
                var targetVersion = versions.FirstOrDefault(v => v.VersionNumber == version);
                
                if (targetVersion == null)
                    return false;
                    
                // This is a simplified rollback - in a real implementation,
                // you would store the actual data for each version
                return true;
            }
        }
        
        /// <summary>
        /// Get version history for a dataset
        /// </summary>
        public IReadOnlyList<TensorVersion> GetVersionHistory(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            lock (_lock)
            {
                if (_versionHistory.TryGetValue(name, out var versions))
                {
                    return versions.AsReadOnly();
                }
                
                return new List<TensorVersion>().AsReadOnly();
            }
        }
        
        /// <summary>
        /// Load a dataset from file asynchronously
        /// </summary>
        public async Task<NDArray> LoadAsync(string name, string filePath = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            filePath ??= $"{name}.npy";
            
            try
            {
                var data = await _serializer.DeserializeAsync(filePath, cancellationToken);
                
                Set(name, data, new Dictionary<string, object>
                {
                    ["file_path"] = filePath,
                    ["loaded_time"] = DateTime.UtcNow
                });
                
                OnDatasetLoaded?.Invoke(name, _datasets[name].Metadata);
                
                return data;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load dataset '{name}' from '{filePath}'", ex);
            }
        }
        
        /// <summary>
        /// Save a dataset to file asynchronously
        /// </summary>
        public async Task SaveAsync(string name, string filePath = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            NDArray data;
            lock (_lock)
            {
                if (!_datasets.TryGetValue(name, out var dataset))
                {
                    throw new KeyNotFoundException($"Dataset '{name}' not found");
                }
                data = dataset.Data;
            }
            
            filePath ??= $"{name}.npy";
            
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
                throw new InvalidOperationException($"Failed to save dataset '{name}' to '{filePath}'", ex);
            }
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
            
            var tasks = datasetNames.Select(name => SaveAsync(name, cancellationToken: cancellationToken));
            await Task.WhenAll(tasks);
        }
        
        /// <summary>
        /// Get dataset metadata
        /// </summary>
        public TensorMetadata GetMetadata(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            lock (_lock)
            {
                if (_datasets.TryGetValue(name, out var dataset))
                {
                    return dataset.Metadata;
                }
                
                throw new KeyNotFoundException($"Dataset '{name}' not found");
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
                    _objectPool.Return(dataset.Data);
                }
                
                _datasets.Clear();
                _versionHistory.Clear();
            }
        }
        
        /// <summary>
        /// Preallocate tensor objects in the pool
        /// </summary>
        public void PreallocatePool(int[][] shapes, Type[] dtypes, int count = 10)
        {
            _objectPool.Preallocate(shapes, dtypes, count);
        }
        
        /// <summary>
        /// Get memory usage report
        /// </summary>
        public DataCore.Monitoring.MemoryUsageReport GetMemoryReport()
        {
            return new DataCore.Monitoring.MemoryUsageReport(
                new Dictionary<string, long>
                {
                    { "Tensor", _memoryTracker.TotalMemoryUsage }
                });
        }
        
        private void CheckMemoryUsage()
        {
            if (CurrentMemoryUsage > MemoryWarningThreshold)
            {
                OnMemoryWarning?.Invoke(CurrentMemoryUsage);
            }
        }
        
        private void StartMemoryMonitoring()
        {
            // In a real implementation, you might want to start a background task
            // to monitor memory usage periodically
        }
        
        private string ComputeChecksum(NDArray data)
        {
            // Simple checksum implementation - in production, use a proper hash
            var bytes = data.ToByteArray();
            var hash = 0;
            foreach (var b in bytes)
            {
                hash = ((hash << 5) + hash) ^ b;
            }
            return hash.ToString("X8");
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                Clear();
                _objectPool.Clear();
                _disposed = true;
            }
        }
        
        private class TensorDataset
        {
            public NDArray Data { get; set; }
            public TensorMetadata Metadata { get; set; }
            public DateTime LastAccessTime { get; set; }
            public int AccessCount { get; set; }
        }
    }
    
    /// <summary>
    /// Memory usage tracking for tensor datasets
    /// </summary>
    public class MemoryUsageTracker
    {
        private long _totalMemoryUsage;
        private readonly object _lock = new object();
        
        public long TotalMemoryUsage
        {
            get
            {
                lock (_lock)
                {
                    return _totalMemoryUsage;
                }
            }
        }
        
        public void AllocateMemory(long bytes)
        {
            lock (_lock)
            {
                _totalMemoryUsage += bytes;
            }
        }
        
        public void ReleaseMemory(long bytes)
        {
            lock (_lock)
            {
                _totalMemoryUsage = Math.Max(0, _totalMemoryUsage - bytes);
            }
        }
        
        public TensorMemoryUsageReport GetReport()
        {
            lock (_lock)
            {
                return new TensorMemoryUsageReport
                {
                    TotalMemoryUsage = _totalMemoryUsage,
                    AllocatedObjects = 0, // Would track object count in real implementation
                    PeakMemoryUsage = _totalMemoryUsage // Would track peak in real implementation
                };
            }
        }
    }
    
    /// <summary>
    /// Tensor memory usage report
    /// </summary>
    public struct TensorMemoryUsageReport
    {
        public long TotalMemoryUsage { get; set; }
        public long PeakMemoryUsage { get; set; }
        public int AllocatedObjects { get; set; }
        
        public double GetUsageInMB() => TotalMemoryUsage / (1024.0 * 1024.0);
        public double GetPeakUsageInMB() => PeakMemoryUsage / (1024.0 * 1024.0);
    }
}