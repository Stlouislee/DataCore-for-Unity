using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

namespace AroAro.DataCore.Editor
{
    [CustomEditor(typeof(DataCoreEditorComponent))]
    public class DataCoreEditor : UnityEditor.Editor
    {
        private DataCoreEditorComponent component;
        private Vector2 scrollPosition;
        private bool showDatasets = true;
        private Dictionary<string, bool> datasetFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, bool> datasetPreviewFoldouts = new Dictionary<string, bool>();
        
        // CSV 导入相关字段
        private string csvFilePath = "";
        private string csvDatasetName = "ImportedCSV";
        private bool csvHasHeader = true;
        private char csvDelimiter = ',';
        
        // 预览相关字段
        private Vector2 previewScrollPosition;
        private int previewMaxRows = 10; // 预览最大行数
        private int previewMaxNodes = 20; // 预览最大节点数

        private void OnEnable()
        {
            component = (DataCoreEditorComponent)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw default properties (includes persistencePath, autoSaveOnExit, clearOnEditMode, lazyLoading)
            DrawDefaultInspector();

            EditorGUILayout.Space();

            // Datasets section
            showDatasets = EditorGUILayout.Foldout(showDatasets, "Datasets", true);
            if (showDatasets)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                
                var store = component.GetStore();
                var names = store.Names.ToList();

                if (names.Count == 0)
                {
                    EditorGUILayout.LabelField("No datasets", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    foreach (var name in names)
                    {
                        if (!datasetFoldouts.ContainsKey(name))
                            datasetFoldouts[name] = false;

                        var metadata = store.GetMetadata(name);
                        var isLoaded = metadata?.IsLoaded ?? true;
                        
                        // 显示加载状态
                        var displayName = name + (isLoaded ? "" : " (Not Loaded)");
                        datasetFoldouts[name] = EditorGUILayout.Foldout(datasetFoldouts[name], displayName, true);
                        if (datasetFoldouts[name])
                        {
                            EditorGUI.indentLevel++;
                            
                            if (isLoaded)
                            {
                                var ds = store.Get<IDataSet>(name);
                                if (ds != null)
                                {
                                    EditorGUILayout.LabelField("Type", ds.Kind.ToString());
                                    
                                    switch (ds.Kind)
                                    {
                                        case DataSetKind.Tabular:
                                            var tabular = store.Get<Tabular.TabularData>(name);
                                            if (tabular != null)
                                            {
                                                EditorGUILayout.LabelField("Rows", tabular.RowCount.ToString());
                                                EditorGUILayout.LabelField("Columns", string.Join(", ", tabular.ColumnNames));
                                                
                                                // 表格数据预览
                                                ShowTabularPreview(tabular, name);
                                            }
                                            break;
                                        case DataSetKind.Graph:
                                            var graph = store.Get<Graph.GraphData>(name);
                                            if (graph != null)
                                            {
                                                EditorGUILayout.LabelField("Nodes", graph.NodeCount.ToString());
                                                EditorGUILayout.LabelField("Edges", graph.EdgeCount.ToString());
                                                
                                                // 图数据预览
                                                ShowGraphPreview(graph, name);
                                            }
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                EditorGUILayout.LabelField("Status", "Not Loaded", EditorStyles.miniLabel);
                                EditorGUILayout.LabelField("Type", metadata?.Kind.ToString() ?? "Unknown");
                                EditorGUILayout.LabelField("File Path", metadata?.FilePath ?? "N/A");
                                
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("Load Now", GUILayout.Width(80)))
                                {
                                    // 手动加载这个数据集
                                    store.Get<IDataSet>(name);
                                    EditorUtility.SetDirty(component);
                                }
                                EditorGUILayout.EndHorizontal();
                            }

                            if (isLoaded)
                            {
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                                {
                                    if (EditorUtility.DisplayDialog("Delete Dataset", $"Are you sure you want to delete '{name}'?", "Delete", "Cancel"))
                                    {
                                        store?.Delete(name);
                                        datasetFoldouts.Remove(name);
                                        EditorUtility.SetDirty(component);
                                    }
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            else
                            {
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("Delete Metadata", GUILayout.Width(100)))
                                {
                                    if (EditorUtility.DisplayDialog("Delete Dataset Metadata", $"Are you sure you want to delete metadata for '{name}'?", "Delete", "Cancel"))
                                    {
                                        store?.Delete(name);
                                        datasetFoldouts.Remove(name);
                                        EditorUtility.SetDirty(component);
                                    }
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.Space();
                    }
                }

                EditorGUILayout.EndScrollView();

                // Create new dataset buttons
                EditorGUILayout.LabelField("Create New Dataset", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                CreateDataset("Tabular", "NewTabular");
                EditorGUILayout.Space();
                CreateDataset("Graph", "NewGraph");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }

            EditorGUILayout.Space();

            // Actions
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save All"))
            {
                SaveAllDatasets();
            }
            if (GUILayout.Button("Load All"))
            {
                LoadAllDatasets();
            }
            if (GUILayout.Button("Run Tests"))
            {
                RunSelfTest();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Preview Settings
            EditorGUILayout.LabelField("Preview Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Max Preview Rows", GUILayout.Width(120));
            previewMaxRows = EditorGUILayout.IntSlider(previewMaxRows, 5, 50);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Max Preview Nodes", GUILayout.Width(120));
            previewMaxNodes = EditorGUILayout.IntSlider(previewMaxNodes, 10, 100);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // CSV Import Section
            EditorGUILayout.LabelField("CSV Import", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            // CSV File selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("CSV File", GUILayout.Width(80));
            csvFilePath = EditorGUILayout.TextField(csvFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var path = EditorUtility.OpenFilePanel("Select CSV File", "", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    csvFilePath = path;
                    // 自动设置数据集名称
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                    csvDatasetName = fileName;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Dataset configuration
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Dataset Name", GUILayout.Width(80));
            csvDatasetName = EditorGUILayout.TextField(csvDatasetName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Has Header", GUILayout.Width(80));
            csvHasHeader = EditorGUILayout.Toggle(csvHasHeader);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Delimiter", GUILayout.Width(80));
            csvDelimiter = EditorGUILayout.TextField(csvDelimiter.ToString(), GUILayout.Width(30))[0];
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Import button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Import CSV", GUILayout.Width(120), GUILayout.Height(25)))
            {
                ImportCsv();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        private void SaveAllDatasets()
        {
            var store = component.GetStore();
            var path = component.GetPersistencePath();
            
            foreach (var name in store.Names)
            {
                var ds = store.Get<IDataSet>(name);
                var filePath = $"{path}/{name}.{(ds.Kind == DataSetKind.Tabular ? "arrow" : "dcgraph")}";
                store.Save(name, filePath);
            }
            
            Debug.Log($"Saved {store.Names.Count} datasets to {path}");
        }

        private void LoadAllDatasets()
        {
            try
            {
                component.LoadAll();
                EditorUtility.DisplayDialog("Load All", "All datasets have been loaded", "OK");
                EditorUtility.SetDirty(component);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Load All Error", $"Failed to load datasets: {ex.Message}", "OK");
            }
        }

        private void RunSelfTest()
        {
            var go = new GameObject("DataCore Test Runner");
            var test = go.AddComponent<DataCoreSelfTest>();
            test.RunTests();
            DestroyImmediate(go);
        }

        private void ImportCsv()
        {
            if (string.IsNullOrEmpty(csvFilePath))
            {
                EditorUtility.DisplayDialog("CSV Import Error", "Please select a CSV file first.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(csvDatasetName))
            {
                EditorUtility.DisplayDialog("CSV Import Error", "Please enter a dataset name.", "OK");
                return;
            }

            try
            {
                // 检测文件大小决定是否使用快速模式
                var fileInfo = new System.IO.FileInfo(csvFilePath);
                bool useFastMode = fileInfo.Length > 1024 * 1024; // 大于1MB的文件使用快速模式
                
                component.ImportCsvToTabular(csvFilePath, csvDatasetName, csvHasHeader, csvDelimiter, useFastMode);
                EditorUtility.DisplayDialog("CSV Import Success", $"Successfully imported CSV to dataset '{csvDatasetName}' ({(useFastMode ? "Fast Mode" : "Standard Mode")})", "OK");
                EditorUtility.SetDirty(component);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("CSV Import Error", $"Failed to import CSV: {ex.Message}", "OK");
            }
        }

        private string newTabularName = "NewTabular";
        private string newGraphName = "NewGraph";

        private void CreateDataset(string type, string defaultName)
        {
            // 使用垂直布局改善排版
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            if (type == "Tabular")
            {
                EditorGUILayout.LabelField("Create Tabular Dataset", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Name", GUILayout.Width(40));
                newTabularName = EditorGUILayout.TextField(newTabularName);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();
                
                if (GUILayout.Button($"Create {newTabularName}", GUILayout.Height(25)))
                {
                    component.CreateTabularDataset(newTabularName);
                    EditorUtility.SetDirty(component);
                    newTabularName = "NewTabular"; // 重置为默认值
                }
            }
            else
            {
                EditorGUILayout.LabelField("Create Graph Dataset", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Name", GUILayout.Width(40));
                newGraphName = EditorGUILayout.TextField(newGraphName);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();
                
                if (GUILayout.Button($"Create {newGraphName}", GUILayout.Height(25)))
                {
                    component.CreateGraphDataset(newGraphName);
                    EditorUtility.SetDirty(component);
                    newGraphName = "NewGraph"; // 重置为默认值
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 显示表格数据预览
        /// </summary>
        private void ShowTabularPreview(AroAro.DataCore.Tabular.TabularData tabular, string datasetName)
        {
            EditorGUILayout.BeginHorizontal();
            if (!datasetPreviewFoldouts.ContainsKey(datasetName))
                datasetPreviewFoldouts[datasetName] = false;

            datasetPreviewFoldouts[datasetName] = EditorGUILayout.Foldout(datasetPreviewFoldouts[datasetName], "Data Preview", true);
            
            if (GUILayout.Button("Open Full Preview", GUILayout.Width(120)))
            {
                DataCorePreviewWindow.ShowWindow(target as DataCoreEditorComponent, datasetName);
            }
            EditorGUILayout.EndHorizontal();
            
            if (datasetPreviewFoldouts[datasetName])
            {
                EditorGUI.indentLevel++;
                
                // 显示列信息
                EditorGUILayout.LabelField("Columns:", EditorStyles.boldLabel);
                foreach (var columnName in tabular.ColumnNames)
                {
                    EditorGUILayout.LabelField($"  {columnName}");
                }
                
                EditorGUILayout.Space();
                
                // 显示数据预览
                EditorGUILayout.LabelField($"First {Math.Min(previewMaxRows, tabular.RowCount)} rows:", EditorStyles.boldLabel);
                previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition, GUILayout.Height(200));
                
                // 显示表头
                EditorGUILayout.BeginHorizontal();
                foreach (var columnName in tabular.ColumnNames)
                {
                    EditorGUILayout.LabelField(columnName, EditorStyles.boldLabel, GUILayout.Width(100));
                }
                EditorGUILayout.EndHorizontal();
                
                // 显示数据行
                int rowsToShow = Math.Min(previewMaxRows, tabular.RowCount);
                for (int i = 0; i < rowsToShow; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    foreach (var columnName in tabular.ColumnNames)
                    {
                        bool valueDisplayed = false;
                        
                        // 先尝试获取数值列
                        try
                        {
                            var numericData = tabular.GetNumericColumn(columnName);
                            if (i < (int)numericData.size)
                            {
                                var value = numericData.Data<double>()[i];
                                EditorGUILayout.LabelField(value.ToString("F2"), GUILayout.Width(100));
                                valueDisplayed = true;
                            }
                        }
                        catch
                        {
                            // 数值列获取失败，继续尝试字符串列
                        }
                        
                        // 如果数值列没有显示成功，尝试字符串列
                        if (!valueDisplayed)
                        {
                            try
                            {
                                var stringData = tabular.GetStringColumn(columnName);
                                if (i < stringData.Length)
                                {
                                    var value = stringData[i] ?? "";
                                    EditorGUILayout.LabelField(value.Length > 20 ? value.Substring(0, 20) + "..." : value, GUILayout.Width(100));
                                    valueDisplayed = true;
                                }
                            }
                            catch
                            {
                                // 字符串列获取也失败
                            }
                        }
                        
                        // 如果两种列类型都获取失败，显示N/A
                        if (!valueDisplayed)
                        {
                            EditorGUILayout.LabelField("N/A", GUILayout.Width(100));
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// 显示图数据预览
        /// </summary>
        private void ShowGraphPreview(AroAro.DataCore.Graph.GraphData graph, string datasetName)
        {
            EditorGUILayout.BeginHorizontal();
            if (!datasetPreviewFoldouts.ContainsKey(datasetName))
                datasetPreviewFoldouts[datasetName] = false;

            datasetPreviewFoldouts[datasetName] = EditorGUILayout.Foldout(datasetPreviewFoldouts[datasetName], "Graph Preview", true);
            
            if (GUILayout.Button("Open Full Preview", GUILayout.Width(120)))
            {
                DataCorePreviewWindow.ShowWindow(target as DataCoreEditorComponent, datasetName);
            }
            EditorGUILayout.EndHorizontal();
            
            if (datasetPreviewFoldouts[datasetName])
            {
                EditorGUI.indentLevel++;
                
                // 显示节点预览
                EditorGUILayout.LabelField("Nodes:", EditorStyles.boldLabel);
                previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition, GUILayout.Height(150));
                
                int nodesToShow = Math.Min(previewMaxNodes, graph.NodeCount);
                var nodeIds = graph.GetNodeIds().Take(nodesToShow).ToList();
                
                foreach (var nodeId in nodeIds)
                {
                    var node = graph.GetNode(nodeId);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Node: {node.Id}", GUILayout.Width(120));
                    
                    if (node.Properties.Count > 0)
                    {
                        var props = string.Join(", ", node.Properties.Select(kv => $"{kv.Key}:{kv.Value}"));
                        EditorGUILayout.LabelField(props.Length > 50 ? props.Substring(0, 50) + "..." : props);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No properties");
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                if (graph.NodeCount > nodesToShow)
                {
                    EditorGUILayout.LabelField($"... and {graph.NodeCount - nodesToShow} more nodes");
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUILayout.Space();
                
                // 显示边预览
                EditorGUILayout.LabelField("Edges:", EditorStyles.boldLabel);
                previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition, GUILayout.Height(150));
                
                int edgesToShow = Math.Min(previewMaxNodes, graph.EdgeCount);
                var edges = graph.Edges().Take(edgesToShow).ToList();
                
                foreach (var edge in edges)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{edge.From} → {edge.To}", GUILayout.Width(120));
                    
                    if (edge.Properties.Count > 0)
                    {
                        var props = string.Join(", ", edge.Properties.Select(kv => $"{kv.Key}:{kv.Value}"));
                        EditorGUILayout.LabelField(props.Length > 50 ? props.Substring(0, 50) + "..." : props);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No properties");
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                if (graph.EdgeCount > edgesToShow)
                {
                    EditorGUILayout.LabelField($"... and {graph.EdgeCount - edgesToShow} more edges");
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUI.indentLevel--;
            }
        }
    }
}