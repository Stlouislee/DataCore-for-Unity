using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AroAro.DataCore;
using AroAro.DataCore.Graph;
using AroAro.DataCore.Session;
using AroAro.DataCore.Tabular;
using Microsoft.Data.Analysis;
using Xunit;

namespace DataCore.Tests.Concurrency
{
    /// <summary>
    /// Tests for Phase 1 fixes: thread safety, concurrency, and critical bug fixes.
    /// Covers issues #93, #57, #65, #35, #94, #16, #14.
    /// </summary>
    public class Phase1FixesTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DataCoreStore _store;

        public Phase1FixesTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"datacore_phase1_{Guid.NewGuid():N}.db");
            _store = new DataCoreStore(_dbPath);
        }

        public void Dispose()
        {
            _store?.Dispose();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        #region #93 — Session Concurrent Dataset Operations

        [Fact]
        public void Session_ConcurrentDatasetOperations_NoCorruption()
        {
            var session = _store.SessionManager.CreateSession("concurrent-session");

            // Create datasets concurrently
            var exceptions = new ConcurrentBag<Exception>();
            Parallel.For(0, 20, i =>
            {
                try
                {
                    session.CreateDataset($"ds_{i}", DataSetKind.Tabular);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
            Assert.Equal(20, session.DatasetCount);

            // Read datasets concurrently
            Parallel.For(0, 20, i =>
            {
                var ds = session.GetDataset($"ds_{i}");
                Assert.NotNull(ds);
            });
        }

        [Fact]
        public void Session_ConcurrentCreateAndRemove_NoDeadlock()
        {
            var session = _store.SessionManager.CreateSession("deadlock-test");
            var exceptions = new ConcurrentBag<Exception>();

            // Create and remove concurrently — should not deadlock
            Parallel.For(0, 50, i =>
            {
                try
                {
                    var name = $"ds_{i % 10}";
                    if (i < 25)
                        session.CreateDataset(name, DataSetKind.Tabular);
                    else
                        session.RemoveDataset(name);
                }
                catch (InvalidOperationException) { } // duplicate name is expected
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
        }

        [Fact]
        public void Session_ConcurrentDataFrameOperations_NoCorruption()
        {
            var session = (AroAro.DataCore.Session.Session)_store.SessionManager.CreateSession("df-concurrent");
            var exceptions = new ConcurrentBag<Exception>();

            Parallel.For(0, 20, i =>
            {
                try
                {
                    var df = session.CreateDataFrame($"df_{i}");
                    Assert.NotNull(df);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
            Assert.Equal(20, session.DataFrameCount);

            // Read concurrently
            Parallel.For(0, 20, i =>
            {
                var df = session.GetDataFrame($"df_{i}");
                Assert.NotNull(df);
            });
        }

        [Fact]
        public void Session_ConcurrentClearAndAccess_NoException()
        {
            var session = _store.SessionManager.CreateSession("clear-test");

            for (int i = 0; i < 10; i++)
                session.CreateDataset($"ds_{i}", DataSetKind.Tabular);

            var exceptions = new ConcurrentBag<Exception>();

            // Clear while other threads access
            Parallel.For(0, 20, i =>
            {
                try
                {
                    if (i == 0)
                        session.Clear();
                    else if (session.DatasetCount > 0) { } // read access
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
        }

        #endregion

        #region #57 — LiteDB Concurrent Read/Write

        [Fact]
        public void LiteDbTabular_ConcurrentReadAndWrite_NoTornRead()
        {
            var tabular = _store.CreateTabular("concurrent-rw");
            tabular.AddNumericColumn("x", new double[] { 1, 2, 3, 4, 5 });

            var readResults = new ConcurrentBag<double[]>();
            var exceptions = new ConcurrentBag<Exception>();

            // Writers
            var writer = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        tabular.AddRow(new Dictionary<string, object> { { "x", (double)(100 + i) } });
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            });

            // Readers
            var readers = Task.Run(() =>
            {
                Parallel.For(0, 10, _ =>
                {
                    try
                    {
                        var col = tabular.GetNumericColumn("x");
                        readResults.Add(col.ToArray<double>());
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            });

            Task.WaitAll(writer, readers);
            Assert.Empty(exceptions);
            Assert.NotEmpty(readResults);

            // Each read should return a consistent snapshot (lengths may vary due to concurrent writes)
            Assert.NotEmpty(readResults);
            // All reads should return valid arrays (no corruption/torn data)
            Assert.All(readResults, r => Assert.True(r.Length >= 5, $"Read returned too few columns: {r.Length}"));
        }

        [Fact]
        public void LiteDbGraph_ConcurrentReadAndWrite_NoTornRead()
        {
            var graph = _store.CreateGraph("concurrent-graph-rw");
            for (int i = 0; i < 10; i++)
                graph.AddNode($"n{i}");
            for (int i = 0; i < 9; i++)
                graph.AddEdge($"n{i}", $"n{i + 1}");

            var exceptions = new ConcurrentBag<Exception>();

            // Writers
            var writer = Task.Run(() =>
            {
                for (int i = 10; i < 50; i++)
                {
                    try
                    {
                        graph.AddNode($"n{i}");
                        graph.AddEdge($"n{i - 1}", $"n{i}");
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            });

            // Readers
            var readers = Task.Run(() =>
            {
                Parallel.For(0, 10, _ =>
                {
                    try
                    {
                        var neighbors = graph.GetOutNeighbors("n0").ToList();
                        var edges = graph.GetEdges().ToList();
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            });

            Task.WaitAll(writer, readers);
            Assert.Empty(exceptions);
        }

        #endregion

        #region #65 — ClearAll Atomicity

        [Fact]
        public void ClearAll_RemovesCachedAndPersistedDatasets()
        {
            // Create datasets and access some to cache them
            var t1 = _store.CreateTabular("cached-tab");
            t1.AddNumericColumn("x", new double[] { 1, 2, 3 });

            var g1 = _store.CreateGraph("cached-graph");
            g1.AddNode("a");

            // Also create datasets we don't access (not cached)
            _store.CreateTabular("uncached-tab");
            _store.CreateGraph("uncached-graph");

            // Access cached ones to ensure they're in cache
            var rc = t1.RowCount;
            var nc = g1.NodeCount;
            Assert.True(rc > 0);
            Assert.True(nc > 0);

            // ClearAll should remove everything
            _store.ClearAll();

            Assert.Empty(_store.TabularNames);
            Assert.Empty(_store.GraphNames);
            Assert.False(_store.HasDataset("cached-tab"));
            Assert.False(_store.HasDataset("cached-graph"));
            Assert.False(_store.HasDataset("uncached-tab"));
            Assert.False(_store.HasDataset("uncached-graph"));
        }

        [Fact]
        public void ClearAll_ThenCreate_Works()
        {
            _store.CreateTabular("before");
            _store.ClearAll();

            var after = _store.CreateTabular("after");
            Assert.NotNull(after);
            Assert.Single(_store.TabularNames);
            Assert.Contains("after", _store.TabularNames);
        }

        #endregion

        #region #35 — BFS Edge Filter with Direction

        [Fact]
        public void BFS_TraverseIn_WithEdgeFilter_FiltersCorrectly()
        {
            var graph = new GraphData("bfs-edge-filter");

            graph.AddNode("a");
            graph.AddNode("b");
            graph.AddNode("c");
            graph.AddNode("d");

            // a→b (weight=1), c→b (weight=5), d→b (weight=10)
            graph.AddEdge("a", "b", new Dictionary<string, object> { ["weight"] = 1.0 });
            graph.AddEdge("c", "b", new Dictionary<string, object> { ["weight"] = 5.0 });
            graph.AddEdge("d", "b", new Dictionary<string, object> { ["weight"] = 10.0 });

            // Traverse IN from b, filter edges with weight > 3
            var result = graph.Query()
                .From("b")
                .TraverseIn()
                .WhereEdgeProperty("weight", QueryOp.Gt, 3.0)
                .ToNodeIds()
                .ToList();

            // Should find c (weight=5) and d (weight=10), but NOT a (weight=1)
            Assert.Contains("c", result);
            Assert.Contains("d", result);
            Assert.DoesNotContain("a", result);
        }

        [Fact]
        public void BFS_TraverseOut_WithEdgeFilter_FiltersCorrectly()
        {
            var graph = new GraphData("bfs-out-filter");

            graph.AddNode("a");
            graph.AddNode("b");
            graph.AddNode("c");

            graph.AddEdge("a", "b", new Dictionary<string, object> { ["weight"] = 1.0 });
            graph.AddEdge("a", "c", new Dictionary<string, object> { ["weight"] = 10.0 });

            // Traverse OUT from a, filter edges with weight > 5
            var result = graph.Query()
                .From("a")
                .TraverseOut()
                .WhereEdgeProperty("weight", QueryOp.Gt, 5.0)
                .ToNodeIds()
                .ToList();

            // Start node a is always returned (BFS yields it before checking edges)
            Assert.Contains("c", result);
            Assert.DoesNotContain("b", result);
        }

        [Fact]
        public void LiteDb_BFS_TraverseIn_WithEdgeFilter_FiltersCorrectly()
        {
            var graph = _store.CreateGraph("litedb-bfs-filter");

            graph.AddNode("a");
            graph.AddNode("b");
            graph.AddNode("c");

            graph.AddEdge("a", "b", new Dictionary<string, object> { ["weight"] = 2.0 });
            graph.AddEdge("c", "b", new Dictionary<string, object> { ["weight"] = 8.0 });

            var result = graph.Query()
                .From("b")
                .TraverseIn()
                .WhereEdgeProperty("weight", QueryOp.Gt, 5.0)
                .ToNodeIds()
                .ToList();

            Assert.Contains("c", result);
            Assert.DoesNotContain("a", result);
        }

        #endregion

        #region #94 — Offset Pagination

        [Fact]
        public void Offset_SkipsFirstNRows()
        {
            var session = (AroAro.DataCore.Session.Session)_store.SessionManager.CreateSession("offset-test");
            var df = session.CreateDataFrame("numbers");

            var col = new PrimitiveDataFrameColumn<double>("val",
                Enumerable.Range(0, 100).Select(i => (double)i).ToArray());
            df.Columns.Add(col);

            var result = session.QueryDataFrame("numbers")
                .Offset(10)
                .ExecuteAsDataFrame();

            Assert.Equal(90, (int)result.Rows.Count);
            // First row should be val=10
            Assert.Equal(10.0, (double)result.Columns["val"][0]);
        }

        [Fact]
        public void Offset_WithOrderBy_PreservesOrder()
        {
            var session = (AroAro.DataCore.Session.Session)_store.SessionManager.CreateSession("offset-order");
            var df = session.CreateDataFrame("nums");

            var col = new PrimitiveDataFrameColumn<double>("val",
                new double[] { 50, 10, 40, 20, 30 });
            df.Columns.Add(col);

            var result = session.QueryDataFrame("nums")
                .OrderBy("val")
                .Offset(2)
                .ExecuteAsDataFrame();

            Assert.Equal(3, (int)result.Rows.Count);
            // After sorting [10,20,30,40,50], skip 2 → [30,40,50]
            Assert.Equal(30.0, (double)result.Columns["val"][0]);
            Assert.Equal(40.0, (double)result.Columns["val"][1]);
            Assert.Equal(50.0, (double)result.Columns["val"][2]);
        }

        [Fact]
        public void Offset_GreaterThanRowCount_ReturnsEmpty()
        {
            var session = (AroAro.DataCore.Session.Session)_store.SessionManager.CreateSession("offset-overflow");
            var df = session.CreateDataFrame("small");

            var col = new PrimitiveDataFrameColumn<double>("val",
                new double[] { 1, 2, 3 });
            df.Columns.Add(col);

            var result = session.QueryDataFrame("small")
                .Offset(100)
                .ExecuteAsDataFrame();

            Assert.Equal(0, (int)result.Rows.Count);
        }

        [Fact]
        public void Offset_Zero_IsNoOp()
        {
            var session = (AroAro.DataCore.Session.Session)_store.SessionManager.CreateSession("offset-zero");
            var df = session.CreateDataFrame("data");

            var col = new PrimitiveDataFrameColumn<double>("val",
                new double[] { 1, 2, 3 });
            df.Columns.Add(col);

            var result = session.QueryDataFrame("data")
                .Offset(0)
                .ExecuteAsDataFrame();

            Assert.Equal(3, (int)result.Rows.Count);
        }

        #endregion

        #region #16 — Edge Key Separator Safety

        [Fact]
        public void GraphData_EdgeKey_WithSpecialCharsInNodeId_RoundTrips()
        {
            var graph = new GraphData("special-chars");

            // Node IDs that could conflict with old \0 separator
            var nodeA = "path/to/node";
            var nodeB = "other\x00node"; // contains null byte
            var nodeC = "normal";

            graph.AddNode(nodeA);
            graph.AddNode(nodeB);
            graph.AddNode(nodeC);

            graph.AddEdge(nodeA, nodeB);
            graph.AddEdge(nodeB, nodeC);

            var edges = graph.GetEdges().ToList();
            Assert.Equal(2, edges.Count);

            // Verify edge endpoints are preserved correctly
            Assert.Contains(edges, e => e.From == nodeA && e.To == nodeB);
            Assert.Contains(edges, e => e.From == nodeB && e.To == nodeC);

            // Verify neighbor queries work
            var outNeighbors = graph.GetOutNeighbors(nodeB).ToList();
            Assert.Contains(nodeC, outNeighbors);

            var inNeighbors = graph.GetInNeighbors(nodeB).ToList();
            Assert.Contains(nodeA, inNeighbors);
        }

        [Fact]
        public void GraphData_EdgeKey_WithSeparatorCharsInNodeId_NoCorruption()
        {
            var graph = new GraphData("separator-test");

            // Use node IDs that contain the new separator \x01\x02
            var nodeA = "abc\x01\x02def";
            var nodeB = "ghi";

            graph.AddNode(nodeA);
            graph.AddNode(nodeB);
            graph.AddEdge(nodeA, nodeB);

            Assert.True(graph.HasEdge(nodeA, nodeB));
            Assert.False(graph.HasEdge(nodeB, nodeA)); // directed

            var edges = graph.GetEdges().ToList();
            Assert.Single(edges);
            Assert.Equal(nodeA, edges[0].From);
            Assert.Equal(nodeB, edges[0].To);
        }

        #endregion

        #region #14 — GraphData.WithName Consistency

        [Fact]
        public void GraphData_WithName_CopiesAllNodesAndEdges()
        {
            var graph = new GraphData("original");
            graph.AddNode("a", new Dictionary<string, object> { ["color"] = "red" });
            graph.AddNode("b", new Dictionary<string, object> { ["color"] = "blue" });
            graph.AddNode("c"); // isolated node
            graph.AddEdge("a", "b", new Dictionary<string, object> { ["weight"] = 5.0 });

            var copy = (IGraphDataset)graph.WithName("copy");

            Assert.Equal("copy", copy.Name);
            Assert.Equal(3, copy.NodeCount);
            Assert.Equal(1, copy.EdgeCount);

            // Verify node properties preserved
            var propsA = copy.GetNodeProperties("a");
            Assert.Equal("red", propsA["color"]);

            // Verify edges preserved
            Assert.True(copy.HasEdge("a", "b"));

            // Verify isolated node has proper adjacency entries
            var cNeighbors = copy.GetNeighbors("c").ToList();
            Assert.Empty(cNeighbors);

            // Verify adjacency works on copy
            var aOut = copy.GetOutNeighbors("a").ToList();
            Assert.Contains("b", aOut);
        }

        [Fact]
        public void GraphData_WithName_IsIndependentOfOriginal()
        {
            var graph = new GraphData("original");
            graph.AddNode("x");
            graph.AddNode("y");
            graph.AddEdge("x", "y");

            var copy = (IGraphDataset)graph.WithName("copy");

            // Modify original
            graph.AddNode("z");
            graph.AddEdge("y", "z");

            // Copy should not be affected
            Assert.Equal(2, copy.NodeCount);
            Assert.Equal(1, copy.EdgeCount);
            Assert.False(copy.HasNode("z"));
        }

        [Fact]
        public void GraphData_WithName_AfterEdgeOperations_AllEdgesCopied()
        {
            var graph = new GraphData("edge-test");
            for (int i = 0; i < 10; i++)
                graph.AddNode($"n{i}");
            for (int i = 0; i < 9; i++)
                graph.AddEdge($"n{i}", $"n{i + 1}");

            var copy = (IGraphDataset)graph.WithName("edge-copy");

            Assert.Equal(10, copy.NodeCount);
            Assert.Equal(9, copy.EdgeCount);

            for (int i = 0; i < 9; i++)
            {
                Assert.True(copy.HasEdge($"n{i}", $"n{i + 1}"),
                    $"Edge n{i}->n{i + 1} missing from copy");
            }
        }

        #endregion
    }
}
