using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AroAro.DataCore;
using AroAro.DataCore.Graph;
using AroAro.DataCore.Tabular;
using Xunit;

namespace DataCore.Tests
{
    public class EdgeCaseTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DataCoreStore _store;

        public EdgeCaseTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"datacore_edge_{Guid.NewGuid():N}.db");
            _store = new DataCoreStore(_dbPath);
        }

        public void Dispose()
        {
            _store?.Dispose();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        // ────────────────────────────────────────────────────────────────
        // Tabular: null, NaN, Infinity, empty
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Tabular_NullStringInColumn_HandledGracefully()
        {
            var ds = _store.CreateTabular("null-str");
            ds.AddStringColumn("name", new string[] { "Alice", null, "Charlie" });

            Assert.Equal(3, ds.RowCount);
            var col = ds.GetStringColumn("name");
            Assert.Equal("Alice", col[0]);
            Assert.Null(col[1]);
            Assert.Equal("Charlie", col[2]);
        }

        [Fact]
        public void Tabular_NaN_InNumericColumn_StoredCorrectly()
        {
            var ds = _store.CreateTabular("nan");
            ds.AddNumericColumn("values", new double[] { 1.0, double.NaN, 3.0, double.PositiveInfinity, double.NegativeInfinity });

            Assert.Equal(5, ds.RowCount);
            var col = ds.GetNumericColumnRaw("values");
            Assert.Equal(1.0, (double)col[0]);
            Assert.True(double.IsNaN((double)col[1]));
            Assert.Equal(3.0, (double)col[2]);
            Assert.True(double.IsPositiveInfinity((double)col[3]));
            Assert.True(double.IsNegativeInfinity((double)col[4]));
        }

        [Fact]
        public void Tabular_Empty_ZeroRows()
        {
            var ds = _store.CreateTabular("empty");
            ds.AddNumericColumn("x", new double[0]);
            ds.AddStringColumn("s", new string[0]);

            Assert.Equal(0, ds.RowCount);
            Assert.Equal(2, ds.ColumnCount);
        }

        [Fact]
        public void Tabular_Empty_QueryReturnsEmpty()
        {
            var ds = _store.CreateTabular("empty-q");
            ds.AddNumericColumn("x", new double[0]);

            var count = ds.Query().WhereGreaterThan("x", 0).Count();
            Assert.Equal(0, count);
        }

        [Fact]
        public void Tabular_WhitespaceOnlyStrings_Stored()
        {
            var ds = _store.CreateTabular("ws");
            ds.AddStringColumn("text", new string[] { "", "  ", "\t\n", "real" });

            Assert.Equal(4, ds.RowCount);
            var col = ds.GetStringColumn("text");
            Assert.Equal("", col[0]);
            Assert.Equal("  ", col[1]);
            Assert.Equal("\t\n", col[2]);
            Assert.Equal("real", col[3]);
        }

        [Fact]
        public void Tabular_VeryLongString_Stored()
        {
            var longStr = new string('x', 100_000);
            var ds = _store.CreateTabular("longstr");
            ds.AddStringColumn("big", new string[] { longStr });

            Assert.Equal(1, ds.RowCount);
            Assert.Equal(longStr, ds.GetStringColumn("big")[0]);
        }

        [Fact]
        public void Tabular_AddRow_WithNullValues()
        {
            var ds = _store.CreateTabular("null-row");
            ds.AddStringColumn("name", new string[0]);
            ds.AddNumericColumn("val", new double[0]);

            ds.AddRow(new Dictionary<string, object> { ["name"] = null, ["val"] = 42.0 });

            Assert.Equal(1, ds.RowCount);
            Assert.Null(ds.GetStringColumn("name")[0]);
            Assert.Equal(42.0, (double)ds.GetNumericColumnRaw("val")[0]);
        }

        [Fact]
        public void Tabular_SingleRow_Works()
        {
            var ds = _store.CreateTabular("single");
            ds.AddNumericColumn("x", new double[] { 1.0 });
            ds.AddStringColumn("s", new string[] { "only" });

            Assert.Equal(1, ds.RowCount);
            var row = ds.GetRow(0);
            Assert.Equal(1.0, row["x"]);
            Assert.Equal("only", row["s"]);
        }

        // ────────────────────────────────────────────────────────────────
        // Graph: empty, single node, self-loop
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Graph_Empty_NoNodesNoEdges()
        {
            var graph = _store.CreateGraph("empty-g");
            Assert.Equal(0, graph.NodeCount);
            Assert.Equal(0, graph.EdgeCount);
            Assert.Empty(graph.GetNodeIds());
        }

        [Fact]
        public void Graph_SingleNode_NoEdges()
        {
            var graph = _store.CreateGraph("single-node");
            graph.AddNode("only");

            Assert.Equal(1, graph.NodeCount);
            Assert.Equal(0, graph.EdgeCount);
            Assert.Empty(graph.GetOutNeighbors("only"));
            Assert.Empty(graph.GetInNeighbors("only"));
        }

        [Fact]
        public void Graph_SelfLoop_CountedAsEdge()
        {
            var graph = _store.CreateGraph("self-loop");
            graph.AddNode("A");
            graph.AddEdge("A", "A");

            Assert.Equal(1, graph.NodeCount);
            Assert.Equal(1, graph.EdgeCount);
            Assert.Contains("A", graph.GetOutNeighbors("A"));
        }

        [Fact]
        public void Graph_NodeWithEmptyProperties()
        {
            var graph = _store.CreateGraph("empty-props");
            graph.AddNode("A", new Dictionary<string, object>());

            Assert.Equal(1, graph.NodeCount);
            var props = graph.GetNodeProperties("A");
            Assert.NotNull(props);
            Assert.Empty(props);
        }

        [Fact]
        public void Graph_NodeWithNullProperties()
        {
            var graph = _store.CreateGraph("null-props");
            graph.AddNode("A", null);

            Assert.Equal(1, graph.NodeCount);
        }

        // ────────────────────────────────────────────────────────────────
        // Query edge cases
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Query_WhereIsNotNull_FiltersCorrectly()
        {
            var ds = _store.CreateTabular("qnull");
            ds.AddStringColumn("name", new string[] { "Alice", null, "Charlie" });
            ds.AddNumericColumn("id", new double[] { 1, 2, 3 });

            var count = ds.Query().WhereIsNotNull("name").Count();
            Assert.Equal(2, count);
        }

        [Fact]
        public void Query_AllRowsFiltered_ReturnsZero()
        {
            var ds = _store.CreateTabular("qempty");
            ds.AddNumericColumn("x", new double[] { 1, 2, 3 });

            var count = ds.Query().WhereGreaterThan("x", 100).Count();
            Assert.Equal(0, count);
        }

        [Fact]
        public void Query_OrderBy_OnSingleElement_Works()
        {
            var ds = _store.CreateTabular("qsingle");
            ds.AddNumericColumn("x", new double[] { 42 });

            var results = ds.Query().OrderBy("x").ToDictionaries();
            Assert.Single(results);
            Assert.Equal(42.0, results[0]["x"]);
        }

        [Fact]
        public void Query_LimitOne_ReturnsSingleRow()
        {
            var ds = _store.CreateTabular("qone");
            ds.AddNumericColumn("x", new double[] { 1, 2, 3 });

            var results = ds.Query().Limit(1).ToDictionaries();
            Assert.Single(results);
        }
    }
}
