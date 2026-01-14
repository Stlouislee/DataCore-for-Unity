using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private bool _disposed;

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
                Connection = ConnectionType.Direct,
                Upgrade = true,
                ReadOnly = _options.ReadOnly
            };

            try
            {
                _database = new LiteDatabase(connectionString);
                InitializeDatabase();
            }
            catch (Exception ex) when (ex.Message.Contains("PAGE_SIZE") || ex is LiteException)
            {
                // If corruption detected (PAGE_SIZE error), dispose, delete and retry once
                _database?.Dispose();
                _database = null;

                try
                {
                    if (File.Exists(DatabasePath)) File.Delete(DatabasePath);
                    // Also delete log file
                    var logPath = DatabasePath + "-log";
                    if (File.Exists(logPath)) File.Delete(logPath);

#if UNITY_2019_1_OR_NEWER
                    Debug.LogWarning($"[LiteDbDataStore] Database corruption detected ({ex.Message}). Deleted and retrying: {DatabasePath}");
#endif
                    _database = new LiteDatabase(connectionString);
                    InitializeDatabase();
                }
                catch (Exception retryEx)
                {
                    throw new Exception($"Failed to initialize LiteDB after recovery attempt: {retryEx.Message}", retryEx);
                }
            }
        }

        #region IDataStore 实现

        public StorageBackend Backend => StorageBackend.LiteDb;

        public IReadOnlyCollection<string> DatasetNames =>
            TabularNames.Concat(GraphNames).ToList().AsReadOnly();

        public IReadOnlyCollection<string> TabularNames =>
            _database.GetCollection<TabularMetadata>("tabular_meta").FindAll().Select(m => m.Name).ToList().AsReadOnly();

        public IReadOnlyCollection<string> GraphNames =>
            _database.GetCollection<GraphMetadata>("graph_meta").FindAll().Select(m => m.Name).ToList().AsReadOnly();

        #endregion

        #region 表格操作

        public ITabularDataset CreateTabular(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required", nameof(name));

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

        public ITabularDataset GetTabular(string name)
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

        public ITabularDataset GetOrCreateTabular(string name)
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

        public bool TryGetTabular(string name, out ITabularDataset tabular)
        {
            try
            {
                tabular = GetTabular(name);
                return true;
            }
            catch (KeyNotFoundException)
            {
                tabular = null;
                return false;
            }
        }

        public bool TabularExists(string name)
        {
            if (_tabularCache.ContainsKey(name)) return true;
            var meta = _database.GetCollection<TabularMetadata>("tabular_meta");
            return meta.Exists(m => m.Name == name);
        }

        public bool DeleteTabular(string name)
        {
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

        #endregion

        #region 图操作

        public IGraphDataset CreateGraph(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required", nameof(name));

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

        public IGraphDataset GetGraph(string name)
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

        public IGraphDataset GetOrCreateGraph(string name)
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

        public bool TryGetGraph(string name, out IGraphDataset graph)
        {
            try
            {
                graph = GetGraph(name);
                return true;
            }
            catch (KeyNotFoundException)
            {
                graph = null;
                return false;
            }
        }

        public bool GraphExists(string name)
        {
            if (_graphCache.ContainsKey(name)) return true;
            var meta = _database.GetCollection<GraphMetadata>("graph_meta");
            return meta.Exists(m => m.Name == name);
        }

        public bool DeleteGraph(string name)
        {
            _graphCache.Remove(name);

            var meta = _database.GetCollection<GraphMetadata>("graph_meta");
            var metadata = meta.FindOne(m => m.Name == name);
            if (metadata == null) return false;

            // 删除数据
            _database.DropCollection($"graph_nodes_{metadata.Id}");
            _database.DropCollection($"graph_edges_{metadata.Id}");

            return meta.Delete(metadata.Id);
        }

        #endregion

        #region 事务

        public bool BeginTransaction() => _database.BeginTrans();
        public bool Commit() => _database.Commit();
        public bool Rollback() => _database.Rollback();

        public void ExecuteInTransaction(Action action)
        {
            BeginTransaction();
            try
            {
                action?.Invoke();
                Commit();
            }
            catch
            {
                Rollback();
                throw;
            }
        }

        public T ExecuteInTransaction<T>(Func<T> action)
        {
            BeginTransaction();
            try
            {
                var result = action != null ? action() : default;
                Commit();
                return result;
            }
            catch
            {
                Rollback();
                throw;
            }
        }

        #endregion

        #region 维护

        public void Checkpoint() => _database.Checkpoint();

        public void ClearAll()
        {
            _tabularCache.Clear();
            _graphCache.Clear();

            foreach (var name in TabularNames.ToList())
                DeleteTabular(name);

            foreach (var name in GraphNames.ToList())
                DeleteGraph(name);
        }

        public long Shrink() => _database.Rebuild();

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
            _tabularCache.Clear();
            _graphCache.Clear();
            _database?.Dispose();
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
        public double Weight { get; set; }
        public BsonDocument Properties { get; set; } = new();
    }

    #endregion
}
