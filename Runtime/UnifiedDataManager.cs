using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataCore.DataFrame;
using DataCore.Graph;
using DataCore.Monitoring;
using DataCore.Platform;
using DataCore.Serialization;
using DataCore.Tensor;
using NumSharp;
using Microsoft.Data.Analysis;

namespace DataCore
{
    /// <summary>
    /// Unified data manager for all data types (Tensor, DataFrame, Graph)
    /// </summary>
    public class UnifiedDataManager : IDisposable
    {
        private static UnifiedDataManager _instance;
        private static readonly object _instanceLock = new object();
        
        private readonly TensorDataManager _tensorManager;
        private readonly DataFrameManager _dataFrameManager;
        private readonly GraphManager _graphManager;
        private readonly DataPipeline _pipeline;
        private readonly MemoryPoolManager _memoryPool;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly IFileSystem _fileSystem;
        
        private bool _disposed;
        
        /// <summary>
        /// Tensor data manager
        /// </summary>
        public TensorDataManager Tensors => _tensorManager;
        
        /// <summary>
        /// DataFrame manager
        /// </summary>
        public DataFrameManager DataFrames => _dataFrameManager;
        
        /// <summary>
        /// Graph manager
        /// </summary>
        public GraphManager Graphs => _graphManager;
        
        /// <summary>
        /// Data transformation pipeline
        /// </summary>
        public DataPipeline Pipeline => _pipeline;
        
        /// <summary>
        /// Memory pool manager
        /// </summary>
        public MemoryPoolManager MemoryPool => _memoryPool;
        
        /// <summary>
        /// Performance monitor
        /// </summary>
        public PerformanceMonitor Performance => _performanceMonitor;
        
        /// <summary>
        /// Get the singleton instance
        /// </summary>
        public static UnifiedDataManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new UnifiedDataManager();
                        }
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Set a custom instance (for testing)
        /// </summary>
        public static void SetInstance(UnifiedDataManager instance)
        {
            lock (_instanceLock)
            {
                _instance = instance;
            }
        }
        
        public UnifiedDataManager(IFileSystem fileSystem = null)
        {
            _fileSystem = fileSystem ?? FileSystemFactory.GetFileSystem();
            _tensorManager = new TensorDataManager(_fileSystem);
            _dataFrameManager = new DataFrameManager(_fileSystem);
            _graphManager = new GraphManager(_fileSystem);
            _pipeline = new DataPipeline();
            _memoryPool = new MemoryPoolManager();
            _performanceMonitor = new PerformanceMonitor();
            
            // Wire up events
            _tensorManager.OnMemoryWarning += OnMemoryWarning;
            _dataFrameManager.OnMemoryWarning += OnMemoryWarning;
            _graphManager.OnMemoryWarning += OnMemoryWarning;
        }
        
        /// <summary>
        /// Get data of any type
        /// </summary>
        public T GetData<T>(string key) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            
            // Try to get as tensor
            if (typeof(T) == typeof(NDArray) || typeof(T).IsAssignableFrom(typeof(NDArray)))
            {
                if (_tensorManager.Contains(key))
                {
                    return _tensorManager.Get(key) as T;
                }
            }
            
            // Try to get as DataFrame
            if (typeof(T) == typeof(DataFrame) || typeof(T).IsAssignableFrom(typeof(DataFrame)))
            {
                if (_dataFrameManager.Contains(key))
                {
                    return _dataFrameManager.Get(key) as T;
                }
            }
            
