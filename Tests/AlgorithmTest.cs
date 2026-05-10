using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
    /// Migrated from MonoBehaviour to NUnit for CI/CD compatibility.
    /// </summary>
    [TestFixture]
    public class AlgorithmTest
    {
        #region Registry Tests

        [Test]
        public void Registry_BuiltInsRegistered()
        {
            var registry = AlgorithmRegistry.Default;
            Assert.That(registry.Count, Is.GreaterThanOrEqualTo(3), "Expected at least 3 built-in algorithms");
            Assert.That(registry.Contains("PageRank"), Is.True, "PageRank should be registered");
            Assert.That(registry.Contains("ConnectedComponents"), Is.True, "ConnectedComponents should be registered");
            Assert.That(registry.Contains("MinMaxNormalize"), Is.True, "MinMaxNormalize should be registered");
        }

        [Test]
        public void Registry_LookupByName()
        {
            var algo = AlgorithmRegistry.Default.Get("PageRank");
            Assert.That(algo, Is.Not.Null, "Should find PageRank");
            Assert.That(algo.Kind, Is.EqualTo(AlgorithmKind.Graph), "PageRank should be a Graph algorithm");
            Assert.That(algo.Parameters.Count, Is.EqualTo(3), "PageRank should have 3 parameters");
        }

        [Test]
        public void Registry_FilterByKind()
        {
            var graphAlgos = AlgorithmRegistry.Default.GetByKind(AlgorithmKind.Graph);
            var tabularAlgos = AlgorithmRegistry.Default.GetByKind(AlgorithmKind.Tabular);

            Assert.That(graphAlgos.Count, Is.GreaterThanOrEqualTo(2), "Expected ≥2 graph algorithms");
            Assert.That(tabularAlgos.Count, Is.GreaterThanOrEqualTo(1), "Expected ≥1 tabular algorithms");
            Assert.That(graphAlgos.All(a => a.Kind == AlgorithmKind.Graph || a.Kind == AlgorithmKind.Any),
                Is.True, "All returned algorithms should be Graph or Any kind");
        }

        #endregion

        #region PageRank Tests

        [Test]
        public void PageRank_Triangle_AllNodesEqualRank()
        {
            var graph = new GraphData("triangle");
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
            graph.AddEdge("A", "B"); graph.AddEdge("B", "C"); graph.AddEdge("C", "A");

            var algo = new PageRankAlgorithm();
            var result = algo.Execute(graph, AlgorithmContext.Empty);

            Assert.That(result.Success, Is.True, $"Should succeed: {result.Error}");
            Assert.That(result.OutputDataset, Is.Not.Null, "Should produce output graph");
            Assert.That((bool)result.Metrics["converged"], Is.True, "Should converge");

            var output = result.OutputDataset as IGraphDataset;
            Assert.That(output.NodeCount, Is.EqualTo(3), "Output should have 3 nodes");

            double rankA = (double)output.GetNodeProperties("A")["pagerank"];
            double rankB = (double)output.GetNodeProperties("B")["pagerank"];
            double rankC = (double)output.GetNodeProperties("C")["pagerank"];
            Assert.That(Math.Abs(rankA - rankB), Is.LessThan(0.01), $"A ({rankA:F4}) and B ({rankB:F4}) should be similar");
            Assert.That(Math.Abs(rankB - rankC), Is.LessThan(0.01), $"B ({rankB:F4}) and C ({rankC:F4}) should be similar");

            Debug.Log($"  PageRank scores: A={rankA:F4}, B={rankB:F4}, C={rankC:F4}");
        }

        [Test]
        public void PageRank_Star_HubHighestRank()
        {
            var graph = new GraphData("star");
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
            graph.AddNode("D"); graph.AddNode("E");
            graph.AddEdge("B", "A"); graph.AddEdge("C", "A");
            graph.AddEdge("D", "A"); graph.AddEdge("E", "A");

            var result = new PageRankAlgorithm().Execute(graph, AlgorithmContext.Empty);
            Assert.That(result.Success, Is.True, $"Should succeed: {result.Error}");

            var output = result.OutputDataset as IGraphDataset;
            double rankA = (double)output.GetNodeProperties("A")["pagerank"];
            double rankB = (double)output.GetNodeProperties("B")["pagerank"];
            Assert.That(rankA, Is.GreaterThan(rankB), $"Hub A ({rankA:F4}) should rank higher than leaf B ({rankB:F4})");

            Debug.Log($"  Hub rank: {rankA:F4}, Leaf rank: {rankB:F4}");
        }

        [Test]
        public void PageRank_Empty_NoOutput()
        {
            var graph = new GraphData("empty");
            var result = new PageRankAlgorithm().Execute(graph, AlgorithmContext.Empty);

            Assert.That(result.Success, Is.True, "Should succeed on empty graph");
            Assert.That(result.OutputDataset, Is.Null, "Empty graph should produce metrics-only result");
            Assert.That((int)result.Metrics["iterations"], Is.EqualTo(0), "Should report 0 iterations");
        }

        [Test]
        public void PageRank_CustomParams_RespectsMaxIterations()
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
            Assert.That(result.Success, Is.True, $"Should succeed with custom params: {result.Error}");

            int iterations = (int)result.Metrics["iterations"];
            Assert.That(iterations, Is.LessThanOrEqualTo(10), $"Should respect maxIterations cap, got {iterations}");
        }

        #endregion

        #region ConnectedComponents Tests

        [Test]
        public void CC_TwoComponents()
        {
            var graph = new GraphData("two-components");
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
            graph.AddEdge("A", "B"); graph.AddEdge("B", "C");
            graph.AddNode("X"); graph.AddNode("Y");
            graph.AddEdge("X", "Y");

            var result = new ConnectedComponentsAlgorithm().Execute(graph, AlgorithmContext.Empty);
            Assert.That(result.Success, Is.True, $"Should succeed: {result.Error}");

            int componentCount = (int)result.Metrics["componentCount"];
            Assert.That(componentCount, Is.EqualTo(2), $"Should find 2 components, got {componentCount}");

            int largestSize = (int)result.Metrics["largestComponentSize"];
            Assert.That(largestSize, Is.EqualTo(3), $"Largest component should have 3 nodes, got {largestSize}");

            var output = result.OutputDataset as IGraphDataset;
            int cidA = (int)output.GetNodeProperties("A")["componentId"];
            int cidB = (int)output.GetNodeProperties("B")["componentId"];
            int cidX = (int)output.GetNodeProperties("X")["componentId"];
            Assert.That(cidA, Is.EqualTo(cidB), "A and B should be in same component");
            Assert.That(cidA, Is.Not.EqualTo(cidX), "A and X should be in different components");

            Debug.Log($"  Components: {componentCount}, largest: {largestSize}");
        }

        [Test]
        public void CC_SingleComponent()
        {
            var graph = new GraphData("connected");
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
            graph.AddEdge("A", "B"); graph.AddEdge("B", "C"); graph.AddEdge("C", "A");

            var result = new ConnectedComponentsAlgorithm().Execute(graph, AlgorithmContext.Empty);
            Assert.That(result.Success, Is.True, $"Should succeed: {result.Error}");
            Assert.That((int)result.Metrics["componentCount"], Is.EqualTo(1), "Fully connected graph should have 1 component");
        }

        [Test]
        public void CC_IsolatedNodes()
        {
            var graph = new GraphData("isolated");
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");

            var result = new ConnectedComponentsAlgorithm().Execute(graph, AlgorithmContext.Empty);
            Assert.That(result.Success, Is.True, $"Should succeed: {result.Error}");
            Assert.That((int)result.Metrics["componentCount"], Is.EqualTo(3), "3 isolated nodes should give 3 components");
        }

        #endregion

        #region MinMaxNormalize Tests

        [Test]
        public void MinMax_Basic_Normalize()
        {
            var table = new TabularData("test-table");
            table.AddNumericColumn("values", new double[] { 10, 20, 30, 40, 50 });
            table.AddStringColumn("labels", new string[] { "a", "b", "c", "d", "e" });

            var result = new MinMaxNormalizeAlgorithm().Execute(table, AlgorithmContext.Empty);
            Assert.That(result.Success, Is.True, $"Should succeed: {result.Error}");

            var output = result.OutputDataset as ITabularDataset;
            Assert.That(output, Is.Not.Null, "Should produce output tabular dataset");
            Assert.That(output.ColumnCount, Is.EqualTo(2), "Should preserve all columns");

            var normalized = output.GetNumericColumn("values").ToArray<double>();
            Assert.That(Math.Abs(normalized[0] - 0.0), Is.LessThan(1e-10), $"Min should be 0, got {normalized[0]}");
            Assert.That(Math.Abs(normalized[4] - 1.0), Is.LessThan(1e-10), $"Max should be 1, got {normalized[4]}");
            Assert.That(Math.Abs(normalized[2] - 0.5), Is.LessThan(1e-10), $"Middle should be 0.5, got {normalized[2]}");

            var labels = output.GetStringColumn("labels");
            Assert.That(labels[0], Is.EqualTo("a"), "String column should be preserved");

            Debug.Log($"  Normalized: [{string.Join(", ", normalized.Select(v => v.ToString("F2")))}]");
        }

        [Test]
        public void MinMax_CustomRange()
        {
            var table = new TabularData("custom-range");
            table.AddNumericColumn("x", new double[] { 0, 50, 100 });

            var ctx = AlgorithmContext.Create()
                .WithParameter("rangeMin", -1.0)
                .WithParameter("rangeMax", 1.0)
                .Build();

            var result = new MinMaxNormalizeAlgorithm().Execute(table, ctx);
            Assert.That(result.Success, Is.True, $"Should succeed: {result.Error}");

            var output = result.OutputDataset as ITabularDataset;
            var values = output.GetNumericColumn("x").ToArray<double>();
            Assert.That(Math.Abs(values[0] - (-1.0)), Is.LessThan(1e-10), $"Min should be -1, got {values[0]}");
            Assert.That(Math.Abs(values[2] - 1.0), Is.LessThan(1e-10), $"Max should be 1, got {values[2]}");
            Assert.That(Math.Abs(values[1] - 0.0), Is.LessThan(1e-10), $"Middle should be 0, got {values[1]}");
        }

        [Test]
        public void MinMax_SpecificColumns()
        {
            var table = new TabularData("selective");
            table.AddNumericColumn("normalize_me", new double[] { 100, 200, 300 });
            table.AddNumericColumn("leave_me", new double[] { 5, 10, 15 });

            var ctx = AlgorithmContext.Create()
                .WithParameter("columns", new string[] { "normalize_me" })
                .Build();

            var result = new MinMaxNormalizeAlgorithm().Execute(table, ctx);
            Assert.That(result.Success, Is.True, $"Should succeed: {result.Error}");

            var output = result.OutputDataset as ITabularDataset;
            var normalized = output.GetNumericColumn("normalize_me").ToArray<double>();
            var untouched = output.GetNumericColumn("leave_me").ToArray<double>();

            Assert.That(Math.Abs(normalized[0] - 0.0), Is.LessThan(1e-10), "Targeted column should be normalized");
            Assert.That(Math.Abs(untouched[0] - 5.0), Is.LessThan(1e-10), $"Non-targeted column should be unchanged, got {untouched[0]}");
            Assert.That((int)result.Metrics["columnsNormalized"], Is.EqualTo(1), "Should report 1 column normalized");
        }

        [Test]
        public void MinMax_ConstantColumn()
        {
            var table = new TabularData("constant");
            table.AddNumericColumn("flat", new double[] { 42, 42, 42 });

            var result = new MinMaxNormalizeAlgorithm().Execute(table, AlgorithmContext.Empty);
            Assert.That(result.Success, Is.True, $"Should succeed: {result.Error}");

            var output = result.OutputDataset as ITabularDataset;
            var values = output.GetNumericColumn("flat").ToArray<double>();
            Assert.That(Math.Abs(values[0] - 0.0), Is.LessThan(1e-10), "Constant value should map to rangeMin");
        }

        #endregion

        #region Pipeline Tests

        [Test]
        public void Pipeline_GraphChain_PageRankThenCC()
        {
            var graph = new GraphData("pipeline-graph");
            graph.AddNode("A"); graph.AddNode("B"); graph.AddNode("C");
            graph.AddEdge("A", "B"); graph.AddEdge("B", "C"); graph.AddEdge("C", "A");

            var pipeline = new AlgorithmPipeline("GraphAnalysis")
                .Add(new PageRankAlgorithm())
                .Add(new ConnectedComponentsAlgorithm());

            var result = pipeline.Execute(graph);
            Assert.That(result.Success, Is.True, $"Pipeline should succeed: {result.Error}");
            Assert.That(result.StepResults.Count, Is.EqualTo(2), "Should have 2 step results");
            Assert.That(result.FinalOutput, Is.Not.Null, "Should have final output");

            var finalGraph = result.FinalOutput as IGraphDataset;
            Assert.That(finalGraph, Is.Not.Null, "Final output should be a graph");

            var propsA = finalGraph.GetNodeProperties("A");
            Assert.That(propsA.ContainsKey("pagerank"), Is.True, "Node A should have pagerank from step 1");
            Assert.That(propsA.ContainsKey("componentId"), Is.True, "Node A should have componentId from step 2");

            Debug.Log($"  Pipeline completed in {result.TotalDuration.TotalMilliseconds:F1}ms");
        }

        [Test]
        public void Pipeline_StopsOnFailure()
        {
            var table = new TabularData("wrong-type");
            table.AddNumericColumn("x", new double[] { 1, 2, 3 });

            var pipeline = new AlgorithmPipeline("WillFail")
                .Add(new PageRankAlgorithm())
                .Add(new ConnectedComponentsAlgorithm());

            var result = pipeline.Execute(table);
            Assert.That(result.Success, Is.False, "Pipeline should fail when first step gets wrong type");
            Assert.That(result.FailedStepIndex, Is.EqualTo(0), $"Should fail at step 0, got {result.FailedStepIndex}");
            Assert.That(result.StepResults.Count, Is.EqualTo(1), "Should only have 1 result (failed step)");
        }

        #endregion

        #region Validation Tests

        [Test]
        public void Validation_WrongDatasetKind_Fails()
        {
            var table = new TabularData("not-a-graph");
            table.AddNumericColumn("x", new double[] { 1, 2, 3 });

            var result = new PageRankAlgorithm().Execute(table, AlgorithmContext.Empty);
            Assert.That(result.Success, Is.False, "PageRank on tabular should fail");
            Assert.That(result.Error.Contains("not compatible"), Is.True, $"Error should mention incompatibility: {result.Error}");
        }

        [Test]
        public void Validation_MissingRequiredParam_Fails()
        {
            var algo = new RequiredParamTestAlgorithm();
            var graph = new GraphData("test");
            graph.AddNode("A");

            var result = algo.Execute(graph, AlgorithmContext.Empty);
            Assert.That(result.Success, Is.False, "Should fail with missing required param");
            Assert.That(result.Error.Contains("requiredParam"), Is.True, $"Error should mention the param: {result.Error}");
        }

        #endregion

        #region Event Tests

        [Test]
        public void Events_AlgorithmStartedAndCompleted_FireCorrectly()
        {
            try
            {
                bool startedFired = false;
                bool completedFired = false;
                string capturedAlgoName = null;

                DataCoreEventManager.SubscribeAlgorithmStarted((s, e) =>
                {
                    startedFired = true;
                    capturedAlgoName = e.AlgorithmName;
                });

                DataCoreEventManager.SubscribeAlgorithmCompleted((s, e) =>
                {
                    completedFired = true;
                    Assert.That(e.Success, Is.True, "Completed event should report success");
                    Assert.That(e.Duration.TotalMilliseconds, Is.GreaterThanOrEqualTo(0), "Duration should be non-negative");
                });

                var graph = new GraphData("event-test");
                graph.AddNode("A"); graph.AddNode("B");
                graph.AddEdge("A", "B");

                new PageRankAlgorithm().Execute(graph, AlgorithmContext.Empty);

                Assert.That(startedFired, Is.True, "AlgorithmStarted event should have fired");
                Assert.That(completedFired, Is.True, "AlgorithmCompleted event should have fired");
                Assert.That(capturedAlgoName, Is.EqualTo("PageRank"), "Event should capture algo name");
            }
            finally
            {
                DataCoreEventManager.ClearAllSubscriptions();
            }
        }

        #endregion

        #region Helpers

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
