using System;
using System.Text;
using UnityEngine;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// Session 功能专项测试
    /// </summary>
    public class SessionTests : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool logToConsole = true;

        private void Start()
        {
            if (runOnStart)
                RunSessionTests();
        }

        public void RunSessionTests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Session Functionality Tests ===");

            try
            {
                TestBasicSessionOperations(sb);
                TestDatasetOperations(sb);
                TestSessionManagerOperations(sb);
                TestSessionEvents(sb);
                TestSessionLifecycle(sb);
                sb.AppendLine("✅ All Session tests passed!");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Session test failed: {ex.Message}");
                sb.AppendLine($"Stack trace: {ex.StackTrace}");
            }

            var result = sb.ToString();
            if (logToConsole)
                Debug.Log(result);
        }

        private static void TestBasicSessionOperations(StringBuilder sb)
        {
            sb.AppendLine("Testing Basic Session Operations...");
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;

            // 测试会话创建
            var session = sessionManager.CreateSession("BasicTestSession");
            if (string.IsNullOrEmpty(session.Id)) throw new Exception("Session ID should not be empty");
            if (session.Name != "BasicTestSession") throw new Exception("Session name incorrect");
            if (session.DatasetCount != 0) throw new Exception("New session should have 0 datasets");
            sb.AppendLine("✅ Basic session creation OK");

            // 测试会话活动时间
            var initialTime = session.LastActivityAt;
            System.Threading.Thread.Sleep(10); // 短暂延迟
            session.Touch();
            if (session.LastActivityAt <= initialTime) throw new Exception("Session touch should update last activity time");
            sb.AppendLine("✅ Session activity tracking OK");

            // 测试会话清理
            session.Clear();
            if (session.DatasetCount != 0) throw new Exception("Session clear should remove all datasets");
            sb.AppendLine("✅ Session clear OK");
        }

        private static void TestDatasetOperations(StringBuilder sb)
        {
            sb.AppendLine("Testing Session Dataset Operations...");
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;
            var session = sessionManager.CreateSession("DatasetTestSession");

            // 测试在会话中创建数据集
            var tabularDataset = session.CreateDataset("TestTabular", DataSetKind.Tabular);
            if (tabularDataset.Kind != DataSetKind.Tabular) throw new Exception("Tabular dataset creation failed");
            
            var graphDataset = session.CreateDataset("TestGraph", DataSetKind.Graph);
            if (graphDataset.Kind != DataSetKind.Graph) throw new Exception("Graph dataset creation failed");
            sb.AppendLine("✅ Session dataset creation OK");

            // 测试数据集计数
            if (session.DatasetCount != 2) throw new Exception($"Expected 2 datasets, got {session.DatasetCount}");
            sb.AppendLine("✅ Session dataset count OK");

            // 测试数据集获取
            var retrievedTabular = session.GetDataset("TestTabular");
            if (retrievedTabular.Name != "TestTabular") throw new Exception("Dataset retrieval failed");
            sb.AppendLine("✅ Session dataset retrieval OK");

            // 测试数据集存在检查
            if (!session.HasDataset("TestTabular")) throw new Exception("Dataset existence check failed");
            if (session.HasDataset("NonExistent")) throw new Exception("Non-existent dataset should return false");
            sb.AppendLine("✅ Session dataset existence check OK");

            // 测试数据集移除
            if (!session.RemoveDataset("TestTabular")) throw new Exception("Dataset removal failed");
            if (session.DatasetCount != 1) throw new Exception($"Expected 1 dataset after removal, got {session.DatasetCount}");
            sb.AppendLine("✅ Session dataset removal OK");
        }

        private static void TestSessionManagerOperations(StringBuilder sb)
        {
            sb.AppendLine("Testing Session Manager Operations...");
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;

            // 测试多个会话
            var session1 = sessionManager.CreateSession("ManagerTest1");
            var session2 = sessionManager.CreateSession("ManagerTest2");
            var session3 = sessionManager.CreateSession("ManagerTest3");

            // 测试会话ID获取
            var sessionIds = sessionManager.SessionIds;
            if (sessionIds.Count != 3) throw new Exception($"Expected 3 sessions, got {sessionIds.Count}");
            sb.AppendLine("✅ Session manager session IDs OK");

            // 测试会话获取
            var retrievedSession = sessionManager.GetSession(session1.Id);
            if (retrievedSession.Name != "ManagerTest1") throw new Exception("Session retrieval failed");
            sb.AppendLine("✅ Session manager session retrieval OK");

            // 测试会话存在检查
            if (!sessionManager.HasSession(session1.Id)) throw new Exception("Session existence check failed");
            if (sessionManager.HasSession("NonExistent")) throw new Exception("Non-existent session should return false");
            sb.AppendLine("✅ Session manager session existence check OK");

            // 测试会话关闭
            if (!sessionManager.CloseSession(session1.Id)) throw new Exception("Session close failed");
            if (sessionManager.SessionIds.Count != 2) throw new Exception($"Expected 2 sessions after close, got {sessionManager.SessionIds.Count}");
            sb.AppendLine("✅ Session manager session close OK");

            // 测试统计信息
            var stats = sessionManager.GetStatistics();
            if (stats.TotalSessions != 2) throw new Exception($"Expected 2 sessions in stats, got {stats.TotalSessions}");
            sb.AppendLine("✅ Session manager statistics OK");

            // 测试关闭所有会话
            sessionManager.CloseAllSessions();
            if (sessionManager.SessionIds.Count != 0) throw new Exception("Close all sessions failed");
            sb.AppendLine("✅ Session manager close all OK");
        }

        private static void TestSessionEvents(StringBuilder sb)
        {
            sb.AppendLine("Testing Session Events...");
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;
            var session = sessionManager.CreateSession("EventTestSession");

            // 测试事件订阅（这里主要是验证事件系统正常工作）
            bool datasetAddedEventFired = false;
            bool datasetCreatedEventFired = false;

            Events.DataCoreEventManager.SessionDatasetAdded += (sender, e) =>
            {
                if (e.Session == session && e.Dataset.Name == "EventTestDataset")
                    datasetAddedEventFired = true;
            };

            Events.DataCoreEventManager.SessionDatasetCreated += (sender, e) =>
            {
                if (e.Session == session && e.Dataset.Name == "EventTestDataset")
                    datasetCreatedEventFired = true;
            };

            // 创建数据集来触发事件
            var dataset = session.CreateDataset("EventTestDataset", DataSetKind.Tabular);

            // 验证事件是否触发（在Unity编辑器中事件是同步的）
            if (!datasetCreatedEventFired) throw new Exception("Session dataset created event not fired");
            sb.AppendLine("✅ Session events OK");

            // 清理事件订阅
            Events.DataCoreEventManager.ClearAllSubscriptions();
        }

        private static void TestSessionLifecycle(StringBuilder sb)
        {
            sb.AppendLine("Testing Session Lifecycle...");
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;

            // 测试空闲会话清理
            var idleSession = sessionManager.CreateSession("IdleTestSession");
            System.Threading.Thread.Sleep(10); // 确保有时间差

            var cleanupCount = sessionManager.CleanupIdleSessions(TimeSpan.FromMilliseconds(1));
            if (cleanupCount != 1) throw new Exception($"Expected 1 idle session cleaned up, got {cleanupCount}");
            sb.AppendLine("✅ Session idle cleanup OK");

            // 测试会话销毁
            var disposableSession = sessionManager.CreateSession("DisposableTestSession");
            disposableSession.Dispose();
            
            // 验证会话是否被正确清理
            try
            {
                disposableSession.GetDataset("AnyDataset");
                throw new Exception("Disposed session should throw exception");
            }
            catch (ObjectDisposedException)
            {
                // 这是预期的行为
                sb.AppendLine("✅ Session disposal OK");
            }
            catch (Exception)
            {
                throw new Exception("Disposed session should throw ObjectDisposedException");
            }
        }
    }
}