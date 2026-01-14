using System;
using System.Collections.Generic;
using System.Linq;
using AroAro.DataCore.Events;
using AroAro.DataCore.Session;

namespace AroAro.DataCore
{
    /// <summary>
    /// 数据集元数据，用于兼容性
    /// </summary>
    public class DatasetMetadata
    {
        public string Name { get; set; }
        public DataSetKind Kind { get; set; }
        public bool IsLoaded { get; set; } = true;
    }

    /// <summary>
    /// DataCore 数据存储 - 封装 LiteDB 后端
    /// 
    /// 这是主要的用户入口点，提供简化的 API
    /// </summary>
    public sealed class DataCoreStore : IDisposable
    {
        private readonly IDataStore _store;
        private SessionManager _sessionManager;
        private bool _disposed;

        /// <summary>
        /// 创建默认的数据存储
        /// </summary>
        public DataCoreStore() : this(DataStoreFactory.GetDefaultPath())
        {
        }

        /// <summary>
        /// 创建指定路径的数据存储
        /// </summary>
        /// <param name="dbPath">数据库文件路径</param>
        public DataCoreStore(string dbPath)
        {
            _store = DataStoreFactory.Create(dbPath);
        }

        /// <summary>
        /// 使用现有的 IDataStore 创建（用于高级场景）
        /// </summary>
        /// <param name="store">数据存储实例</param>
        public DataCoreStore(IDataStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        /// 底层数据存储
        /// </summary>
        public IDataStore UnderlyingStore => _store;

        /// <summary>
        /// 数据库路径
        /// </summary>
        public string DatabasePath => (_store as LiteDb.LiteDbDataStore)?.DatabasePath;

        /// <summary>
        /// 所有数据集名称
        /// </summary>
        public IReadOnlyCollection<string> Names => _store.DatasetNames;

        /// <summary>
        /// 所有表格数据集名称
        /// </summary>
        public IReadOnlyCollection<string> TabularNames => _store.TabularNames;

        /// <summary>
        /// 所有图数据集名称
        /// </summary>
        public IReadOnlyCollection<string> GraphNames => _store.GraphNames;

        /// <summary>
        /// 会话管理器
        /// </summary>
        public SessionManager SessionManager
        {
            get
            {
                _sessionManager ??= new SessionManager(this);
                return _sessionManager;
            }
        }

        #region 创建数据集

        /// <summary>
        /// 创建表格数据集
        /// </summary>
        public ITabularDataset CreateTabular(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) 
                throw new ArgumentException("Name required", nameof(name));
            
            var tabular = _store.CreateTabular(name);
            DataCoreEventManager.RaiseDatasetCreated(tabular);
            return tabular;
        }

        /// <summary>
        /// 创建图数据集
        /// </summary>
        public IGraphDataset CreateGraph(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) 
                throw new ArgumentException("Name required", nameof(name));
            
            var graph = _store.CreateGraph(name);
            DataCoreEventManager.RaiseDatasetCreated(graph);
            return graph;
        }

        #endregion

        #region 获取数据集

        /// <summary>
        /// 获取数据集
        /// </summary>
        public T Get<T>(string name) where T : class, IDataSet
        {
            if (!TryGet(name, out var ds))
                throw new KeyNotFoundException($"Dataset not found: {name}");
            
            if (ds is not T typed)
                throw new InvalidOperationException($"Dataset '{name}' is {ds.Kind}, not {typeof(T).Name}");
            
            return typed;
        }

        /// <summary>
        /// 尝试获取数据集
        /// </summary>
        public bool TryGet(string name, out IDataSet dataSet)
        {
            dataSet = null;
            
            if (_store.TryGetTabular(name, out var tabular))
            {
                dataSet = tabular;
                return true;
            }
            
            if (_store.TryGetGraph(name, out var graph))
            {
                dataSet = graph;
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// 获取表格数据集
        /// </summary>
        public ITabularDataset GetTabular(string name) => _store.GetTabular(name);

        /// <summary>
        /// 获取图数据集
        /// </summary>
        public IGraphDataset GetGraph(string name) => _store.GetGraph(name);

        /// <summary>
        /// 获取或创建表格数据集
        /// </summary>
        public ITabularDataset GetOrCreateTabular(string name) => _store.GetOrCreateTabular(name);

        /// <summary>
        /// 获取或创建图数据集
        /// </summary>
        public IGraphDataset GetOrCreateGraph(string name) => _store.GetOrCreateGraph(name);

        #endregion

        #region 检查与删除

        /// <summary>
        /// 检查数据集是否存在
        /// </summary>
        public bool HasDataset(string name)
        {
            return _store.TabularExists(name) || _store.GraphExists(name);
        }

        /// <summary>
        /// 删除数据集
        /// </summary>
        public bool Delete(string name)
        {
            var kind = DataSetKind.Tabular;
            bool removed = false;

            if (_store.TabularExists(name))
            {
                kind = DataSetKind.Tabular;
                removed = _store.DeleteTabular(name);
            }
            else if (_store.GraphExists(name))
            {
                kind = DataSetKind.Graph;
                removed = _store.DeleteGraph(name);
            }

            if (removed)
            {
                DataCoreEventManager.RaiseDatasetDeleted(name, kind);
            }

            return removed;
        }

        #endregion

        #region 元数据

        /// <summary>
        /// 获取数据集元数据
        /// </summary>
        public DatasetMetadata GetMetadata(string name)
        {
            if (_store.TabularExists(name))
            {
                return new DatasetMetadata { Name = name, Kind = DataSetKind.Tabular, IsLoaded = true };
            }
            if (_store.GraphExists(name))
            {
                return new DatasetMetadata { Name = name, Kind = DataSetKind.Graph, IsLoaded = true };
            }
            return null;
        }

        /// <summary>
        /// 获取所有数据集元数据
        /// </summary>
        public IEnumerable<DatasetMetadata> GetAllMetadata()
        {
            foreach (var name in _store.TabularNames)
            {
                yield return new DatasetMetadata { Name = name, Kind = DataSetKind.Tabular, IsLoaded = true };
            }
            foreach (var name in _store.GraphNames)
            {
                yield return new DatasetMetadata { Name = name, Kind = DataSetKind.Graph, IsLoaded = true };
            }
        }

        #endregion

        #region 事务

        /// <summary>
        /// 开始事务
        /// </summary>
        public bool BeginTransaction() => _store.BeginTransaction();

        /// <summary>
        /// 提交事务
        /// </summary>
        public bool Commit() => _store.Commit();

        /// <summary>
        /// 回滚事务
        /// </summary>
        public bool Rollback() => _store.Rollback();

        /// <summary>
        /// 在事务中执行操作
        /// </summary>
        public void ExecuteInTransaction(Action action) => _store.ExecuteInTransaction(action);

        /// <summary>
        /// 在事务中执行操作并返回结果
        /// </summary>
        public T ExecuteInTransaction<T>(Func<T> func) => _store.ExecuteInTransaction(func);

        #endregion

        #region 维护

        /// <summary>
        /// 执行检查点
        /// </summary>
        public void Checkpoint() => _store.Checkpoint();

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void ClearAll() => _store.ClearAll();

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            
            _sessionManager?.CloseAllSessions();
            _store?.Dispose();
            _disposed = true;
        }

        #endregion
    }
}
