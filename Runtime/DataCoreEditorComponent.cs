using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AroAro.DataCore
{
    /// <summary>
    /// DataCore 编辑器组件，挂到 GameObject 上用于配置和预览
    /// 使用 LiteDB 作为底层存储，自动持久化所有数据
    /// 支持多实例模式：每个实例独立管理自己的 DataCoreStore
    /// </summary>
    [DisallowMultipleComponent]
    public class DataCoreEditorComponent : MonoBehaviour
    {
        // ────────────────────────────────────────────────────────────
        // 多实例注册表
        // ────────────────────────────────────────────────────────────

        private static readonly List<DataCoreEditorComponent> _instances = new List<DataCoreEditorComponent>();

        /// <summary>
        /// 所有活跃的 DataCoreEditorComponent 实例
        /// </summary>
        public static IReadOnlyList<DataCoreEditorComponent> AllInstances => _instances;

        /// <summary>
        /// 向后兼容：返回第一个注册的实例（等效于旧的单例）
        /// 新代码建议使用 FindByName() 或直接持有引用
        /// </summary>
        public static DataCoreEditorComponent Instance => _instances.Count > 0 ? _instances[0] : null;

        /// <summary>
        /// 按名称查找实例
        /// </summary>
        public static DataCoreEditorComponent FindByName(string name)
        {
            return _instances.FirstOrDefault(i => i.InstanceName == name);
        }

        /// <summary>
        /// 按数据库路径查找实例
        /// </summary>
        public static DataCoreEditorComponent FindByPath(string path)
        {
            return _instances.FirstOrDefault(i => i.databasePath == path);
        }

        // ────────────────────────────────────────────────────────────
        // 实例配置
        // ────────────────────────────────────────────────────────────

        [Header("DataCore Configuration")]
        [Tooltip("唯一标识名称，用于 FindByName 查找")]
        [SerializeField] private string instanceName = "Default";
        [SerializeField] private string databasePath = "DataCore/datacore.db";
        [SerializeField] private bool loadSampleDatasets = true;

        [Header("Runtime Store")]
        private DataCoreStore _store;

        /// <summary>
        /// 实例名称，用于在多实例场景中标识和查找
        /// </summary>
        public string InstanceName
        {
            get => instanceName;
            set => instanceName = value;
        }

        // ────────────────────────────────────────────────────────────
        // 生命周期
        // ────────────────────────────────────────────────────────────

        private void Awake()
        {
            Debug.Log($"DataCoreEditorComponent '{instanceName}' Awake started on '{gameObject.name}'.");

            // 检测同路径冲突（警告，不销毁）
            var conflict = _instances.FirstOrDefault(i => i != this && i.databasePath == databasePath);
            if (conflict != null)
            {
                Debug.LogWarning(
                    $"DataCoreEditorComponent '{instanceName}' on '{gameObject.name}' uses the same database path " +
                    $"'{databasePath}' as '{conflict.instanceName}' on '{conflict.gameObject.name}'. " +
                    $"This may cause LiteDB file lock conflicts. Consider using different database paths.");
            }

            // 注册到实例列表
            _instances.Add(this);

            // 初始化存储
            InitializeStore();

            // 加载示例数据集
            if (loadSampleDatasets)
            {
                InitializeSampleDatasets();
            }

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
            Debug.Log($"DataCoreEditorComponent '{instanceName}' Awake finished. Datasets: {string.Join(", ", _store.Names)}");
        }

        private void OnDestroy()
        {
            // 从注册表移除
            _instances.Remove(this);

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif

            // LiteDB 会自动保存，只需要 dispose
            _store?.Dispose();
        }

        // ────────────────────────────────────────────────────────────
        // 存储初始化
        // ────────────────────────────────────────────────────────────

        private void InitializeStore()
        {
            if (_store != null) return;

            var resolvedPath = databasePath;

#if UNITY_2019_1_OR_NEWER
            if (!Path.IsPathRooted(databasePath))
            {
                resolvedPath = Path.Combine(Application.persistentDataPath, databasePath);
            }
#endif

            // 确保目录存在
            var directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Debug.Log($"Initializing DataCoreStore '{instanceName}' at: {resolvedPath}");

            try
            {
                _store = new DataCoreStore(resolvedPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize DataCoreStore '{instanceName}': {ex.Message}");
                throw;
            }
        }

        private void InitializeSampleDatasets()
        {
            // Auto-load CSVs from Resources/AroAro/DataCore/AutoLoad
            const string resourceFolder = "AroAro/DataCore/AutoLoad";
            var csvAssets = Resources.LoadAll<TextAsset>(resourceFolder);
            if (csvAssets == null || csvAssets.Length == 0)
            {
                Debug.Log($"No auto-load datasets found in Resources/{resourceFolder}");
                return;
            }

            foreach (var csvAsset in csvAssets)
            {
                if (csvAsset == null) continue;

                var datasetName = csvAsset.name;
                if (string.IsNullOrWhiteSpace(datasetName))
                    continue;

                // 1) DB 中存在则跳过
                if (_store.HasDataset(datasetName))
                {
                    Debug.Log($"Dataset '{datasetName}' already exists, skipping auto-import.");
                    continue;
                }

                // 2) DB 中不存在则从 CSV 导入
                Debug.Log($"Auto-importing dataset '{datasetName}' from Resources/{resourceFolder}...");
                try
                {
                    var tabular = Import.CsvImporter.ImportFromText(_store, csvAsset.text, datasetName);
                    if (tabular != null)
                    {
                        Debug.Log($"✅ Auto-imported dataset: {datasetName} ({tabular.RowCount} rows)");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error auto-importing dataset '{datasetName}': {ex.Message}");
                }
            }
        }

#if UNITY_EDITOR
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _store?.Checkpoint();
                Debug.Log($"DataCore '{instanceName}' checkpoint completed before exiting play mode.");
            }
        }
#endif

        // ────────────────────────────────────────────────────────────
        // Public API
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 获取数据存储实例
        /// </summary>
        public DataCoreStore GetStore()
        {
            InitializeStore();
            return _store;
        }

        /// <summary>
        /// 获取数据库路径
        /// </summary>
        public string GetDatabasePath() => databasePath;

        /// <summary>
        /// 设置数据库路径（需要重新初始化）
        /// </summary>
        public void SetDatabasePath(string path)
        {
            if (databasePath != path)
            {
                databasePath = path;
                _store?.Dispose();
                _store = null;
                InitializeStore();
            }
        }

        /// <summary>
        /// 获取所有数据集名称
        /// </summary>
        public IEnumerable<string> GetDatasetNames() => _store?.Names ?? Array.Empty<string>();

        /// <summary>
        /// 获取数据集
        /// </summary>
        public IDataSet GetDataset(string name)
        {
            if (_store?.TryGet(name, out var ds) == true)
                return ds;
            return null;
        }

        /// <summary>
        /// 创建表格数据集
        /// </summary>
        public ITabularDataset CreateTabularDataset(string name)
        {
            return _store?.CreateTabular(name);
        }

        /// <summary>
        /// 创建图数据集
        /// </summary>
        public IGraphDataset CreateGraphDataset(string name)
        {
            return _store?.CreateGraph(name);
        }

        /// <summary>
        /// 删除数据集
        /// </summary>
        public bool DeleteDataset(string name)
        {
            return _store?.Delete(name) == true;
        }

        /// <summary>
        /// 从 CSV 文件导入数据
        /// </summary>
        public ITabularDataset ImportCsvToTabular(string csvFilePath, string datasetName, bool hasHeader = true, char delimiter = ',')
        {
            if (string.IsNullOrEmpty(csvFilePath))
                throw new ArgumentException("CSV file path cannot be null or empty", nameof(csvFilePath));

            if (string.IsNullOrEmpty(datasetName))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(datasetName));

            if (!File.Exists(csvFilePath))
                throw new FileNotFoundException($"CSV file not found: {csvFilePath}");

            InitializeStore();
            return Import.CsvImporter.ImportFromFile(_store, csvFilePath, datasetName, hasHeader, delimiter);
        }

        /// <summary>
        /// 从 GraphML 文件导入数据
        /// </summary>
        public IGraphDataset ImportGraphMLToGraph(string graphmlFilePath, string datasetName)
        {
            if (string.IsNullOrEmpty(graphmlFilePath))
                throw new ArgumentException("GraphML file path cannot be null or empty", nameof(graphmlFilePath));

            if (string.IsNullOrEmpty(datasetName))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(datasetName));

            if (!File.Exists(graphmlFilePath))
                throw new FileNotFoundException($"GraphML file not found: {graphmlFilePath}");

            InitializeStore();
            return Import.GraphMLImporter.ImportFromFile(_store, graphmlFilePath, datasetName);
        }

        /// <summary>
        /// 执行检查点（刷新数据到磁盘）
        /// </summary>
        public void Checkpoint()
        {
            _store?.Checkpoint();
            Debug.Log($"DataCore '{instanceName}' checkpoint completed.");
        }

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void ClearAll()
        {
            _store?.ClearAll();
            Debug.Log($"All datasets cleared from '{instanceName}'.");
        }

        /// <summary>
        /// 彻底删除数据库文件（用于重置或修复损坏）
        /// </summary>
        public void DeleteDatabaseFile()
        {
            if (_store != null)
            {
                _store.Dispose();
                _store = null;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();

            var resolvedPath = databasePath;
#if UNITY_2019_1_OR_NEWER
            if (!Path.IsPathRooted(databasePath))
            {
                resolvedPath = Path.Combine(Application.persistentDataPath, databasePath);
            }
#endif
            try
            {
                bool deleted = false;
                if (File.Exists(resolvedPath))
                {
                    File.Delete(resolvedPath);
                    Debug.Log($"Deleted database file: {resolvedPath}");
                    deleted = true;
                }

                var logPath = resolvedPath + "-log";
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                    Debug.Log($"Deleted log file: {logPath}");
                    deleted = true;
                }

                if (!deleted)
                {
                    Debug.Log($"No database file found at: {resolvedPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete database file: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出数据库文件到指定位置
        /// </summary>
        public bool ExportDatabaseFile(string destinationPath)
        {
            _store?.Checkpoint();

            var sourcePath = databasePath;
#if UNITY_2019_1_OR_NEWER
            if (!Path.IsPathRooted(databasePath))
            {
                sourcePath = Path.Combine(Application.persistentDataPath, databasePath);
            }
#endif

            if (!File.Exists(sourcePath))
            {
                Debug.LogError($"Database file not found at: {sourcePath}");
                return false;
            }

            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            try
            {
                File.Copy(sourcePath, destinationPath, true);
                Debug.Log($"Database exported successfully to: {destinationPath}");

                var sourceLogPath = sourcePath + "-log";
                if (File.Exists(sourceLogPath))
                {
                    var destLogPath = destinationPath + "-log";
                    File.Copy(sourceLogPath, destLogPath, true);
                    Debug.Log($"Database log file also exported.");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to export database: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取会话管理器
        /// </summary>
        public Session.SessionManager GetSessionManager()
        {
            InitializeStore();
            return _store.SessionManager;
        }

        #region Editor Setup

        private void Reset()
        {
#if UNITY_EDITOR
            string resourcesPath = Path.Combine(Application.dataPath, "Resources");
            string autoLoadPath = Path.Combine(resourcesPath, "AroAro", "DataCore", "AutoLoad");

            if (!Directory.Exists(autoLoadPath))
            {
                try
                {
                    Directory.CreateDirectory(autoLoadPath);
                    Debug.Log($"Created AutoLoad directory at: {autoLoadPath}");
                    AssetDatabase.Refresh();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to create AutoLoad directory: {ex.Message}");
                }
            }
#endif
        }

        [ContextMenu("Setup Sample Datasets")]
        public void SetupSampleDatasets()
        {
#if UNITY_EDITOR
            Reset();
#else
            Debug.LogWarning("Setup Sample Datasets is only available in the Unity Editor.");
#endif
        }

        #endregion
    }
}
