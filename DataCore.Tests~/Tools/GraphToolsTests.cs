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
    public class GraphToolsTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;

        public GraphToolsTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dc_graph_" + Guid.NewGuid().ToString("N"));
            _store = new DataCoreStore(Path.Combine(_tempDir, "test.db"));
            DataCoreTools.Initialize(_store);
        }

        public void Dispose()
        {
            _store?.Dispose();
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private void SeedGraph()
        {
            var graph = _store.CreateGraph("SocialGraph");
            graph.AddNode("alice", new Dictionary<string, object> { ["age"] = 25, ["city"] = "Shanghai" });
            graph.AddNode("bob", new Dictionary<string, object> { ["age"] = 30, ["city"] = "Beijing" });
            graph.AddNode("charlie", new Dictionary<string, object> { ["age"] = 35, ["city"] = "Shanghai" });
            graph.AddEdge("alice", "bob", new Dictionary<string, object> { ["type"] = "friend" });
            graph.AddEdge("bob", "charlie", new Dictionary<string, object> { ["type"] = "colleague" });
            graph.AddEdge("alice", "charlie");
        }

        private JsonElement Parse(string json) => JsonSerializer.Deserialize<JsonElement>(json);

        [Fact]
        public void OpenGraph_LoadsIntoWorkspace()
        {
            SeedGraph();
            var result = Parse(DataCoreTools.Execute("workspace_open_graph",
                new Dictionary<string, object>
                {
                    ["dataset"] = "SocialGraph",
                    ["resultName"] = "myGraph"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(3, result.GetProperty("result").GetProperty("nodes").GetInt32());
            Assert.Equal(3, result.GetProperty("result").GetProperty("edges").GetInt32());
        }

        [Fact]
        public void OpenGraph_NotFound_ReturnsError()
        {
            var result = Parse(DataCoreTools.Execute("workspace_open_graph",
                new Dictionary<string, object> { ["dataset"] = "nonexistent" }));
            Assert.False(result.GetProperty("success").GetBoolean());
        }

        [Fact]
        public void AddNodes_ToGraph()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object>
            {
                ["dataset"] = "SocialGraph"
            });

            var result = Parse(DataCoreTools.Execute("workspace_add_nodes",
                new Dictionary<string, object>
                {
                    ["graph"] = "SocialGraph",
                    ["nodes"] = new List<Dictionary<string, object>>
                    {
                        new() { ["id"] = "dave", ["age"] = 28 },
                        new() { ["id"] = "eve", ["age"] = 22 }
                    }
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(2, result.GetProperty("result").GetProperty("added").GetInt32());
            Assert.Equal(5, result.GetProperty("result").GetProperty("totalNodes").GetInt32());
        }

        [Fact]
        public void AddEdges_ToGraph()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object>
            {
                ["dataset"] = "SocialGraph"
            });

            var result = Parse(DataCoreTools.Execute("workspace_add_edges",
                new Dictionary<string, object>
                {
                    ["graph"] = "SocialGraph",
                    ["edges"] = new List<Dictionary<string, object>>
                    {
                        new() { ["from"] = "alice", ["to"] = "dave" }
                    }
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(1, result.GetProperty("result").GetProperty("added").GetInt32());
        }

        [Fact]
        public void GraphNeighbors_OutDirection()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object>
            {
                ["dataset"] = "SocialGraph"
            });

            var result = Parse(DataCoreTools.Execute("workspace_graph_neighbors",
                new Dictionary<string, object>
                {
                    ["graph"] = "SocialGraph",
                    ["nodeId"] = "alice",
                    ["direction"] = "out"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(2, result.GetProperty("result").GetProperty("count").GetInt32());
        }

        [Fact]
        public void GraphNeighbors_InDirection()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object>
            {
                ["dataset"] = "SocialGraph"
            });

            var result = Parse(DataCoreTools.Execute("workspace_graph_neighbors",
                new Dictionary<string, object>
                {
                    ["graph"] = "SocialGraph",
                    ["nodeId"] = "charlie",
                    ["direction"] = "in"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(2, result.GetProperty("result").GetProperty("count").GetInt32());
        }

        [Fact]
        public void GraphNeighbors_NodeNotFound_ReturnsError()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object>
            {
                ["dataset"] = "SocialGraph"
            });

            var result = Parse(DataCoreTools.Execute("workspace_graph_neighbors",
                new Dictionary<string, object>
                {
                    ["graph"] = "SocialGraph",
                    ["nodeId"] = "ghost"
                }));
            Assert.False(result.GetProperty("success").GetBoolean());
        }

        [Fact]
        public void DescribeGraph_ReturnsInfo()
        {
            SeedGraph();
            DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object>
            {
                ["dataset"] = "SocialGraph"
            });

            var result = Parse(DataCoreTools.Execute("workspace_describe_graph",
                new Dictionary<string, object>
                {
                    ["graph"] = "SocialGraph"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(3, result.GetProperty("result").GetProperty("nodes").GetInt32());
            Assert.Equal(3, result.GetProperty("result").GetProperty("edges").GetInt32());
            Assert.True(result.GetProperty("result").GetProperty("nodeSample").GetArrayLength() > 0);
        }

        [Fact]
        public void DescribeGraph_NotGraph_ReturnsError()
        {
            var tabular = _store.CreateTabular("NotGraph");
            tabular.AddStringColumn("x", new[] { "a" });

            var result = Parse(DataCoreTools.Execute("workspace_describe_graph",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["graph"] = "NotGraph"
                }));
            Assert.False(result.GetProperty("success").GetBoolean());
        }
    }
}
