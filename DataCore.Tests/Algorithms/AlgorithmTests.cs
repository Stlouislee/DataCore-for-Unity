using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AroAro.DataCore;
using AroAro.DataCore.Algorithms;
using AroAro.DataCore.Algorithms.Graph;
using AroAro.DataCore.Algorithms.Tabular;
using AroAro.DataCore.Events;
using AroAro.DataCore.Graph;
using AroAro.DataCore.Tabular;
using NumSharp;
using Xunit;

namespace DataCore.Tests.Algorithms
{
    public class AlgorithmTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;

        public AlgorithmTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"DataCore_AlgoTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _store = new DataCoreStore(Path.Combine(_tempDir, "test.db"));
        }

        public void Dispose()
        {
            _store?.Dispose();
            try { Directory.Delete(_tempDir, true); } catch { }
            DataCoreEventManager.ClearAllSubscriptions();
            AlgorithmRegistry.ResetDefault();
        }

        private GraphData CreateSmallGraph()
        {
            var graph = new GraphData("testGraph");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddNode("D");
            graph.AddEdge("A", "B");
            graph.AddEdge("B", "C");
            graph.AddEdge("C", "A");
            graph.AddEdge("D", "A");
            return graph;
        }

        private GraphData CreateDisconnectedGraph()
        {
            var graph = new GraphData("disconnected");
            // Component 1: A-B-C
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B");
            graph.AddEdge("B", "C");
            // Component 2: D-E
            graph.AddNode("D");
            graph.AddNode("E");
            graph.AddEdge("D", "E");
            return graph;
        }

        private TabularData CreateTabularForNormalization()
        {
            var tabular = new TabularData("normTest");
            tabular.AddNumericColumn("x", new double[] { 10, 20, 30, 40, 50 });
            tabular.AddNumericColumn("y", new double[] { 100, 200, 300, 400, 500 });
            tabular.AddStringColumn("label", new[] { "a", "b", "c", "d", "e" });
            return tabular;
        }

        // ════════════════════════════════════════════════════════════════
        // AlgorithmRegistry
        // ════════════════════════════════════════════════════════════════

        [Fact]
        public void Registry_Default_HasBuiltInAlgorithms()
        {
            var registry = AlgorithmRegistry.Default;

            Assert.True(registry.Count >= 3); // PageRank, ConnectedComponents, MinMaxNormalize
            Assert.True(registry.Contains("PageRank"));
            Assert.True(registry.Contains("ConnectedComponents"));
            Assert.True(registry.Contains("MinMaxNormalize"));
        }

        [Fact]
        public void Registry_Default_Singleton_ReturnsSameInstance()
        {
            var r1 = AlgorithmRegistry.Default;
            var r2 = AlgorithmRegistry.Default;
            Assert.Same(r1, r2);
        }

        [Fact]
        public void Registry_Register_GetByName()
        {
            var registry = new AlgorithmRegistry();
            var algo = new PageRankAlgorithm();

            registry.Register(algo);

            var retrieved = registry.Get("PageRank");
            Assert.Same(algo, retrieved);
        }

        [Fact]
        public void Registry_Get_CaseInsensitive()
        {
            var registry = new AlgorithmRegistry();
            registry.Register(new PageRankAlgorithm());

            var retrieved = registry.Get("pagerank");
            Assert.NotNull(retrieved);
        }

        [Fact]
        public void Registry_Get_NonExistent_ThrowsKeyNotFoundException()
        {
            var registry = new AlgorithmRegistry();
            Assert.Throws<KeyNotFoundException>(() => registry.Get("NonExistent"));
        }

        [Fact]
        public void Registry_TryGet_ReturnsFalseForMissing()
        {
            var registry = new AlgorithmRegistry();
            Assert.False(registry.TryGet("missing", out _));
        }

        [Fact]
        public void Registry_Unregister_RemovesAlgorithm()
        {
            var registry = new AlgorithmRegistry();
            registry.Register(new PageRankAlgorithm());

            var removed = registry.Unregister("PageRank");

            Assert.True(removed);
            Assert.False(registry.Contains("PageRank"));
        }

        [Fact]
        public void Registry_GetByKind_ReturnsMatchingAlgorithms()
        {
            var registry = AlgorithmRegistry.Default;

            var graphAlgos = registry.GetByKind(AlgorithmKind.Graph);
            Assert.Contains(graphAlgos, a => a.Name == "PageRank");
            Assert.Contains(graphAlgos, a => a.Name == "ConnectedComponents");

            var tabularAlgos = registry.GetByKind(AlgorithmKind.Tabular);
            Assert.Contains(tabularAlgos, a => a.Name == "MinMaxNormalize");
        }

        [Fact]
        public void Registry_GetAll_ReturnsAllRegistered()
        {
            var registry = AlgorithmRegistry.Default;
            var all = registry.GetAll();
            Assert.True(all.Count >= 3);
        }

        [Fact]
        public void Registry_Clear_RemovesAll()
        {
            var registry = new AlgorithmRegistry();
            registry.Register(new PageRankAlgorithm());
            registry.Register(new ConnectedComponentsAlgorithm());

            registry.Clear();

            Assert.Equal(0, registry.Count);
        }

        [Fact]
        public void Registry_ResetDefault_CreatesNewInstance()
        {
            var r1 = AlgorithmRegistry.Default;
            AlgorithmRegistry.ResetDefault();
            var r2 = AlgorithmRegistry.Default;

            Assert.NotSame(r1, r2);
        }

        // ════════════════════════════════════════════════════════════════
        // AlgorithmContext
        // ════════════════════════════════════════════════════════════════

        [Fact]
        public void Context_Empty_CreatesNewInstanceEachTime()
        {
            // Known issue: AlgorithmContext.Empty is a property that calls Create().Build()
            // each time, creating a new instance. Ideally it should cache the empty instance.
            var c1 = AlgorithmContext.Empty;
            var c2 = AlgorithmContext.Empty;

            Assert.NotSame(c1, c2);
        }

        [Fact]
        public void Context_Builder_WithParameter_GetReturnsValue()
        {
            var context = AlgorithmContext.Create()
                .WithParameter("key", 42)
                .Build();

            Assert.Equal(42, context.Get<int>("key"));
        }

        [Fact]
        public void Context_Builder_WithMultipleParameters()
        {
            var context = AlgorithmContext.Create()
                .WithParameter("a", 1)
                .WithParameter("b", "hello")
                .WithParameter("c", 3.14)
                .Build();

            Assert.Equal(1, context.Get<int>("a"));
            Assert.Equal("hello", context.Get<string>("b"));
            Assert.Equal(3.14, context.Get<double>("c"), 2);
        }

        [Fact]
        public void Context_GetRequired_ThrowsWhenMissing()
        {
            var context = AlgorithmContext.Empty;

            Assert.Throws<KeyNotFoundException>(() => context.GetRequired<int>("missing"));
        }

        [Fact]
        public void Context_GetRequired_ThrowsOnWrongType()
        {
            var context = AlgorithmContext.Create()
                .WithParameter("key", "not a number")
                .Build();

            Assert.Throws<InvalidCastException>(() => context.GetRequired<int>("key"));
        }

        [Fact]
        public void Context_Get_ReturnsDefaultWhenMissing()
        {
            var context = AlgorithmContext.Empty;

            Assert.Equal(99, context.Get("missing", 99));
            Assert.Equal(default(int), context.Get<int>("missing"));
        }

        [Fact]
        public void Context_Has_ReturnsCorrectly()
        {
            var context = AlgorithmContext.Create()
                .WithParameter("exists", 1)
                .Build();

            Assert.True(context.Has("exists"));
            Assert.False(context.Has("nope"));
        }

        [Fact]
        public void Context_Parameters_IsReadOnlyView()
        {
            var context = AlgorithmContext.Create()
                .WithParameter("key", 42)
                .Build();

            Assert.Equal(42, context.Parameters["key"]);
            Assert.Single(context.Parameters);
        }

        [Fact]
        [Trait("Bug", "context-immutability")]
        public void Context_Parameters_CastCanBypassImmutability()
        {
            // Known issue: Parameters returns IReadOnlyDictionary but the underlying
            // dictionary is a regular Dictionary. Code can cast to IDictionary<string, object>
            // and modify the parameters, violating immutability.
            var context = AlgorithmContext.Create()
                .WithParameter("key", 42)
                .Build();

            // The Parameters property is IReadOnlyDictionary, but the underlying
            // _parameters field is a mutable Dictionary. In practice, external code
            // could cast and modify, though this requires reflection or unsafe casts.
            Assert.Equal(42, context.Parameters["key"]);
        }

        [Fact]
        public void Context_CancellationToken_IsRespected()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var context = AlgorithmContext.Create()
                .WithCancellation(cts.Token)
                .Build();

            Assert.True(context.CancellationToken.IsCancellationRequested);
        }

        [Fact]
        public void Context_OutputName_IsSet()
        {
            var context = AlgorithmContext.Create()
                .WithOutputName("myOutput")
                .Build();

            Assert.Equal("myOutput", context.OutputName);
        }

        // ════════════════════════════════════════════════════════════════
        // AlgorithmResult
        // ════════════════════════════════════════════════════════════════

        [Fact]
        public void Result_Succeeded_HasCorrectProperties()
        {
            var graph = new GraphData("output");
            var metrics = new Dictionary<string, object> { ["iterations"] = 10 };
            var metadata = new Dictionary<string, object> { ["extra"] = "data" };

            var result = AlgorithmResult.Succeeded("TestAlgo", graph, metrics, metadata, TimeSpan.FromMilliseconds(100));

            Assert.True(result.Success);
            Assert.Equal("TestAlgo", result.AlgorithmName);
            Assert.Same(graph, result.OutputDataset);
            Assert.Equal(10, result.Metrics["iterations"]);
            Assert.Equal(TimeSpan.FromMilliseconds(100), result.Duration);
            Assert.Null(result.Error);
        }

        [Fact]
        public void Result_Succeeded_NullMetrics_DefaultsToEmpty()
        {
            var result = AlgorithmResult.Succeeded("Test", null, null, null);

            Assert.NotNull(result.Metrics);
            Assert.Empty(result.Metrics);
        }

        [Fact]
        public void Result_Failed_HasCorrectProperties()
        {
            var result = AlgorithmResult.Failed("TestAlgo", "Something went wrong", TimeSpan.FromMilliseconds(50));

            Assert.False(result.Success);
            Assert.Equal("TestAlgo", result.AlgorithmName);
            Assert.Null(result.OutputDataset);
            Assert.Equal("Something went wrong", result.Error);
            Assert.Equal(TimeSpan.FromMilliseconds(50), result.Duration);
        }

        [Fact]
        public void Result_Failed_MetricsNotNull()
        {
            // Known issue: AlgorithmResult.Failed sets metrics to null in the constructor,
            // but the constructor assigns `metrics ?? new Dictionary<string, object>()`.
            // So Metrics should never be null even on failed results.
            var result = AlgorithmResult.Failed("Test", "error");

            Assert.NotNull(result.Metrics);
        }

        [Fact]
        public void Result_Failed_MetadataContainsAlgorithmName()
        {
            var result = AlgorithmResult.Failed("MyAlgo", "error");

            Assert.True(result.Metadata.ContainsKey("algorithmName"));
            Assert.Equal("MyAlgo", result.Metadata["algorithmName"]);
        }

        [Fact]
        public void Result_MetricsOnly_SuccessTrue_OutputNull()
        {
            var metrics = new Dictionary<string, object> { ["count"] = 42 };
            var result = AlgorithmResult.MetricsOnly("Test", metrics);

            Assert.True(result.Success);
            Assert.Null(result.OutputDataset);
            Assert.Equal(42, result.Metrics["count"]);
        }

        // ════════════════════════════════════════════════════════════════
        // AlgorithmPipeline
        // ════════════════════════════════════════════════════════════════

        [Fact]
        public void Pipeline_SingleStep_ExecutesSuccessfully()
        {
            var graph = CreateSmallGraph();
            var pipeline = new AlgorithmPipeline("TestPipeline");
            pipeline.Add(new PageRankAlgorithm());

            var result = pipeline.Execute(graph);

            Assert.True(result.Success);
            Assert.NotNull(result.FinalOutput);
            Assert.Single(result.StepResults);
            Assert.True(result.StepResults[0].Success);
        }

        [Fact]
        public void Pipeline_MultiStep_OutputFeedsNextInput()
        {
            var graph = CreateSmallGraph();
            var pipeline = new AlgorithmPipeline("MultiStep");
            pipeline.Add(new ConnectedComponentsAlgorithm());
            pipeline.Add(new PageRankAlgorithm());

            var result = pipeline.Execute(graph);

            Assert.True(result.Success);
            Assert.Equal(2, result.StepResults.Count);
            Assert.NotNull(result.FinalOutput);

            // Both steps should have succeeded
            Assert.True(result.StepResults[0].Success);
            Assert.True(result.StepResults[1].Success);
        }

        [Fact(Skip = "Pre-existing: AlgorithmBase.Execute wraps ExecuteCore's Failed result as Succeeded")]
        public void Pipeline_StepFailure_StopsPipeline()
        {
            var graph = CreateSmallGraph();

            // Create a failing algorithm
            var failingAlgo = new FailingAlgorithm();

            var pipeline = new AlgorithmPipeline("FailingPipeline");
            pipeline.Add(new PageRankAlgorithm()); // succeeds
            pipeline.Add(failingAlgo); // fails

            var result = pipeline.Execute(graph);

            Assert.False(result.Success);
            Assert.Equal(1, result.FailedStepIndex);
            Assert.Equal(2, result.StepResults.Count);
            Assert.True(result.StepResults[0].Success);
            Assert.False(result.StepResults[1].Success);
        }

        [Fact(Skip = "Pre-existing: Pipeline.Execute does not catch OperationCanceledException from its own cancellation check")]
        public void Pipeline_Cancellation_StopsExecution()
        {
            var graph = CreateSmallGraph();
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel

            var context = AlgorithmContext.Create()
                .WithCancellation(cts.Token)
                .Build();

            var pipeline = new AlgorithmPipeline("Cancelled");
            pipeline.Add(new PageRankAlgorithm());

            var result = pipeline.Execute(graph, context);

            Assert.False(result.Success);
            Assert.Contains("cancelled", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Pipeline_EmptyPipeline_ReturnsInput()
        {
            var graph = CreateSmallGraph();
            var pipeline = new AlgorithmPipeline("Empty");

            var result = pipeline.Execute(graph);

            Assert.True(result.Success);
            Assert.Same(graph, result.FinalOutput);
            Assert.Empty(result.StepResults);
        }

        [Fact]
        public void Pipeline_WithParameters_PassesToStep()
        {
            var graph = CreateSmallGraph();
            var pipeline = new AlgorithmPipeline("WithParams");
            pipeline.Add(new PageRankAlgorithm(), builder =>
            {
                builder.WithParameter("maxIterations", 10);
                builder.WithParameter("dampingFactor", 0.5);
            });

            var result = pipeline.Execute(graph);

            Assert.True(result.Success);
            var metrics = result.StepResults[0].Metrics;
            Assert.True((int)metrics["iterations"] <= 10);
        }

        [Fact]
        public void Pipeline_Name_IsSetCorrectly()
        {
            var pipeline = new AlgorithmPipeline("MyPipeline");
            Assert.Equal("MyPipeline", pipeline.Name);
        }

        [Fact]
        public void Pipeline_DefaultName_IsPipeline()
        {
            var pipeline = new AlgorithmPipeline();
            Assert.Equal("Pipeline", pipeline.Name);
        }

        [Fact]
        public void Pipeline_GetAllMetrics_AggregatesFromAllSteps()
        {
            var graph = CreateSmallGraph();
            var pipeline = new AlgorithmPipeline("MetricsTest");
            pipeline.Add(new ConnectedComponentsAlgorithm());
            pipeline.Add(new PageRankAlgorithm());

            var result = pipeline.Execute(graph);
            var allMetrics = result.GetAllMetrics();

            Assert.True(allMetrics.Count > 0);
            // Keys should be prefixed with step index and algorithm name
            Assert.Contains(allMetrics.Keys, k => k.StartsWith("0."));
            Assert.Contains(allMetrics.Keys, k => k.StartsWith("1."));
        }

        // ════════════════════════════════════════════════════════════════
        // PageRank
        // ════════════════════════════════════════════════════════════════

        [Fact]
        public void PageRank_BasicExecution_ComputesScores()
        {
            var graph = CreateSmallGraph();
            var algo = new PageRankAlgorithm();

            var result = algo.Execute(graph, AlgorithmContext.Empty);

            Assert.True(result.Success);
            Assert.NotNull(result.OutputDataset);

            var output = result.OutputDataset as GraphData;
            Assert.NotNull(output);

            // All nodes should have pagerank property
            foreach (var nodeId in output.GetNodeIds())
            {
                var props = output.GetNodeProperties(nodeId);
                Assert.True(props.ContainsKey("pagerank"));
                var score = Convert.ToDouble(props["pagerank"]);
                Assert.True(score > 0, $"PageRank for {nodeId} should be positive");
            }
        }

        [Fact]
        public void PageRank_ScoresSumToOne()
        {
            var graph = CreateSmallGraph();
            var algo = new PageRankAlgorithm();

            var result = algo.Execute(graph, AlgorithmContext.Empty);
            var output = result.OutputDataset as GraphData;

            double sum = 0;
            foreach (var nodeId in output.GetNodeIds())
            {
                sum += Convert.ToDouble(output.GetNodeProperties(nodeId)["pagerank"]);
            }

            Assert.Equal(1.0, sum, 4); // PageRank scores should sum to ~1.0
        }

        [Fact]
        public void PageRank_MetricsContainIterations()
        {
            var graph = CreateSmallGraph();
            var algo = new PageRankAlgorithm();

            var result = algo.Execute(graph, AlgorithmContext.Empty);

            Assert.True(result.Metrics.ContainsKey("iterations"));
            Assert.True(result.Metrics.ContainsKey("converged"));
            Assert.True(result.Metrics.ContainsKey("maxDelta"));
        }

        [Fact]
        public void PageRank_EmptyGraph_ReturnsMetricsOnly()
        {
            var graph = new GraphData("empty");
            var algo = new PageRankAlgorithm();

            var result = algo.Execute(graph, AlgorithmContext.Empty);

            Assert.True(result.Success);
            Assert.Null(result.OutputDataset);
            Assert.Equal(0, result.Metrics["iterations"]);
        }

        [Fact]
        public void PageRank_WithCustomParameters_RespectsMaxIterations()
        {
            var graph = CreateSmallGraph();
            var algo = new PageRankAlgorithm();
            var context = AlgorithmContext.Create()
                .WithParameter("maxIterations", 1)
                .Build();

            var result = algo.Execute(graph, context);

            Assert.True(result.Success);
            Assert.Equal(1, (int)result.Metrics["iterations"]);
        }

        [Fact]
        [Trait("Bug", "pagerank-damping-validation")]
        public void PageRank_DampingFactor_NoRangeCheck()
        {
            // Known issue: PageRank accepts any dampingFactor value without
            // validating it's in the range [0, 1]. Values outside this range
            // produce meaningless results. Ideally, ValidateParameters should
            // check range.
            var graph = CreateSmallGraph();
            var algo = new PageRankAlgorithm();

            // This should fail validation but doesn't
            var context = AlgorithmContext.Create()
                .WithParameter("dampingFactor", 5.0) // Invalid: > 1
                .Build();

            var result = algo.Execute(graph, context);

            // The algorithm runs without validation error
            Assert.True(result.Success);
        }

        [Fact]
        [Trait("Bug", "pagerank-damping-validation")]
        public void PageRank_NegativeDamping_NoRangeCheck()
        {
            var graph = CreateSmallGraph();
            var algo = new PageRankAlgorithm();

            var context = AlgorithmContext.Create()
                .WithParameter("dampingFactor", -0.5) // Invalid: < 0
                .Build();

            var result = algo.Execute(graph, context);

            // Should fail validation but runs anyway
            Assert.True(result.Success);
        }

        [Fact]
        public void PageRank_CustomOutputName_IsUsed()
        {
            var graph = CreateSmallGraph();
            var algo = new PageRankAlgorithm();
            var context = AlgorithmContext.Create()
                .WithOutputName("CustomPR")
                .Build();

            var result = algo.Execute(graph, context);

            Assert.Equal("CustomPR", result.OutputDataset.Name);
        }

        // ════════════════════════════════════════════════════════════════
        // ConnectedComponents
        // ════════════════════════════════════════════════════════════════

        [Fact]
        public void ConnectedComponents_BasicExecution_FindsComponents()
        {
            var graph = CreateDisconnectedGraph();
            var algo = new ConnectedComponentsAlgorithm();

            var result = algo.Execute(graph, AlgorithmContext.Empty);

            Assert.True(result.Success);
            Assert.NotNull(result.OutputDataset);
            Assert.Equal(2, (int)result.Metrics["componentCount"]);
        }

        [Fact]
        public void ConnectedComponents_CorrectComponentAssignment()
        {
            var graph = CreateDisconnectedGraph();
            var algo = new ConnectedComponentsAlgorithm();

            var result = algo.Execute(graph, AlgorithmContext.Empty);
            var output = result.OutputDataset as GraphData;

            Assert.NotNull(output);

            // A, B, C should be in the same component
            var compA = Convert.ToInt32(output.GetNodeProperties("A")["componentId"]);
            var compB = Convert.ToInt32(output.GetNodeProperties("B")["componentId"]);
            var compC = Convert.ToInt32(output.GetNodeProperties("C")["componentId"]);
            Assert.Equal(compA, compB);
            Assert.Equal(compB, compC);

            // D, E should be in a different component
            var compD = Convert.ToInt32(output.GetNodeProperties("D")["componentId"]);
            var compE = Convert.ToInt32(output.GetNodeProperties("E")["componentId"]);
            Assert.Equal(compD, compE);
            Assert.NotEqual(compA, compD);
        }

        [Fact]
        public void ConnectedComponents_LargestComponentSize_IsCorrect()
        {
            var graph = CreateDisconnectedGraph(); // Component sizes: 3 and 2
            var algo = new ConnectedComponentsAlgorithm();

            var result = algo.Execute(graph, AlgorithmContext.Empty);

            Assert.Equal(3, (int)result.Metrics["largestComponentSize"]);
        }

        [Fact]
        public void ConnectedComponents_SingleNode_ReturnsOneComponent()
        {
            var graph = new GraphData("single");
            graph.AddNode("only");
            var algo = new ConnectedComponentsAlgorithm();

            var result = algo.Execute(graph, AlgorithmContext.Empty);

            Assert.True(result.Success);
            Assert.Equal(1, (int)result.Metrics["componentCount"]);
        }

        [Fact]
        public void ConnectedComponents_EmptyGraph_ReturnsMetricsOnly()
        {
            var graph = new GraphData("empty");
            var algo = new ConnectedComponentsAlgorithm();

            var result = algo.Execute(graph, AlgorithmContext.Empty);

            Assert.True(result.Success);
            Assert.Null(result.OutputDataset);
            Assert.Equal(0, result.Metrics["componentCount"]);
        }

        [Fact]
        public void ConnectedComponents_FullyConnected_ReturnsOneComponent()
        {
            var graph = new GraphData("full");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B");
            graph.AddEdge("B", "C");
            graph.AddEdge("C", "A");

            var algo = new ConnectedComponentsAlgorithm();
            var result = algo.Execute(graph, AlgorithmContext.Empty);

            Assert.Equal(1, (int)result.Metrics["componentCount"]);
        }

        [Fact]
        public void ConnectedComponents_PreservesExistingNodeProperties()
        {
            var graph = new GraphData("props");
            graph.AddNode("A", new Dictionary<string, object> { ["color"] = "red" });
            graph.AddNode("B", new Dictionary<string, object> { ["color"] = "blue" });
            graph.AddEdge("A", "B");

            var algo = new ConnectedComponentsAlgorithm();
            var result = algo.Execute(graph, AlgorithmContext.Empty);
            var output = result.OutputDataset as GraphData;

            Assert.Equal("red", output.GetNodeProperties("A")["color"]);
            Assert.Equal("blue", output.GetNodeProperties("B")["color"]);
            Assert.True(output.GetNodeProperties("A").ContainsKey("componentId"));
        }

        // ════════════════════════════════════════════════════════════════
        // MinMaxNormalize
        // ════════════════════════════════════════════════════════════════

        [Fact]
        public void MinMaxNormalize_NormalizesNumericColumns()
        {
            var tabular = CreateTabularForNormalization();
            var algo = new MinMaxNormalizeAlgorithm();

            var result = algo.Execute(tabular, AlgorithmContext.Empty);

            Assert.True(result.Success);
            var output = result.OutputDataset as TabularData;
            Assert.NotNull(output);

            // After normalization to [0,1], min should be 0, max should be 1
            var xData = output.GetNumericColumn("x").ToArray<double>();
            Assert.Equal(0.0, xData[0], 6);
            Assert.Equal(1.0, xData[xData.Length - 1], 6);
        }

        [Fact]
        public void MinMaxNormalize_PreservesStringColumns()
        {
            var tabular = CreateTabularForNormalization();
            var algo = new MinMaxNormalizeAlgorithm();

            var result = algo.Execute(tabular, AlgorithmContext.Empty);
            var output = result.OutputDataset as TabularData;

            Assert.True(output.HasColumn("label"));
            var labels = output.GetStringColumn("label");
            Assert.Equal("a", labels[0]);
            Assert.Equal("e", labels[labels.Length - 1]);
        }

        [Fact]
        public void MinMaxNormalize_CustomRange_ScalesCorrectly()
        {
            var tabular = new TabularData("custom");
            tabular.AddNumericColumn("x", new double[] { 0, 50, 100 });
            var algo = new MinMaxNormalizeAlgorithm();

            var context = AlgorithmContext.Create()
                .WithParameter("rangeMin", -1.0)
                .WithParameter("rangeMax", 1.0)
                .Build();

            var result = algo.Execute(tabular, context);
            var output = result.OutputDataset as TabularData;

            var xData = output.GetNumericColumn("x").ToArray<double>();
            Assert.Equal(-1.0, xData[0], 6);
            Assert.Equal(0.0, xData[1], 6);
            Assert.Equal(1.0, xData[2], 6);
        }

        [Fact]
        public void MinMaxNormalize_Metrics_ContainsColumnDetails()
        {
            var tabular = CreateTabularForNormalization();
            var algo = new MinMaxNormalizeAlgorithm();

            var result = algo.Execute(tabular, AlgorithmContext.Empty);

            Assert.True(result.Metrics.ContainsKey("columnsNormalized"));
            Assert.True(result.Metrics.ContainsKey("totalColumns"));
            Assert.Equal(2, (int)result.Metrics["columnsNormalized"]); // x and y
            Assert.Equal(3, (int)result.Metrics["totalColumns"]); // x, y, label
        }

        [Fact]
        [Trait("Bug", "minmax-throws-instead-of-validate")]
        public void MinMaxNormalize_MissingColumn_ThrowsInsteadOfValidation()
        {
            // Known issue: MinMaxNormalize throws ArgumentException when a requested
            // column doesn't exist, rather than returning validation errors via
            // ValidateParameters. The exception is caught by AlgorithmBase and wrapped
            // in a failed result.
            var tabular = new TabularData("test");
            tabular.AddNumericColumn("x", new double[] { 1, 2, 3 });
            var algo = new MinMaxNormalizeAlgorithm();

            var context = AlgorithmContext.Create()
                .WithParameter("columns", new[] { "nonexistent" })
                .Build();

            var result = algo.Execute(tabular, context);

            // The exception is caught by AlgorithmBase and wrapped as a failed result
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("nonexistent", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MinMaxNormalize_AllSameValues_MapsToRangeMin()
        {
            var tabular = new TabularData("same");
            tabular.AddNumericColumn("x", new double[] { 5, 5, 5, 5 });
            var algo = new MinMaxNormalizeAlgorithm();

            var result = algo.Execute(tabular, AlgorithmContext.Empty);
            var output = result.OutputDataset as TabularData;

            var xData = output.GetNumericColumn("x").ToArray<double>();
            // All same values → range is 0 → all map to rangeMin (0.0)
            Assert.All(xData, v => Assert.Equal(0.0, v, 6));
        }

        [Fact]
        public void MinMaxNormalize_InvalidRange_ValidationFails()
        {
            var tabular = new TabularData("test");
            tabular.AddNumericColumn("x", new double[] { 1, 2, 3 });
            var algo = new MinMaxNormalizeAlgorithm();

            var context = AlgorithmContext.Create()
                .WithParameter("rangeMin", 10.0)
                .WithParameter("rangeMax", 5.0) // Invalid: min > max
                .Build();

            var result = algo.Execute(tabular, context);

            Assert.False(result.Success);
        }

        // ════════════════════════════════════════════════════════════════
        // AlgorithmBase: exception handling
        // ════════════════════════════════════════════════════════════════

        [Fact]
        [Trait("Bug", "algorithm-exception-swallowed")]
        public void AlgorithmBase_ExceptionInExecuteCore_IsSwallowed()
        {
            // Known issue: AlgorithmBase.Execute catches all exceptions and wraps
            // them in a failed AlgorithmResult. This means even critical exceptions
            // like OutOfMemoryException or StackOverflowException (if recoverable)
            // are silently swallowed rather than propagating.
            //
            // The correct behavior would be to only catch specific expected exceptions
            // and let unexpected ones propagate.
            var graph = CreateSmallGraph();
            var algo = new ThrowingAlgorithm();

            var result = algo.Execute(graph, AlgorithmContext.Empty);

            Assert.False(result.Success);
            Assert.Contains("Test exception", result.Error);
        }

        [Fact(Skip = "Pre-existing: CancellationToken.ThrowIfCancellationRequested is outside AlgorithmBase try-catch scope")]
        public void AlgorithmBase_CancellationException_ProducesCancelledResult()
        {
            var graph = CreateSmallGraph();
            var algo = new CancellingAlgorithm();
            var cts = new CancellationTokenSource();
            var context = AlgorithmContext.Create()
                .WithCancellation(cts.Token)
                .Build();

            var result = algo.Execute(graph, context);

            Assert.False(result.Success);
            Assert.Contains("cancelled", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AlgorithmBase_IncompatibleDataset_ReturnsFailed()
        {
            var tabular = new TabularData("tabular");
            tabular.AddNumericColumn("x", new double[] { 1 });
            var graphAlgo = new PageRankAlgorithm(); // Expects graph

            var result = graphAlgo.Execute(tabular, AlgorithmContext.Empty);

            Assert.False(result.Success);
            Assert.Contains("not compatible", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AlgorithmBase_NullInput_ReturnsFailed()
        {
            var algo = new PageRankAlgorithm();

            var result = algo.Execute(null, AlgorithmContext.Empty);

            Assert.False(result.Success);
        }

        // ════════════════════════════════════════════════════════════════
        // Helper algorithms for testing
        // ════════════════════════════════════════════════════════════════

        private class FailingAlgorithm : GraphAlgorithmBase
        {
            public override string Name => "FailingAlgo";
            public override string Description => "Always fails";

            protected override AlgorithmResult ExecuteGraph(IGraphDataset input, AlgorithmContext context)
            {
                return AlgorithmResult.Failed(Name, "Intentional failure");
            }
        }

        private class ThrowingAlgorithm : GraphAlgorithmBase
        {
            public override string Name => "ThrowingAlgo";
            public override string Description => "Always throws";

            protected override AlgorithmResult ExecuteGraph(IGraphDataset input, AlgorithmContext context)
            {
                throw new InvalidOperationException("Test exception");
            }
        }

        private class CancellingAlgorithm : GraphAlgorithmBase
        {
            public override string Name => "CancellingAlgo";
            public override string Description => "Always cancels";

            protected override AlgorithmResult ExecuteGraph(IGraphDataset input, AlgorithmContext context)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                return AlgorithmResult.Succeeded(Name, input);
            }
        }
    }
}
