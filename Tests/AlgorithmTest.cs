using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using AroAro.DataCore.Algorithms;
using AroAro.DataCore.Algorithms.Graph;
using AroAro.DataCore.Algorithms.Tabular;
using AroAro.DataCore.Graph;
using AroAro.DataCore.Tabular;
using AroAro.DataCore.Events;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// Algorithm framework tests — PageRank, ConnectedComponents,
    /// MinMaxNormalize, Pipeline, Registry, and event integration.
    /// </summary>
    public class AlgorithmTest : MonoBehaviour
    {
        [Header("Test Configuration")]
        [Tooltip("Run tests automatically on Start")]
        [SerializeField] private bool runOnStart = true;

        private int _passed;
        private int _failed;

        void Start()
        {
            if (runOnStart)
                RunAllTests();
        }

        [ContextMenu("Run Algorithm Tests")]
        public void RunAllTests()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("═══════════════════════════════════════════════");
            Debug.Log("  Algorithm Framework Tests");
            Debug.Log("═══════════════════════════════════════════════");

            RunTest("Registry: built-in algorithms registered", TestRegistryBuiltIns);
            RunTest("Registry: lookup by name", TestRegistryLookup);
            RunTest("Registry: filter by kind", TestRegistryFilterByKind);

            RunTest("PageRank: simple triangle graph", TestPageRankTriangle);
            RunTest("PageRank: star graph (hub node)", TestPageRankStar);
            RunTest("PageRank: empty graph", TestPageRankEmpty);
            RunTest("PageRank: custom parameters", TestPageRankCustomParams);

            RunTest("ConnectedComponents: two components", TestCCTwoComponents);
            RunTest("ConnectedComponents: single component", TestCCSingleComponent);
            RunTest("ConnectedComponents: isolated nodes", TestCCIsolatedNodes);

            RunTest("MinMaxNormalize: basic normalize", TestMinMaxBasic);
            RunTest("MinMaxNormalize: custom range", TestMinMaxCustomRange);
            RunTest("MinMaxNormalize: specific columns", TestMinMaxSpecificColumns);
            RunTest("MinMaxNormalize: constant column", TestMinMaxConstantColumn);

            RunTest("Pipeline: PageRank → ConnectedComponents", TestPipelineGraphChain);
            RunTest("Pipeline: stops on failure", TestPipelineStopsOnFailure);

            RunTest("Validation: wrong dataset kind", TestValidationWrongKind);
            RunTest("Validation: missing required param", TestValidationMissingParam);

            RunTest("Events: algorithm started/completed", TestEventsFireCorrectly);

            Debug.Log("═══════════════════════════════════════════════");
            Debug.Log($"  Results: {_passed} passed, {_failed} failed, {_passed + _failed} total");
            Debug.Log("═══════════════════════════════════════════════");
        }

        #region Registry Tests

        void TestRegistryBuiltIns()
        {
            var registry = AlgorithmRegistry.Default;
            Assert(registry.Count >= 3, $"Expected at least 3 built-in algorithms, got {registry.Count}");
            Assert(registry.Contains("PageRank"), "PageRank should be registered");
            Assert(registry.Contains("ConnectedComponents"), "ConnectedComponents should be registered");
            Assert(registry.Contains("MinMaxNormalize"), "MinMaxNormalize should be registered");
        }

        void TestRegistryLookup()
        {
            var algo = AlgorithmRegistry.Default.Get("PageRank");
            Assert(algo != null, "Should find PageRank");
            Assert(algo.Kind == AlgorithmKind.Graph, "PageRank should be a Graph algorithm");
            Assert(algo.Parameters.Count == 3, $"PageRank should have 3 parameters, got {algo.Parameters.Count}");
        }

        void TestRegistryFilterByKind()
        {
            var graphAlgos = AlgorithmRegistry.Default.GetByKind(AlgorithmKind.Graph);
            var tabularAlgos = AlgorithmRegistry.Default.GetByKind(AlgorithmKind.Tabular);

            Assert(graphAlgos.Count >= 2, $"Expected ≥2 graph algorithms, got {graphAlgos.Count}");
            Assert(tabularAlgos.Count >= 1, $"Expected ≥1 tabular algorithms, got {tabularAlgos.Count}");
            Assert(graphAlgos.All(a => a.Kind == AlgorithmKind.Graph || a.Kind == AlgorithmKind.Any),
                "All returned algorithms should be Graph or Any kind");
        }

        #endregion

        #region PageRank Tests

        void TestPageRankTriangle()
        {
            // A→B→C→A (cycle — all nodes should have similar rank)
            var graph = new GraphData("triangle");
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
            graph.AddEdge("A", "B"); graph.AddEdge("B", "C"); graph.AddEdge("C", "A");

            var algo = new PageRankAlgorithm();
            var result = algo.Execute(graph, AlgorithmContext.Empty);

            Assert(result.Success, $"Should succeed: {result.Error}");
            Assert(result.OutputDataset != null, "Should produce output graph");
            Assert((bool)result.Metrics["converged"], "Should converge");

            var output = result.OutputDataset as IGraphDataset;
            Assert(output.NodeCount == 3, "Output should have 3 nodes");

            // All nodes in a cycle should have roughly equal rank
            double rankA = (double)output.GetNodeProperties("A")["pagerank"];
            double rankB = (double)output.GetNodeProperties("B")["pagerank"];
            double rankC = (double)output.GetNodeProperties("C")["pagerank"];
            Assert(Math.Abs(rankA - rankB) < 0.01, $"A ({rankA:F4}) and B ({rankB:F4}) should be similar");
            Assert(Math.Abs(rankB - rankC) < 0.01, $"B ({rankB:F4}) and C ({rankC:F4}) should be similar");

            Debug.Log($"  PageRank scores: A={rankA:F4}, B={rankB:F4}, C={rankC:F4}");
        }

        void TestPageRankStar()
        {
            // Hub: A←B, A←C, A←D, A←E (A should have highest rank)
            var graph = new GraphData("star");
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
            graph.AddNode("D"); graph.AddNode("E");
            graph.AddEdge("B", "A"); graph.AddEdge("C", "A");
            graph.AddEdge("D", "A"); graph.AddEdge("E", "A");

            var result = new PageRankAlgorithm().Execute(graph, AlgorithmContext.Empty);
            Assert(result.Success, $"Should succeed: {result.Error}");

            var output = result.OutputDataset as IGraphDataset;
            double rankA = (double)output.GetNodeProperties("A")["pagerank"];
            double rankB = (double)output.GetNodeProperties("B")["pagerank"];
            Assert(rankA > rankB, $"Hub A ({rankA:F4}) should rank higher than leaf B ({rankB:F4})");

            Debug.Log($"  Hub rank: {rankA:F4}, Leaf rank: {rankB:F4}");
        }

        void TestPageRankEmpty()
        {
            var graph = new GraphData("empty");
            var result = new PageRankAlgorithm().Execute(graph, AlgorithmContext.Empty);

            Assert(result.Success, "Should succeed on empty graph");
            Assert(result.OutputDataset == null, "Empty graph should produce metrics-only result");
            Assert((int)result.Metrics["iterations"] == 0, "Should report 0 iterations");
        }

        void TestPageRankCustomParams()
        {
            var graph = new GraphData("custom");
            graph.AddNode("A"); graph.AddNode("B");
            graph.AddEdge("A", "B"); graph.AddEdge("B", "A");

            var ctx = AlgorithmContext.Create()
                .WithParameter("dampingFactor", 0.5)
                .WithParameter("maxIterations", 10)
                .WithParameter("tolerance", 1e-3)
                .Build();

            var result = new PageRankAlgorithm().Execute(graph, ctx);
            Assert(result.Success, $"Should succeed with custom params: {result.Error}");

            int iterations = (int)result.Metrics["iterations"];
            Assert(iterations <= 10, $"Should respect maxIterations cap, got {iterations}");
        }

        #endregion

        #region ConnectedComponents Tests

        void TestCCTwoComponents()
        {
            var graph = new GraphData("two-components");
            // Component 1: A—B—C
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
            graph.AddEdge("A", "B"); graph.AddEdge("B", "C");
            // Component 2: X—Y
            graph.AddNode("X"); graph.AddNode("Y");
            graph.AddEdge("X", "Y");

            var result = new ConnectedComponentsAlgorithm().Execute(graph, AlgorithmContext.Empty);
            Assert(result.Success, $"Should succeed: {result.Error}");

            int componentCount = (int)result.Metrics["componentCount"];
            Assert(componentCount == 2, $"Should find 2 components, got {componentCount}");

            int largestSize = (int)result.Metrics["largestComponentSize"];
            Assert(largestSize == 3, $"Largest component should have 3 nodes, got {largestSize}");

            // Verify A,B,C share same component and X,Y share different component
            var output = result.OutputDataset as IGraphDataset;
            int cidA = (int)output.GetNodeProperties("A")["componentId"];
            int cidB = (int)output.GetNodeProperties("B")["componentId"];
            int cidX = (int)output.GetNodeProperties("X")["componentId"];
            Assert(cidA == cidB, "A and B should be in same component");
            Assert(cidA != cidX, "A and X should be in different components");

            Debug.Log($"  Components: {componentCount}, largest: {largestSize}");
        }

        void TestCCSingleComponent()
        {
            var graph = new GraphData("connected");
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
            graph.AddEdge("A", "B"); graph.AddEdge("B", "C"); graph.AddEdge("C", "A");

            var result = new ConnectedComponentsAlgorithm().Execute(graph, AlgorithmContext.Empty);
            Assert(result.Success, $"Should succeed: {result.Error}");
            Assert((int)result.Metrics["componentCount"] == 1, "Fully connected graph should have 1 component");
        }

        void TestCCIsolatedNodes()
        {
            var graph = new GraphData("isolated");
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
            // No edges

            var result = new ConnectedComponentsAlgorithm().Execute(graph, AlgorithmContext.Empty);
            Assert(result.Success, $"Should succeed: {result.Error}");
            Assert((int)result.Metrics["componentCount"] == 3, "3 isolated nodes should give 3 components");
        }

        #endregion

        #region MinMaxNormalize Tests

        void TestMinMaxBasic()
        {
            var table = new TabularData("test-table");
            table.AddNumericColumn("values", new double[] { 10, 20, 30, 40, 50 });
            table.AddStringColumn("labels", new string[] { "a", "b", "c", "d", "e" });

            var result = new MinMaxNormalizeAlgorithm().Execute(table, AlgorithmContext.Empty);
            Assert(result.Success, $"Should succeed: {result.Error}");

            var output = result.OutputDataset as ITabularDataset;
            Assert(output != null, "Should produce output tabular dataset");
            Assert(output.ColumnCount == 2, "Should preserve all columns");

            var normalized = output.GetNumericColumn("values").ToArray<double>();
            Assert(Math.Abs(normalized[0] - 0.0) < 1e-10, $"Min should be 0, got {normalized[0]}");
            Assert(Math.Abs(normalized[4] - 1.0) < 1e-10, $"Max should be 1, got {normalized[4]}");
            Assert(Math.Abs(normalized[2] - 0.5) < 1e-10, $"Middle should be 0.5, got {normalized[2]}");

            // String column should be preserved
            var labels = output.GetStringColumn("labels");
            Assert(labels[0] == "a", "String column should be preserved");

            Debug.Log($"  Normalized: [{string.Join(", ", normalized.Select(v => v.ToString("F2")))}]");
        }

        void TestMinMaxCustomRange()
        {
            var table = new TabularData("custom-range");
            table.AddNumericColumn("x", new double[] { 0, 50, 100 });

            var ctx = AlgorithmContext.Create()
                .WithParameter("rangeMin", -1.0)
                .WithParameter("rangeMax", 1.0)
                .Build();

            var result = new MinMaxNormalizeAlgorithm().Execute(table, ctx);
            Assert(result.Success, $"Should succeed: {result.Error}");

            var output = result.OutputDataset as ITabularDataset;
            var values = output.GetNumericColumn("x").ToArray<double>();
            Assert(Math.Abs(values[0] - (-1.0)) < 1e-10, $"Min should be -1, got {values[0]}");
            Assert(Math.Abs(values[2] - 1.0) < 1e-10, $"Max should be 1, got {values[2]}");
            Assert(Math.Abs(values[1] - 0.0) < 1e-10, $"Middle should be 0, got {values[1]}");
        }

        void TestMinMaxSpecificColumns()
        {
            var table = new TabularData("selective");
            table.AddNumericColumn("normalize_me", new double[] { 100, 200, 300 });
            table.AddNumericColumn("leave_me", new double[] { 5, 10, 15 });

            var ctx = AlgorithmContext.Create()
                .WithParameter("columns", new string[] { "normalize_me" })
                .Build();

            var result = new MinMaxNormalizeAlgorithm().Execute(table, ctx);
            Assert(result.Success, $"Should succeed: {result.Error}");

            var output = result.OutputDataset as ITabularDataset;
            var normalized = output.GetNumericColumn("normalize_me").ToArray<double>();
            var untouched = output.GetNumericColumn("leave_me").ToArray<double>();

            Assert(Math.Abs(normalized[0] - 0.0) < 1e-10, "Targeted column should be normalized");
            Assert(Math.Abs(untouched[0] - 5.0) < 1e-10, $"Non-targeted column should be unchanged, got {untouched[0]}");
            Assert((int)result.Metrics["columnsNormalized"] == 1, "Should report 1 column normalized");
        }

        void TestMinMaxConstantColumn()
        {
            var table = new TabularData("constant");
            table.AddNumericColumn("flat", new double[] { 42, 42, 42 });

            var result = new MinMaxNormalizeAlgorithm().Execute(table, AlgorithmContext.Empty);
            Assert(result.Success, $"Should succeed: {result.Error}");

            var output = result.OutputDataset as ITabularDataset;
            var values = output.GetNumericColumn("flat").ToArray<double>();
            // Constant column should map to rangeMin
            Assert(Math.Abs(values[0] - 0.0) < 1e-10, "Constant value should map to rangeMin");
        }

        #endregion

        #region Pipeline Tests

        void TestPipelineGraphChain()
        {
            var graph = new GraphData("pipeline-graph");
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
            graph.AddEdge("A", "B"); graph.AddEdge("B", "C"); graph.AddEdge("C", "A");

            var pipeline = new AlgorithmPipeline("GraphAnalysis")
                .Add(new PageRankAlgorithm())
                .Add(new ConnectedComponentsAlgorithm());

            var result = pipeline.Execute(graph);
            Assert(result.Success, $"Pipeline should succeed: {result.Error}");
            Assert(result.StepResults.Count == 2, "Should have 2 step results");
            Assert(result.FinalOutput != null, "Should have final output");

            // Final output should have both pagerank and componentId properties
            var finalGraph = result.FinalOutput as IGraphDataset;
            Assert(finalGraph != null, "Final output should be a graph");

            var propsA = finalGraph.GetNodeProperties("A");
            Assert(propsA.ContainsKey("pagerank"), "Node A should have pagerank from step 1");
            Assert(propsA.ContainsKey("componentId"), "Node A should have componentId from step 2");

            Debug.Log($"  Pipeline completed in {result.TotalDuration.TotalMilliseconds:F1}ms");

            var allMetrics = result.GetAllMetrics();
            Debug.Log($"  Aggregated metrics: {allMetrics.Count} entries");
        }

        void TestPipelineStopsOnFailure()
        {
            var table = new TabularData("wrong-type");
            table.AddNumericColumn("x", new double[] { 1, 2, 3 });

            // PageRank expects a graph, so it should fail on a tabular dataset
            var pipeline = new AlgorithmPipeline("WillFail")
                .Add(new PageRankAlgorithm())
                .Add(new ConnectedComponentsAlgorithm());

            var result = pipeline.Execute(table);
            Assert(!result.Success, "Pipeline should fail when first step gets wrong type");
            Assert(result.FailedStepIndex == 0, $"Should fail at step 0, got {result.FailedStepIndex}");
            Assert(result.StepResults.Count == 1, "Should only have 1 result (failed step)");
        }

        #endregion

        #region Validation Tests

        void TestValidationWrongKind()
        {
            var table = new TabularData("not-a-graph");
            table.AddNumericColumn("x", new double[] { 1, 2, 3 });

            var result = new PageRankAlgorithm().Execute(table, AlgorithmContext.Empty);
            Assert(!result.Success, "PageRank on tabular should fail");
            Assert(result.Error.Contains("not compatible"), $"Error should mention incompatibility: {result.Error}");
        }

        void TestValidationMissingParam()
        {
            // Create a test algorithm that has a required parameter
            var algo = new RequiredParamTestAlgorithm();
            var graph = new GraphData("test");
            graph.AddNode("A");

            var result = algo.Execute(graph, AlgorithmContext.Empty);
            Assert(!result.Success, "Should fail with missing required param");
            Assert(result.Error.Contains("requiredParam"), $"Error should mention the param: {result.Error}");
        }

        #endregion

        #region Event Tests

        void TestEventsFireCorrectly()
        {
            bool startedFired = false;
            bool completedFired = false;
            string capturedAlgoName = null;

            DataCoreEventManager.AlgorithmStarted += (s, e) =>
            {
                startedFired = true;
                capturedAlgoName = e.AlgorithmName;
            };

            DataCoreEventManager.AlgorithmCompleted += (s, e) =>
            {
                completedFired = true;
                Assert(e.Success, "Completed event should report success");
                Assert(e.Duration.TotalMilliseconds >= 0, "Duration should be non-negative");
            };

            var graph = new GraphData("event-test");
            graph.AddNode("A"); graph.AddNode("B");
            graph.AddEdge("A", "B");

            new PageRankAlgorithm().Execute(graph, AlgorithmContext.Empty);

            Assert(startedFired, "AlgorithmStarted event should have fired");
            Assert(completedFired, "AlgorithmCompleted event should have fired");
            Assert(capturedAlgoName == "PageRank", $"Event should capture algo name, got '{capturedAlgoName}'");

            // Clean up subscriptions
            DataCoreEventManager.ClearAllSubscriptions();
        }

        #endregion

        #region Helpers

        private void RunTest(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Debug.Log($"✅ {name}");
            }
            catch (Exception ex)
            {
                _failed++;
                Debug.LogError($"❌ {name}\n   {ex.Message}\n   {ex.StackTrace}");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception($"Assertion failed: {message}");
        }

        /// <summary>Test helper: algorithm with a required parameter.</summary>
        private class RequiredParamTestAlgorithm : GraphAlgorithmBase
        {
            public override string Name => "RequiredParamTest";
            public override string Description => "Test algorithm with required parameter";

            public override IReadOnlyList<AlgorithmParameterDescriptor> Parameters { get; } =
                new List<AlgorithmParameterDescriptor>
                {
                    new("requiredParam", "A required parameter", typeof(string), required: true),
                };

            protected override AlgorithmResult ExecuteGraph(IGraphDataset input, AlgorithmContext context)
            {
                return AlgorithmResult.MetricsOnly(Name, new Dictionary<string, object>());
            }
        }

        #endregion
    }
}