            // Try to get as Graph
            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Graph<,>))
            {
                // This would require more complex reflection to handle properly
                // For now, throw not supported
                throw new NotSupportedException("Generic graph types require specific GetGraph method");
            }
            
            throw new KeyNotFoundException($"Data with key '{key}' not found or type mismatch");
        }
        
        /// <summary>
        /// Set data of any type
        /// </summary>
        public void SetData<T>(string key, T data) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            
            // Set as tensor
            if (data is NDArray ndarray)
            {
                _tensorManager.Set(key, ndarray);
                return;
            }
            
            // Set as DataFrame
            if (data is DataFrame dataframe)
            {
                _dataFrameManager.Set(key, dataframe);
                return;
            }
            
            throw new NotSupportedException($"Type {typeof(T).Name} is not supported. Use specific manager for custom types.");
        }
        
        /// <summary>
        /// Check if data exists
        /// </summary>
        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            
            return _tensorManager.Contains(key) || _dataFrameManager.Contains(key) || _graphManager.Contains(key);
        }
        
        /// <summary>
        /// Remove data
        /// </summary>
        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            
            return _tensorManager.Remove(key) || _dataFrameManager.Remove(key) || _graphManager.Remove(key);
        }
        
        /// <summary>
        /// Get all dataset names
        /// </summary>
        public string[] GetAllDatasetNames()
        {
            var names = new HashSet<string>();
            
            names.UnionWith(_tensorManager.GetDatasetNames());
            names.UnionWith(_dataFrameManager.GetDatasetNames());
            names.UnionWith(_graphManager.GetDatasetNames());
            
            return names.ToArray();
        }
        
        /// <summary>
        /// Get dataset metadata
        /// </summary>
        public DatasetMetadata GetMetadata(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            
            if (_tensorManager.Contains(key))
            {
                var metadata = _tensorManager.GetMetadata(key);
                return new DatasetMetadata
                {
                    Name = metadata.Name,
                    Type = "Tensor",
                    CreatedTime = metadata.CreatedTime,
                    ModifiedTime = metadata.ModifiedTime,
                    SizeInBytes = metadata.GetEstimatedSizeInBytes(),
                    Properties = metadata.Properties
                };
            }
            
            if (_dataFrameManager.Contains(key))
            {
                var metadata = _dataFrameManager.GetMetadata(key);
                return new DatasetMetadata
                {
                    Name = metadata.Name,
                    Type = "DataFrame",
                    CreatedTime = metadata.CreatedTime,
                    ModifiedTime = metadata.ModifiedTime,
                    SizeInBytes = metadata.GetEstimatedSizeInBytes(),
                    Properties = metadata.Properties
                };
            }
            
            if (_graphManager.Contains(key))
            {
                var metadata = _graphManager.GetMetadata(key);
                return new DatasetMetadata
                {
                    Name = metadata.Name,
                    Type = "Graph",
                    CreatedTime = metadata.CreatedTime,
                    ModifiedTime = metadata.ModifiedTime,
                    SizeInBytes = metadata.GetEstimatedSizeInBytes(),
                    Properties = metadata.Properties
                };
            }
            
            throw new KeyNotFoundException($"Dataset '{key}' not found");
        }
        
        /// <summary>
        /// Save all datasets
        /// </summary>
        public async Task SaveAllAsync(CancellationToken cancellationToken = default)
        {
            var tensorTask = _tensorManager.SaveAllAsync(cancellationToken);
            var dataFrameTask = _dataFrameManager.SaveAllAsync(cancellationToken);
            var graphTask = _graphManager.SaveAllAsync(cancellationToken);
            
            await Task.WhenAll(tensorTask, dataFrameTask, graphTask);
        }
        
        /// <summary>
        /// Clear all datasets
        /// </summary>
        public void Clear()
        {
            _tensorManager.Clear();
            _dataFrameManager.Clear();
            _graphManager.Clear();
        }
        
        /// <summary>
        /// Get total memory usage across all managers
        /// </summary>
        public long GetTotalMemoryUsage()
        {
            return _tensorManager.CurrentMemoryUsage + _dataFrameManager.CurrentMemoryUsage + _graphManager.CurrentMemoryUsage;
        }
        
        /// <summary>
        /// Get memory usage report
        /// </summary>
        public UnifiedMemoryReport GetMemoryReport()
        {
            return new UnifiedMemoryReport
            {
                TensorMemory = _tensorManager.GetMemoryReport(),
                DataFrameMemory = _dataFrameManager.GetMemoryReport(),
                GraphMemory = _graphManager.GetMemoryReport(),
                PoolMemory = _memoryPool.GetStatistics(),
                TotalMemory = GetTotalMemoryUsage()
            };
        }
        
        /// <summary>
        /// Prefetch datasets into memory
        /// </summary>
        public void Prefetch(string[] datasetNames)
        {
            if (datasetNames == null || datasetNames.Length == 0)
                return;
            
            foreach (var name in datasetNames)
            {
                // Access each dataset to load it into memory
                if (_tensorManager.Contains(name))
                {
                    _tensorManager.Get(name);
                }
                else if (_dataFrameManager.Contains(name))
                {
                    _dataFrameManager.Get(name);
                }
                else if (_graphManager.Contains(name))
                {
                    // Would need to handle generic graph types
                    Debug.LogWarning($"Prefetching graph '{name}' requires specific type parameters");
                }
            }
        }
        
        private void OnMemoryWarning(long memoryUsage)
        {
            Debug.LogWarning($"Memory usage warning: {memoryUsage / (1024.0 * 1024.0):F2} MB");
            
            // Trigger garbage collection
            _memoryPool.CleanupExpired();
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _tensorManager?.Dispose();
                _dataFrameManager?.Dispose();
                _graphManager?.Dispose();
                _memoryPool?.Clear();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Dataset metadata for unified interface
    /// </summary>
    public class DatasetMetadata
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime ModifiedTime { get; set; }
        public long SizeInBytes { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }
    
    /// <summary>
    /// Unified memory report
    /// </summary>
    public class UnifiedMemoryReport
    {
        public DataCore.Monitoring.MemoryUsageReport TensorMemory { get; set; }
        public DataCore.Monitoring.MemoryUsageReport DataFrameMemory { get; set; }
        public DataCore.Monitoring.MemoryUsageReport GraphMemory { get; set; }
        public PoolStatistics PoolMemory { get; set; }
        public long TotalMemory { get; set; }
        
        public double GetTotalMemoryInMB() => TotalMemory / (1024.0 * 1024.0);
    }
}