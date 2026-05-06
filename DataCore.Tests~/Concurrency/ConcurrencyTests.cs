using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AroAro.DataCore;
using AroAro.DataCore.Algorithms;
using AroAro.DataCore.Algorithms.Graph;
using AroAro.DataCore.Graph;
using AroAro.DataCore.Session;
using Xunit;

namespace DataCore.Tests
{
    public class ConcurrencyTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DataCoreStore _store;

        public ConcurrencyTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"datacore_conc_{Guid.NewGuid():N}.db");
            _store = new DataCoreStore(_dbPath);
        }

        public void Dispose()
        {
            _store?.Dispose();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public void ConcurrentTabularAddRows_NoCorruption()
        {
            var tabular = _store.CreateTabular("concurrent");
            tabular.AddNumericColumn("x", new double[0]);

            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        tabular.AddRow(new Dictionary<string, object> { { "x", (double)(taskId * 100 + j) } });
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());
            Assert.Equal(1000, tabular.RowCount);
        }

        [Fact]
        public void ConcurrentTabularAndGraphCreation_AllCreated()
        {
            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                int idx = i;
                tasks.Add(Task.Run(() =>
                {
                    if (idx % 2 == 0)
                        _store.CreateTabular($"tab_{idx}");
                    else
                        _store.CreateGraph($"graph_{idx}");
                }));
            }

            Task.WaitAll(tasks.ToArray());

            var tabularNames = _store.TabularNames.ToHashSet();
            var graphNames = _store.GraphNames.ToHashSet();

            for (int i = 0; i < 20; i++)
            {
                if (i % 2 == 0)
                    Assert.Contains($"tab_{i}", tabularNames);
                else
                    Assert.Contains($"graph_{i}", graphNames);
            }
        }

        [Fact]
        public void ConcurrentSessionCreation_DoesNotThrow()
        {
            var manager = _store.SessionManager;

            var exceptions = new ConcurrentBag<Exception>();
            Parallel.For(0, 50, i =>
            {
                try
                {
                    var session = manager.CreateSession($"session_{i}");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
        }

        [Fact]
        public void ParallelPageRank_NoRaceCondition()
        {
            var graph = new GraphData("parallel-pr");
            for (int i = 0; i < 50; i++)
                graph.AddNode($"n{i}");
            for (int i = 0; i < 49; i++)
                graph.AddEdge($"n{i}", $"n{i + 1}");

            var results = new ConcurrentBag<AlgorithmResult>();

            Parallel.For(0, 10, _ =>
            {
                var result = new PageRankAlgorithm().Execute(graph, AlgorithmContext.Empty);
                results.Add(result);
            });

            Assert.All(results, r => Assert.True(r.Success));
            Assert.Equal(10, results.Count);
        }

        [Fact]
        public void ParallelConnectedComponents_NoRaceCondition()
        {
            var graph = new GraphData("parallel-cc");
            for (int i = 0; i < 30; i++)
                graph.AddNode($"n{i}");
            // Two components
            for (int i = 0; i < 14; i++)
                graph.AddEdge($"n{i}", $"n{i + 1}");
            for (int i = 15; i < 29; i++)
                graph.AddEdge($"n{i}", $"n{i + 1}");

            var results = new ConcurrentBag<AlgorithmResult>();

            Parallel.For(0, 10, _ =>
            {
                var result = new ConnectedComponentsAlgorithm().Execute(graph, AlgorithmContext.Empty);
                results.Add(result);
            });

            Assert.All(results, r => Assert.True(r.Success));
            Assert.All(results, r => Assert.Equal(2, (int)r.Metrics["componentCount"]));
        }

        [Fact]
        public void ConcurrentGraphAddNodesAndEdges_AllPresent()
        {
            var graph = _store.CreateGraph("concurrent-graph");

            // Add nodes concurrently
            Parallel.For(0, 100, i =>
            {
                graph.AddNode($"node_{i}", new Dictionary<string, object> { ["index"] = i });
            });

            Assert.Equal(100, graph.NodeCount);

            // Add edges concurrently
            Parallel.For(0, 99, i =>
            {
                graph.AddEdge($"node_{i}", $"node_{i + 1}");
            });

            Assert.Equal(99, graph.EdgeCount);
        }

        [Fact]
        public void ConcurrentTabularQuery_NoCorruption()
        {
            var tabular = _store.CreateTabular("query-test");
            tabular.AddNumericColumn("val", Enumerable.Range(0, 1000).Select(i => (double)i).ToArray());

            // Concurrent reads
            var results = new ConcurrentBag<int>();
            Parallel.For(0, 20, _ =>
            {
                var count = tabular.Query().WhereGreaterThan("val", 500).Count();
                results.Add(count);
            });

            // All queries should return the same count
            Assert.All(results, c => Assert.Equal(499, c));
        }
    }
}
