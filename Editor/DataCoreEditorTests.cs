using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace AroAro.DataCore.Editor
{
    public static class DataCoreEditorTests
    {
        [MenuItem("GameObject/Data Core/Create Data Core", false, 10)]
        public static void CreateDataCoreGameObject()
        {
            var go = new GameObject("Data Core");
            var dataCore = go.AddComponent<DataCoreEditorComponent>();
            
            // Add self-test component for easy testing
            go.AddComponent<DataCoreSelfTest>();
            
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Data Core");
        }

        [MenuItem("Tools/DataCore/Import CSV")]
        public static void ImportCsvFromMenu()
        {
            var path = EditorUtility.OpenFilePanel("Select CSV File", "", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            var datasetName = EditorUtility.DisplayDialogComplex(
                "CSV Import",
                $"Import CSV file: {System.IO.Path.GetFileName(path)}",
                "Import",
                "Cancel",
                "Use custom name"
            );

            string finalName;
            if (datasetName == 0) // Import
            {
                finalName = fileName;
            }
            else if (datasetName == 2) // Use custom name
            {
                finalName = EditorInputDialog.Show("CSV Import", "Enter dataset name:", fileName);
                if (string.IsNullOrEmpty(finalName))
                    return;
            }
            else // Cancel
            {
                return;
            }

            // 查找或创建 Data Core 组件
            var dataCore = Object.FindFirstObjectByType<DataCoreEditorComponent>();
            if (dataCore == null)
            {
                EditorUtility.DisplayDialog("CSV Import Error", "No Data Core component found in the scene. Please create one first.", "OK");
                return;
            }

            try
            {
                dataCore.ImportCsvToTabular(path, finalName, true, ',');
                EditorUtility.DisplayDialog("CSV Import Success", $"Successfully imported CSV to dataset '{finalName}'", "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("CSV Import Error", $"Failed to import CSV: {ex.Message}", "OK");
            }
        }

        [MenuItem("Tools/DataCore/Run Self-Test")]
        public static void RunSelfTest()
        {
            var go = new GameObject("DataCore Test Runner");
            var test = go.AddComponent<DataCoreSelfTest>();
            test.RunTests();
            Object.DestroyImmediate(go);
        }

        [MenuItem("Tools/DataCore/Create Data Core GameObject")]
        public static void CreateDataCoreGameObjectMenu()
        {
            CreateDataCoreGameObject();
        }
    }
}