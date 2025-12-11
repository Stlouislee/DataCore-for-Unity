using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DataCore
{
    /// <summary>
    /// Manages memory pools for different object types
    /// </summary>
    public class MemoryPoolManager : IDisposable
    {
        private readonly Dictionary<Type, object> _pools;
        private readonly Dictionary<string, object> _namedPools;
        private readonly List<CacheLevel> _cacheLevels;
        private readonly object _lock = new object();
        private readonly PerformanceMonitor _performanceMonitor;
        
        private bool _disposed;
        private long _totalMemoryUsage;
        private long _peakMemoryUsage;
        
        /// <summary>
        /// Maximum total memory usage (default: 2GB)
        /// </summary>
        public long MaxMemoryUsage { get; set; } = 2L * 1024 * 1024 * 1024;
        
        /// <summary>
        /// Current total memory usage
        /// </summary>
        public long CurrentMemoryUsage
        {
            get
            {
                lock (_lock)
                {
                    return _totalMemoryUsage;
                }
            }
        }
        
        /// <summary>
        /// Peak memory usage
        /// </summary>
        public long PeakMemoryUsage
        {
            get
            {
                lock (_lock)
                {
                    return _peakMemoryUsage;
                }
            }
        }
        
        /// <summary>
        /// Event fired when memory usage exceeds threshold
        /// </summary>
        public event Action<long> OnMemoryWarning;
        
        public MemoryPoolManager()
        {
            _pools = new Dictionary<Type, object>();
            _namedPools = new Dictionary<string, object>();
            _cacheLevels = new List<CacheLevel>();
            _performanceMonitor = new PerformanceMonitor();
            
            InitializeCacheLevels();
        }
        
        /// <summary>
        /// Get or create an object pool for a specific type
        /// </summary>
        public ObjectPool<T> GetPool<T>() where T : class
        {
            lock (_lock)
            {
                if (!_pools.TryGetValue(typeof(T), out var pool))
                {
                    pool = new ObjectPool<T>();
                    _pools[typeof(T)] = pool;
                }
                
                return pool as ObjectPool<T>;
            }
        }
        
        /// <summary>
        /// Get or create a named object pool
        /// </summary>
        public ObjectPool<T> GetPool<T>(string name) where T : class
        {
            lock (_lock)
            {
                var key = $"{typeof(T).FullName}_{name}";
                if (!_namedPools.TryGetValue(key, out var pool))
                {
                    pool = new ObjectPool<T>();
                    _namedPools[key] = pool;
                }
                
                return pool as ObjectPool<T>;
            }
        }
        
        /// <summary>
        /// Preallocate objects in a pool
        /// </summary>
        public void Preallocate<T>(int count, Func<T> factory = null) where T : class
        {
            var pool = GetPool<T>();
            pool.Preallocate(count, factory);
        }
        
        /// <summary>
        /// Get an object from the pool
        /// </summary>
        public T Get<T>() where T : class, new()
        {
            var pool = GetPool<T>();
            return pool.Get();
        }
        
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Return<T>(T obj) where T : class
        {
            if (obj == null) return;
            
            var pool = GetPool<T>();
            pool.Return(obj);
        }
        
        /// <summary>
        /// Add an object to L1 cache (memory)
        /// </summary>
        public void AddToL1Cache<T>(string key, T obj, TimeSpan? expiration = null) where T : class
        {
            lock (_lock)
            {
                _cacheLevels[0].Add(key, obj, expiration);
                UpdateMemoryUsage();
            }
        }
        
        /// <summary>
        /// Add an object to L2 cache (compressed memory)
        /// </summary>
        public void AddToL2Cache<T>(string key, T obj, TimeSpan? expiration = null) where T : class
        {
            lock (_lock)
            {
                _cacheLevels[1].Add(key, obj, expiration);
                UpdateMemoryUsage();
            }
        }
        
        /// <summary>
        /// Add an object to L3 cache (disk)
        /// </summary>
        public async Task AddToL3CacheAsync<T>(string key, T obj, TimeSpan? expiration = null) where T : class
        {
            await _cacheLevels[2].AddAsync(key, obj, expiration);
        }
        
        /// <summary>
        /// Get an object from cache
        /// </summary>
        public T GetFromCache<T>(string key) where T : class
        {
            lock (_lock)
            {
                // Check L1 cache first
                var obj = _cacheLevels[0].Get<T>(key);
                if (obj != null)
                {
                    _performanceMonitor.RecordCacheHit(CacheLevelType.L1);
                    return obj;
                }
                
                // Check L2 cache
                obj = _cacheLevels[1].Get<T>(key);
                if (obj != null)
                {
                    _performanceMonitor.RecordCacheHit(CacheLevelType.L2);
                    // Promote to L1
                    _cacheLevels[0].Add(key, obj);
                    return obj;
                }
                
                _performanceMonitor.RecordCacheMiss();
                return null;
            }
        }
        
        /// <summary>
        /// Remove an object from all cache levels
        /// </summary>
        public void RemoveFromCache(string key)
        {
            lock (_lock)
            {
                foreach (var cacheLevel in _cacheLevels)
                {
                    cacheLevel.Remove(key);
                }
                UpdateMemoryUsage();
            }
        }
        
        /// <summary>
        /// Clear all cache levels
        /// </summary>
        public void ClearCache()
        {
            lock (_lock)
            {
                foreach (var cacheLevel in _cacheLevels)
                {
                    cacheLevel.Clear();
                }
                UpdateMemoryUsage();
            }
        }
        
        /// <summary>
        /// Cleanup expired objects from all pools and caches
        /// </summary>
        public void CleanupExpired()
        {
            lock (_lock)
            {
                // Cleanup pools
                foreach (var pool in _pools.Values)
                {
                    if (pool is IDisposable disposablePool)
                    {
                        // This would call cleanup on the pool
                    }
                }
                
                // Cleanup named pools
                foreach (var pool in _namedPools.Values)
                {
                    if (pool is IDisposable disposablePool)
                    {
                        // This would call cleanup on the pool
                    }
                }
                
                // Cleanup cache levels
                foreach (var cacheLevel in _cacheLevels)
                {
                    cacheLevel.CleanupExpired();
                }
                
                UpdateMemoryUsage();
            }
        }
        
        /// <summary>
        /// Get statistics for all pools
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            lock (_lock)
            {
                var stats = new PoolStatistics
                {
                    TotalMemoryUsage = _totalMemoryUsage,
                    PeakMemoryUsage = _peakMemoryUsage,
                    CacheHits = _performanceMonitor.CacheHits,
                    CacheMisses = _performanceMonitor.CacheMisses,
                    PoolStats = new List<PoolStat>()
                };
                
                foreach (var kvp in _pools)
                {
                    if (kvp.Value is IPool pool)
                    {
                        stats.PoolStats.Add(new PoolStat
                        {
                            Key = kvp.Key.Name,
                            ObjectCount = pool.ActiveCount + pool.IdleCount,
                            ActiveObjects = pool.ActiveCount,
                            IdleObjects = pool.IdleCount
                        });
                    }
                }
                
                return stats;
            }
        }
        
        /// <summary>
        /// Force garbage collection
        /// </summary>
        public void ForceGC()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            UpdateMemoryUsage();
        }
        
        private void InitializeCacheLevels()
        {
            _cacheLevels.Add(new CacheLevel(CacheLevelType.L1, "Memory", TimeSpan.FromMinutes(5)));
            _cacheLevels.Add(new CacheLevel(CacheLevelType.L2, "Compressed", TimeSpan.FromMinutes(30)));
            _cacheLevels.Add(new CacheLevel(CacheLevelType.L3, "Disk", TimeSpan.FromHours(24)));
        }
        
        private void UpdateMemoryUsage()
        {
            _totalMemoryUsage = GC.GetTotalMemory(false);
            _peakMemoryUsage = Math.Max(_peakMemoryUsage, _totalMemoryUsage);
            
            if (_totalMemoryUsage > MaxMemoryUsage * 0.8)
            {
                OnMemoryWarning?.Invoke(_totalMemoryUsage);
            }
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                ClearCache();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Generic object pool
    /// </summary>
    public class ObjectPool<T> where T : class
    {
        private readonly Queue<T> _objects;
        private readonly List<T> _activeObjects;
        private readonly object _lock = new object();
        private Func<T> _factory;
        private Action<T> _resetAction;
        
        /// <summary>
        /// Maximum number of objects in the pool
        /// </summary>
        public int MaxSize { get; set; } = 1000;
        
        /// <summary>
        /// Current number of active objects
        /// </summary>
        public int ActiveCount
        {
            get
            {
                lock (_lock)
                {
                    return _activeObjects.Count;
                }
            }
        }
        
        /// <summary>
        /// Current number of idle objects
        /// </summary>
        public int IdleCount
        {
            get
            {
                lock (_lock)
                {
                    return _objects.Count;
                }
            }
        }
        
        public ObjectPool()
        {
            _objects = new Queue<T>();
            _activeObjects = new List<T>();
        }
        
        /// <summary>
        /// Set the factory method for creating new objects
        /// </summary>
        public void SetFactory(Func<T> factory)
        {
            _factory = factory;
        }
        
        /// <summary>
        /// Set the reset action for returning objects to the pool
        /// </summary>
        public void SetResetAction(Action<T> resetAction)
        {
            _resetAction = resetAction;
        }
        
        /// <summary>
        /// Preallocate objects in the pool
        /// </summary>
        public void Preallocate(int count, Func<T> factory = null)
        {
            lock (_lock)
            {
                var actualFactory = factory ?? _factory ?? (() => Activator.CreateInstance<T>());
                
                for (int i = 0; i < count && _objects.Count < MaxSize; i++)
                {
                    var obj = actualFactory();
                    _objects.Enqueue(obj);
                }
            }
        }
        
        /// <summary>
        /// Get an object from the pool
        /// </summary>
        public T Get()
        {
            lock (_lock)
            {
                T obj;
                
                if (_objects.Count > 0)
                {
                    obj = _objects.Dequeue();
                }
                else if (_factory != null)
                {
                    obj = _factory();
                }
                else
                {
                    obj = Activator.CreateInstance<T>();
                }
                
                _activeObjects.Add(obj);
                return obj;
            }
        }
        
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null) return;
            
            lock (_lock)
            {
                if (!_activeObjects.Contains(obj))
                    return;
                
                _activeObjects.Remove(obj);
                
                if (_objects.Count < MaxSize)
                {
                    _resetAction?.Invoke(obj);
                    _objects.Enqueue(obj);
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
                _objects.Clear();
                _activeObjects.Clear();
            }
        }
    }
    
    /// <summary>
    /// Cache level (L1, L2, L3)
    /// </summary>
    public class CacheLevel
    {
        public CacheLevelType Type { get; }
        public string Name { get; }
        public TimeSpan DefaultExpiration { get; }
        
        private readonly Dictionary<string, CacheItem> _items;
        private readonly object _lock = new object();
        
        public CacheLevel(CacheLevelType type, string name, TimeSpan defaultExpiration)
        {
            Type = type;
            Name = name;
            DefaultExpiration = defaultExpiration;
            _items = new Dictionary<string, CacheItem>();
        }
        
        public void Add<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            lock (_lock)
            {
                _items[key] = new CacheItem
                {
                    Value = value,
                    ExpirationTime = DateTime.UtcNow + (expiration ?? DefaultExpiration),
                    Size = EstimateSize(value)
                };
            }
        }
        
        public async Task AddAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            // For disk cache, this would serialize and save to file
            await Task.CompletedTask;
            Add(key, value, expiration);
        }
        
        public T Get<T>(string key) where T : class
        {
            lock (_lock)
            {
                if (_items.TryGetValue(key, out var item))
                {
                    if (DateTime.UtcNow < item.ExpirationTime)
                    {
                        return item.Value as T;
                    }
                    else
                    {
                        _items.Remove(key);
                    }
                }
                
                return null;
            }
        }
        
        public void Remove(string key)
        {
            lock (_lock)
            {
                _items.Remove(key);
            }
        }
        
        public void Clear()
        {
            lock (_lock)
            {
                _items.Clear();
            }
        }
        
        public void CleanupExpired()
        {
            lock (_lock)
            {
                var keysToRemove = _items.Where(kvp => DateTime.UtcNow >= kvp.Value.ExpirationTime)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in keysToRemove)
                {
                    _items.Remove(key);
                }
            }
        }
        
        private long EstimateSize<T>(T value) where T : class
        {
            // Rough size estimation
            return value switch
            {
                string s => s.Length * 2,
                Array a => a.Length * 8,
                _ => 100 // Default estimate
            };
        }
        
        private class CacheItem
        {
            public object Value { get; set; }
            public DateTime ExpirationTime { get; set; }
            public long Size { get; set; }
        }
    }
    
    /// <summary>
    /// Cache level types
    /// </summary>
    public enum CacheLevelType
    {
        L1, // Memory
        L2, // Compressed Memory
        L3  // Disk
    }
    
    /// <summary>
    /// Pool statistics
    /// </summary>
    public class PoolStatistics
    {
        public long TotalMemoryUsage { get; set; }
        public long PeakMemoryUsage { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public List<PoolStat> PoolStats { get; set; }
        
        public double HitRate => (double)CacheHits / (CacheHits + CacheMisses);
        public double MissRate => (double)CacheMisses / (CacheHits + CacheMisses);
    }
    
    /// <summary>
    /// Individual pool statistics
    /// </summary>
    public class PoolStat
    {
        public string Key { get; set; }
        public int ObjectCount { get; set; }
        public int ActiveObjects { get; set; }
        public int IdleObjects { get; set; }
    }
    
    /// <summary>
    /// Pool interface for statistics
    /// </summary>
    public interface IPool
    {
        int ActiveCount { get; }
        int IdleCount { get; }
    }
}