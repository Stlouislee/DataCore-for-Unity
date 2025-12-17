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
                TestPersistence(sb);
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
            var store = new DataCoreStore();
            var t = store.CreateTabular("test-table");

            // 添加列
            t.AddNumericColumn("x", NumSharp.np.array(new double[] { 1, 2, 3 }));
            t.AddStringColumn("s", new[] { "a", "b", "c" });

            // 查询
            var idx = t.Query().Where("x", Tabular.TabularOp.Gt, 1).ToRowIndices();
            if (idx.Length != 2) throw new Exception($"Expected 2 rows, got {idx.Length}");

            sb.AppendLine("✅ Tabular CRUD/Query OK");
        }

        private static void TestGraph(StringBuilder sb)
        {
            sb.AppendLine("Testing Graph...");
            var store = new DataCoreStore();
            var g = store.CreateGraph("test-graph");

            g.AddNode("a", new System.Collections.Generic.Dictionary<string, string> { ["type"] = "root" });
            g.AddNode("b");
            g.AddEdge("a", "b");

            var nodes = g.Query().WhereNodePropertyEquals("type", "root").ToNodeIds();
            if (nodes.Length != 1 || nodes[0] != "a") throw new Exception("Graph query failed");

            sb.AppendLine("✅ Graph CRUD/Query OK");
        }

        private static void TestPersistence(StringBuilder sb)
        {
            sb.AppendLine("Testing Persistence...");
            var store = new DataCoreStore();

            // Tabular persistence
            var t = store.CreateTabular("persist-table");
            t.AddNumericColumn("val", NumSharp.np.array(new double[] { 10, 20 }));
            store.Save("persist-table", "test.arrow");

            var loaded = store.Load("test.arrow", registerAsName: "loaded-table");
            if (loaded.Name != "loaded-table") throw new Exception("Tabular load failed");

            // Graph persistence
            var g = store.CreateGraph("persist-graph");
            g.AddNode("n1");
            store.Save("persist-graph", "test.dcgraph");

            var loadedGraph = store.Load("test.dcgraph", registerAsName: "loaded-graph");
            if (loadedGraph.Name != "loaded-graph") throw new Exception("Graph load failed");

            sb.AppendLine("✅ Persistence OK");
        }

        private static void TestSessions(StringBuilder sb)
        {
            sb.AppendLine("Testing Sessions...");
            var store = new DataCoreStore();
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
            var cleanupCount = sessionManager.CleanupIdleSessions(TimeSpan.FromMilliseconds(1));
            if (cleanupCount != 1) throw new Exception($"Expected 1 session cleaned up, got {cleanupCount}");
            sb.AppendLine("✅ Session cleanup OK");

            sb.AppendLine("✅ Sessions OK");
        }
    }
}