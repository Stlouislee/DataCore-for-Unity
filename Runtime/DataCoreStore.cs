using System;
using System.Collections.Generic;
using AroAro.DataCore.Events;
using AroAro.DataCore.Session;

namespace AroAro.DataCore
{
    /// <summary>
    /// 数据集元数据，用于延迟加载
    /// </summary>
    public class DatasetMetadata
    {
        public string Name { get; set; }
        public DataSetKind Kind { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsLoaded { get; set; }
    }

    public sealed class DataCoreStore
    {
        private readonly Dictionary<string, IDataSet> _dataSets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DatasetMetadata> _metadata = new(StringComparer.Ordinal);
        private SessionManager _sessionManager;

        public IReadOnlyCollection<string> Names => _metadata.Keys;

        /// <summary>
        /// 会话管理器
        /// </summary>
        public SessionManager SessionManager
        {
            get
            {
                if (_sessionManager == null)
                {
                    _sessionManager = new SessionManager(this);
                }
                return _sessionManager;
            }
        }

        public Tabular.TabularData CreateTabular(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
            var ds = new Tabular.TabularData(name);
            _dataSets[name] = ds;
            
            // 创建元数据
            _metadata[name] = new DatasetMetadata
            {
                Name = name,
                Kind = DataSetKind.Tabular,
                IsLoaded = true
            };
            
            // 触发创建事件
            DataCoreEventManager.RaiseDatasetCreated(ds);
            
            return ds;
        }

        public Graph.GraphData CreateGraph(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
            var ds = new Graph.GraphData(name);
            _dataSets[name] = ds;
            
            // 创建元数据
            _metadata[name] = new DatasetMetadata
            {
                Name = name,
                Kind = DataSetKind.Graph,
                IsLoaded = true
            };
            
            // 触发创建事件
            DataCoreEventManager.RaiseDatasetCreated(ds);
            
            return ds;
        }

        public bool TryGet(string name, out IDataSet dataSet)
        {
            if (_dataSets.TryGetValue(name, out dataSet))
                return true;
            
            // 检查是否有未加载的元数据
            if (_metadata.TryGetValue(name, out var metadata) && !metadata.IsLoaded)
            {
                // 延迟加载
                dataSet = LazyLoadDataset(metadata);
                return dataSet != null;
            }
            
            dataSet = null;
            return false;
        }

        /// <summary>
        /// 检查数据集是否存在（已加载或仅元数据）
        /// </summary>
        public bool HasDataset(string name)
        {
            return _dataSets.ContainsKey(name) || _metadata.ContainsKey(name);
        }

        public T Get<T>(string name) where T : class, IDataSet
        {
            if (!TryGet(name, out var ds))
                throw new KeyNotFoundException($"Dataset not found: {name}");
            if (ds is not T typed)
                throw new InvalidOperationException($"Dataset '{name}' is {ds.Kind}, not {typeof(T).Name}");
            return typed;
        }

        /// <summary>
        /// 清理资源，包括关闭所有会话
        /// </summary>
        public void Dispose()
        {
            _sessionManager?.CloseAllSessions();
            _dataSets.Clear();
            _metadata.Clear();
        }

        public bool Delete(string name)
        {
            // 获取数据集信息用于事件
            DataSetKind kind = DataSetKind.Tabular;
            if (_metadata.TryGetValue(name, out var metadata))
            {
                kind = metadata.Kind;
            }
            
            _metadata.Remove(name);
            var removed = _dataSets.Remove(name);
            
            // 触发删除事件
            if (removed)
            {
                DataCoreEventManager.RaiseDatasetDeleted(name, kind);
            }
            
            return removed;
        }

        public void Register(IDataSet dataSet)
        {
            if (dataSet == null) throw new ArgumentNullException(nameof(dataSet));
            _dataSets[dataSet.Name] = dataSet;
            
            // 更新元数据
            _metadata[dataSet.Name] = new DatasetMetadata
            {
                Name = dataSet.Name,
                Kind = dataSet.Kind,
                IsLoaded = true
            };
            
            // 触发注册事件（视为创建事件）
            DataCoreEventManager.RaiseDatasetCreated(dataSet);
        }

        /// <summary>
        /// 注册数据集元数据（延迟加载用）
        /// </summary>
        public void RegisterMetadata(string name, DataSetKind kind, string filePath = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
            
            _metadata[name] = new DatasetMetadata
            {
                Name = name,
                Kind = kind,
                FilePath = filePath,
                IsLoaded = false
            };
        }

#if DATACORE_APACHE_ARROW
        public void Save(string datasetName, string path, Persistence.IStorageBackend storage = null)
        {
            if (!_dataSets.TryGetValue(datasetName, out var ds))
                throw new KeyNotFoundException($"Dataset not found: {datasetName}");
            var backend = storage ?? Persistence.FileStorageBackend.Default;
            var bytes = Persistence.DataCorePersistence.Serialize(ds, path);
            backend.WriteAllBytes(path, bytes);
            
            // 触发保存事件
            DataCoreEventManager.RaiseDatasetSaved(ds, path);
        }

        public IDataSet Load(string path, Persistence.IStorageBackend storage = null, string registerAsName = null)
        {
            var backend = storage ?? Persistence.FileStorageBackend.Default;
            var bytes = backend.ReadAllBytes(path);
            var ds = Persistence.DataCorePersistence.Deserialize(bytes, path);

            if (!string.IsNullOrWhiteSpace(registerAsName) && !string.Equals(ds.Name, registerAsName, StringComparison.Ordinal))
                ds = ds.WithName(registerAsName);

            Register(ds);
            
            // 触发加载事件
            DataCoreEventManager.RaiseDatasetLoaded(ds);
            
            return ds;
        }
#endif // DATACORE_APACHE_ARROW

        /// <summary>
        /// 延迟加载数据集
        /// </summary>
        private IDataSet LazyLoadDataset(DatasetMetadata metadata)
        {
            if (metadata.IsLoaded)
                return _dataSets.TryGetValue(metadata.Name, out var ds) ? ds : null;

            try
            {
                if (string.IsNullOrEmpty(metadata.FilePath))
                {
                    // 如果没有文件路径，创建空数据集
                    IDataSet dataset = metadata.Kind switch
                    {
                        DataSetKind.Tabular => new Tabular.TabularData(metadata.Name),
                        DataSetKind.Graph => new Graph.GraphData(metadata.Name),
                        _ => throw new NotSupportedException($"Unknown dataset kind: {metadata.Kind}")
                    };
                    
                    Register(dataset);
                    metadata.IsLoaded = true;
                    return dataset;
                }
                else
                {
                    // 从文件加载
                    var dataset = Load(metadata.FilePath, registerAsName: metadata.Name);
                    metadata.IsLoaded = true;
                    return dataset;
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to lazy load dataset '{metadata.Name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取数据集元数据
        /// </summary>
        public DatasetMetadata GetMetadata(string name)
        {
            return _metadata.TryGetValue(name, out var metadata) ? metadata : null;
        }

        /// <summary>
        /// 获取所有数据集元数据
        /// </summary>
        public IEnumerable<DatasetMetadata> GetAllMetadata()
        {
            return _metadata.Values;
        }

        /// <summary>
        /// 预加载所有数据集（兼容旧行为）
        /// </summary>
        public void PreloadAll()
        {
            foreach (var metadata in _metadata.Values)
            {
                if (!metadata.IsLoaded)
                {
                    LazyLoadDataset(metadata);
                }
            }
        }
    }
}
