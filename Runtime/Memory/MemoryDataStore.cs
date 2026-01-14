using System;
using System.Collections.Generic;
using System.Linq;
using NumSharp;

namespace AroAro.DataCore.Memory
{
    /// <summary>
    /// 内存数据存储实现 - 不持久化，适用于测试和临时数据
    /// </summary>
    public sealed class MemoryDataStore : IDataStore
    {
        private readonly Dictionary<string, MemoryTabularDataset> _tabulars = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MemoryGraphDataset> _graphs = new(StringComparer.Ordinal);
        private readonly DataStoreOptions _options;
        private bool _disposed;

        public MemoryDataStore(DataStoreOptions options = null)
        {
            _options = options ?? new DataStoreOptions();
        }

        #region IDataStore 实现

        public StorageBackend Backend => StorageBackend.Memory;

        public IReadOnlyCollection<string> DatasetNames =>
            _tabulars.Keys.Concat(_graphs.Keys).ToList().AsReadOnly();

        public IReadOnlyCollection<string> TabularNames =>
            _tabulars.Keys.ToList().AsReadOnly();

        public IReadOnlyCollection<string> GraphNames =>
            _graphs.Keys.ToList().AsReadOnly();

        #endregion

        #region 表格操作

        public ITabularDataset CreateTabular(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required", nameof(name));

            if (_tabulars.ContainsKey(name))
                throw new InvalidOperationException($"Tabular '{name}' already exists");

            var tabular = new MemoryTabularDataset(name);
            _tabulars[name] = tabular;
            return tabular;
        }

        public ITabularDataset GetTabular(string name)
        {
            if (!_tabulars.TryGetValue(name, out var tabular))
                throw new KeyNotFoundException($"Tabular '{name}' not found");
            return tabular;
        }

        public ITabularDataset GetOrCreateTabular(string name)
        {
            if (_tabulars.TryGetValue(name, out var tabular))
                return tabular;
            return CreateTabular(name);
        }

        public bool TryGetTabular(string name, out ITabularDataset tabular)
        {
            if (_tabulars.TryGetValue(name, out var t))
            {
                tabular = t;
                return true;
            }
            tabular = null;
            return false;
        }

        public bool TabularExists(string name) => _tabulars.ContainsKey(name);

        public bool DeleteTabular(string name) => _tabulars.Remove(name);

        #endregion

        #region 图操作

        public IGraphDataset CreateGraph(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required", nameof(name));

            if (_graphs.ContainsKey(name))
                throw new InvalidOperationException($"Graph '{name}' already exists");

            var graph = new MemoryGraphDataset(name);
            _graphs[name] = graph;
            return graph;
        }

        public IGraphDataset GetGraph(string name)
        {
            if (!_graphs.TryGetValue(name, out var graph))
                throw new KeyNotFoundException($"Graph '{name}' not found");
            return graph;
        }

        public IGraphDataset GetOrCreateGraph(string name)
        {
            if (_graphs.TryGetValue(name, out var graph))
                return graph;
            return CreateGraph(name);
        }

        public bool TryGetGraph(string name, out IGraphDataset graph)
        {
            if (_graphs.TryGetValue(name, out var g))
            {
                graph = g;
                return true;
            }
            graph = null;
            return false;
        }

        public bool GraphExists(string name) => _graphs.ContainsKey(name);

        public bool DeleteGraph(string name) => _graphs.Remove(name);

        #endregion

        #region 事务（内存存储不需要真正的事务）

        public bool BeginTransaction() => true;
        public bool Commit() => true;
        public bool Rollback() => true;

        public void ExecuteInTransaction(Action action)
        {
            action?.Invoke();
        }

        public T ExecuteInTransaction<T>(Func<T> action)
        {
            return action != null ? action() : default;
        }

        #endregion

        #region 维护

        public void Checkpoint() { } // 内存存储无需操作

        public void ClearAll()
        {
            _tabulars.Clear();
            _graphs.Clear();
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearAll();
        }
    }
}
