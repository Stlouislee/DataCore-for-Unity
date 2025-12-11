using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace DataCore.Editor
{
    /// <summary>
    /// Graph visualization window for DataCore graphs
    /// </summary>
    public class GraphVisualizer : EditorWindow
    {
        private Vector2 scrollPosition;
        private Dictionary<string, bool> expandedNodes = new Dictionary<string, bool>();
        
        [MenuItem("DataCore/Graph Visualizer")]
        public static void ShowWindow()
        {
            GetWindow<GraphVisualizer>("Graph Visualizer");
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Graph Visualizer", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            // Placeholder for graph visualization
            EditorGUILayout.HelpBox("Graph visualization will be implemented here", MessageType.Info);
            
            EditorGUILayout.EndScrollView();
        }
    }
    
    /// <summary>
    /// Custom property drawer for graph data
    /// </summary>
    [CustomPropertyDrawer(typeof(DataCore.Graph.Graph<,>))]
    public class GraphPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            // Draw the foldout
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            
            // Draw basic graph info
            var vertexCountProp = property.FindPropertyRelative("vertexCount");
            var edgeCountProp = property.FindPropertyRelative("edgeCount");
            
            if (vertexCountProp != null && edgeCountProp != null)
            {
                EditorGUI.LabelField(new Rect(position.x, position.y, position.width, 16), 
                    $"Vertices: {vertexCountProp.intValue}, Edges: {edgeCountProp.intValue}");
            }
            
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}