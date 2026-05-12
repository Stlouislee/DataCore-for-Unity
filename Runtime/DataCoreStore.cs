using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AroAro.DataCore.Events;
using AroAro.DataCore.Session;
using AroAro.DataCore.Workspace;

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
        private readonly Lazy<SessionManager> _sessionManager;
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
            _sessionManager = new Lazy<SessionManager>(() => new SessionManager(this));
            Workspace = new Workspace.Workspace(this);
        }

        /// <summary>
        /// 使用现有的 IDataStore 创建（用于高级场景）
        /// </summary>
        /// <param name="store">数据存储实例</param>
        public DataCoreStore(IDataStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _sessionManager = new Lazy<SessionManager>(() => new SessionManager(this));
            Workspace = new Workspace.Workspace(this);
        }

        /// <summary>
        /// 统一内存工作区 — 构造即存在，永远可用。
        /// </summary>
        public IWorkspace Workspace { get; }

        /// <summary>
        /// 底层数据存储
        /// </summary>
        public IDataStore UnderlyingStore => _store;

        /// <summary>
        /// 数据库文件路径。仅在底层存储为 LiteDB 时可用。
        /// </summary>
        /// <exception cref="NotSupportedException">底层存储不支持路径查询时抛出</exception>
        public string DatabasePath
        {
            get
            {
                if (_store is LiteDb.LiteDbDataStore liteDb)
                    return liteDb.DatabasePath;
                throw new NotSupportedException(
                    $"DatabasePath is not supported for storage backend type '{_store.GetType().Name}'.");
            }
        }

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
        [Obsolete("Use Workspace instead. SessionManager will be removed in a future version.")]
        public SessionManager SessionManager => _sessionManager.Value;

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

        #region 异步操作

        /// <summary>
        /// 异步清空所有数据（在后台线程运行）
        /// </summary>
        /// <remarks>
        /// <para>⚠️ Performance note: Runs on a background thread via Task.Run to avoid blocking
        /// the Unity main thread. Use this instead of <see cref="ClearAll"/> when working with
        /// large datasets to prevent frame drops.</para>
        /// </remarks>
        /// <param name="ct">取消令牌</param>
        public Task ClearAllAsync(CancellationToken ct = default)
        {
            return Task.Run(() => ClearAll(), ct);
        }

        /// <summary>
        /// 异步执行检查点（在后台线程运行）
        /// </summary>
        /// <remarks>
        /// <para>⚠️ Performance note: Runs on a background thread via Task.Run to avoid blocking
        /// the Unity main thread. Checkpoint flushes data to disk and may be slow for large databases.</para>
        /// </remarks>
        /// <param name="ct">取消令牌</param>
        public Task CheckpointAsync(CancellationToken ct = default)
        {
            return Task.Run(() => Checkpoint(), ct);
        }

        /// <summary>
        /// 异步在事务中执行操作（在后台线程运行）
        /// </summary>
        /// <remarks>
        /// <para>⚠️ Performance note: Runs on a background thread via Task.Run to avoid blocking
        /// the Unity main thread. The entire action executes atomically inside a transaction.</para>
        /// </remarks>
        /// <param name="action">要执行的操作</param>
        /// <param name="ct">取消令牌</param>
        public Task ExecuteInTransactionAsync(Action action, CancellationToken ct = default)
        {
            return Task.Run(() => ExecuteInTransaction(action), ct);
        }

        /// <summary>
        /// 异步在事务中执行操作并返回结果（在后台线程运行）
        /// </summary>
        /// <remarks>
        /// <para>⚠️ Performance note: Runs on a background thread via Task.Run to avoid blocking
        /// the Unity main thread. The entire function executes atomically inside a transaction.</para>
        /// </remarks>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="func">要执行的函数</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>操作结果</returns>
        public Task<T> ExecuteInTransactionAsync<T>(Func<T> func, CancellationToken ct = default)
        {
            return Task.Run(() => ExecuteInTransaction(func), ct);
        }

        /// <summary>
        /// 异步执行原生 LiteDB SQL 查询（在后台线程运行）
        /// </summary>
        /// <remarks>
        /// <para>⚠️ Performance note: Runs on a background thread via Task.Run to avoid blocking
        /// the Unity main thread. Recommended for complex queries on large datasets.</para>
        /// </remarks>
        /// <param name="datasetName">数据集名称</param>
        /// <param name="sql">SQL-like 查询语句</param>
        /// <param name="args">参数值（@0, @1...）</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>执行结果</returns>
        /// <exception cref="KeyNotFoundException">数据集不存在时抛出</exception>
        /// <exception cref="InvalidOperationException">数据集不是表格类型时抛出</exception>
        public Task<RawResult> ExecuteRawAsync(string datasetName, string sql, object[] args, CancellationToken ct = default)
        {
            var tabular = GetTabular(datasetName);
            return tabular.ExecuteRawAsync(sql, args, ct);
        }

        /// <summary>
        /// 异步导出数据集为 CSV 字符串（在后台线程运行）
        /// </summary>
        /// <remarks>
        /// <para>⚠️ Performance note: Runs on a background thread via Task.Run to avoid blocking
        /// the Unity main thread. Recommended for datasets with &gt;10000 rows.</para>
        /// </remarks>
        /// <param name="datasetName">数据集名称</param>
        /// <param name="delimiter">分隔符</param>
        /// <param name="includeHeader">是否包含表头</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>CSV 字符串</returns>
        /// <exception cref="KeyNotFoundException">数据集不存在时抛出</exception>
        /// <exception cref="InvalidOperationException">数据集不是表格类型时抛出</exception>
        public Task<string> ExportToCsvAsync(string datasetName, char delimiter = ',', bool includeHeader = true, CancellationToken ct = default)
        {
            var tabular = GetTabular(datasetName);
            return tabular.ExportToCsvAsync(delimiter, includeHeader, ct);
        }

        /// <summary>
        /// 异步从 CSV 字符串导入数据到数据集（在后台线程运行）
        /// </summary>
        /// <remarks>
        /// <para>⚠️ Performance note: Runs on a background thread via Task.Run to avoid blocking
        /// the Unity main thread. Recommended for large CSV imports.</para>
        /// </remarks>
        /// <param name="datasetName">数据集名称</param>
        /// <param name="csvContent">CSV 内容</param>
        /// <param name="hasHeader">是否包含表头</param>
        /// <param name="delimiter">分隔符</param>
        /// <param name="ct">取消令牌</param>
        /// <exception cref="KeyNotFoundException">数据集不存在时抛出</exception>
        /// <exception cref="InvalidOperationException">数据集不是表格类型时抛出</exception>
        public Task ImportFromCsvAsync(string datasetName, string csvContent, bool hasHeader = true, char delimiter = ',', CancellationToken ct = default)
        {
            var tabular = GetTabular(datasetName);
            return tabular.ImportFromCsvAsync(csvContent, hasHeader, delimiter, ct);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            
            Workspace?.Dispose();
            if (_sessionManager.IsValueCreated)
                _sessionManager.Value.CloseAllSessions();
            _store?.Dispose();
            _disposed = true;
        }

        #endregion
    }
}
