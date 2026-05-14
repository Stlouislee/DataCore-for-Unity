using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB;

#if UNITY_2019_1_OR_NEWER
using UnityEngine;
#endif

namespace AroAro.DataCore.LiteDb
{
    /// <summary>
    /// LiteDB 数据存储实现
    /// </summary>
    public sealed class LiteDbDataStore : IDataStore
    {
        private readonly LiteDatabase _database;
        private readonly DataStoreOptions _options;
        private readonly Dictionary<string, LiteDbTabularDataset> _tabularCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LiteDbGraphDataset> _graphCache = new(StringComparer.Ordinal);
        private readonly object _lock = new object();
        private bool _disposed;

#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
        /// <summary>
        /// Concurrency guard for mobile platforms using Direct mode.
        /// Direct mode has no file lock protection, so we serialize all
        /// LiteDB operations through this semaphore to prevent data corruption
        /// from concurrent access (coroutines, Task.Run, UniTask, etc.).
        /// </summary>
        private static readonly SemaphoreSlim _dbSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Execute an operation while holding the mobile concurrency lock.
        /// On non-mobile platforms this is a no-op (the caller uses lock(_lock) instead).
        /// </summary>
        private T ExecuteWithMobileLock<T>(Func<T> operation)
        {
            _dbSemaphore.Wait();
            try
            {
                return operation();
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        private void ExecuteWithMobileLock(Action operation)
        {
            _dbSemaphore.Wait();
            try
            {
                operation();
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }
#endif

        public string DatabasePath { get; }

        public LiteDbDataStore(string dbPath, DataStoreOptions options = null)
        {
            _options = options ?? new DataStoreOptions();
            DatabasePath = ResolvePath(dbPath ?? "DataCore/datacore.db");
            EnsureDirectory(DatabasePath);

            // Handle 0-byte files which can crash LiteDB
            if (File.Exists(DatabasePath))
            {
                try
                {
                    var info = new FileInfo(DatabasePath);
                    if (info.Length == 0)
                    {
                        File.Delete(DatabasePath);
#if UNITY_2019_1_OR_NEWER
                        Debug.LogWarning($"[LiteDbDataStore] Deleted 0-byte database file: {DatabasePath}");
#endif
                    }
                }
                catch (Exception) { /* ignore access errors here, let LiteDB handle it */ }
            }

            var connectionString = new ConnectionString
            {
                Filename = DatabasePath,
                Connection = ResolveConnectionType(),
                Upgrade = true,
                ReadOnly = _options.ReadOnly
            };

            int retryCount = 0;
            const int maxRetries = 3;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    _database = new LiteDatabase(connectionString);
                    InitializeDatabase();
                    break; // Success
                }
                catch (Exception ex) when (
                    (ex.Message.Contains("PAGE_SIZE") || ex is LiteException ||
                     ex is System.IO.IOException || ex.Message.Contains("Sharing violation")) 
                    && retryCount < maxRetries - 1)
                {
                    retryCount++;
                    
                    // Ensure complete disposal — flush handles first
                    if (_database != null)
                    {
                        try { _database.Checkpoint(); } catch { /* best-effort flush */ }
                        _database.Dispose();
                        _database = null;
                    }

                    try
                    {
                        // Wait for file release with retry instead of fragile GC.Collect + Sleep
                        DeleteFileWithRetry(DatabasePath, maxWaitMs: 1000);
                        DeleteFileWithRetry(DatabasePath + "-log", maxWaitMs: 1000);

#if UNITY_2019_1_OR_NEWER
                        Debug.LogWarning($"[LiteDbDataStore] Database corruption detected ({ex.Message}). Deleted and retrying ({retryCount}/{maxRetries}): {DatabasePath}");
#endif
                    }
                    catch (Exception deleteEx)
                    {
#if UNITY_2019_1_OR_NEWER
                        Debug.LogError($"[LiteDbDataStore] Failed to delete corrupted database: {deleteEx.Message}");
#endif
                        throw new Exception($"Failed to recover from database corruption: {deleteEx.Message}", deleteEx);
                    }
                }
                catch (Exception ex)
                {
                    // Non-recoverable error
                    throw new Exception($"Failed to initialize LiteDB: {ex.Message}", ex);
                }
            }
        }

        private static ConnectionType ResolveConnectionType()
        {
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
            // Shared mode requires named mutex support, which is not available on some runtime platforms.
            return ConnectionType.Direct;
#else
            return ConnectionType.Shared;
#endif
        }

        #region IDataStore 实现

        public StorageBackend Backend => StorageBackend.LiteDb;

        public IReadOnlyCollection<string> DatasetNames
        {
            get
            {
                ThrowIfDisposed();
                return TabularNames.Concat(GraphNames).ToList().AsReadOnly();
            }
        }

        public IReadOnlyCollection<string> TabularNames
        {
            get
            {
                ThrowIfDisposed();
                lock (_lock)
                {
                    return _database.GetCollection<TabularMetadata>("tabular_meta").FindAll().Select(m => m.Name).ToList().AsReadOnly();
                }
            }
        }

        public IReadOnlyCollection<string> GraphNames
        {
            get
            {
                ThrowIfDisposed();
                lock (_lock)
                {
                    return _database.GetCollection<GraphMetadata>("graph_meta").FindAll().Select(m => m.Name).ToList().AsReadOnly();
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LiteDbDataStore), $"Data store at '{DatabasePath}' has been disposed");
        }

        #endregion

        #region 表格操作

        public ITabularDataset CreateTabular(string name)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required", nameof(name));

            lock (_lock)
            {
                var meta = _database.GetCollection<TabularMetadata>("tabular_meta");
                if (meta.Exists(m => m.Name == name))
                    throw new InvalidOperationException($"Tabular '{name}' already exists");

                var metadata = new TabularMetadata
                {
                    Id = ObjectId.NewObjectId(),
                    Name = name,
                    DatasetId = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                };
                meta.Insert(metadata);

                var tabular = new LiteDbTabularDataset(_database, metadata);
                _tabularCache[name] = tabular;
                return tabular;
            }
        }

