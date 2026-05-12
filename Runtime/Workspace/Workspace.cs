using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AroAro.DataCore.Events;

namespace AroAro.DataCore.Workspace
{
    /// <summary>
    /// Workspace 实现 — 统一内存工作区
    /// </summary>
    public sealed class Workspace : IWorkspace
    {
        private const int AutoWeakThreshold = 100_000;
        private const int SampleRowCount = 3;

        private readonly DataCoreStore _store;
        private readonly ConcurrentDictionary<string, WorkspaceSlot> _slots = new(StringComparer.Ordinal);
        private readonly object _metadataLock = new();
        private volatile bool _metadataDirty = true;
        private IReadOnlyList<WorkspaceEntry> _cachedEntries;
        private bool _disposed;

        public Workspace(DataCoreStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        #region IWorkspace — 基础查询

        /// <inheritdoc/>
        public IReadOnlyCollection<string> DatasetNames
        {
            get { ThrowIfDisposed(); return _slots.Keys.ToList().AsReadOnly(); }
        }

        /// <inheritdoc/>
        public int DatasetCount
        {
            get { ThrowIfDisposed(); return _slots.Count; }
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<string> AllNames
        {
            get
            {
                var names = new HashSet<string>(_slots.Keys, StringComparer.Ordinal);
                foreach (var n in _store.Names)
                    names.Add(n);
                return names.ToList().AsReadOnly();
            }
        }

        #endregion

        #region IWorkspace — 注册

        /// <inheritdoc/>
        public void Register(string name, IDataSet dataset,
            DataSource source = DataSource.Derived,
            WorkspaceRetentionPolicy retention = WorkspaceRetentionPolicy.Auto)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name required", nameof(name));
            if (dataset == null)
                throw new ArgumentNullException(nameof(dataset));

            var policy = ResolvePolicy(dataset, retention);
            var slot = new WorkspaceSlot(dataset, source, policy);

            if (!_slots.TryAdd(name, slot))
                throw new InvalidOperationException($"Dataset already exists in workspace: {name}");

            MarkDirty();
            DataCoreEventManager.RaiseWorkspaceDatasetRegistered(this, dataset);
        }

        /// <inheritdoc/>
        public void Register(string name, IEnumerable<Dictionary<string, object>> data,
            DataSource source = DataSource.Derived,
            WorkspaceRetentionPolicy retention = WorkspaceRetentionPolicy.Auto)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name required", nameof(name));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var rows = data.ToList();

            // Create the dataset in the store
            var tabular = _store.CreateTabular(name);

            if (rows.Count > 0)
            {
                // Auto-detect columns from first row
                var firstRow = rows[0];
                foreach (var kvp in firstRow)
                {
                    if (kvp.Value is IConvertible && kvp.Value is not bool and not char and not string)
                    {
                        tabular.AddNumericColumn(kvp.Key, new double[0]);
                    }
                    else
                    {
                        tabular.AddStringColumn(kvp.Key, new string[0]);
                    }
                }

                tabular.AddRows(rows);
            }

            var policy = ResolvePolicy(tabular, retention);
            var slot = new WorkspaceSlot(tabular, source, policy);

            if (!_slots.TryAdd(name, slot))
            {
                try { _store.Delete(name); } catch { /* best-effort */ }
                throw new InvalidOperationException($"Dataset already exists in workspace: {name}");
            }

            MarkDirty();
        }

        /// <inheritdoc/>
        public void RegisterAuto(string baseName, IDataSet dataset,
            DataSource source = DataSource.Derived,
            WorkspaceRetentionPolicy retention = WorkspaceRetentionPolicy.Auto)
        {
            var name = AutoName(baseName);
            Register(name, dataset, source, retention);
        }

        #endregion

        #region IWorkspace — 获取

        /// <inheritdoc/>
        public IDataSet Get(string name)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name required", nameof(name));

            // 优先查 workspace
            if (_slots.TryGetValue(name, out var slot))
            {
                var ds = slot.TryGetTarget();
                if (ds != null) return ds;

                // 弱引用已被回收，移除条目
                _slots.TryRemove(name, out _);
                MarkDirty();
            }

            // Fallback: 从 store 加载（直接引用，不复制）
            if (_store.TryGet(name, out var storeDs))
            {
                var policy = ResolvePolicy(storeDs, WorkspaceRetentionPolicy.Auto);
                _slots[name] = new WorkspaceSlot(storeDs, DataSource.Store, policy);
                MarkDirty();
                return storeDs;
            }

            throw new KeyNotFoundException($"Dataset not found: {name}");
        }

        /// <inheritdoc/>
        public bool Has(string name)
        {
            ThrowIfDisposed();
            // Only check workspace slots. Use AllNames for the unified view.
            return _slots.ContainsKey(name);
        }

