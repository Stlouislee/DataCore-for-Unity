using System;
using System.Collections.Generic;
using System.Linq;
using AroAro.DataCore.Events;
using AroAro.DataCore.Tabular;
using AroAro.DataCore.Graph;

namespace AroAro.DataCore.Session
{
    /// <summary>
    /// 会话实现类，管理临时数据集和操作
    /// </summary>
    public class Session : ISession
    {
        private readonly Dictionary<string, IDataSet> _datasets = new(StringComparer.Ordinal);
        private readonly DataCoreStore _store;
        private bool _disposed = false;

        public string Id { get; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; }
        public DateTime LastActivityAt { get; private set; }

        public int DatasetCount => _datasets.Count;
        public IReadOnlyCollection<string> DatasetNames => _datasets.Keys;

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
                // 更新现有数据集
                // 注意：这里假设数据集支持更新操作
                // 实际实现可能需要根据具体的数据集类型来处理
                throw new InvalidOperationException($"Dataset already exists in global store: {target}");
            }

            // 将数据集添加到全局存储
            // 这里需要根据数据集类型进行特殊处理
            switch (dataset.Kind)
            {
                case DataSetKind.Tabular:
                    {
                        var tabular = dataset as TabularData;
                        if (tabular != null)
                        {
                            // 由于CreateTabular会创建新的空数据集，我们需要复制数据
                            var newTabular = _store.CreateTabular(target);
                            
                            // 复制所有列
                            foreach (var columnName in tabular.ColumnNames)
                            {
                                // 这里需要根据列类型进行复制
                                // 由于TabularData的内部结构，我们需要通过反射或其他方式复制
                                // 暂时抛出异常，表示需要实现
                                throw new NotImplementedException("Persisting tabular data requires implementation");
                            }
                        }
                    }
                    break;
                case DataSetKind.Graph:
                    {
                        var graph = dataset as GraphData;
                        if (graph != null)
                        {
                            var newGraph = _store.CreateGraph(target);
                            // 复制图数据
                            throw new NotImplementedException("Persisting graph data requires implementation");
                        }
                    }
                    break;
            }

            Touch();
            return true;
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
                _disposed = true;
            }
        }
    }
}