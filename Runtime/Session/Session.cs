using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;
using AroAro.DataCore.Events;

namespace AroAro.DataCore.Session
{
    /// <summary>
    /// 会话实现类，管理临时数据集和操作
    /// </summary>
    public class Session : ISession
    {
        private readonly Dictionary<string, IDataSet> _datasets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DataFrame> _dataFrameCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, WeakReference<DataFrame>> _weakDataFrames = new(StringComparer.Ordinal);
        private readonly DataCoreStore _store;
        private bool _disposed = false;

        public string Id { get; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; }
        public DateTime LastActivityAt { get; private set; }

        public int DatasetCount => _datasets.Count;
        public IReadOnlyCollection<string> DatasetNames => _datasets.Keys;

        /// <summary>
        /// DataFrame缓存数量
        /// </summary>
        public int DataFrameCount => _dataFrameCache.Count;

        /// <summary>
        /// DataFrame名称列表
        /// </summary>
        public IReadOnlyCollection<string> DataFrameNames => _dataFrameCache.Keys;

        public Session(string name, DataCoreStore store)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            CreatedAt = DateTime.Now;
            LastActivityAt = CreatedAt;
        }

        public IDataSet OpenDataset(string name, string copyName = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));

            // 检查全局存储中是否存在该数据集
            if (!_store.HasDataset(name))
                throw new KeyNotFoundException($"Dataset not found in global store: {name}");

            // 生成副本名称
            var targetName = copyName ?? name;
            if (string.IsNullOrWhiteSpace(targetName))
                throw new ArgumentException("Copy name cannot be null or empty", nameof(copyName));

            // 检查会话中是否已存在同名数据集
            if (_datasets.ContainsKey(targetName))
                throw new InvalidOperationException($"Dataset already exists in session: {targetName}");

            // 从全局存储加载数据集
            var dataset = _store.Get<IDataSet>(name);
            
            // 创建副本
            var copy = dataset.WithName(targetName);
            _datasets[targetName] = copy;

            // 触发会话数据集添加事件
            DataCoreEventManager.RaiseSessionDatasetAdded(this, copy);

            Touch();
            return copy;
        }

        public IDataSet CreateDataset(string name, DataSetKind kind)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));

            if (_datasets.ContainsKey(name))
                throw new InvalidOperationException($"Dataset already exists in session: {name}");

            IDataSet dataset;
            switch (kind)
            {
                case DataSetKind.Tabular:
                    dataset = _store.CreateTabular(name);
                    break;
                case DataSetKind.Graph:
                    dataset = _store.CreateGraph(name);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported dataset kind: {kind}");
            }

            _datasets[name] = dataset;

            // 触发会话数据集创建事件
            DataCoreEventManager.RaiseSessionDatasetCreated(this, dataset);

            Touch();
            return dataset;
        }

        public IDataSet GetDataset(string name)
        {
            if (!_datasets.TryGetValue(name, out var dataset))
                throw new KeyNotFoundException($"Dataset not found in session: {name}");

            Touch();
            return dataset;
        }

        public bool HasDataset(string name)
        {
            return _datasets.ContainsKey(name);
        }

        public bool RemoveDataset(string name)
        {
            if (!_datasets.ContainsKey(name))
                return false;

            var dataset = _datasets[name];
            _datasets.Remove(name);

            // 触发会话数据集移除事件
            DataCoreEventManager.RaiseSessionDatasetRemoved(this, dataset);

            Touch();
            return true;
        }

        public IDataSet SaveQueryResult(string sourceName, Func<IDataSet, IDataSet> query, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New dataset name cannot be null or empty", nameof(newName));

            if (_datasets.ContainsKey(newName))
                throw new InvalidOperationException($"Dataset already exists in session: {newName}");

            // 获取源数据集
            var source = GetDataset(sourceName);
            
            // 执行查询并获取结果
            var result = query(source);
            
            // 重命名结果
            var renamedResult = result.WithName(newName);
            _datasets[newName] = renamedResult;

            // 触发会话查询结果保存事件
            DataCoreEventManager.RaiseSessionQueryResultSaved(this, source, renamedResult);

            Touch();
            return renamedResult;
        }

        public bool PersistDataset(string name, string targetName = null)
        {
            if (!_datasets.ContainsKey(name))
                return false;

            var dataset = _datasets[name];
            var target = targetName ?? name;

            // 检查全局存储中是否已存在同名数据集
            if (_store.HasDataset(target))
            {
                throw new InvalidOperationException($"Dataset already exists in global store: {target}");
            }

            // 根据数据集类型持久化到 LiteDB
            switch (dataset.Kind)
            {
                case DataSetKind.Tabular:
                    {
                        if (dataset is ITabularDataset sourceTabular)
                        {
                            var newTabular = _store.CreateTabular(target);
                            CopyTabularData(sourceTabular, newTabular);
                        }
                    }
                    break;
                case DataSetKind.Graph:
                    {
                        if (dataset is IGraphDataset sourceGraph)
                        {
                            var newGraph = _store.CreateGraph(target);
                            CopyGraphData(sourceGraph, newGraph);
                        }
                    }
                    break;
            }

            Touch();
            return true;
        }

        private void CopyTabularData(ITabularDataset source, ITabularDataset target)
        {
            // 通过导出/导入 CSV 字符串来复制数据
            var csvContent = source.ExportToCsv();
            target.ImportFromCsv(csvContent);
        }

        private void CopyGraphData(IGraphDataset source, IGraphDataset target)
        {
            // 复制所有节点
            foreach (var nodeId in source.GetNodeIds())
            {
                var props = source.GetNodeProperties(nodeId);
                target.AddNode(nodeId, props as IDictionary<string, object>);
            }

            // 复制所有边
            foreach (var edge in source.GetEdges())
            {
                target.AddEdge(edge.From, edge.To);
            }
        }

        public void Clear()
        {
            _datasets.Clear();
            Touch();
        }

        public void Touch()
        {
            LastActivityAt = DateTime.Now;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _datasets.Clear();
                _dataFrameCache.Clear();
                _weakDataFrames.Clear();
                _disposed = true;
            }
        }

        #region DataFrame Support Methods

        /// <summary>
        /// 创建新的DataFrame
        /// </summary>
        public DataFrame CreateDataFrame(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("DataFrame name cannot be null or empty", nameof(name));

            if (_dataFrameCache.ContainsKey(name))
                throw new InvalidOperationException($"DataFrame already exists in session: {name}");

            var df = new DataFrame();
            _dataFrameCache[name] = df;

            // 触发DataFrame创建事件
            DataCoreEventManager.RaiseSessionDataFrameCreated(this, name);

            Touch();
            return df;
        }

        /// <summary>
        /// 获取DataFrame
        /// </summary>
        public DataFrame GetDataFrame(string name)
        {
            if (!_dataFrameCache.TryGetValue(name, out var df))
                throw new KeyNotFoundException($"DataFrame not found in session: {name}");

            Touch();
            return df;
        }

        /// <summary>
        /// 检查是否存在DataFrame
        /// </summary>
        public bool HasDataFrame(string name)
        {
            return _dataFrameCache.ContainsKey(name);
        }

        /// <summary>
        /// 移除DataFrame
        /// </summary>
        public bool RemoveDataFrame(string name)
        {
            if (!_dataFrameCache.ContainsKey(name))
                return false;

            _dataFrameCache.Remove(name);

            // 触发DataFrame移除事件
            DataCoreEventManager.RaiseSessionDataFrameRemoved(this, name);

            Touch();
            return true;
        }

        /// <summary>
        /// 执行DataFrame查询并保存结果
        /// </summary>
        public IDataSet ExecuteDataFrameQuery(string sourceName, Func<DataFrame, DataFrame> query, string resultName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
                throw new ArgumentException("Source DataFrame name cannot be null or empty", nameof(sourceName));

            if (string.IsNullOrWhiteSpace(resultName))
                throw new ArgumentException("Result dataset name cannot be null or empty", nameof(resultName));

            if (_datasets.ContainsKey(resultName))
                throw new InvalidOperationException($"Dataset already exists in session: {resultName}");

            // 获取源DataFrame
            var sourceDf = GetDataFrame(sourceName);

            // 执行查询
            var resultDf = query(sourceDf);

            // 创建DataFrame适配器
            var adapter = new DataFrameAdapter(resultName, resultDf);
            _datasets[resultName] = adapter;

            // 缓存结果DataFrame
            _dataFrameCache[resultName] = resultDf;

            Touch();
            return adapter;
        }

        /// <summary>
        /// 将现有数据集转换为DataFrame
        /// </summary>
        public DataFrame ConvertToDataFrame(string datasetName)
        {
            var dataset = GetDataset(datasetName);
            
            if (dataset is ITabularDataset tabular)
            {
                return TabularToDataFrame(tabular);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported dataset type for DataFrame conversion: {dataset.Kind}");
            }
        }

        /// <summary>
        /// ITabularDataset转换为DataFrame
        /// </summary>
        private DataFrame TabularToDataFrame(ITabularDataset tabular)
        {
            var df = new DataFrame();

            // 通过查询获取数据
            var results = tabular.Query().ToDictionaries();
            if (!results.Any())
                return df;

            // 从第一行获取列名
            var firstRow = results.First();
            var columns = firstRow.Keys.ToList();

            // 为每列创建 DataFrameColumn
            foreach (var col in columns)
            {
                var values = results.Select(r => r.TryGetValue(col, out var v) ? v : null).ToArray();
                
                // 尝试确定列类型
                var nonNullValues = values.Where(v => v != null).ToList();
                if (!nonNullValues.Any())
                {
                    df.Columns.Add(new StringDataFrameColumn(col, values.Select(v => v?.ToString()).ToArray()));
                }
                else if (nonNullValues.All(v => v is double || v is int || v is float || v is long))
                {
                    var doubleValues = values.Select(v => v != null ? Convert.ToDouble(v) : double.NaN).ToArray();
                    df.Columns.Add(new DoubleDataFrameColumn(col, doubleValues));
                }
                else
                {
                    df.Columns.Add(new StringDataFrameColumn(col, values.Select(v => v?.ToString()).ToArray()));
                }
            }

            return df;
        }

        /// <summary>
        /// 注册弱引用DataFrame（用于大数据集内存管理）
        /// </summary>
        public void RegisterWeakDataFrame(string name, DataFrame df)
        {
            _weakDataFrames[name] = new WeakReference<DataFrame>(df);
        }

        /// <summary>
        /// 尝试获取弱引用DataFrame
        /// </summary>
        public bool TryGetWeakDataFrame(string name, out DataFrame df)
        {
            if (_weakDataFrames.TryGetValue(name, out var weakRef) && weakRef.TryGetTarget(out df))
                return true;

            df = null;
            return false;
        }

        /// <summary>
        /// 清理无效的弱引用
        /// </summary>
        public void CleanupWeakReferences()
        {
            var toRemove = _weakDataFrames
                .Where(kv => !kv.Value.TryGetTarget(out _))
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                _weakDataFrames.Remove(key);
            }
        }

        /// <summary>
        /// 获取DataFrame统计信息
        /// </summary>
        public Dictionary<string, object> GetDataFrameStatistics(string name)
        {
            var df = GetDataFrame(name);
            var adapter = new DataFrameAdapter(name, df);
            return adapter.GetStatistics();
        }

        #endregion
    }
}