        /// <inheritdoc/>
        public bool TryPeek(string name, out WorkspaceEntry entry)
        {
            ThrowIfDisposed();
            entry = null;

            if (_slots.TryGetValue(name, out var slot))
            {
                var ds = slot.TryGetTarget();
                if (ds != null)
                {
                    entry = BuildEntry(name, ds, slot.Source);
                    return true;
                }
            }

            // Fallback: 从 store 查元数据（不加载数据到 workspace）
            if (_store.TryGet(name, out var storeDs))
            {
                entry = BuildEntry(name, storeDs, DataSource.Store);
                return true;
            }

            return false;
        }

        #endregion

        #region IWorkspace — 自省

        /// <inheritdoc/>
        public WorkspaceEntry Describe(string name)
        {
            ThrowIfDisposed();
            if (!TryPeek(name, out var entry))
                throw new KeyNotFoundException($"Dataset not found: {name}");
            return entry;
        }

        /// <inheritdoc/>
        public IReadOnlyList<WorkspaceEntry> DescribeAll()
        {
            ThrowIfDisposed();
            if (_metadataDirty)
            {
                lock (_metadataLock)
                {
                    if (_metadataDirty)
                    {
                        _cachedEntries = ComputeAllEntries();
                        _metadataDirty = false;
                    }
                }
            }
            return _cachedEntries;
        }

        /// <inheritdoc/>
        public string Summary()
        {
            ThrowIfDisposed();
            var all = DescribeAll();
            var storeCount = all.Count(e => e.Source == DataSource.Store);
            var derivedCount = all.Count(e => e.Source == DataSource.Derived);
            var importedCount = all.Count(e => e.Source == DataSource.Imported);

            var parts = new List<string>();
            if (storeCount > 0) parts.Add($"{storeCount} store");
            if (derivedCount > 0) parts.Add($"{derivedCount} derived");
            if (importedCount > 0) parts.Add($"{importedCount} imported");

            return parts.Count == 0
                ? "Workspace: empty"
                : $"Workspace: {all.Count} datasets ({string.Join(", ", parts)})";
        }

        #endregion

        #region IWorkspace — 生命周期

        /// <inheritdoc/>
        public bool Remove(string name)
        {
            ThrowIfDisposed();
            if (!_slots.TryRemove(name, out _))
                return false;

            MarkDirty();
            return true;
        }

