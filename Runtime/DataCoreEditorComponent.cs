using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AroAro.DataCore
{
    /// <summary>
    /// DataCore 编辑器组件，挂到 GameObject 上用于配置和预览
    /// 使用 LiteDB 作为底层存储，自动持久化所有数据
    /// </summary>
    public class DataCoreEditorComponent : MonoBehaviour
    {
        /// <summary>
        /// Shared instance accessible from anywhere in the scene
        /// </summary>
        public static DataCoreEditorComponent Instance { get; private set; }

        [Header("DataCore Configuration")]
        [SerializeField] private string databasePath = "DataCore/datacore.db";
        [SerializeField] private bool loadSampleDatasets = true;

        [Header("Runtime Store")]
        private DataCoreStore _store;

        private void Awake()
        {
            Debug.Log("DataCoreEditorComponent Awake started.");
            
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Multiple DataCoreEditorComponent instances detected. Destroying duplicate on '{gameObject.name}'.");
                Destroy(this);
                return;
            }

            Instance = this;
            
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
            Debug.Log($"DataCoreEditorComponent Awake finished. Datasets: {string.Join(", ", _store.Names)}");
        }

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

            Debug.Log($"Initializing DataCoreStore at: {resolvedPath}");
            
            try
            {
                _store = new DataCoreStore(resolvedPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize DataCoreStore: {ex.Message}");
                throw;
            }
        }

        private void InitializeSampleDatasets()
        {
            // Auto-load CSVs from Resources/AroAro/DataCore/AutoLoad
            var csvAssets = Resources.LoadAll<TextAsset>("AroAro/DataCore/AutoLoad");
            if (csvAssets == null || csvAssets.Length == 0)
            {
                Debug.Log("No auto-load datasets found in Resources/AroAro/DataCore/AutoLoad");
                return;
            }

            // Verify store accessibility before starting
            try 
            {
                var check = _store.Names; 
            } 
            catch (Exception ex) 
            { 
                Debug.LogError($"DataStore is not accessible ({ex.Message}). Skipping dataset auto-load."); 
                return; 
            }

            foreach (var csvAsset in csvAssets)
            {
                string datasetName = csvAsset.name;

                try 
                {
                    // Check if dataset exists in DB
                    if (_store.HasDataset(datasetName))
                    {
                        Debug.Log($"Dataset '{datasetName}' already exists in database, skipping import.");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error checking existence of '{datasetName}': {ex.Message}. Skipping.");
                    continue;
                }

                Debug.Log($"Dataset '{datasetName}' not in DB. Importing from CSV Resource...");
                try
                {
                    var tabular = Import.CsvImporter.ImportFromText(_store, csvAsset.text, datasetName);
                    if (tabular != null)
                    {
                        Debug.Log($"✅ Successfully imported dataset: {datasetName} ({tabular.RowCount} rows)");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error importing dataset {datasetName}: {ex.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
            
            // LiteDB 会自动保存，只需要 dispose
            _store?.Dispose();
        }

#if UNITY_EDITOR
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // 执行检查点确保数据写入
                _store?.Checkpoint();
                Debug.Log("DataCore checkpoint completed before exiting play mode.");
            }
        }
#endif

        #region Public API

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
            return Import.CsvImporter.ImportFromFile(_store.UnderlyingStore, csvFilePath, datasetName, hasHeader, delimiter);
        }

        /// <summary>
        /// 执行检查点（刷新数据到磁盘）
        /// </summary>
        public void Checkpoint()
        {
            _store?.Checkpoint();
            Debug.Log("DataCore checkpoint completed.");
        }

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void ClearAll()
        {
            _store?.ClearAll();
            Debug.Log("All datasets cleared.");
        }

        /// <summary>
        /// 彻底删除数据库文件（用于重置或修复损坏）
        /// </summary>
        public void DeleteDatabaseFile()
        {
            // 1. Dispose existing store
            if (_store != null)
            {
                _store.Dispose();
                _store = null;
            }

            // Force GC to release file handles (important on Windows)
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 2. Resolve path
            var resolvedPath = databasePath;
#if UNITY_2019_1_OR_NEWER
            if (!Path.IsPathRooted(databasePath))
            {
                resolvedPath = Path.Combine(Application.persistentDataPath, databasePath);
            }
#endif
            // 3. Delete files
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
        /// 获取会话管理器
        /// </summary>
        public Session.SessionManager GetSessionManager()
        {
            InitializeStore();
            return _store.SessionManager;
        }

        #endregion

        #region Editor Setup

        private void Reset()
        {
#if UNITY_EDITOR
            // 确保 AutoLoad 目录存在
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
