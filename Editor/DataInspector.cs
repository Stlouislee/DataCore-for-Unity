using UnityEditor;
using UnityEngine;

namespace DataCore.Editor
{
    /// <summary>
    /// Custom inspector for DataCore components
    /// </summary>
    [CustomEditor(typeof(DataCore.UnityIntegration.DataManagerBehaviour))]
    public class DataManagerBehaviourInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var behaviour = (DataCore.UnityIntegration.DataManagerBehaviour)target;
            
            EditorGUILayout.LabelField("DataCore Data Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            if (behaviour.DataManager != null)
            {
                EditorGUILayout.LabelField("Status", "Active");
                EditorGUILayout.LabelField("Dataset Count", behaviour.DataManager.DatasetCount.ToString());
                EditorGUILayout.LabelField("Memory Usage", $"{behaviour.DataManager.GetTotalMemoryUsage() / (1024.0 * 1024.0):F2} MB");
                
                EditorGUILayout.Space();
                
                if (GUILayout.Button("Open Data Manager"))
                {
                    DataManagerWindow.ShowWindow();
                }
            }
            else
            {
                EditorGUILayout.LabelField("Status", "Not Initialized");
            }
        }
    }
    
    [CustomEditor(typeof(DataCore.UnityIntegration.DataBindingComponent))]
    public class DataBindingComponentInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var component = (DataCore.UnityIntegration.DataBindingComponent)target;
            
            EditorGUILayout.LabelField("DataCore Data Binding", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty("DataKey"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnDataLoaded"));
            
            serializedObject.ApplyModifiedProperties();
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Test Load Data"))
            {
                // Test loading the data
                Debug.Log($"Testing load for dataset: {component.DataKey}");
            }
        }
    }
}