        /// <inheritdoc/>
        public bool Rename(string oldName, string newName)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(oldName))
                throw new ArgumentException("Old name required", nameof(oldName));
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New name required", nameof(newName));
            if (string.Equals(oldName, newName, StringComparison.Ordinal))
                return true;

            if (!_slots.TryRemove(oldName, out var slot))
                return false;

            if (!_slots.TryAdd(newName, slot))
            {
                // 回滚
                _slots[oldName] = slot;
                throw new InvalidOperationException($"Dataset already exists in workspace: {newName}");
            }

            MarkDirty();
            return true;
        }

        /// <inheritdoc/>
        public IDataSet Clone(string name, string newName)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Source name required", nameof(name));
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New name required", nameof(newName));

            var source = Get(name); // triggers load if needed

            // Create a new tabular and copy data via CSV
            IDataSet cloned;
            if (source is ITabularDataset tabular)
            {
                var newTabular = _store.CreateTabular(newName);
                var csv = tabular.ExportToCsv();
                newTabular.ImportFromCsv(csv);
                cloned = newTabular;
            }
            else if (source is IGraphDataset graph)
            {
                var newGraph = _store.CreateGraph(newName);
                foreach (var nodeId in graph.GetNodeIds())
                {
                    var props = graph.GetNodeProperties(nodeId);
                    newGraph.AddNode(nodeId, props as IDictionary<string, object>);
                }
                foreach (var edge in graph.GetEdges())
                {
                    var props = graph.GetEdgeProperties(edge.From, edge.To);
                    newGraph.AddEdge(edge.From, edge.To, props);
                }
                cloned = newGraph;
            }
            else
            {
                throw new InvalidOperationException($"Unsupported dataset kind: {source.Kind}");
            }

            var policy = ResolvePolicy(cloned, WorkspaceRetentionPolicy.Auto);
            _slots[newName] = new WorkspaceSlot(cloned, DataSource.Derived, policy);

            MarkDirty();
            return cloned;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            ThrowIfDisposed();
            _slots.Clear();
            MarkDirty();
        }

        #endregion

        #region IWorkspace — 异步 API

        /// <inheritdoc/>
        public async Task<IReadOnlyList<WorkspaceEntry>> DescribeAllAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return await Task.Run(() => DescribeAll(), ct);
        }

        /// <inheritdoc/>
        public async Task RegisterAsync(string name, IDataSet dataset,
            DataSource source = DataSource.Derived,
            WorkspaceRetentionPolicy retention = WorkspaceRetentionPolicy.Auto,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await Task.Run(() => Register(name, dataset, source, retention), ct);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Only dispose datasets that the workspace "owns" (derived/imported),
            // not store datasets which are owned by the store.
            foreach (var kv in _slots)
            {
                if (kv.Value.Source != DataSource.Store)
                {
                    var ds = kv.Value.TryGetTarget();
                    if (ds is IDisposable disposable)
                    {
                        try { disposable.Dispose(); } catch { /* best-effort */ }
                    }
                }
            }

            _slots.Clear();
            _cachedEntries = null;
        }

        #endregion

        #region 内部方法

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Workspace));
        }

        private void MarkDirty()
        {
            _metadataDirty = true;
        }

        private string AutoName(string baseName)
        {
            if (!_slots.ContainsKey(baseName) && !_store.HasDataset(baseName))
                return baseName;

            int i = 2;
            string candidate;
            do
            {
                candidate = $"{baseName}_{i}";
                i++;
            } while (_slots.ContainsKey(candidate) || _store.HasDataset(candidate));

            return candidate;
        }

        private static WorkspaceRetentionPolicy ResolvePolicy(IDataSet dataset, WorkspaceRetentionPolicy requested)
        {
            if (requested != WorkspaceRetentionPolicy.Auto)
                return requested;

            if (dataset is ITabularDataset tabular)
            {
                return tabular.RowCount >= AutoWeakThreshold
                    ? WorkspaceRetentionPolicy.Weak
                    : WorkspaceRetentionPolicy.Strong;
            }

            return WorkspaceRetentionPolicy.Strong;
        }

        private IReadOnlyList<WorkspaceEntry> ComputeAllEntries()
        {
            var entries = new List<WorkspaceEntry>();

            foreach (var kv in _slots)
            {
                var ds = kv.Value.TryGetTarget();
                if (ds != null)
                {
                    entries.Add(BuildEntry(kv.Key, ds, kv.Value.Source));
                }
            }

            // 补充 store 中有但 workspace 没有的
            foreach (var name in _store.Names)
            {
                if (!_slots.ContainsKey(name) && _store.TryGet(name, out var storeDs))
                {
                    entries.Add(BuildEntry(name, storeDs, DataSource.Store));
                }
            }

            return entries;
        }

        private static WorkspaceEntry BuildEntry(string name, IDataSet dataset, DataSource source)
        {
            var entry = new WorkspaceEntry
            {
                Name = name,
                Kind = dataset.Kind,
                Source = source,
                Rows = 0,
                Columns = 0,
                Schema = Array.Empty<ColumnInfo>(),
                Sample = Array.Empty<Dictionary<string, object>>()
            };

            if (dataset is ITabularDataset tabular)
            {
                entry.Rows = tabular.RowCount;
                entry.Columns = tabular.ColumnCount;
                entry.Schema = tabular.ColumnNames
                    .Select(c => new ColumnInfo(c, tabular.GetColumnType(c)))
                    .ToList()
                    .AsReadOnly();

                // 取前 3 行样例
                try
                {
                    var sampleRows = new List<Dictionary<string, object>>();
                    var count = Math.Min(SampleRowCount, tabular.RowCount);
                    for (int i = 0; i < count; i++)
                    {
                        sampleRows.Add(tabular.GetRow(i));
                    }
                    entry.Sample = sampleRows.AsReadOnly();
                }
                catch
                {
                    entry.Sample = Array.Empty<Dictionary<string, object>>();
                }
            }
            else if (dataset is IGraphDataset graph)
            {
                try
                {
                    entry.Rows = graph.GetNodeIds().Count();
                    entry.Columns = graph.GetEdges().Count();
                }
                catch
                {
                    // Some graph implementations may not support these queries
                }
            }

            return entry;
        }

        #endregion

        #region 内部槽位

        private class WorkspaceSlot
        {
            public DataSource Source { get; }
            public WorkspaceRetentionPolicy Retention { get; }

            // Strong reference
            private readonly IDataSet _strongRef;
            // Weak reference
            private readonly WeakReference<IDataSet> _weakRef;

            public WorkspaceSlot(IDataSet dataset, DataSource source, WorkspaceRetentionPolicy retention)
            {
                Source = source;
                Retention = retention;

                if (retention == WorkspaceRetentionPolicy.Weak)
                {
                    _weakRef = new WeakReference<IDataSet>(dataset);
                    _strongRef = null;
                }
                else
                {
                    _strongRef = dataset;
                    _weakRef = null;
                }
            }

            public IDataSet TryGetTarget()
            {
                if (_strongRef != null) return _strongRef;
                if (_weakRef != null && _weakRef.TryGetTarget(out var target)) return target;
                return null;
            }
        }

        #endregion
    }
}
