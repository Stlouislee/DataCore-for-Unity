using UnityEditor;
using UnityEngine;

namespace AroAro.DataCore.Editor
{
    public static class EditorInputDialog
    {
        public static string Show(string title, string message, string defaultValue = "")
        {
            return EditorUtility.SaveFilePanel(title, "", defaultValue, "");
        }
        
        public static string ShowInputDialog(string title, string message, string defaultValue = "")
        {
            return EditorUtility.SaveFilePanel(title, "", defaultValue, "");
        }
    }
}