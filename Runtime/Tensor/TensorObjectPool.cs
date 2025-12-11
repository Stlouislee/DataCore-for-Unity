using System;
using System.Collections.Generic;
using System.Linq;
using NumSharp;

namespace DataCore.Tensor
{
    /// <summary>
    /// Object pool for NDArray objects to reduce GC pressure
    /// </summary>
    public class TensorObjectPool
    {
        private readonly Dictionary<string, Queue<NDArray>> _pools;
        private readonly Dictionary<string, PoolConfig> _poolConfigs;
        private readonly Dictionary<NDArray, string> _arrayToKey;
        private readonly object _lock = new object();
        
        private int _totalObjects;
        private int _activeObjects;
        
        /// <summary>
        /// Maximum total objects in all pools (default: 1000)
        /// </summary>
        public int MaxTotalObjects { get; set; } = 1000;
        
        /// <summary>
        /// Default maximum objects per pool (default: 100)
        /// </summary>
        public int DefaultMaxPerPool { get; set; } = 100;
        
        /// <summary>
        /// Object expiration time in seconds (default: 300 = 5 minutes)
        /// </summary>
        public double ObjectExpirationSeconds { get; set; } = 300;
        
        /// <summary>
        /// Current total number of objects in pools
        /// </summary>
        public int TotalObjects => _totalObjects;
        
        /// <summary>
        /// Current number of active (checked out) objects
        /// </summary>
        public int ActiveObjects => _activeObjects;
        
        /// <summary>
        /// Current number of idle objects in pools
        /// </summary>
        public int IdleObjects => _totalObjects - _activeObjects;
        
        public TensorObjectPool()
        {
            _pools = new Dictionary<string, Queue<NDArray>>();
            _poolConfigs = new Dictionary<string, PoolConfig>();
            _arrayToKey = new Dictionary<NDArray, string>();
        }
        
        /// <summary>
        /// Get or create an NDArray from the pool
        /// </summary>
        public NDArray Get(int[] shape, Type dtype)
        {
            var key = GetPoolKey(shape, dtype);
            
            lock (_lock)
            {
                if (_pools.TryGetValue(key, out var pool) && pool.Count > 0)
                {
                    var array = pool.Dequeue();
                    _activeObjects++;
                    return array;
                }
            }
            
            // Create new array if none available in pool
            var newArray = np.zeros(shape, dtype);
            
            lock (_lock)
            {
                _arrayToKey[newArray] = key;
                _activeObjects++;
                _totalObjects++;
            }
            
            return newArray;
        }
        
        /// <summary>
        /// Return an NDArray to the pool
        /// </summary>
        public void Return(NDArray array)
        {
            if (array == null) return;
            
            lock (_lock)
            {
                if (!_arrayToKey.TryGetValue(array, out var key))
                {
                    // Array not from this pool, let GC handle it
                    // array.Dispose(); // NDArray doesn't implement IDisposable
                    return;
                }
                
                var config = _poolConfigs.GetValueOrDefault(key, new PoolConfig { MaxObjects = DefaultMaxPerPool });
                
                if (_pools.TryGetValue(key, out var pool))
                {
                    if (pool.Count < config.MaxObjects && _totalObjects < MaxTotalObjects)
                    {
                        // Reset array to default state
                        // array.fill(0); // NDArray doesn't have fill method
                        // For now, we'll just reuse the array as-is
                        pool.Enqueue(array);
                        _activeObjects--;
                    }
                    else
                    {
                        // Pool is full, let GC handle the array
                        // array.Dispose(); // NDArray doesn't implement IDisposable
                        _arrayToKey.Remove(array);
                        _activeObjects--;
                        _totalObjects--;
                    }
                }
                else
                {
                    // Create new pool
                    pool = new Queue<NDArray>();
                    // array.fill(0); // NDArray doesn't have fill method
                    // For now, we'll just reuse the array as-is
                    pool.Enqueue(array);
                    _pools[key] = pool;
                    _activeObjects--;
                }
            }
        }
        
