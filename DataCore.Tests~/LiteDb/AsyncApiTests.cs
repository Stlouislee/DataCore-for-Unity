using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AroAro.DataCore;
using AroAro.DataCore.LiteDb;
using Xunit;

namespace DataCore.Tests.LiteDb
{
    [Collection("LiteDB")]
    public class AsyncApiTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly LiteDbDataStore _store;

        public AsyncApiTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"datacore_async_test_{Guid.NewGuid():N}.db");
            _store = new LiteDbDataStore(_dbPath);
        }

        public void Dispose()
        {
            try { _store.Dispose(); } catch { /* cleanup */ }
            try
            {
                if (File.Exists(_dbPath)) File.Delete(_dbPath);
                var logPath = _dbPath + "-log";
                if (File.Exists(logPath)) File.Delete(logPath);
            }
            catch { /* cleanup */ }
        }

        #region Graph Async

        [Fact]
        public async Task AddNodeAsync_AddsNodeSuccessfully()
        {
            var g = _store.CreateGraph("g1");
            await g.AddNodeAsync("n1", new Dictionary<string, object> { { "name", "Alice" } });

            Assert.True(g.HasNode("n1"));
            Assert.Equal(1, g.NodeCount);
        }

        [Fact]
        public async Task AddNodesAsync_BatchOperation_AddsAll()
        {
            var g = _store.CreateGraph("g2");
            var nodes = new[]
            {
                ("n1", (IDictionary<string, object>)new Dictionary<string, object> { { "name", "A" } }),
                ("n2", (IDictionary<string, object>)new Dictionary<string, object> { { "name", "B" } }),
                ("n3", (IDictionary<string, object>)new Dictionary<string, object> { { "name", "C" } })
            };

            int count = await g.AddNodesAsync(nodes);

            Assert.Equal(3, count);
            Assert.Equal(3, g.NodeCount);
        }

        [Fact]
        public async Task GetOutNeighborsAsync_ReturnsCorrectNeighbors()
        {
            var g = _store.CreateGraph("g3");
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b");
            g.AddEdge("a", "c");

            var neighbors = await g.GetOutNeighborsAsync("a");

            Assert.Equal(2, neighbors.Count());
            Assert.Contains("b", neighbors);
            Assert.Contains("c", neighbors);
        }

        [Fact]
        public async Task AddEdgeAsync_AddsEdgeSuccessfully()
        {
            var g = _store.CreateGraph("g4");
            g.AddNode("a");
            g.AddNode("b");

            await g.AddEdgeAsync("a", "b", new Dictionary<string, object> { { "weight", 1.5 } });

            Assert.True(g.HasEdge("a", "b"));
            Assert.Equal(1, g.EdgeCount);
        }

        [Fact]
        public async Task AddEdgesAsync_BatchOperation_AddsAll()
        {
            var g = _store.CreateGraph("g5");
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");

            var edges = new[]
            {
                ("a", "b", (IDictionary<string, object>)null),
                ("b", "c", (IDictionary<string, object>)null),
                ("c", "a", (IDictionary<string, object>)null)
            };

            int count = await g.AddEdgesAsync(edges);

            Assert.Equal(3, count);
            Assert.Equal(3, g.EdgeCount);
        }

        [Fact]
        public async Task RemoveNodeAsync_RemovesSuccessfully()
        {
            var g = _store.CreateGraph("g6");
            g.AddNode("n1");
            g.AddNode("n2");
            g.AddEdge("n1", "n2");

            bool removed = await g.RemoveNodeAsync("n1");

            Assert.True(removed);
            Assert.False(g.HasNode("n1"));
            Assert.Equal(1, g.NodeCount);
        }

        [Fact]
        public async Task ClearAsync_RemovesAllData()
        {
            var g = _store.CreateGraph("g7");
            g.AddNode("n1");
            g.AddNode("n2");
            g.AddEdge("n1", "n2");

            await g.ClearAsync();

            Assert.Equal(0, g.NodeCount);
            Assert.Equal(0, g.EdgeCount);
        }

        [Fact]
        public async Task AddNodeAsync_Cancellation_ThrowsOperationCanceled()
        {
            var g = _store.CreateGraph("g8");
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => g.AddNodeAsync("n1", ct: cts.Token));
        }

        [Fact]
        public async Task GetOutNeighborsAsync_Cancellation_ThrowsOperationCanceled()
        {
            var g = _store.CreateGraph("g9");
            g.AddNode("n1");
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => g.GetOutNeighborsAsync("n1", cts.Token));
        }

        [Fact]
        public async Task AddNodeAsync_DuplicateId_ThrowsInvalidOperation()
        {
            var g = _store.CreateGraph("g10");
            g.AddNode("n1");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => g.AddNodeAsync("n1"));
        }

        [Fact]
        public async Task GraphAsync_ConcurrentWrites_AreSerialized()
        {
            var g = _store.CreateGraph("g11");
            var tasks = new List<Task>();

            // Fire 50 concurrent add operations
            for (int i = 0; i < 50; i++)
            {
                int idx = i;
                tasks.Add(g.AddNodeAsync($"node_{idx}"));
            }

            await Task.WhenAll(tasks);

            Assert.Equal(50, g.NodeCount);
        }

        #endregion

        #region Tabular Async

        [Fact]
        public async Task AddRowAsync_AddsRowSuccessfully()
        {
            var t = _store.CreateTabular("t1");

            await t.AddRowAsync(new Dictionary<string, object>
            {
                { "name", "Alice" },
                { "age", 30.0 }
            });

            Assert.Equal(1, t.RowCount);
        }

        [Fact]
        public async Task AddRowsAsync_BatchOperation_AddsAll()
        {
            var t = _store.CreateTabular("t2");
            var rows = new[]
            {
                new Dictionary<string, object> { { "x", 1.0 } },
                new Dictionary<string, object> { { "x", 2.0 } },
                new Dictionary<string, object> { { "x", 3.0 } }
            };

            int count = await t.AddRowsAsync(rows);

            Assert.Equal(3, count);
            Assert.Equal(3, t.RowCount);
        }

        [Fact]
        public async Task AddNumericColumnAsync_AddsColumn()
        {
            var t = _store.CreateTabular("t3");

            await t.AddNumericColumnAsync("values", new[] { 1.0, 2.0, 3.0 });

            Assert.True(t.HasColumn("values"));
            Assert.Equal(3, t.RowCount);
        }

        [Fact]
        public async Task AddStringColumnAsync_AddsColumn()
        {
            var t = _store.CreateTabular("t4");

            await t.AddStringColumnAsync("names", new[] { "a", "b", "c" });

            Assert.True(t.HasColumn("names"));
            Assert.Equal(3, t.RowCount);
        }

        [Fact]
        public async Task ClearAsync_Tabular_RemovesAllRows()
        {
            var t = _store.CreateTabular("t5");
            t.AddNumericColumn("x", new[] { 1.0, 2.0 });

            int cleared = await t.ClearAsync();

            Assert.Equal(2, cleared);
            Assert.Equal(0, t.RowCount);
        }

        [Fact]
        public async Task AddRowAsync_Cancellation_ThrowsOperationCanceled()
        {
            var t = _store.CreateTabular("t6");
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => t.AddRowAsync(new Dictionary<string, object> { { "x", 1.0 } }, cts.Token));
        }

        [Fact]
        public async Task TabularAsync_ConcurrentWrites_AreSerialized()
        {
            var t = _store.CreateTabular("t7");
            t.AddNumericColumn("x", Array.Empty<double>());

            var tasks = new List<Task>();
            for (int i = 0; i < 50; i++)
            {
                int idx = i;
                tasks.Add(t.AddRowAsync(new Dictionary<string, object> { { "x", (double)idx } }));
            }

            await Task.WhenAll(tasks);

            Assert.Equal(50, t.RowCount);
        }

        #endregion
    }
}
