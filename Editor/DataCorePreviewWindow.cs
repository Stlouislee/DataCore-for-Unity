using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using AroAro.DataCore.Workspace;

namespace AroAro.DataCore.Editor
{
    public class DataCorePreviewWindow : EditorWindow
    {
        private DataCoreEditorComponent _component;
        private string _datasetName;
        private IDataSet _dataset;
        private string _source;
        private Vector2 _scrollPosition;
        private int _maxRowsToShow = 100;
        private int _maxNodesToShow = 50;

        // Track open windows by "{source}:{datasetName}" to prevent duplicates
        private static readonly Dictionary<string, DataCorePreviewWindow> _openWindows = new Dictionary<string, DataCorePreviewWindow>();

        private static string GetWindowKey(string source, string datasetName) => $"{source}:{datasetName}";

        private static DataCorePreviewWindow FindOrReopen(string key, string source, string datasetName)
        {
            if (_openWindows.TryGetValue(key, out var existing) && existing != null)
            {
                existing.Focus();
                return existing;
            }
            return null;
        }

        public static void ShowWindow(DataCoreEditorComponent component, string datasetName)
        {
            var key = GetWindowKey("Store", datasetName);
            var existing = FindOrReopen(key, "Store", datasetName);
            if (existing != null)
            {
                // Refresh data in case it changed
                existing._dataset = component.GetStore().Get<IDataSet>(datasetName);
                existing.Repaint();
                return;
            }

            var window = CreateInstance<DataCorePreviewWindow>();
            window.titleContent = new GUIContent($"DataCore Preview - {datasetName}");
            window._component = component;
            window._datasetName = datasetName;
            window._source = "Store";
            window._dataset = component.GetStore().Get<IDataSet>(datasetName);
            window.minSize = new Vector2(800, 600);
            _openWindows[key] = window;
            window.ShowUtility();
        }

        /// <summary>
        /// 从 Workspace 打开预览窗口
        /// </summary>
        public static void ShowWorkspaceWindow(DataCoreEditorComponent component, string datasetName)
        {
            var key = GetWindowKey("Workspace", datasetName);
            var existing = FindOrReopen(key, "Workspace", datasetName);
            if (existing != null)
            {
                existing._dataset = component.GetWorkspaceDataset(datasetName);
                existing.Repaint();
                return;
            }

            var window = CreateInstance<DataCorePreviewWindow>();
            window.titleContent = new GUIContent($"DataCore Preview - {datasetName} (Workspace)");
            window._component = component;
            window._datasetName = datasetName;
            window._source = "Workspace";
            window._dataset = component.GetWorkspaceDataset(datasetName);
            window.minSize = new Vector2(800, 600);
            _openWindows[key] = window;
            window.ShowUtility();
        }

        private void OnDestroy()
        {
            var key = GetWindowKey(_source, _datasetName);
            _openWindows.Remove(key);
        }

        private void OnGUI()
        {
            if (_dataset == null)
            {
                EditorGUILayout.HelpBox("Dataset not found or not loaded.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField($"Dataset: {_datasetName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Type: {_dataset.Kind}  |  Source: {_source}");
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            if (_dataset is ITabularDataset tabular)
            {
                ShowTabularPreview(tabular);
            }
            else if (_dataset is IGraphDataset graph)
            {
                ShowGraphPreview(graph);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space();
            if (GUILayout.Button("Close"))
            {
                Close();
            }
        }

        private void ShowTabularPreview(ITabularDataset tabular)
        {
            EditorGUILayout.LabelField("Tabular Data Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Rows: {tabular.RowCount}, Columns: {tabular.ColumnCount}");
            
            EditorGUILayout.Space();
            
            // 获取数据
            var data = tabular.Query().Page(1, _maxRowsToShow).ToDictionaries().ToList();
            if (data.Count == 0)
            {
                EditorGUILayout.LabelField("No data to display");
                return;
            }

            // 获取列名
            var columns = data.First().Keys.ToList();
            
            // 显示列信息
            EditorGUILayout.LabelField("Columns:", EditorStyles.boldLabel);
            foreach (var columnName in columns)
            {
                EditorGUILayout.LabelField($"  {columnName}");
            }
            
            EditorGUILayout.Space();
            
            // 显示数据表格
            EditorGUILayout.LabelField($"Showing {data.Count} rows:", EditorStyles.boldLabel);
            
            // 表头
            EditorGUILayout.BeginHorizontal();
            foreach (var columnName in columns)
            {
                EditorGUILayout.LabelField(columnName, EditorStyles.boldLabel, GUILayout.Width(120));
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 数据行
            foreach (var row in data)
            {
                EditorGUILayout.BeginHorizontal();
                foreach (var columnName in columns)
                {
                    var value = row.TryGetValue(columnName, out var v) ? v?.ToString() ?? "" : "";
                    var displayValue = value.Length > 15 ? value.Substring(0, 15) + "..." : value;
                    EditorGUILayout.LabelField(displayValue, GUILayout.Width(120));
                }
                EditorGUILayout.EndHorizontal();
            }
            
            if (tabular.RowCount > _maxRowsToShow)
            {
                EditorGUILayout.LabelField($"... and {tabular.RowCount - _maxRowsToShow} more rows");
            }
        }

        private void ShowGraphPreview(IGraphDataset graph)
        {
            EditorGUILayout.LabelField("Graph Data Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Nodes: {graph.NodeCount}, Edges: {graph.EdgeCount}");
            
            EditorGUILayout.Space();
            
            // 显示节点
            EditorGUILayout.LabelField("Nodes:", EditorStyles.boldLabel);
            var nodeIds = graph.GetNodeIds().Take(_maxNodesToShow).ToList();
            
            foreach (var nodeId in nodeIds)
            {
                var properties = graph.GetNodeProperties(nodeId);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Node: {nodeId}", EditorStyles.boldLabel);
                
                if (properties != null && properties.Count > 0)
                {
                    EditorGUI.indentLevel++;
                    foreach (var prop in properties)
                    {
                        EditorGUILayout.LabelField($"{prop.Key}: {prop.Value}");
                    }
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.LabelField("No properties");
                }
                EditorGUILayout.EndVertical();
            }
            
            if (graph.NodeCount > _maxNodesToShow)
            {
                EditorGUILayout.LabelField($"... and {graph.NodeCount - _maxNodesToShow} more nodes");
            }
            
            EditorGUILayout.Space();
            
            // 显示边
            EditorGUILayout.LabelField("Edges:", EditorStyles.boldLabel);
            var edges = graph.GetEdges().Take(_maxNodesToShow).ToList();
            
            foreach (var edge in edges)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Edge: {edge.From} → {edge.To}", EditorStyles.boldLabel);
                EditorGUILayout.EndVertical();
            }
            
            if (graph.EdgeCount > _maxNodesToShow)
            {
                EditorGUILayout.LabelField($"... and {graph.EdgeCount - _maxNodesToShow} more edges");
            }
        }
    }
}