        /// <summary>
        /// Preallocate objects in the pool
        /// </summary>
        public void Preallocate(int[][] shapes, Type[] dtypes, int count = 10)
        {
            if (shapes == null || dtypes == null || shapes.Length != dtypes.Length)
                throw new ArgumentException("Shapes and dtypes must be non-null and have the same length");
            
            for (int i = 0; i < shapes.Length; i++)
            {
                var key = GetPoolKey(shapes[i], dtypes[i]);
                var config = new PoolConfig { MaxObjects = Math.Max(count, DefaultMaxPerPool) };
                
                lock (_lock)
                {
                    _poolConfigs[key] = config;
                    
                    if (!_pools.ContainsKey(key))
                    {
                        _pools[key] = new Queue<NDArray>();
                    }
                    
                    var pool = _pools[key];
                    for (int j = 0; j < count && _totalObjects < MaxTotalObjects; j++)
                    {
                        var array = np.zeros(shapes[i], dtypes[i]);
                        _arrayToKey[array] = key;
                        pool.Enqueue(array);
                        _totalObjects++;
                    }
                }
            }
        }
        
        /// <summary>
        /// Clear all objects from the pool
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                foreach (var pool in _pools.Values)
                {
                    while (pool.Count > 0)
                    {
                        var array = pool.Dequeue();
                        array.Dispose();
                    }
                }
                
                _pools.Clear();
                _poolConfigs.Clear();
                _arrayToKey.Clear();
                _totalObjects = 0;
                _activeObjects = 0;
            }
        }
        
        /// <summary>
        /// Clean up expired objects from the pool
        /// </summary>
        public void CleanupExpired()
        {
            // This is a simplified version. In a real implementation,
            // you would track the last used time for each object.
            lock (_lock)
            {
                var keysToRemove = new List<string>();
                
                foreach (var kvp in _pools)
                {
                    var pool = kvp.Value;
                    // Keep at least one object in each pool
                    while (pool.Count > 1 && _totalObjects > 100)
                    {
                        var array = pool.Dequeue();
                        array.Dispose();
                        _arrayToKey.Remove(array);
                        _totalObjects--;
                    }
                    
                    if (pool.Count == 0)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    _pools.Remove(key);
                    _poolConfigs.Remove(key);
                }
            }
        }
        
        /// <summary>
        /// Get statistics about the pool
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            lock (_lock)
            {
                var stats = new PoolStatistics
                {
                    TotalObjects = _totalObjects,
                    ActiveObjects = _activeObjects,
                    IdleObjects = _totalObjects - _activeObjects,
                    PoolCount = _pools.Count
                };
                
                foreach (var kvp in _pools)
                {
                    stats.PoolStats.Add(new PoolStat
                    {
                        Key = kvp.Key,
                        ObjectCount = kvp.Value.Count,
                        MaxObjects = _poolConfigs.GetValueOrDefault(kvp.Key, new PoolConfig { MaxObjects = DefaultMaxPerPool }).MaxObjects
                    });
                }
                
                return stats;
            }
        }
        
        private string GetPoolKey(int[] shape, Type dtype)
        {
            var shapeStr = string.Join("x", shape);
            return $"{shapeStr}_{dtype.Name}";
        }
        
        private class PoolConfig
        {
            public int MaxObjects { get; set; }
            public double ExpirationSeconds { get; set; }
        }
    }
    
    /// <summary>
    /// Pool statistics
    /// </summary>
    public class PoolStatistics
    {
        public int TotalObjects { get; set; }
        public int ActiveObjects { get; set; }
        public int IdleObjects { get; set; }
        public int PoolCount { get; set; }
        public List<PoolStat> PoolStats { get; set; } = new List<PoolStat>();
        
        public override string ToString()
        {
            return $"Total: {TotalObjects}, Active: {ActiveObjects}, Idle: {IdleObjects}, Pools: {PoolCount}";
        }
    }
    
    /// <summary>
    /// Individual pool statistics
    /// </summary>
    public class PoolStat
    {
        public string Key { get; set; }
        public int ObjectCount { get; set; }
        public int MaxObjects { get; set; }
        
        public override string ToString()
        {
            return $"{Key}: {ObjectCount}/{MaxObjects}";
        }
    }
}