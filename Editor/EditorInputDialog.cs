using System;
using UnityEditor;
using UnityEngine;

namespace AroAro.DataCore.Editor
{
    /// <summary>
    /// Editor-level text input dialog using a custom EditorWindow.
    /// Unity has no built-in text input dialog API, so we implement one.
    /// </summary>
    public static class EditorInputDialog
    {
        public static string Show(string title, string message, string defaultValue = "")
        {
            return ShowInputDialog(title, message, defaultValue);
        }

        public static string ShowInputDialog(string title, string message, string defaultValue = "")
        {
            string result = null;
            var window = ScriptableObject.CreateInstance<InputDialogWindow>();
            window.titleContent = new GUIContent(title);
            window.Initialize(message, defaultValue, (value) => { result = value; });
            window.ShowModal();
            return result;
        }

        private class InputDialogWindow : EditorWindow
        {
            private string _message;
            private string _inputValue;
            private Action<string> _onComplete;
            private bool _submitted;

            public void Initialize(string message, string defaultValue, Action<string> onComplete)
            {
                _message = message;
                _inputValue = defaultValue;
                _onComplete = onComplete;
                _submitted = false;
                minSize = new Vector2(350, 130);
                maxSize = new Vector2(350, 130);
            }

            private void OnGUI()
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(4);

                GUI.SetNextControlName("InputField");
                _inputValue = EditorGUILayout.TextField(_inputValue);

                if (Event.current.type == EventType.Repaint)
                    GUI.FocusControl("InputField");

                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("OK", GUILayout.Width(80)))
                    Submit();

                if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                {
                    _submitted = true;
                    _onComplete?.Invoke(null);
                    Close();
                }

                EditorGUILayout.EndHorizontal();

                // Enter key submits
                if (Event.current.isKey && Event.current.keyCode == KeyCode.Return && !_submitted)
                {
                    Submit();
                    Event.current.Use();
                }

                // Escape key cancels
                if (Event.current.isKey && Event.current.keyCode == KeyCode.Escape)
                {
                    _submitted = true;
                    _onComplete?.Invoke(null);
                    Close();
                    Event.current.Use();
                }
            }

            private void Submit()
            {
                _submitted = true;
                _onComplete?.Invoke(_inputValue);
                Close();
            }
        }
    }
}
