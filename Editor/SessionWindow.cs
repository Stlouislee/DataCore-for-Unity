using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Editor
{
    /// <summary>
    /// Session 管理窗口
    /// </summary>
    public class SessionWindow : EditorWindow
    {
        private DataCoreEditorComponent component;
        private Vector2 scrollPosition;
        private Dictionary<string, bool> sessionFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, bool> datasetFoldouts = new Dictionary<string, bool>();
        private string newSessionName = "NewSession";
        private string selectedSessionId;

        [MenuItem("Window/AroAro DataCore/Session Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<SessionWindow>("Session Manager");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            // 查找场景中的 DataCoreEditorComponent
            component = FindFirstObjectByType<DataCoreEditorComponent>();
            if (component == null)
            {
                Debug.LogWarning("No DataCoreEditorComponent found in scene. Please add one to a GameObject.");
            }
        }

        private void OnGUI()
        {
            if (component == null)
            {
                EditorGUILayout.HelpBox("No DataCoreEditorComponent found in scene. Please add one to a GameObject.", MessageType.Warning);
                if (GUILayout.Button("Create DataCore Component"))
                {
                    CreateDataCoreComponent();
                }
                return;
            }

            var sessionManager = component.GetSessionManager();
            var sessionIds = sessionManager.SessionIds;

            EditorGUILayout.LabelField("Session Manager", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            // 创建新会话
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Create New Session", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            newSessionName = EditorGUILayout.TextField("Session Name", newSessionName);
            if (GUILayout.Button("Create", GUILayout.Width(80)))
            {
                if (!string.IsNullOrEmpty(newSessionName))
                {
                    var session = sessionManager.CreateSession(newSessionName);
                    Debug.Log($"Created session: {session.Name} (ID: {session.Id})");
                    newSessionName = "NewSession";
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 会话统计
            var stats = sessionManager.GetStatistics();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Active Sessions: {stats.TotalSessions}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Datasets: {stats.TotalDatasets}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Avg per Session: {stats.AverageDatasetsPerSession:F1}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 会话列表
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (sessionIds.Count == 0)
            {
                EditorGUILayout.HelpBox("No active sessions. Create a new session to get started.", MessageType.Info);
            }
            else
            {
                foreach (var sessionId in sessionIds)
                {
                    try
                    {
                        var session = sessionManager.GetSession(sessionId);
                        DrawSessionCard(session, sessionManager);
                    }
                    catch (Exception ex)
                    {
                        EditorGUILayout.HelpBox($"Error loading session {sessionId}: {ex.Message}", MessageType.Error);
                    }
                    EditorGUILayout.Space();
                }
            }

            EditorGUILayout.EndScrollView();

            // 批量操作
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Close All Sessions"))
            {
                if (EditorUtility.DisplayDialog("Close All Sessions", "Are you sure you want to close all sessions?", "Close All", "Cancel"))
                {
                    sessionManager.CloseAllSessions();
                    sessionFoldouts.Clear();
                    datasetFoldouts.Clear();
                    Repaint();
                }
            }
            if (GUILayout.Button("Cleanup Idle Sessions"))
            {
                var idleTimeout = TimeSpan.FromMinutes(30);
                var cleanedCount = sessionManager.CleanupIdleSessions(idleTimeout);
                Debug.Log($"Cleaned up {cleanedCount} idle sessions");
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSessionCard(ISession session, SessionManager sessionManager)
        {
            var sessionId = session.Id;
            if (!sessionFoldouts.ContainsKey(sessionId))
                sessionFoldouts[sessionId] = false;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            // Session 标题行
            EditorGUILayout.BeginHorizontal();
            
            var foldoutContent = new GUIContent($"{session.Name}", $"ID: {session.Id}\nCreated: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}\nLast Activity: {session.LastActivityAt:yyyy-MM-dd HH:mm:ss}");
            sessionFoldouts[sessionId] = EditorGUILayout.Foldout(sessionFoldouts[sessionId], foldoutContent, true);
            
            GUILayout.FlexibleSpace();
            
            // Session 信息
            EditorGUILayout.LabelField($"Datasets: {session.DatasetCount}", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField($"Active: {(DateTime.Now - session.LastActivityAt).TotalMinutes:F1}m ago", EditorStyles.miniLabel, GUILayout.Width(120));
            
            // 操作按钮
            if (GUILayout.Button("Focus", GUILayout.Width(60)))
            {
                selectedSessionId = sessionId;
                sessionFoldouts[sessionId] = true;
            }
            
            if (GUILayout.Button("Close", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Close Session", $"Are you sure you want to close session '{session.Name}'?", "Close", "Cancel"))
                {
                    sessionManager.CloseSession(sessionId);
                    sessionFoldouts.Remove(sessionId);
                    Repaint();
                }
            }
            
            EditorGUILayout.EndHorizontal();

            if (sessionFoldouts[sessionId])
            {
                EditorGUI.indentLevel++;
                DrawSessionDetails(session, sessionManager);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawSessionDetails(ISession session, SessionManager sessionManager)
        {
            EditorGUILayout.Space();
            
            // Session 详细信息
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Session Information", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID", session.Id);
            EditorGUILayout.LabelField("Created", session.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            EditorGUILayout.LabelField("Last Activity", session.LastActivityAt.ToString("yyyy-MM-dd HH:mm:ss"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 数据集管理
            var datasetNames = session.DatasetNames;
            if (datasetNames.Count == 0)
            {
                EditorGUILayout.HelpBox("No datasets in this session", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Datasets ({datasetNames.Count})", EditorStyles.boldLabel);
                
                foreach (var datasetName in datasetNames)
                {
                    var datasetKey = $"{session.Id}_{datasetName}";
                    if (!datasetFoldouts.ContainsKey(datasetKey))
                        datasetFoldouts[datasetKey] = false;

                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    EditorGUILayout.BeginHorizontal();
                    
                    datasetFoldouts[datasetKey] = EditorGUILayout.Foldout(datasetFoldouts[datasetKey], datasetName, true);
                    
                    GUILayout.FlexibleSpace();
                    
                    try
                    {
                        var dataset = session.GetDataset(datasetName);
                        EditorGUILayout.LabelField(dataset.Kind.ToString(), EditorStyles.miniLabel, GUILayout.Width(80));
                        
                        // 操作按钮
                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        {
                            if (EditorUtility.DisplayDialog("Remove Dataset", $"Remove dataset '{datasetName}' from session?", "Remove", "Cancel"))
                            {
                                session.RemoveDataset(datasetName);
                                datasetFoldouts.Remove(datasetKey);
                                Repaint();
                            }
                        }
                        
                        if (GUILayout.Button("Persist", GUILayout.Width(70)))
                        {
                            try
                            {
                                if (session.PersistDataset(datasetName))
                                {
                                    Debug.Log($"Persisted dataset '{datasetName}' from session");
                                }
                                else
                                {
                                    EditorUtility.DisplayDialog("Persist Failed", $"Failed to persist dataset '{datasetName}'", "OK");
                                }
                            }
                            catch (Exception ex)
                            {
                                EditorUtility.DisplayDialog("Persist Error", $"Error persisting dataset: {ex.Message}", "OK");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        EditorGUILayout.LabelField("Error", EditorStyles.miniLabel, GUILayout.Width(80));
                    }
                    
                    EditorGUILayout.EndHorizontal();

                    if (datasetFoldouts[datasetKey])
                    {
                        EditorGUI.indentLevel++;
                        try
                        {
                            var dataset = session.GetDataset(datasetName);
                            DrawDatasetDetails(dataset);
                        }
                        catch (Exception ex)
                        {
                            EditorGUILayout.HelpBox($"Error loading dataset: {ex.Message}", MessageType.Error);
                        }
                        EditorGUI.indentLevel--;
                    }
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space();
                }

                // 会话操作
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear Session"))
                {
                    if (EditorUtility.DisplayDialog("Clear Session", $"Clear all datasets from session '{session.Name}'?", "Clear", "Cancel"))
                    {
                        session.Clear();
                        datasetFoldouts.Clear();
                        Repaint();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();

            // 快速操作
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Dataset"))
            {
                // 打开数据集对话框
                ShowOpenDatasetDialog(session);
            }
            if (GUILayout.Button("Create Dataset"))
            {
                // 创建数据集对话框
                ShowCreateDatasetDialog(session);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDatasetDetails(IDataSet dataset)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Dataset Details", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID", dataset.Id);
            EditorGUILayout.LabelField("Kind", dataset.Kind.ToString());

            switch (dataset.Kind)
            {
                case DataSetKind.Tabular:
                    var tabular = dataset as Tabular.TabularData;
                    if (tabular != null)
                    {
                        EditorGUILayout.LabelField("Rows", tabular.RowCount.ToString());
                        EditorGUILayout.LabelField("Columns", string.Join(", ", tabular.ColumnNames));
                    }
                    break;
                case DataSetKind.Graph:
                    var graph = dataset as Graph.GraphData;
                    if (graph != null)
                    {
                        EditorGUILayout.LabelField("Nodes", graph.NodeCount.ToString());
                        EditorGUILayout.LabelField("Edges", graph.EdgeCount.ToString());
                    }
                    break;
            }
            EditorGUILayout.EndVertical();
        }

        private void ShowOpenDatasetDialog(ISession session)
        {
            // 实现打开数据集对话框
            EditorUtility.DisplayDialog("Open Dataset", "This feature will be implemented in a future version.", "OK");
        }

        private void ShowCreateDatasetDialog(ISession session)
        {
            // 实现创建数据集对话框
            EditorUtility.DisplayDialog("Create Dataset", "This feature will be implemented in a future version.", "OK");
        }

        private void CreateDataCoreComponent()
        {
            var go = new GameObject("DataCore");
            component = go.AddComponent<DataCoreEditorComponent>();
            Debug.Log("Created DataCore component on new GameObject");
        }

        private void Update()
        {
            // 定期重绘以更新活动时间
            if (Time.frameCount % 60 == 0) // 每秒重绘一次
            {
                Repaint();
            }
        }
    }
}