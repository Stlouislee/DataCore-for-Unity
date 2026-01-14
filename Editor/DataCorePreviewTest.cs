using UnityEditor;
using UnityEngine;
using System.Linq;

namespace AroAro.DataCore.Editor
{
    public class DataCorePreviewTest : EditorWindow
    {
        [MenuItem("Tools/DataCore/Preview Test")]
        public static void ShowWindow()
        {
            GetWindow<DataCorePreviewTest>("DataCore Preview Test");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("DataCore Preview Functionality Test", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 检查编辑器组件是否存在
            var component = FindAnyObjectByType<DataCoreEditorComponent>();
            if (component == null)
            {
                EditorGUILayout.HelpBox("No DataCoreEditorComponent found in scene. Please add one to test preview functionality.", MessageType.Warning);
                
                if (GUILayout.Button("Create DataCore Component"))
                {
                    var go = new GameObject("DataCore");
                    go.AddComponent<DataCoreEditorComponent>();
                }
                return;
            }

            EditorGUILayout.LabelField("DataCore Component found on: " + component.gameObject.name);
            EditorGUILayout.Space();

            // 测试按钮
            EditorGUILayout.LabelField("Test Operations:", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Create Sample Tabular Dataset"))
            {
                CreateSampleTabularData(component);
            }

            if (GUILayout.Button("Create Sample Graph Dataset"))
            {
                CreateSampleGraphData(component);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current Datasets:", EditorStyles.boldLabel);
            
            var store = component.GetStore();
            foreach (var name in store.Names)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(name);
                
                if (GUILayout.Button("Preview", GUILayout.Width(80)))
                {
                    DataCorePreviewWindow.ShowWindow(component, name);
                }
                
                if (GUILayout.Button("Delete", GUILayout.Width(80)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Delete", $"Delete dataset '{name}'?", "Yes", "No"))
                    {
                        store.Delete(name);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Open DataCore Editor Component"))
            {
                Selection.activeGameObject = component.gameObject;
            }
        }

        private void CreateSampleTabularData(DataCoreEditorComponent component)
        {
            try
            {
                var tabular = component.CreateTabularDataset("SampleTabularData");
                if (tabular == null)
                {
                    EditorUtility.DisplayDialog("Error", "Failed to create tabular dataset", "OK");
                    return;
                }
                
                // 添加示例数据
                var ids = Enumerable.Range(1, 10).Select(i => (double)i).ToArray();
                var names = Enumerable.Range(1, 10).Select(i => $"Item{i}").ToArray();
                var values = Enumerable.Range(1, 10).Select(i => (double)(i * 10)).ToArray();
                
                tabular.AddNumericColumn("ID", ids);
                tabular.AddStringColumn("Name", names);
                tabular.AddNumericColumn("Value", values);
                
                EditorUtility.DisplayDialog("Success", $"Sample tabular dataset created with {tabular.RowCount} rows!", "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to create sample data: {ex.Message}", "OK");
            }
        }

        private void CreateSampleGraphData(DataCoreEditorComponent component)
        {
            try
            {
                var graph = component.CreateGraphDataset("SampleGraphData");
                if (graph == null)
                {
                    EditorUtility.DisplayDialog("Error", "Failed to create graph dataset", "OK");
                    return;
                }
                
                // 添加示例节点
                for (int i = 1; i <= 5; i++)
                {
                    graph.AddNode(i.ToString(), new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["label"] = $"Node{i}",
                        ["value"] = i
                    });
                }
                
                // 添加边
                graph.AddEdge("1", "2");
                graph.AddEdge("2", "3");
                graph.AddEdge("3", "4");
                graph.AddEdge("4", "5");
                graph.AddEdge("1", "5");
                
                EditorUtility.DisplayDialog("Success", $"Sample graph dataset created with {graph.NodeCount} nodes and {graph.EdgeCount} edges!", "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to create sample data: {ex.Message}", "OK");
            }
        }
    }
}
