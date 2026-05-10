using System;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// Session 冒烟测试 - 快速验证基本功能是否正常
    /// Migrated from MonoBehaviour to NUnit for CI/CD compatibility.
    /// </summary>
    [TestFixture]
    public class SessionSmokeTest
    {
        [Test]
        public void RunSmokeTest()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Session Smoke Test ===");

            try
            {
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
                Assert.Fail(ex.Message);
            }

            Debug.Log(sb.ToString());
        }

        private static void TestSessionCreation(StringBuilder sb)
        {
            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;

                var session = sessionManager.CreateSession("SmokeTestSession");
                Assert.That(string.IsNullOrEmpty(session.Id), Is.False, "Session ID should not be empty");
                Assert.That(session.Name, Is.EqualTo("SmokeTestSession"), "Session name incorrect");
                sb.AppendLine("✅ Session creation OK");
            }
            finally
            {
                store.Dispose();
            }
        }

        private static void TestDatasetOperations(StringBuilder sb)
        {
            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;
                var session = sessionManager.CreateSession("DatasetSmokeTest");

                var dataset = session.CreateDataset("SmokeTestDataset", DataSetKind.Tabular);
                Assert.That(dataset.Name, Is.EqualTo("SmokeTestDataset"), "Dataset creation failed");

                Assert.That(session.HasDataset("SmokeTestDataset"), Is.True, "Dataset existence check failed");

                var retrieved = session.GetDataset("SmokeTestDataset");
                Assert.That(retrieved.Name, Is.EqualTo("SmokeTestDataset"), "Dataset retrieval failed");
                sb.AppendLine("✅ Dataset operations OK");
            }
            finally
            {
                store.Dispose();
            }
        }

        private static void TestSessionManager(StringBuilder sb)
        {
            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;

                var session1 = sessionManager.CreateSession("SmokeTest1");
                var session2 = sessionManager.CreateSession("SmokeTest2");

                Assert.That(sessionManager.SessionIds.Count, Is.EqualTo(2), "Session count incorrect");

                var stats = sessionManager.GetStatistics();
                Assert.That(stats.TotalSessions, Is.EqualTo(2), "Session statistics incorrect");
                sb.AppendLine("✅ Session manager OK");
            }
            finally
            {
                store.Dispose();
            }
        }

        private static void TestSessionCleanup(StringBuilder sb)
        {
            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;

                var session = sessionManager.CreateSession("CleanupSmokeTest");

                Assert.That(sessionManager.CloseSession(session.Id), Is.True, "Session close failed");

                Assert.That(sessionManager.SessionIds.Count, Is.EqualTo(0), "Session cleanup failed");
                sb.AppendLine("✅ Session cleanup OK");
            }
            finally
            {
                store.Dispose();
            }
        }
    }
}
