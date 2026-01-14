using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AroAro.DataCore
{
    /// <summary>
    /// 挂到 GameObject 上，进 Play 模式自动测试 DataCore 功能
    /// </summary>
    public class DataCoreSelfTest : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool logToConsole = true;

        private void Start()
        {
            if (runOnStart)
                RunTests();
        }

        public void RunTests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== DataCore Self-Test ===");

            try
            {
                TestTabular(sb);
                TestGraph(sb);
                TestSessions(sb);
                sb.AppendLine("✅ All tests passed!");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Test failed: {ex.Message}");
                sb.AppendLine($"Stack trace: {ex.StackTrace}");
            }

            var result = sb.ToString();
            if (logToConsole)
                Debug.Log(result);
        }

        private static void TestTabular(StringBuilder sb)
        {
            sb.AppendLine("Testing Tabular...");
            using var store = new DataCoreStore();
            var t = store.CreateTabular("test-table");

            // 添加列
            t.AddNumericColumn("x", new double[] { 1, 2, 3 });
            t.AddStringColumn("s", new[] { "a", "b", "c" });

            if (t.RowCount != 3) throw new Exception($"Expected 3 rows, got {t.RowCount}");
            if (t.ColumnCount != 2) throw new Exception($"Expected 2 columns, got {t.ColumnCount}");

            // 查询
            var results = t.Query()
                .WhereGreaterThan("x", 1.5)
                .ToDictionaries()
                .ToList();

            if (results.Count != 2) throw new Exception($"Expected 2 rows from query, got {results.Count}");

            sb.AppendLine("✅ Tabular CRUD/Query OK");
            
            // 清理
            store.Delete("test-table");
        }

        private static void TestGraph(StringBuilder sb)
        {
            sb.AppendLine("Testing Graph...");
            using var store = new DataCoreStore();
            var g = store.CreateGraph("test-graph");

            g.AddNode("a", new System.Collections.Generic.Dictionary<string, object> { ["type"] = "root" });
            g.AddNode("b");
            g.AddEdge("a", "b");

            if (g.NodeCount != 2) throw new Exception($"Expected 2 nodes, got {g.NodeCount}");
            if (g.EdgeCount != 1) throw new Exception($"Expected 1 edge, got {g.EdgeCount}");

            var neighbors = g.GetNeighbors("a").ToList();
            if (neighbors.Count != 1 || neighbors[0] != "b") throw new Exception("Graph neighbors query failed");

            sb.AppendLine("✅ Graph CRUD/Query OK");
            
            // 清理
            store.Delete("test-graph");
        }

        private static void TestSessions(StringBuilder sb)
        {
            sb.AppendLine("Testing Sessions...");
            using var store = new DataCoreStore();
            var sessionManager = store.SessionManager;

            // 测试会话创建
            var session1 = sessionManager.CreateSession("TestSession1");
            if (session1.Name != "TestSession1") throw new Exception("Session creation failed");
            sb.AppendLine("✅ Session creation OK");

            // 测试在会话中创建数据集
            var dataset = session1.CreateDataset("SessionDataset", DataSetKind.Tabular);
            if (dataset.Name != "SessionDataset") throw new Exception("Session dataset creation failed");
            sb.AppendLine("✅ Session dataset creation OK");

            // 测试会话数据集计数
            if (session1.DatasetCount != 1) throw new Exception($"Expected 1 dataset, got {session1.DatasetCount}");
            sb.AppendLine("✅ Session dataset count OK");

            // 测试会话数据集名称
            var datasetNames = session1.DatasetNames;
            if (datasetNames.Count != 1 || !datasetNames.Any(name => string.Equals(name, "SessionDataset", StringComparison.Ordinal))) 
                throw new Exception("Session dataset names incorrect");
            sb.AppendLine("✅ Session dataset names OK");

            // 测试会话数据集获取
            var retrievedDataset = session1.GetDataset("SessionDataset");
            if (retrievedDataset.Name != "SessionDataset") throw new Exception("Session dataset retrieval failed");
            sb.AppendLine("✅ Session dataset retrieval OK");

            // 测试会话数据集存在检查
            if (!session1.HasDataset("SessionDataset")) throw new Exception("Session dataset existence check failed");
            sb.AppendLine("✅ Session dataset existence check OK");

            // 测试会话管理器统计
            var stats = sessionManager.GetStatistics();
            if (stats.TotalSessions != 1) throw new Exception($"Expected 1 session, got {stats.TotalSessions}");
            sb.AppendLine("✅ Session manager statistics OK");

            // 测试多个会话
            var session2 = sessionManager.CreateSession("TestSession2");
            if (sessionManager.SessionIds.Count != 2) throw new Exception("Multiple sessions failed");
            sb.AppendLine("✅ Multiple sessions OK");

            // 测试会话关闭
            if (!sessionManager.CloseSession(session1.Id)) throw new Exception("Session close failed");
            sb.AppendLine("✅ Session close OK");

            // 测试会话清理
            System.Threading.Thread.Sleep(10); // 确保有一点时间间隔
            var cleanupCount = sessionManager.CleanupIdleSessions(TimeSpan.FromMilliseconds(1));
            if (cleanupCount < 1) throw new Exception($"Expected at least 1 session cleaned up, got {cleanupCount}");
            sb.AppendLine("✅ Session cleanup OK");

            sb.AppendLine("✅ Sessions OK");
        }
    }
}
