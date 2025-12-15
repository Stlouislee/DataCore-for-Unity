using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AroAro.DataCore.Editor
{
    public class DataCorePreviewWindow : EditorWindow
    {
        private DataCoreEditorComponent _component;
        private string _datasetName;
        private IDataSet _dataset;
        private Vector2 _scrollPosition;
        private int _maxRowsToShow = 100;
        private int _maxNodesToShow = 50;
        
        public static void ShowWindow(DataCoreEditorComponent component, string datasetName)
        {
            var window = CreateInstance<DataCorePreviewWindow>();
            window.titleContent = new GUIContent($"DataCore Preview - {datasetName}");
            window._component = component;
            window._datasetName = datasetName;
            window._dataset = component.GetStore().Get<IDataSet>(datasetName);
            window.minSize = new Vector2(800, 600);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (_dataset == null)
            {
                EditorGUILayout.HelpBox("Dataset not found or not loaded.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField($"Dataset: {_datasetName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Type: {_dataset.Kind}");
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            if (_dataset is Tabular.TabularData tabular)
            {
                ShowTabularPreview(tabular);
            }
            else if (_dataset is Graph.GraphData graph)
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

        private void ShowTabularPreview(Tabular.TabularData tabular)
        {
            EditorGUILayout.LabelField("Tabular Data Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Rows: {tabular.RowCount}, Columns: {tabular.ColumnNames.Count}");
            
            EditorGUILayout.Space();
            
            // 显示列信息
            EditorGUILayout.LabelField("Columns:", EditorStyles.boldLabel);
            foreach (var columnName in tabular.ColumnNames)
            {
                EditorGUILayout.LabelField($"  {columnName}");
            }
            
            EditorGUILayout.Space();
            
            // 显示数据表格
            EditorGUILayout.LabelField($"First {_maxRowsToShow} rows:", EditorStyles.boldLabel);
            
            // 表头
            EditorGUILayout.BeginHorizontal();
            foreach (var columnName in tabular.ColumnNames)
            {
                EditorGUILayout.LabelField(columnName, EditorStyles.boldLabel, GUILayout.Width(120));
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 数据行
            int rowsToShow = Math.Min(_maxRowsToShow, tabular.RowCount);
            for (int i = 0; i < rowsToShow; i++)
            {
                EditorGUILayout.BeginHorizontal();
                foreach (var columnName in tabular.ColumnNames)
                {
                    bool valueDisplayed = false;
                    
                    // 先尝试数值列
                    try
                    {
                        var numericData = tabular.GetNumericColumn(columnName);
                        if (i < (int)numericData.size)
                        {
                            var value = numericData.Data<double>()[i];
                            EditorGUILayout.LabelField(value.ToString("F4"), GUILayout.Width(120));
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
                                EditorGUILayout.LabelField(value.Length > 15 ? value.Substring(0, 15) + "..." : value, GUILayout.Width(120));
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
                        EditorGUILayout.LabelField("N/A", GUILayout.Width(120));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            
            if (tabular.RowCount > _maxRowsToShow)
            {
                EditorGUILayout.LabelField($"... and {tabular.RowCount - _maxRowsToShow} more rows");
            }
        }

        private void ShowGraphPreview(Graph.GraphData graph)
        {
            EditorGUILayout.LabelField("Graph Data Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Nodes: {graph.NodeCount}, Edges: {graph.EdgeCount}");
            
            EditorGUILayout.Space();
            
            // 显示节点
            EditorGUILayout.LabelField("Nodes:", EditorStyles.boldLabel);
            int nodesToShow = Math.Min(_maxNodesToShow, graph.NodeCount);
            var nodeIds = graph.GetNodeIds().Take(nodesToShow).ToList();
            
            foreach (var nodeId in nodeIds)
            {
                var node = graph.GetNode(nodeId);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Node: {node.Id}", EditorStyles.boldLabel);
                
                if (node.Properties.Count > 0)
                {
                    EditorGUI.indentLevel++;
                    foreach (var prop in node.Properties)
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
            int edgesToShow = Math.Min(_maxNodesToShow, graph.EdgeCount);
            var edges = graph.Edges().Take(edgesToShow).ToList();
            
            foreach (var edge in edges)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Edge: {edge.From} → {edge.To}", EditorStyles.boldLabel);
                
                if (edge.Properties.Count > 0)
                {
                    EditorGUI.indentLevel++;
                    foreach (var prop in edge.Properties)
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
            
            if (graph.EdgeCount > _maxNodesToShow)
            {
                EditorGUILayout.LabelField($"... and {graph.EdgeCount - _maxNodesToShow} more edges");
            }
        }
    }
}