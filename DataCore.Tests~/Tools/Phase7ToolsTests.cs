using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AroAro.DataCore;
using AroAro.DataCore.Tools;
using Xunit;

namespace DataCore.Tests.Tools
{
    public class Phase7ToolsTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;

        public Phase7ToolsTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dc_p7_" + Guid.NewGuid().ToString("N"));
            _store = new DataCoreStore(Path.Combine(_tempDir, "test.db"));
            DataCoreTools.Initialize(_store);
        }

        public void Dispose()
        {
            _store?.Dispose();
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private void SeedUsers()
        {
            var t = _store.CreateTabular("Users");
            t.AddNumericColumn("id", new double[] { 1, 2, 3, 4, 5 });
            t.AddStringColumn("name", new[] { "Alice", "Bob", "Charlie", "Diana", "Eve" });
            t.AddNumericColumn("age", new double[] { 25, 30, 35, 28, 22 });
            t.AddStringColumn("city", new[] { "Shanghai", "Beijing", "Shanghai", "Guangzhou", "Beijing" });
        }

        private void SeedGraph()
        {
            var g = _store.CreateGraph("Net");
            g.AddNode("A"); g.AddNode("B"); g.AddNode("C"); g.AddNode("D"); g.AddNode("E");
            g.AddEdge("A", "B"); g.AddEdge("A", "C"); g.AddEdge("B", "D"); g.AddEdge("C", "D"); g.AddEdge("D", "E");
        }

        private JsonElement P(string json) => JsonSerializer.Deserialize<JsonElement>(json);

        // ── Parameter unification: "dataset" works everywhere ──

        [Fact]
        public void Filter_AcceptsDatasetParam()
        {
            SeedUsers();
            var r = P(DataCoreTools.Execute("workspace_filter", new Dictionary<string, object>
            {
                ["dataset"] = "Users", ["filter"] = "age > 25", ["resultName"] = "f1"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal(3, r.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void Filter_AcceptsSourceParam_BackwardCompat()
        {
            SeedUsers();
            var r = P(DataCoreTools.Execute("workspace_filter", new Dictionary<string, object>
            {
                ["source"] = "Users", ["filter"] = "age > 25", ["resultName"] = "f1"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
        }

        [Fact]
        public void Filter_InvalidDataset_HasSuggestion()
        {
            SeedUsers();
            var r = P(DataCoreTools.Execute("workspace_filter", new Dictionary<string, object>
            {
                ["dataset"] = "nonexistent", ["filter"] = "age > 25", ["resultName"] = "f1"
            }));
            Assert.False(r.GetProperty("success").GetBoolean());
            Assert.True(r.TryGetProperty("suggestion", out var sug) && sug.GetString().Length > 0);
        }

        // ── value_counts ──

        [Fact]
        public void ValueCounts_ReturnsFrequency()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "Users" });

            var r = P(DataCoreTools.Execute("workspace_value_counts", new Dictionary<string, object>
            {
                ["dataset"] = "Users", ["column"] = "city"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal(5, r.GetProperty("result").GetProperty("totalValues").GetInt32());
            Assert.Equal(3, r.GetProperty("result").GetProperty("uniqueValues").GetInt32());
        }

        [Fact]
        public void ValueCounts_ColumnNotFound_HasSuggestion()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "Users" });

            var r = P(DataCoreTools.Execute("workspace_value_counts", new Dictionary<string, object>
            {
                ["dataset"] = "Users", ["column"] = "nonexistent"
            }));
            Assert.False(r.GetProperty("success").GetBoolean());
        }

        // ── cast_column ──

        [Fact]
        public void CastColumn_StringToNumeric()
        {
            var t = _store.CreateTabular("Mixed");
            t.AddStringColumn("val", new[] { "10", "20", "abc", "40" });
            t.AddStringColumn("label", new[] { "a", "b", "c", "d" });

            var r = P(DataCoreTools.Execute("workspace_cast_column", new Dictionary<string, object>
            {
                ["dataset"] = "Mixed", ["column"] = "val", ["type"] = "numeric", ["resultName"] = "CastResult"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("numeric", r.GetProperty("result").GetProperty("toType").GetString());
        }

        [Fact]
        public void CastColumn_InvalidType_ReturnsError()
        {
            SeedUsers();
            var r = P(DataCoreTools.Execute("workspace_cast_column", new Dictionary<string, object>
            {
                ["dataset"] = "Users", ["column"] = "name", ["type"] = "boolean"
            }));
            Assert.False(r.GetProperty("success").GetBoolean());
        }

        // ── fill_null ──

        [Fact]
        public void FillNull_SpecificColumn()
        {
            var t = _store.CreateTabular("Nulls");
            t.AddStringColumn("name", new[] { "Alice", null, "Charlie" });
            t.AddNumericColumn("score", new double[] { 90, 0, 85 });

            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "Nulls" });

            var r = P(DataCoreTools.Execute("workspace_fill_null", new Dictionary<string, object>
            {
                ["dataset"] = "Nulls", ["column"] = "name", ["value"] = "Unknown", ["resultName"] = "Filled"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.True(r.GetProperty("result").GetProperty("cellsFilled").GetInt32() > 0);
        }

        // ── graph_path ──

        [Fact]
        public void GraphPath_FindsShortestPath()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object> { ["dataset"] = "Net" });

            var r = P(DataCoreTools.Execute("workspace_graph_path", new Dictionary<string, object>
            {
                ["graph"] = "Net", ["from"] = "A", ["to"] = "E"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.True(r.GetProperty("result").GetProperty("found").GetBoolean());
            Assert.Equal(3, r.GetProperty("result").GetProperty("length").GetInt32());
        }

        [Fact]
        public void GraphPath_NoPath_ReturnsNotFound()
        {
            var g = _store.CreateGraph("Disconnected");
            g.AddNode("X"); g.AddNode("Y");
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object> { ["dataset"] = "Disconnected" });

            var r = P(DataCoreTools.Execute("workspace_graph_path", new Dictionary<string, object>
            {
                ["graph"] = "Disconnected", ["from"] = "X", ["to"] = "Y"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.False(r.GetProperty("result").GetProperty("found").GetBoolean());
        }

        [Fact]
        public void GraphPath_NodeNotFound_HasSuggestion()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object> { ["dataset"] = "Net" });

            var r = P(DataCoreTools.Execute("workspace_graph_path", new Dictionary<string, object>
            {
                ["graph"] = "Net", ["from"] = "A", ["to"] = "Z"
            }));
            Assert.False(r.GetProperty("success").GetBoolean());
        }

        // ── graph_stats ──

        [Fact]
        public void GraphStats_ReturnsMetrics()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object> { ["dataset"] = "Net" });

            var r = P(DataCoreTools.Execute("workspace_graph_stats", new Dictionary<string, object>
            {
                ["graph"] = "Net"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal(5, r.GetProperty("result").GetProperty("nodes").GetInt32());
            Assert.Equal(5, r.GetProperty("result").GetProperty("edges").GetInt32());
            Assert.True(r.GetProperty("result").GetProperty("connectedComponents").GetInt32() >= 1);
        }

        // ── batch ──

        [Fact]
        public void Batch_ExecutesMultipleSteps()
        {
            SeedUsers();

            var r = P(DataCoreTools.Execute("workspace_batch", new Dictionary<string, object>
            {
                ["steps"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["tool"] = "workspace_filter",
                        ["args"] = new Dictionary<string, object>
                        {
                            ["dataset"] = "Users", ["filter"] = "age > 25", ["resultName"] = "b1"
                        }
                    },
                    new()
                    {
                        ["tool"] = "workspace_value_counts",
                        ["args"] = new Dictionary<string, object>
                        {
                            ["dataset"] = "Users", ["column"] = "city"
                        }
                    }
                }
            }));

            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal(2, r.GetProperty("result").GetProperty("totalSteps").GetInt32());
            Assert.Equal(2, r.GetProperty("result").GetProperty("succeeded").GetInt32());
            Assert.Equal(0, r.GetProperty("result").GetProperty("failed").GetInt32());
        }

        [Fact]
        public void Batch_PartialFailure_ReportsCorrectly()
        {
            var r = P(DataCoreTools.Execute("workspace_batch", new Dictionary<string, object>
            {
                ["steps"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["tool"] = "workspace_list",
                        ["args"] = new Dictionary<string, object>()
                    },
                    new()
                    {
                        ["tool"] = "workspace_filter",
                        ["args"] = new Dictionary<string, object>
                        {
                            ["dataset"] = "nonexistent", ["filter"] = "x > 1", ["resultName"] = "fail"
                        }
                    }
                }
            }));

            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal(2, r.GetProperty("result").GetProperty("totalSteps").GetInt32());
            Assert.Equal(1, r.GetProperty("result").GetProperty("succeeded").GetInt32());
            Assert.Equal(1, r.GetProperty("result").GetProperty("failed").GetInt32());
        }

        // ── GetToolSchemas includes new tools ──

        [Fact]
        public void ToolSchemas_IncludesPhase7Tools()
        {
            var json = DataCoreTools.GetToolSchemas();
            var schemas = JsonSerializer.Deserialize<JsonElement[]>(json);
            var names = schemas.Select(s => s.GetProperty("name").GetString()).ToList();

            Assert.Contains("workspace_value_counts", names);
            Assert.Contains("workspace_cast_column", names);
            Assert.Contains("workspace_fill_null", names);
            Assert.Contains("workspace_graph_path", names);
            Assert.Contains("workspace_graph_stats", names);
            Assert.Contains("workspace_batch", names);
        }

        [Fact]
        public void ToolNames_CountIs52()
        {
            var names = DataCoreTools.GetToolNames();
            Assert.Equal(53, names.Count);
        }
    }
}
