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
    public class Phase8ToolsTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;

        public Phase8ToolsTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dc_p8_" + Guid.NewGuid().ToString("N"));
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
            t.AddNumericColumn("score", new double[] { 85, 90, 78, 92, 88 });
            t.AddStringColumn("city", new[] { "Shanghai", "Beijing", "Shanghai", "Guangzhou", "Beijing" });
        }

        private void SeedGraph()
        {
            var g = _store.CreateGraph("Net");
            g.AddNode("A"); g.AddNode("B"); g.AddNode("C"); g.AddNode("D"); g.AddNode("E");
            g.AddEdge("A", "B"); g.AddEdge("A", "C"); g.AddEdge("B", "D"); g.AddEdge("C", "D"); g.AddEdge("D", "E");
        }

        private void SeedOutlierData()
        {
            var t = _store.CreateTabular("SensorData");
            t.AddNumericColumn("value", new double[] { 10, 12, 11, 13, 10, 12, 11, 100, 12, 11, 13, 10, 12, 11, -50 });
            t.AddStringColumn("label", new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o" });
        }

        private JsonElement P(string json) => JsonSerializer.Deserialize<JsonElement>(json);

        // ── workspace_analysis: describe ──

        [Fact]
        public void Describe_ReturnsPerColumnStats()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "Users" });

            var r = P(DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
            {
                ["dataset"] = "Users", ["analysis"] = "describe"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("describe", r.GetProperty("result").GetProperty("analysis").GetString());
            Assert.Equal(5, r.GetProperty("result").GetProperty("rows").GetInt32());

            var columns = r.GetProperty("result").GetProperty("columns");
            Assert.Equal(5, columns.GetArrayLength());

            // Find the "age" column and verify stats
            JsonElement ageCol = default;
            foreach (var col in columns.EnumerateArray())
            {
                if (col.GetProperty("name").GetString() == "age")
                {
                    ageCol = col;
                    break;
                }
            }
            Assert.Equal("Numeric", ageCol.GetProperty("type").GetString());
            Assert.Equal(5, ageCol.GetProperty("nonNull").GetInt32());
            Assert.Equal(5, ageCol.GetProperty("unique").GetInt32());
            Assert.True(ageCol.TryGetProperty("mean", out _));
            Assert.True(ageCol.TryGetProperty("median", out _));
            Assert.True(ageCol.TryGetProperty("std", out _));
            Assert.True(ageCol.TryGetProperty("p25", out _));
            Assert.True(ageCol.TryGetProperty("p75", out _));
            Assert.True(ageCol.TryGetProperty("skewness", out _));
            Assert.True(ageCol.TryGetProperty("kurtosis", out _));
            Assert.Equal(0, ageCol.GetProperty("nullRate").GetDouble(), 4);
        }

        // ── workspace_analysis: correlation ──

        [Fact]
        public void Correlation_ReturnsMatrix()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "Users" });

            var r = P(DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
            {
                ["dataset"] = "Users", ["analysis"] = "correlation",
                ["columns"] = new[] { "age", "score" }
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("correlation", r.GetProperty("result").GetProperty("analysis").GetString());

            var matrix = r.GetProperty("result").GetProperty("matrix");
            Assert.Equal(1.0, matrix.GetProperty("age").GetProperty("age").GetDouble(), 4);
            Assert.Equal(1.0, matrix.GetProperty("score").GetProperty("score").GetDouble(), 4);
            // Cross-correlation should be between -1 and 1
            double crossCorr = matrix.GetProperty("age").GetProperty("score").GetDouble();
            Assert.InRange(crossCorr, -1.0, 1.0);
        }

        // ── workspace_analysis: outliers ──

        [Fact]
        public void Outliers_DetectsOutlierRows()
        {
            SeedOutlierData();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "SensorData" });

            var r = P(DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
            {
                ["dataset"] = "SensorData", ["analysis"] = "outliers", ["column"] = "value"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("outliers", r.GetProperty("result").GetProperty("analysis").GetString());
            // 100 and -50 should be outliers
            Assert.True(r.GetProperty("result").GetProperty("outlierCount").GetInt32() >= 2);
            Assert.Equal(15, r.GetProperty("result").GetProperty("totalRows").GetInt32());
        }

        // ── workspace_analysis: distribution ──

        [Fact]
        public void Distribution_ReturnsHistogramBins()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "Users" });

            var r = P(DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
            {
                ["dataset"] = "Users", ["analysis"] = "distribution", ["column"] = "age", ["bins"] = 3
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("distribution", r.GetProperty("result").GetProperty("analysis").GetString());

            var bins = r.GetProperty("result").GetProperty("bins");
            Assert.Equal(3, bins.GetArrayLength());

            // Total count across bins should equal row count
            int total = 0;
            foreach (var bin in bins.EnumerateArray())
                total += bin.GetProperty("count").GetInt32();
            Assert.Equal(5, total);

            var stats = r.GetProperty("result").GetProperty("stats");
            Assert.Equal(5, stats.GetProperty("count").GetInt32());
            Assert.True(stats.TryGetProperty("mean", out _));
            Assert.True(stats.TryGetProperty("std", out _));
        }

        // ── workspace_analysis: clustering ──

        [Fact]
        public void Clustering_AssignsClusterLabels()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "Users" });

            var r = P(DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
            {
                ["dataset"] = "Users", ["analysis"] = "clustering", ["k"] = 2, ["resultName"] = "UsersClusters"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("clustering", r.GetProperty("result").GetProperty("analysis").GetString());
            Assert.Equal(2, r.GetProperty("result").GetProperty("k").GetInt32());

            var centroids = r.GetProperty("result").GetProperty("centroids");
            Assert.Equal(2, centroids.GetArrayLength());
            Assert.True(r.GetProperty("result").TryGetProperty("inertia", out _));
        }

        // ── workspace_analysis: centrality ──

        [Fact]
        public void Centrality_ComputesDegreeCentrality()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object> { ["dataset"] = "Net" });

            var r = P(DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
            {
                ["graph"] = "Net", ["analysis"] = "centrality", ["method"] = "degree"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("centrality", r.GetProperty("result").GetProperty("analysis").GetString());
            Assert.Equal("degree", r.GetProperty("result").GetProperty("method").GetString());
            Assert.Equal(5, r.GetProperty("result").GetProperty("nodes").GetInt32());

            var topNodes = r.GetProperty("result").GetProperty("topNodes");
            Assert.True(topNodes.GetArrayLength() > 0);
            // Node D has highest degree (connected to B, C, E → degree 3)
            Assert.Equal("D", topNodes[0].GetProperty("id").GetString());
        }

        [Fact]
        public void Centrality_Betweenness()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object> { ["dataset"] = "Net" });

            var r = P(DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
            {
                ["graph"] = "Net", ["analysis"] = "centrality", ["method"] = "betweenness"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("betweenness", r.GetProperty("result").GetProperty("method").GetString());
            var topNodes = r.GetProperty("result").GetProperty("topNodes");
            Assert.True(topNodes.GetArrayLength() > 0);
        }

        [Fact]
        public void Centrality_Closeness()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object> { ["dataset"] = "Net" });

            var r = P(DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
            {
                ["graph"] = "Net", ["analysis"] = "centrality", ["method"] = "closeness"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("closeness", r.GetProperty("result").GetProperty("method").GetString());
            var topNodes = r.GetProperty("result").GetProperty("topNodes");
            Assert.True(topNodes.GetArrayLength() > 0);
        }

        // ── workspace_analysis: communities ──

        [Fact]
        public void Communities_DetectsCommunities()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object> { ["dataset"] = "Net" });

            var r = P(DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
            {
                ["graph"] = "Net", ["analysis"] = "communities"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("communities", r.GetProperty("result").GetProperty("analysis").GetString());
            Assert.Equal(5, r.GetProperty("result").GetProperty("nodes").GetInt32());
            Assert.True(r.GetProperty("result").GetProperty("communities").GetInt32() >= 1);

            var data = r.GetProperty("result").GetProperty("data");
            Assert.True(data.GetArrayLength() > 0);
            // Each community has size and members
            var first = data[0];
            Assert.True(first.GetProperty("size").GetInt32() > 0);
            Assert.True(first.GetProperty("members").GetArrayLength() > 0);
        }

        // ── workspace_analysis: shortest_path ──

        [Fact]
        public void ShortestPath_FindsPath()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object> { ["dataset"] = "Net" });

            var r = P(DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
            {
                ["graph"] = "Net", ["analysis"] = "shortest_path", ["from"] = "A", ["to"] = "E"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("shortest_path", r.GetProperty("result").GetProperty("analysis").GetString());
            Assert.True(r.GetProperty("result").GetProperty("found").GetBoolean());
            Assert.Equal(3, r.GetProperty("result").GetProperty("length").GetInt32());

            var path = r.GetProperty("result").GetProperty("path");
            Assert.Equal(4, path.GetArrayLength());
            Assert.Equal("A", path[0].GetString());
            Assert.Equal("E", path[3].GetString());
        }

        // ── workspace_analysis: invalid analysis ──

        [Fact]
        public void InvalidAnalysis_ReturnsErrorWithSuggestion()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "Users" });

            var r = P(DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
            {
                ["dataset"] = "Users", ["analysis"] = "nonexistent"
            }));
            Assert.False(r.GetProperty("success").GetBoolean());
            Assert.True(r.TryGetProperty("suggestion", out var sug) && sug.GetString().Length > 0);
        }

        // ── workspace_algorithm: list ──

        [Fact]
        public void AlgorithmList_ReturnsAllRegisteredAlgorithms()
        {
            var r = P(DataCoreTools.Execute("workspace_algorithm", new Dictionary<string, object>
            {
                ["algorithm"] = "list"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("list", r.GetProperty("result").GetProperty("mode").GetString());
            Assert.True(r.GetProperty("result").GetProperty("count").GetInt32() >= 3);

            var algorithms = r.GetProperty("result").GetProperty("algorithms");
            var names = new List<string>();
            foreach (var a in algorithms.EnumerateArray())
                names.Add(a.GetProperty("name").GetString());

            Assert.Contains("PageRank", names);
            Assert.Contains("ConnectedComponents", names);
            Assert.Contains("MinMaxNormalize", names);
        }

        // ── workspace_algorithm: execute PageRank ──

        [Fact]
        public void AlgorithmExecute_PageRank()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object> { ["dataset"] = "Net" });

            var r = P(DataCoreTools.Execute("workspace_algorithm", new Dictionary<string, object>
            {
                ["algorithm"] = "PageRank", ["graph"] = "Net", ["resultName"] = "NetPageRank"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("PageRank", r.GetProperty("result").GetProperty("algorithm").GetString());
            Assert.True(r.GetProperty("result").GetProperty("success").GetBoolean());
        }

        // ── workspace_algorithm: execute MinMaxNormalize ──

        [Fact]
        public void AlgorithmExecute_MinMaxNormalize()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "Users" });

            var r = P(DataCoreTools.Execute("workspace_algorithm", new Dictionary<string, object>
            {
                ["algorithm"] = "MinMaxNormalize", ["dataset"] = "Users", ["resultName"] = "UsersNorm"
            }));
            Assert.True(r.GetProperty("success").GetBoolean());
            Assert.Equal("MinMaxNormalize", r.GetProperty("result").GetProperty("algorithm").GetString());
            Assert.True(r.GetProperty("result").GetProperty("success").GetBoolean());
        }

        // ── workspace_algorithm: invalid algorithm ──

        [Fact]
        public void AlgorithmExecute_InvalidName_ReturnsError()
        {
            var r = P(DataCoreTools.Execute("workspace_algorithm", new Dictionary<string, object>
            {
                ["algorithm"] = "NonexistentAlgo", ["dataset"] = "Users"
            }));
            Assert.False(r.GetProperty("success").GetBoolean());
            Assert.Contains("NonexistentAlgo", r.GetProperty("error").GetString());
        }

        // ── workspace_algorithm: missing input ──

        [Fact]
        public void AlgorithmExecute_MissingInput_ReturnsError()
        {
            var r = P(DataCoreTools.Execute("workspace_algorithm", new Dictionary<string, object>
            {
                ["algorithm"] = "PageRank"
            }));
            Assert.False(r.GetProperty("success").GetBoolean());
            Assert.Contains("Missing", r.GetProperty("error").GetString());
        }

        // ── GetToolSchemas includes Phase 8 tools ──

        [Fact]
        public void ToolSchemas_IncludesPhase8Tools()
        {
            var json = DataCoreTools.GetToolSchemas();
            var schemas = JsonSerializer.Deserialize<JsonElement[]>(json);
            var names = schemas.Select(s => s.GetProperty("name").GetString()).ToList();

            Assert.Contains("workspace_analysis", names);
            Assert.Contains("workspace_algorithm", names);
        }

        [Fact]
        public void ToolNames_CountIs55()
        {
            var names = DataCoreTools.GetToolNames();
            Assert.Equal(55, names.Count);
        }
    }
}
