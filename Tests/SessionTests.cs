using System;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// Session 功能专项测试
    /// Migrated from MonoBehaviour to NUnit for CI/CD compatibility.
    /// </summary>
    [TestFixture]
    public class SessionTests
    {
        [Test]
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
                Assert.Fail(ex.Message);
            }

            Debug.Log(sb.ToString());
        }

        private static void TestBasicSessionOperations(StringBuilder sb)
        {
            sb.AppendLine("Testing Basic Session Operations...");
            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;

                var session = sessionManager.CreateSession("BasicTestSession");
                Assert.That(string.IsNullOrEmpty(session.Id), Is.False, "Session ID should not be empty");
                Assert.That(session.Name, Is.EqualTo("BasicTestSession"), "Session name incorrect");
                Assert.That(session.DatasetCount, Is.EqualTo(0), "New session should have 0 datasets");
                sb.AppendLine("✅ Basic session creation OK");

                var initialTime = session.LastActivityAt;
                System.Threading.Thread.Sleep(10);
                session.Touch();
                Assert.That(session.LastActivityAt > initialTime, Is.True, "Session touch should update last activity time");
                sb.AppendLine("✅ Session activity tracking OK");

                session.Clear();
                Assert.That(session.DatasetCount, Is.EqualTo(0), "Session clear should remove all datasets");
                sb.AppendLine("✅ Session clear OK");
            }
            finally
            {
                store.Dispose();
            }
        }

        private static void TestDatasetOperations(StringBuilder sb)
        {
            sb.AppendLine("Testing Session Dataset Operations...");
            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;
                var session = sessionManager.CreateSession("DatasetTestSession");

                var tabularDataset = session.CreateDataset("TestTabular", DataSetKind.Tabular);
                Assert.That(tabularDataset.Kind, Is.EqualTo(DataSetKind.Tabular), "Tabular dataset creation failed");

                var graphDataset = session.CreateDataset("TestGraph", DataSetKind.Graph);
                Assert.That(graphDataset.Kind, Is.EqualTo(DataSetKind.Graph), "Graph dataset creation failed");
                sb.AppendLine("✅ Session dataset creation OK");

                Assert.That(session.DatasetCount, Is.EqualTo(2), "Expected 2 datasets");
                sb.AppendLine("✅ Session dataset count OK");

                var retrievedTabular = session.GetDataset("TestTabular");
                Assert.That(retrievedTabular.Name, Is.EqualTo("TestTabular"), "Dataset retrieval failed");
                sb.AppendLine("✅ Session dataset retrieval OK");

                Assert.That(session.HasDataset("TestTabular"), Is.True, "Dataset existence check failed");
                Assert.That(session.HasDataset("NonExistent"), Is.False, "Non-existent dataset should return false");
                sb.AppendLine("✅ Session dataset existence check OK");

                Assert.That(session.RemoveDataset("TestTabular"), Is.True, "Dataset removal failed");
                Assert.That(session.DatasetCount, Is.EqualTo(1), "Expected 1 dataset after removal");
                sb.AppendLine("✅ Session dataset removal OK");
            }
            finally
            {
                store.Dispose();
            }
        }

        private static void TestSessionManagerOperations(StringBuilder sb)
        {
            sb.AppendLine("Testing Session Manager Operations...");
            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;

                var session1 = sessionManager.CreateSession("ManagerTest1");
                var session2 = sessionManager.CreateSession("ManagerTest2");
                var session3 = sessionManager.CreateSession("ManagerTest3");

                Assert.That(sessionManager.SessionIds.Count, Is.EqualTo(3), "Expected 3 sessions");
                sb.AppendLine("✅ Session manager session IDs OK");

                var retrievedSession = sessionManager.GetSession(session1.Id);
                Assert.That(retrievedSession.Name, Is.EqualTo("ManagerTest1"), "Session retrieval failed");
                sb.AppendLine("✅ Session manager session retrieval OK");

                Assert.That(sessionManager.HasSession(session1.Id), Is.True, "Session existence check failed");
                Assert.That(sessionManager.HasSession("NonExistent"), Is.False, "Non-existent session should return false");
                sb.AppendLine("✅ Session manager session existence check OK");

                Assert.That(sessionManager.CloseSession(session1.Id), Is.True, "Session close failed");
                Assert.That(sessionManager.SessionIds.Count, Is.EqualTo(2), "Expected 2 sessions after close");
                sb.AppendLine("✅ Session manager session close OK");

                var stats = sessionManager.GetStatistics();
                Assert.That(stats.TotalSessions, Is.EqualTo(2), "Expected 2 sessions in stats");
                sb.AppendLine("✅ Session manager statistics OK");

                sessionManager.CloseAllSessions();
                Assert.That(sessionManager.SessionIds.Count, Is.EqualTo(0), "Close all sessions failed");
                sb.AppendLine("✅ Session manager close all OK");
            }
            finally
            {
                store.Dispose();
            }
        }

        private static void TestSessionEvents(StringBuilder sb)
        {
            sb.AppendLine("Testing Session Events...");
            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;
                var session = sessionManager.CreateSession("EventTestSession");

                bool datasetCreatedEventFired = false;

                Events.DataCoreEventManager.SubscribeSessionDatasetCreated((sender, e) =>
                {
                    if (e.Session == session && e.Dataset.Name == "EventTestDataset")
                        datasetCreatedEventFired = true;
                });

                var dataset = session.CreateDataset("EventTestDataset", DataSetKind.Tabular);

                Assert.That(datasetCreatedEventFired, Is.True, "Session dataset created event not fired");
                sb.AppendLine("✅ Session events OK");
            }
            finally
            {
                Events.DataCoreEventManager.ClearAllSubscriptions();
                store.Dispose();
            }
        }

        private static void TestSessionLifecycle(StringBuilder sb)
        {
            sb.AppendLine("Testing Session Lifecycle...");
            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;

                var idleSession = sessionManager.CreateSession("IdleTestSession");
                System.Threading.Thread.Sleep(10);

                var cleanupCount = sessionManager.CleanupIdleSessions(TimeSpan.FromMilliseconds(1));
                Assert.That(cleanupCount, Is.EqualTo(1), "Expected 1 idle session cleaned up");
                sb.AppendLine("✅ Session idle cleanup OK");

                var disposableSession = sessionManager.CreateSession("DisposableTestSession");
                disposableSession.Dispose();

                try
                {
                    disposableSession.GetDataset("AnyDataset");
                    Assert.Fail("Disposed session should throw exception");
                }
                catch (ObjectDisposedException)
                {
                    sb.AppendLine("✅ Session disposal OK");
                }
                catch (Exception)
                {
                    Assert.Fail("Disposed session should throw ObjectDisposedException");
                }
            }
            finally
            {
                store.Dispose();
            }
        }
    }
}
