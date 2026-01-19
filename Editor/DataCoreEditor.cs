using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using AroAro.DataCore.Session;

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
        
        // GraphML 导入相关字段
        private string graphmlFilePath = "";
        private string graphmlDatasetName = "ImportedGraphML";
        
        // 预览相关字段
        private Vector2 previewScrollPosition;
        private int previewMaxRows = 10;
        private int previewMaxNodes = 20;

        // Session 相关字段
        private bool showSessions = true;
        private Dictionary<string, bool> sessionFoldouts = new Dictionary<string, bool>();
        private string newSessionName = "NewSession";

        // 创建数据集
        private string newTabularName = "NewTabular";
        private string newGraphName = "NewGraph";

        private void OnEnable()
        {
            component = (DataCoreEditorComponent)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space();

            // 数据集管理
            DrawDatasetsSection();
            
            // CSV 导入
            DrawCsvImportSection();
            
            // GraphML 导入
            DrawGraphMLImportSection();
            
            // 创建数据集
            DrawCreateDatasetSection();
            
            // 操作按钮
            DrawActionsSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDatasetsSection()
        {
            showDatasets = EditorGUILayout.Foldout(showDatasets, "Datasets", true);
            
            if (showDatasets)
            {
                EditorGUI.indentLevel++;
                
                DataCoreStore store = null;
                List<string> names = new List<string>();

                try
                {
                    store = component.GetStore();
                    // Accessing Names can throw if store is disposed/corrupt
                    if (store != null)
                    {
                        names = store.Names.ToList();
                    }
                }
                catch
                {
                    // Ignore transient errors or disposed object exceptions during UI draw
                }
                
                if (store == null)
                {
                     EditorGUILayout.HelpBox("Store not initialized.", MessageType.Warning);
                }
                else if (names.Count == 0)
                {
                    EditorGUILayout.HelpBox("No datasets loaded.", MessageType.Info);
                }
                else
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
                    
                    foreach (var name in names)
                    {
                        DrawDatasetItem(store, name);
                    }
                    
                    EditorGUILayout.EndScrollView();
                }
                
                EditorGUI.indentLevel--;
            }
        }

        private void DrawDatasetItem(DataCoreStore store, string name)
        {
            if (!datasetFoldouts.ContainsKey(name))
                datasetFoldouts[name] = false;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            EditorGUILayout.BeginHorizontal();
            datasetFoldouts[name] = EditorGUILayout.Foldout(datasetFoldouts[name], name, true);
            
            if (GUILayout.Button("Preview", GUILayout.Width(60)))
            {
                DataCorePreviewWindow.ShowWindow(component, name);
            }
            
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Delete Dataset", $"Are you sure you want to delete '{name}'?", "Delete", "Cancel"))
                {
                    store.Delete(name);
                    datasetFoldouts.Remove(name);
                    EditorUtility.SetDirty(component);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            if (datasetFoldouts[name])
            {
                EditorGUI.indentLevel++;
                
                if (store.TryGet(name, out var ds))
                {
                    EditorGUILayout.LabelField("Type", ds.Kind.ToString());
                    
                    if (ds is ITabularDataset tabular)
                    {
                        EditorGUILayout.LabelField("Rows", tabular.RowCount.ToString());
                        EditorGUILayout.LabelField("Columns", tabular.ColumnCount.ToString());
                        ShowTabularPreview(tabular, name);
                    }
                    else if (ds is IGraphDataset graph)
                    {
                        EditorGUILayout.LabelField("Nodes", graph.NodeCount.ToString());
                        EditorGUILayout.LabelField("Edges", graph.EdgeCount.ToString());
                        ShowGraphPreview(graph, name);
                    }
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }

        private void ShowTabularPreview(ITabularDataset tabular, string datasetName)
        {
            if (!datasetPreviewFoldouts.ContainsKey(datasetName))
                datasetPreviewFoldouts[datasetName] = false;

            datasetPreviewFoldouts[datasetName] = EditorGUILayout.Foldout(datasetPreviewFoldouts[datasetName], "Data Preview", true);
            
            if (datasetPreviewFoldouts[datasetName])
            {
                EditorGUI.indentLevel++;
                
                var data = tabular.Query().Page(1, previewMaxRows).ToDictionaries().ToList();
                if (data.Count == 0)
                {
                    EditorGUILayout.LabelField("No data to display");
                }
                else
                {
                    var columns = data.First().Keys.ToList();
                    
                    previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition, GUILayout.Height(150));
                    
                    // 表头
                    EditorGUILayout.BeginHorizontal();
                    foreach (var col in columns)
                    {
                        EditorGUILayout.LabelField(col, EditorStyles.boldLabel, GUILayout.Width(80));
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    // 数据行
                    foreach (var row in data)
                    {
                        EditorGUILayout.BeginHorizontal();
                        foreach (var col in columns)
                        {
                            var value = row.TryGetValue(col, out var v) ? v?.ToString() ?? "" : "";
                            var display = value.Length > 10 ? value.Substring(0, 10) + "..." : value;
                            EditorGUILayout.LabelField(display, GUILayout.Width(80));
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.EndScrollView();
                    
                    if (tabular.RowCount > previewMaxRows)
                    {
                        EditorGUILayout.LabelField($"... and {tabular.RowCount - previewMaxRows} more rows");
                    }
                }
                
                EditorGUI.indentLevel--;
            }
        }

        private void ShowGraphPreview(IGraphDataset graph, string datasetName)
        {
            if (!datasetPreviewFoldouts.ContainsKey(datasetName))
                datasetPreviewFoldouts[datasetName] = false;

            datasetPreviewFoldouts[datasetName] = EditorGUILayout.Foldout(datasetPreviewFoldouts[datasetName], "Graph Preview", true);
            
            if (datasetPreviewFoldouts[datasetName])
            {
                EditorGUI.indentLevel++;
                
                previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition, GUILayout.Height(150));
                
                // 显示节点
                EditorGUILayout.LabelField("Nodes:", EditorStyles.boldLabel);
                var nodeIds = graph.GetNodeIds().Take(previewMaxNodes).ToList();
                foreach (var nodeId in nodeIds)
                {
                    EditorGUILayout.LabelField($"  {nodeId}");
                }
                
                if (graph.NodeCount > previewMaxNodes)
                {
                    EditorGUILayout.LabelField($"  ... and {graph.NodeCount - previewMaxNodes} more");
                }
                
                EditorGUILayout.Space();
                
                // 显示边
                EditorGUILayout.LabelField("Edges:", EditorStyles.boldLabel);
                var edges = graph.GetEdges().Take(previewMaxNodes).ToList();
                foreach (var edge in edges)
                {
                    EditorGUILayout.LabelField($"  {edge.From} → {edge.To}");
                }
                
                if (graph.EdgeCount > previewMaxNodes)
                {
                    EditorGUILayout.LabelField($"  ... and {graph.EdgeCount - previewMaxNodes} more");
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUI.indentLevel--;
            }
        }

        private void DrawCsvImportSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Import CSV", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            EditorGUILayout.BeginHorizontal();
            csvFilePath = EditorGUILayout.TextField("CSV File", csvFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var path = EditorUtility.OpenFilePanel("Select CSV File", "", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    csvFilePath = path;
                    csvDatasetName = System.IO.Path.GetFileNameWithoutExtension(path);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            csvDatasetName = EditorGUILayout.TextField("Dataset Name", csvDatasetName);
            csvHasHeader = EditorGUILayout.Toggle("Has Header", csvHasHeader);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Delimiter");
            csvDelimiter = EditorGUILayout.TextField(csvDelimiter.ToString(), GUILayout.Width(30)).FirstOrDefault();
            if (csvDelimiter == '\0') csvDelimiter = ',';
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Import CSV", GUILayout.Width(100)))
            {
                ImportCsv();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void ImportCsv()
        {
            if (string.IsNullOrEmpty(csvFilePath))
            {
                EditorUtility.DisplayDialog("Import CSV", "Please select a CSV file first.", "OK");
                return;
            }
            
            if (string.IsNullOrEmpty(csvDatasetName))
            {
                EditorUtility.DisplayDialog("Import CSV", "Please enter a dataset name.", "OK");
                return;
            }
            
            try
            {
                var tabular = component.ImportCsvToTabular(csvFilePath, csvDatasetName, csvHasHeader, csvDelimiter);
                EditorUtility.DisplayDialog("Import CSV", $"Successfully imported {tabular.RowCount} rows.", "OK");
                EditorUtility.SetDirty(component);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import CSV Error", ex.Message, "OK");
            }
        }

        private void DrawGraphMLImportSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Import GraphML", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            EditorGUILayout.BeginHorizontal();
            graphmlFilePath = EditorGUILayout.TextField("GraphML File", graphmlFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var path = EditorUtility.OpenFilePanel("Select GraphML File", "", "graphml,xml");
                if (!string.IsNullOrEmpty(path))
                {
                    graphmlFilePath = path;
                    graphmlDatasetName = System.IO.Path.GetFileNameWithoutExtension(path);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            graphmlDatasetName = EditorGUILayout.TextField("Dataset Name", graphmlDatasetName);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Import GraphML", GUILayout.Width(100)))
            {
                ImportGraphML();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void ImportGraphML()
        {
            if (string.IsNullOrEmpty(graphmlFilePath))
            {
                EditorUtility.DisplayDialog("Import GraphML", "Please select a GraphML file first.", "OK");
                return;
            }
            
            if (string.IsNullOrEmpty(graphmlDatasetName))
            {
                EditorUtility.DisplayDialog("Import GraphML", "Please enter a dataset name.", "OK");
                return;
            }
            
            try
            {
                var graph = component.ImportGraphMLToGraph(graphmlFilePath, graphmlDatasetName);
                EditorUtility.DisplayDialog("Import GraphML", $"Successfully imported {graph.NodeCount} nodes and {graph.EdgeCount} edges.", "OK");
                EditorUtility.SetDirty(component);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import GraphML Error", ex.Message, "OK");
            }
        }

        private void DrawCreateDatasetSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Create Dataset", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            // 创建表格数据集
            EditorGUILayout.BeginHorizontal();
            newTabularName = EditorGUILayout.TextField("Tabular Name", newTabularName);
            if (GUILayout.Button("Create", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(newTabularName))
                {
                    component.CreateTabularDataset(newTabularName);
                    EditorUtility.SetDirty(component);
                    newTabularName = "NewTabular";
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // 创建图数据集
            EditorGUILayout.BeginHorizontal();
            newGraphName = EditorGUILayout.TextField("Graph Name", newGraphName);
            if (GUILayout.Button("Create", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(newGraphName))
                {
                    component.CreateGraphDataset(newGraphName);
                    EditorUtility.SetDirty(component);
                    newGraphName = "NewGraph";
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawActionsSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Checkpoint"))
            {
                component.Checkpoint();
                EditorUtility.DisplayDialog("Checkpoint", "Data flushed to disk.", "OK");
            }
            
            if (GUILayout.Button("Clear All"))
            {
                if (EditorUtility.DisplayDialog("Clear All", "Are you sure you want to delete all datasets?", "Yes", "No"))
                {
                    component.ClearAll();
                    datasetFoldouts.Clear();
                    EditorUtility.SetDirty(component);
                }
            }
            
            if (GUILayout.Button("Run Self Test"))
            {
                RunSelfTest();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            
            // Export Database Button
            if (GUILayout.Button("Export Database File", GUILayout.Height(30)))
            {
                string exportPath = EditorUtility.SaveFilePanel(
                    "Export Database",
                    "",
                    "datacore_export.db",
                    "db");
                
                if (!string.IsNullOrEmpty(exportPath))
                {
                    if (component.ExportDatabaseFile(exportPath))
                    {
                        EditorUtility.DisplayDialog("Export Success", 
                            $"Database has been exported to:\n{exportPath}", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Export Failed", 
                            "Failed to export database. Check console for details.", "OK");
                    }
                }
            }
            
            EditorGUILayout.Space();
            
            // Hard Reset Button (Red)
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Delete Database File (Hard Reset)", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Delete Database", 
                    "Are you sure you want to delete the database file? This will ERASE ALL DATA irrevocably.\n\nUse this if the database is corrupted.", 
                    "Yes, Delete Everything", "Cancel"))
                {
                    component.DeleteDatabaseFile();
                    datasetFoldouts.Clear();
                    datasetPreviewFoldouts.Clear();
                    EditorUtility.SetDirty(component);
                    EditorUtility.DisplayDialog("Database Deleted", "Database file has been deleted. It will be recreated on next access.", "OK");
                }
            }
            GUI.backgroundColor = originalColor;
        }


        private void RunSelfTest()
        {
            var go = new GameObject("DataCore Test Runner");
            var test = go.AddComponent<DataCoreSelfTest>();
            test.RunTests();
            DestroyImmediate(go);
        }
    }
}
