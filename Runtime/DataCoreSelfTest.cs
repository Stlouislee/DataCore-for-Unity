using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using AroAro.DataCore.Algorithms;
using AroAro.DataCore.Algorithms.Graph;
using AroAro.DataCore.Algorithms.Tabular;
using AroAro.DataCore.Graph;
using AroAro.DataCore.Tabular;

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
                TestAlgorithms(sb);
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

        /// <summary>
        /// Create a temporary DataCoreStore for testing to avoid sharing violations
        /// with any already-open database (e.g. from DataCoreEditorComponent).
        /// </summary>
        private static DataCoreStore CreateTestStore()
        {
            var tempPath = System.IO.Path.Combine(
                Application.temporaryCachePath, "DataCore", $"selftest_{System.Diagnostics.Process.GetCurrentProcess().Id}.db");
            return new DataCoreStore(tempPath);
        }

        private static void TestTabular(StringBuilder sb)
        {
            sb.AppendLine("Testing Tabular...");
            using var store = CreateTestStore();
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
            using var store = CreateTestStore();
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
            using var store = CreateTestStore();
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

        private static void TestAlgorithms(StringBuilder sb)
        {
            sb.AppendLine("Testing Algorithms...");

            // --- Registry ---
            var registry = AlgorithmRegistry.Default;
            if (registry.Count < 3)
                throw new Exception($"Expected at least 3 registered algorithms, got {registry.Count}");
            if (!registry.Contains("PageRank"))
                throw new Exception("PageRank not registered");
            if (!registry.Contains("ConnectedComponents"))
                throw new Exception("ConnectedComponents not registered");
            if (!registry.Contains("MinMaxNormalize"))
                throw new Exception("MinMaxNormalize not registered");
            sb.AppendLine("✅ Algorithm registry OK");

            // --- PageRank ---
            {
                var graph = new GraphData("pr-test");
                graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
                graph.AddEdge("A", "B"); graph.AddEdge("B", "C"); graph.AddEdge("C", "A");

                var result = new PageRankAlgorithm().Execute(graph, AlgorithmContext.Empty);
                if (!result.Success) throw new Exception($"PageRank failed: {result.Error}");
                if (result.OutputDataset == null) throw new Exception("PageRank produced no output");

                var output = result.OutputDataset as IGraphDataset;
                double rankA = (double)output.GetNodeProperties("A")["pagerank"];
                double rankB = (double)output.GetNodeProperties("B")["pagerank"];
                if (Math.Abs(rankA - rankB) > 0.01)
                    throw new Exception($"Cycle nodes should have similar rank: A={rankA:F4}, B={rankB:F4}");
            }
            sb.AppendLine("✅ PageRank OK");

            // --- ConnectedComponents ---
            {
                var graph = new GraphData("cc-test");
                graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
                graph.AddEdge("A", "B"); graph.AddEdge("B", "C");
                graph.AddNode("X"); graph.AddNode("Y");
                graph.AddEdge("X", "Y");

                var result = new ConnectedComponentsAlgorithm().Execute(graph, AlgorithmContext.Empty);
                if (!result.Success) throw new Exception($"ConnectedComponents failed: {result.Error}");

                int count = (int)result.Metrics["componentCount"];
                if (count != 2) throw new Exception($"Expected 2 components, got {count}");

                var output = result.OutputDataset as IGraphDataset;
                int cidA = (int)output.GetNodeProperties("A")["componentId"];
                int cidX = (int)output.GetNodeProperties("X")["componentId"];
                if (cidA == cidX) throw new Exception("Nodes in different components should have different IDs");
            }
            sb.AppendLine("✅ ConnectedComponents OK");

            // --- MinMaxNormalize ---
            {
                var table = new TabularData("norm-test");
                table.AddNumericColumn("values", new double[] { 10, 20, 30, 40, 50 });
                table.AddStringColumn("labels", new[] { "a", "b", "c", "d", "e" });

                var result = new MinMaxNormalizeAlgorithm().Execute(table, AlgorithmContext.Empty);
                if (!result.Success) throw new Exception($"MinMaxNormalize failed: {result.Error}");

                var output = result.OutputDataset as ITabularDataset;
                var normalized = output.GetNumericColumn("values").ToArray<double>();
                if (Math.Abs(normalized[0]) > 1e-10) throw new Exception($"Min should be 0, got {normalized[0]}");
                if (Math.Abs(normalized[4] - 1.0) > 1e-10) throw new Exception($"Max should be 1, got {normalized[4]}");
                if (output.GetStringColumn("labels")[0] != "a") throw new Exception("String column not preserved");
            }
            sb.AppendLine("✅ MinMaxNormalize OK");

            // --- Pipeline ---
            {
                var graph = new GraphData("pipe-test");
                graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
                graph.AddEdge("A", "B"); graph.AddEdge("B", "C"); graph.AddEdge("C", "A");

                var pipeline = new AlgorithmPipeline("SelfTestPipeline")
                    .Add(new PageRankAlgorithm())
                    .Add(new ConnectedComponentsAlgorithm());

                var result = pipeline.Execute(graph);
                if (!result.Success) throw new Exception($"Pipeline failed: {result.Error}");
                if (result.StepResults.Count != 2) throw new Exception($"Expected 2 steps, got {result.StepResults.Count}");

                var finalGraph = result.FinalOutput as IGraphDataset;
                if (finalGraph == null) throw new Exception("Pipeline should produce graph output");

                var props = finalGraph.GetNodeProperties("A");
                if (!props.ContainsKey("pagerank")) throw new Exception("Missing pagerank from pipeline step 1");
                if (!props.ContainsKey("componentId")) throw new Exception("Missing componentId from pipeline step 2");
            }
            sb.AppendLine("✅ Algorithm pipeline OK");

            // --- Validation: wrong dataset kind ---
            {
                var table = new TabularData("wrong-kind");
                table.AddNumericColumn("x", new double[] { 1, 2, 3 });

                var result = new PageRankAlgorithm().Execute(table, AlgorithmContext.Empty);
                if (result.Success) throw new Exception("PageRank on tabular should fail");
                if (!result.Error.Contains("not compatible"))
                    throw new Exception($"Error should mention incompatibility: {result.Error}");
            }
            sb.AppendLine("✅ Algorithm validation OK");

            sb.AppendLine("✅ Algorithms OK");
        }
    }
}
