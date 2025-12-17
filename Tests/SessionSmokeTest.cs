using System;
using System.Text;
using UnityEngine;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// Session 冒烟测试 - 快速验证基本功能是否正常
    /// </summary>
    public class SessionSmokeTest : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool logToConsole = true;

        private void Start()
        {
            if (runOnStart)
                RunSmokeTest();
        }

        public void RunSmokeTest()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Session Smoke Test ===");

            try
            {
                // 快速验证基本功能
                TestSessionCreation(sb);
                TestDatasetOperations(sb);
                TestSessionManager(sb);
                TestSessionCleanup(sb);
                sb.AppendLine("✅ Session smoke test passed!");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Session smoke test failed: {ex.Message}");
                sb.AppendLine($"Stack trace: {ex.StackTrace}");
            }

            var result = sb.ToString();
            if (logToConsole)
                Debug.Log(result);
        }

        private static void TestSessionCreation(StringBuilder sb)
        {
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;

            // 创建会话
            var session = sessionManager.CreateSession("SmokeTestSession");
            if (string.IsNullOrEmpty(session.Id)) throw new Exception("Session ID should not be empty");
            if (session.Name != "SmokeTestSession") throw new Exception("Session name incorrect");
            sb.AppendLine("✅ Session creation OK");
        }

        private static void TestDatasetOperations(StringBuilder sb)
        {
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;
            var session = sessionManager.CreateSession("DatasetSmokeTest");

            // 创建数据集
            var dataset = session.CreateDataset("SmokeTestDataset", DataSetKind.Tabular);
            if (dataset.Name != "SmokeTestDataset") throw new Exception("Dataset creation failed");
            
            // 验证数据集存在
            if (!session.HasDataset("SmokeTestDataset")) throw new Exception("Dataset existence check failed");
            
            // 获取数据集
            var retrieved = session.GetDataset("SmokeTestDataset");
            if (retrieved.Name != "SmokeTestDataset") throw new Exception("Dataset retrieval failed");
            sb.AppendLine("✅ Dataset operations OK");
        }

        private static void TestSessionManager(StringBuilder sb)
        {
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;

            // 创建多个会话
            var session1 = sessionManager.CreateSession("SmokeTest1");
            var session2 = sessionManager.CreateSession("SmokeTest2");

            // 验证会话数量
            if (sessionManager.SessionIds.Count != 2) throw new Exception("Session count incorrect");
            
            // 验证统计信息
            var stats = sessionManager.GetStatistics();
            if (stats.TotalSessions != 2) throw new Exception("Session statistics incorrect");
            sb.AppendLine("✅ Session manager OK");
        }

        private static void TestSessionCleanup(StringBuilder sb)
        {
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;

            // 创建会话
            var session = sessionManager.CreateSession("CleanupSmokeTest");
            
            // 关闭会话
            if (!sessionManager.CloseSession(session.Id)) throw new Exception("Session close failed");
            
            // 验证会话已关闭
            if (sessionManager.SessionIds.Count != 0) throw new Exception("Session cleanup failed");
            sb.AppendLine("✅ Session cleanup OK");
        }
    }
}