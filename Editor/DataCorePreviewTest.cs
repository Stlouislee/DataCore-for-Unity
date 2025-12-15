using UnityEditor;
using UnityEngine;
using System.Linq;
using NumSharp;

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
                
                if (GUILayout.Button("Add DataCoreEditorComponent to Scene"))
                {
                    var go = new GameObject("DataCoreManager");
                    go.AddComponent<DataCoreEditorComponent>();
                    Selection.activeGameObject = go;
                }
                return;
            }

            var store = component.GetStore();
            var datasets = store.Names.ToList();

            EditorGUILayout.LabelField("Available Datasets:", EditorStyles.boldLabel);
            
            if (datasets.Count == 0)
            {
                EditorGUILayout.HelpBox("No datasets available. Create some datasets to test preview functionality.", MessageType.Info);
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Test Data Creation:", EditorStyles.boldLabel);
                
                if (GUILayout.Button("Create Sample Tabular Dataset"))
                {
                    CreateSampleTabularData(component);
                }
                
                if (GUILayout.Button("Create Sample Graph Dataset"))
                {
                    CreateSampleGraphData(component);
                }
            }
            else
            {
                EditorGUILayout.LabelField($"Found {datasets.Count} datasets:");
                foreach (var name in datasets)
                {
                    var metadata = store.GetMetadata(name);
                    var isLoaded = metadata?.IsLoaded ?? true;
                    EditorGUILayout.LabelField($"- {name} ({metadata?.Kind}) {(isLoaded ? "[Loaded]" : "[Not Loaded]")}");
                }
                
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Preview functionality is now available in the DataCoreEditorComponent inspector. Select the component in the scene to see dataset previews.", MessageType.Info);
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
                component.CreateTabularDataset("SampleTabularData");
                var store = component.GetStore();
                var tabular = store.Get<Tabular.TabularData>("SampleTabularData");
                
                // 添加示例数据
                tabular.AddNumericColumn("ID", np.array(Enumerable.Range(1, 10).Select(i => (double)i).ToArray()));
                tabular.AddStringColumn("Name", Enumerable.Range(1, 10).Select(i => $"Item{i}").ToArray());
                tabular.AddNumericColumn("Value", np.array(Enumerable.Range(1, 10).Select(i => (double)i * 10).ToArray()));
                
                EditorUtility.DisplayDialog("Success", "Sample tabular dataset created successfully!", "OK");
                EditorUtility.SetDirty(component);
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
                component.CreateGraphDataset("SampleGraphData");
                var store = component.GetStore();
                var graph = store.Get<Graph.GraphData>("SampleGraphData");
                
                // 添加示例节点和边
                for (int i = 1; i <= 5; i++)
                {
                    graph.AddNode(i.ToString(), new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["label"] = $"Node{i}",
                        ["value"] = i.ToString()
                    });
                }
                
                // 添加边
                graph.AddEdge("1", "2", new System.Collections.Generic.Dictionary<string, string>
                {
                    ["weight"] = "1.0",
                    ["type"] = "connection"
                });
                
                graph.AddEdge("2", "3", new System.Collections.Generic.Dictionary<string, string>
                {
                    ["weight"] = "2.0",
                    ["type"] = "connection"
                });
                
                graph.AddEdge("3", "4", new System.Collections.Generic.Dictionary<string, string>
                {
                    ["weight"] = "1.5",
                    ["type"] = "connection"
                });
                
                EditorUtility.DisplayDialog("Success", "Sample graph dataset created successfully!", "OK");
                EditorUtility.SetDirty(component);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to create sample data: {ex.Message}", "OK");
            }
        }
    }
}