        public ITabularDataset GetTabular(string name)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                if (_tabularCache.TryGetValue(name, out var cached))
                    return cached;

                var meta = _database.GetCollection<TabularMetadata>("tabular_meta");
                var metadata = meta.FindOne(m => m.Name == name);
                if (metadata == null)
                    throw new KeyNotFoundException($"Tabular '{name}' not found");

                var tabular = new LiteDbTabularDataset(_database, metadata);
                _tabularCache[name] = tabular;
                return tabular;
            }
        }

        public ITabularDataset GetOrCreateTabular(string name)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                if (_tabularCache.TryGetValue(name, out var cached))
                    return cached;

                var meta = _database.GetCollection<TabularMetadata>("tabular_meta");
                var metadata = meta.FindOne(m => m.Name == name);

                if (metadata != null)
                {
                    var tabular = new LiteDbTabularDataset(_database, metadata);
                    _tabularCache[name] = tabular;
                    return tabular;
                }

                return CreateTabular(name);
            }
        }

        public bool TryGetTabular(string name, out ITabularDataset tabular)
        {
            tabular = null;
            if (_disposed) return false;
            
            try
            {
                tabular = GetTabular(name);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public bool TabularExists(string name)
        {
            if (_disposed) return false;
            
            lock (_lock)
            {
                if (_tabularCache.ContainsKey(name)) return true;
                var meta = _database.GetCollection<TabularMetadata>("tabular_meta");
                return meta.Exists(m => m.Name == name);
            }
        }

        public bool DeleteTabular(string name)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                // Mark cached dataset as disposed before removing
                if (_tabularCache.TryGetValue(name, out var cachedDataset))
                {
                    cachedDataset.MarkDisposed();
                }
                _tabularCache.Remove(name);

                var meta = _database.GetCollection<TabularMetadata>("tabular_meta");
                var metadata = meta.FindOne(m => m.Name == name);
                if (metadata == null) return false;

                // 删除数据
                var rows = _database.GetCollection<TabularRow>($"tabular_{metadata.Id}");
                rows.DeleteAll();
                _database.DropCollection($"tabular_{metadata.Id}");

                return meta.Delete(metadata.Id);
            }
        }

        #endregion

        #region 图操作

        public IGraphDataset CreateGraph(string name)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required", nameof(name));

            lock (_lock)
            {
                var meta = _database.GetCollection<GraphMetadata>("graph_meta");
                if (meta.Exists(m => m.Name == name))
                    throw new InvalidOperationException($"Graph '{name}' already exists");

                var metadata = new GraphMetadata
                {
                    Id = ObjectId.NewObjectId(),
                    Name = name,
                    DatasetId = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                };
                meta.Insert(metadata);

                var graph = new LiteDbGraphDataset(_database, metadata);
                _graphCache[name] = graph;
                return graph;
            }
        }

        public IGraphDataset GetGraph(string name)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                if (_graphCache.TryGetValue(name, out var cached))
                    return cached;

                var meta = _database.GetCollection<GraphMetadata>("graph_meta");
                var metadata = meta.FindOne(m => m.Name == name);
                if (metadata == null)
                    throw new KeyNotFoundException($"Graph '{name}' not found");

                var graph = new LiteDbGraphDataset(_database, metadata);
                _graphCache[name] = graph;
                return graph;
            }
        }

        public IGraphDataset GetOrCreateGraph(string name)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                if (_graphCache.TryGetValue(name, out var cached))
                    return cached;

                var meta = _database.GetCollection<GraphMetadata>("graph_meta");
                var metadata = meta.FindOne(m => m.Name == name);

                if (metadata != null)
                {
                    var graph = new LiteDbGraphDataset(_database, metadata);
                    _graphCache[name] = graph;
                    return graph;
                }

                return CreateGraph(name);
            }
        }

        public bool TryGetGraph(string name, out IGraphDataset graph)
        {
            graph = null;
            if (_disposed) return false;
            
            try
            {
                graph = GetGraph(name);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public bool GraphExists(string name)
        {
            if (_disposed) return false;
            
            lock (_lock)
            {
                if (_graphCache.ContainsKey(name)) return true;
                var meta = _database.GetCollection<GraphMetadata>("graph_meta");
                return meta.Exists(m => m.Name == name);
            }
        }

        public bool DeleteGraph(string name)
        {
            ThrowIfDisposed();
            
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
            return ExecuteWithMobileLock(() => DeleteGraphUnlocked(name));
#else
            lock (_lock)
            {
                return DeleteGraphUnlocked(name);
            }
#endif
        }

        private bool DeleteGraphUnlocked(string name)
        {
            // Mark cached dataset as disposed before removing
            if (_graphCache.TryGetValue(name, out var cachedDataset))
            {
                cachedDataset.MarkDisposed();
            }
            _graphCache.Remove(name);

            var meta = _database.GetCollection<GraphMetadata>("graph_meta");
            var metadata = meta.FindOne(m => m.Name == name);
            if (metadata == null) return false;

            // 删除数据
            _database.DropCollection($"graph_{metadata.Id}_nodes");
            _database.DropCollection($"graph_{metadata.Id}_edges");

            return meta.Delete(metadata.Id);
        }

        #endregion

        #region 事务

        public bool BeginTransaction()
        {
            ThrowIfDisposed();
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
            return ExecuteWithMobileLock(() => _database.BeginTrans());
#else
            return _database.BeginTrans();
#endif
        }
        
        public bool Commit()
        {
            ThrowIfDisposed();
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
            return ExecuteWithMobileLock(() => _database.Commit());
#else
            return _database.Commit();
#endif
        }
        
        public bool Rollback()
        {
            ThrowIfDisposed();
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
            return ExecuteWithMobileLock(() => _database.Rollback());
#else
            return _database.Rollback();
#endif
        }

        public void ExecuteInTransaction(Action action)
        {
            ThrowIfDisposed();
            
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
            ExecuteWithMobileLock(() =>
            {
                _database.BeginTrans();
                try
                {
                    action?.Invoke();
                    _database.Commit();
                }
                catch
                {
                    _database.Rollback();
                    throw;
                }
            });
#else
            _database.BeginTrans();
            try
            {
                action?.Invoke();
                _database.Commit();
            }
            catch
            {
                _database.Rollback();
                throw;
            }
#endif
        }

        public T ExecuteInTransaction<T>(Func<T> action)
        {
            ThrowIfDisposed();
            
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
            return ExecuteWithMobileLock(() =>
            {
                _database.BeginTrans();
                try
                {
                    var result = action != null ? action() : default;
                    _database.Commit();
                    return result;
                }
                catch
                {
                    _database.Rollback();
                    throw;
                }
            });
#else
            _database.BeginTrans();
            try
            {
                var result = action != null ? action() : default;
                _database.Commit();
                return result;
            }
            catch
            {
                _database.Rollback();
                throw;
            }
#endif
        }

        #endregion

        #region 维护

        public void Checkpoint()
        {
            ThrowIfDisposed();
            
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
            ExecuteWithMobileLock(() =>
            {
                // First flush all pending metadata updates in cached datasets
                foreach (var graph in _graphCache.Values)
                {
                    graph.FlushMetadata();
                }
                foreach (var tabular in _tabularCache.Values)
                {
                    tabular.FlushMetadata();
                }
                
                _database.Checkpoint();
            });
#else
            // First flush all pending metadata updates in cached datasets
            lock (_lock)
            {
                foreach (var graph in _graphCache.Values)
                {
                    graph.FlushMetadata();
                }
                foreach (var tabular in _tabularCache.Values)
                {
                    tabular.FlushMetadata();
                }
            }
            
            _database.Checkpoint();
#endif
        }

        public void ClearAll()
        {
            ThrowIfDisposed();
            
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
            ExecuteWithMobileLock(() => ClearAllUnlocked());
#else
            lock (_lock)
            {
                ClearAllUnlocked();
            }
#endif
        }

        private void ClearAllUnlocked()
        {
            // Step 1: Mark all cached datasets as disposed first
            foreach (var graph in _graphCache.Values)
            {
                graph.MarkDisposed();
            }
            foreach (var tabular in _tabularCache.Values)
            {
                tabular.MarkDisposed();
            }

            // Step 2: Delete from database (read names from DB, not cache)
            var tabularMeta = _database.GetCollection<TabularMetadata>("tabular_meta");
            var graphMeta = _database.GetCollection<GraphMetadata>("graph_meta");

            foreach (var metadata in tabularMeta.FindAll().ToList())
            {
                var rows = _database.GetCollection<TabularRow>($"tabular_{metadata.Id}");
                rows.DeleteAll();
                _database.DropCollection($"tabular_{metadata.Id}");
                tabularMeta.Delete(metadata.Id);
            }

            foreach (var metadata in graphMeta.FindAll().ToList())
            {
                _database.DropCollection($"graph_{metadata.Id}_nodes");
                _database.DropCollection($"graph_{metadata.Id}_edges");
                graphMeta.Delete(metadata.Id);
            }

            // Step 3: Clear caches last
            _tabularCache.Clear();
            _graphCache.Clear();
        }

        public long Shrink()
        {
            ThrowIfDisposed();
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
            return ExecuteWithMobileLock(() => _database.Rebuild());
#else
            return _database.Rebuild();
#endif
        }

        public long GetDatabaseSize()
        {
            if (File.Exists(DatabasePath))
                return new FileInfo(DatabasePath).Length;
            return 0;
        }

        #endregion

        #region 私有方法

        private void InitializeDatabase()
        {
            // 初始化索引
            var tabularMeta = _database.GetCollection<TabularMetadata>("tabular_meta");
            tabularMeta.EnsureIndex(m => m.Name, true);

            var graphMeta = _database.GetCollection<GraphMetadata>("graph_meta");
            graphMeta.EnsureIndex(m => m.Name, true);
        }

        private static void DeleteFileWithRetry(string path, int maxWaitMs = 1000)
        {
            if (!File.Exists(path)) return;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < maxWaitMs)
            {
                try
                {
                    File.Delete(path);
                    return;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50); // Short wait, retry
                }
            }

            File.Delete(path); // Final attempt — let exception propagate if still locked
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required", nameof(path));

            if (!Path.IsPathRooted(path))
            {
#if UNITY_2019_1_OR_NEWER
                return Path.Combine(Application.persistentDataPath, path);
#else
                return Path.GetFullPath(path);
#endif
            }
            return path;
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
            ExecuteWithMobileLock(() =>
            {
                // Mark all cached datasets as disposed before clearing
                foreach (var graph in _graphCache.Values)
                {
                    graph.MarkDisposed();
                }
                foreach (var tabular in _tabularCache.Values)
                {
                    tabular.MarkDisposed();
                }
                
                _tabularCache.Clear();
                _graphCache.Clear();
                
                _database?.Dispose();
            });
#else
            lock (_lock)
            {
                // Mark all cached datasets as disposed before clearing
                foreach (var graph in _graphCache.Values)
                {
                    graph.MarkDisposed();
                }
                foreach (var tabular in _tabularCache.Values)
                {
                    tabular.MarkDisposed();
                }
                
                _tabularCache.Clear();
                _graphCache.Clear();
            }
            
            _database?.Dispose();
#endif
        }
    }

    #region 内部文档类

    internal class TabularMetadata
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public string DatasetId { get; set; }
        public List<ColumnMeta> Columns { get; set; } = new();
        public int RowCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }

    internal class ColumnMeta
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int Index { get; set; }
        public bool Indexed { get; set; }
    }

    internal class TabularRow
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public int RowIndex { get; set; }
        public BsonDocument Data { get; set; } = new();
    }

    internal class GraphMetadata
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public string DatasetId { get; set; }
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }

    internal class GraphNode
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public string NodeId { get; set; }
        public BsonDocument Properties { get; set; } = new();
    }

    internal class GraphEdge
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public string FromNodeId { get; set; }
        public string ToNodeId { get; set; }
        public string Type { get; set; } = string.Empty;
        public double Weight { get; set; }
        public BsonDocument Properties { get; set; } = new();
    }

    #endregion
}
