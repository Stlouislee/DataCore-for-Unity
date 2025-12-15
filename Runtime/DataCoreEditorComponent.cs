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
    /// Acts as a shared singleton instance that other scripts can access
    /// </summary>
    public class DataCoreEditorComponent : MonoBehaviour
    {
        /// <summary>
        /// Shared instance accessible from anywhere in the scene
        /// </summary>
        public static DataCoreEditorComponent Instance { get; private set; }

        [Header("DataCore Configuration")]
        [SerializeField] private string persistencePath = "DataCore/";
        [SerializeField] private bool autoSaveOnExit = true;
        [SerializeField] private bool clearOnEditMode = false; // 控制是否在进入 Edit 模式时清空数据（默认保留运行时数据）

        [Header("Runtime Store")]
        [SerializeField] private DataCoreStore store;

        [Header("Performance Settings")]
        [SerializeField] private bool lazyLoading = true; // 是否启用延迟加载

        private void Awake()
        {
            // Singleton pattern: ensure only one instance exists
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Multiple DataCoreEditorComponent instances detected. Destroying duplicate on '{gameObject.name}'.");
                Destroy(this);
                return;
            }

            Instance = this;

            if (store == null)
                store = new DataCoreStore();

#if UNITY_EDITOR
            // Register for play mode state changes
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        private void Start()
        {
            // 默认在启动时自动加载所有持久化的数据集
            LoadAllDatasets();
        }

        private void LoadAllDatasets()
        {
            if (string.IsNullOrEmpty(persistencePath))
                return;

            // 确保store存在
            if (store == null)
                store = new DataCoreStore();

            try
            {
                var backend = new Persistence.FileStorageBackend();
                var resolvedPath = persistencePath;
                
#if UNITY_2019_1_OR_NEWER
                if (!System.IO.Path.IsPathRooted(persistencePath))
                    resolvedPath = System.IO.Path.Combine(Application.persistentDataPath, persistencePath);
#endif

                if (!System.IO.Directory.Exists(resolvedPath))
                {
                    Debug.Log($"Persistence path does not exist: {resolvedPath}. Creating directory...");
                    System.IO.Directory.CreateDirectory(resolvedPath);
                    return;
                }

                var arrowFiles = System.IO.Directory.GetFiles(resolvedPath, "*.arrow");
                var graphFiles = System.IO.Directory.GetFiles(resolvedPath, "*.dcgraph");

                int metadataCount = 0;

                // 只注册元数据，不实际加载数据
                foreach (var file in arrowFiles)
                {
                    try
                    {
                        var name = System.IO.Path.GetFileNameWithoutExtension(file);
                        var fileInfo = new System.IO.FileInfo(file);
                        
                        store.RegisterMetadata(name, DataSetKind.Tabular, file);
                        Debug.Log($"Registered tabular dataset metadata '{name}' from {file}");
                        metadataCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to register metadata for {file}: {ex.Message}");
                    }
                }

                foreach (var file in graphFiles)
                {
                    try
                    {
                        var name = System.IO.Path.GetFileNameWithoutExtension(file);
                        var fileInfo = new System.IO.FileInfo(file);
                        
                        store.RegisterMetadata(name, DataSetKind.Graph, file);
                        Debug.Log($"Registered graph dataset metadata '{name}' from {file}");
                        metadataCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to register metadata for {file}: {ex.Message}");
                    }
                }

                if (metadataCount > 0)
                {
                    Debug.Log($"Successfully registered {metadataCount} dataset metadata from {resolvedPath}");
                }
                else
                {
                    Debug.Log($"No datasets found in {resolvedPath}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load datasets: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            // Clear singleton reference when destroyed
            if (Instance == this)
            {
                Instance = null;
            }

#if UNITY_EDITOR
            // Unregister from play mode state changes
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif

            if (autoSaveOnExit && store != null)
            {
                // 检查是否有需要保存的数据集
                var namesCopy = new List<string>(store.Names);
                if (namesCopy.Count > 0)
                {
                    SaveAllDatasets();
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Handle Unity Editor play mode transitions
        /// </summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Save datasets when exiting play mode
                if (autoSaveOnExit && store != null)
                {
                    Debug.Log("Saving datasets before exiting play mode...");
                    SaveAllDatasets();
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                // Clear the store when returning to edit mode (可选)
                if (store != null && clearOnEditMode)
                {
                    Debug.Log("Clearing runtime datasets after exiting play mode...");
                    // Create a copy of the names list to avoid modification during iteration
                    var namesCopy = new List<string>(store.Names);
                    foreach (var name in namesCopy)
                    {
                        store.Delete(name);
                    }
                }
            }
        }
#endif

        private void SaveAllDatasets()
        {
            if (store == null)
                return;

            // 创建名称副本以避免并发修改问题
            var namesCopy = new List<string>(store.Names);
            
            if (namesCopy.Count == 0)
                return;

            foreach (var name in namesCopy)
            {
                if (!store.TryGet(name, out var dataset))
                    continue;

                try
                {
                    var extension = dataset.Kind == DataSetKind.Tabular ? ".arrow" : ".dcgraph";
                    var fileName = $"{name}{extension}";
                    var filePath = string.IsNullOrEmpty(persistencePath) ? fileName : $"{persistencePath}/{fileName}";
                    
                    store.Save(name, filePath);
                    Debug.Log($"Saved dataset '{name}' to {filePath}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to save dataset '{name}': {ex.Message}");
                }
            }
        }

        public DataCoreStore GetStore()
        {
            if (store == null)
            {
                store = new DataCoreStore();
                
                // 在非Play模式下自动加载持久化数据
                if (!Application.isPlaying)
                {
                    if (lazyLoading)
                    {
                        LoadAllDatasets(); // 只加载元数据
                    }
                    else
                    {
                        LoadAllDatasets();
                        store.PreloadAll(); // 预加载所有数据
                    }
                }
            }
            return store;
        }

        public string GetPersistencePath() => persistencePath;

        public void SetPersistencePath(string path) => persistencePath = path;

        public IEnumerable<string> GetDatasetNames() => store?.Names ?? new List<string>();

        public IDataSet GetDataset(string name)
        {
            if (store?.TryGet(name, out var ds) == true)
                return ds;
            return null;
        }

        public void CreateTabularDataset(string name)
        {
            store?.CreateTabular(name);
        }

        public void CreateGraphDataset(string name)
        {
            store?.CreateGraph(name);
        }

        public bool DeleteDataset(string name)
        {
            return store?.Delete(name) == true;
        }

        /// <summary>
        /// 从 CSV 文件导入数据到新的 Tabular 数据集
        /// </summary>
        /// <param name="csvFilePath">CSV 文件路径</param>
        /// <param name="datasetName">新数据集的名称</param>
        /// <param name="hasHeader">第一行是否为列名</param>
        /// <param name="delimiter">分隔符，默认为逗号</param>
        /// <param name="useFastMode">是否使用快速模式（适用于简单CSV文件）</param>
        public void ImportCsvToTabular(string csvFilePath, string datasetName, bool hasHeader = true, char delimiter = ',', bool useFastMode = false)
        {
            if (string.IsNullOrEmpty(csvFilePath))
                throw new ArgumentException("CSV file path cannot be null or empty", nameof(csvFilePath));

            if (string.IsNullOrEmpty(datasetName))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(datasetName));

            if (!System.IO.File.Exists(csvFilePath))
                throw new FileNotFoundException($"CSV file not found: {csvFilePath}");

            try
            {
                // 创建新的 Tabular 数据集
                var tabular = store?.CreateTabular(datasetName);
                if (tabular == null)
                    throw new InvalidOperationException("Failed to create tabular dataset");

                // 导入 CSV
                tabular.ImportFromCsvFile(csvFilePath, hasHeader, delimiter, useFastMode);
                
                Debug.Log($"Successfully imported CSV '{csvFilePath}' to dataset '{datasetName}'");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to import CSV: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Manually save all datasets to the persistence path
        /// </summary>
        public void SaveAll()
        {
            SaveAllDatasets();
        }

        /// <summary>
        /// Manually load all datasets from the persistence path
        /// </summary>
        public void LoadAll()
        {
            if (store != null)
            {
                store.PreloadAll();
                Debug.Log("All datasets preloaded");
            }
        }

        /// <summary>
        /// 只加载元数据（延迟加载模式）
        /// </summary>
        public void LoadMetadataOnly()
        {
            LoadAllDatasets();
        }

        /// <summary>
        /// 性能测试：比较新旧CSV导入方法的性能
        /// </summary>
        public void PerformanceTestCsvImport(string csvFilePath, string datasetName)
        {
            if (!System.IO.File.Exists(csvFilePath))
            {
                Debug.LogError($"CSV file not found: {csvFilePath}");
                return;
            }

            var stopwatch = new System.Diagnostics.Stopwatch();
            
            // 测试标准模式
            stopwatch.Start();
            try
            {
                ImportCsvToTabular(csvFilePath, datasetName + "_standard", true, ',', false);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Standard mode failed: {ex.Message}");
            }
            stopwatch.Stop();
            var standardTime = stopwatch.ElapsedMilliseconds;

            // 测试快速模式
            stopwatch.Restart();
            try
            {
                ImportCsvToTabular(csvFilePath, datasetName + "_fast", true, ',', true);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Fast mode failed: {ex.Message}");
            }
            stopwatch.Stop();
            var fastTime = stopwatch.ElapsedMilliseconds;

            Debug.Log($"CSV Import Performance Test:\n" +
                     $"File: {csvFilePath}\n" +
                     $"Standard Mode: {standardTime}ms\n" +
                     $"Fast Mode: {fastTime}ms\n" +
                     $"Speedup: {((double)standardTime / fastTime):F2}x faster");
        }

        /// <summary>
        /// Save a specific dataset
        /// </summary>
        public void SaveDataset(string name)
        {
            if (store == null || !store.TryGet(name, out var dataset))
            {
                Debug.LogError($"Dataset '{name}' not found");
                return;
            }

            try
            {
                var extension = dataset.Kind == DataSetKind.Tabular ? ".arrow" : ".dcgraph";
                var fileName = $"{name}{extension}";
                var filePath = string.IsNullOrEmpty(persistencePath) ? fileName : $"{persistencePath}/{fileName}";
                
                store.Save(name, filePath);
                Debug.Log($"Saved dataset '{name}' to {filePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save dataset '{name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Persist a dataset so it survives play mode transitions
        /// </summary>
        public void PersistDataset(string name)
        {
            SaveDataset(name);
        }

        /// <summary>
        /// Load a persisted dataset after play mode transition
        /// </summary>
        public void LoadPersistedDataset(string name)
        {
            try
            {
                var tabularPath = $"{persistencePath}/{name}.arrow";
                var graphPath = $"{persistencePath}/{name}.dcgraph";

                if (System.IO.File.Exists(tabularPath))
                {
                    store.Load(tabularPath, registerAsName: name);
                    Debug.Log($"Loaded persisted dataset '{name}' from {tabularPath}");
                }
                else if (System.IO.File.Exists(graphPath))
                {
                    store.Load(graphPath, registerAsName: name);
                    Debug.Log($"Loaded persisted dataset '{name}' from {graphPath}");
                }
                else
                {
                    Debug.LogWarning($"Persisted dataset '{name}' not found");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load persisted dataset '{name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a dataset has been persisted
        /// </summary>
        public bool IsDatasetPersisted(string name)
        {
            var tabularPath = $"{persistencePath}/{name}.arrow";
            var graphPath = $"{persistencePath}/{name}.dcgraph";
            return System.IO.File.Exists(tabularPath) || System.IO.File.Exists(graphPath);
        }
    }
}