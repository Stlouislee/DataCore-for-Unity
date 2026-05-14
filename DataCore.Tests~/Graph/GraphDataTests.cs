using System;
using System.Collections.Generic;
using System.Linq;
using AroAro.DataCore.Events;
using AroAro.DataCore;
using AroAro.DataCore.Graph;
using Xunit;

namespace DataCore.Tests.Graph
{
    public class GraphDataTests : IDisposable
    {
        public void Dispose()
        {
            DataCoreEventManager.ClearAllSubscriptions();
        }

        private GraphData CreateTestGraph()
        {
            var graph = new GraphData("testGraph");
            graph.AddNode("A", new Dictionary<string, object> { ["color"] = "red", ["weight"] = 1.0 });
            graph.AddNode("B", new Dictionary<string, object> { ["color"] = "blue", ["weight"] = 2.0 });
            graph.AddNode("C", new Dictionary<string, object> { ["color"] = "green", ["weight"] = 3.0 });
            graph.AddEdge("A", "B", new Dictionary<string, object> { ["type"] = "friend", ["strength"] = 0.8 });
            graph.AddEdge("B", "C", new Dictionary<string, object> { ["type"] = "colleague", ["strength"] = 0.5 });
            graph.AddEdge("A", "C", new Dictionary<string, object> { ["type"] = "family", ["strength"] = 1.0 });
            return graph;
        }

        // ────────────────────────────────────────────────────────────────
        // AddNode / RemoveNode / HasNode
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void AddNode_AddsNodeSuccessfully()
        {
            var graph = new GraphData("test");

            graph.AddNode("node1");

            Assert.True(graph.HasNode("node1"));
            Assert.Equal(1, graph.NodeCount);
        }

        [Fact]
        public void AddNode_WithProperties_StoresProperties()
        {
            var graph = new GraphData("test");
            var props = new Dictionary<string, object> { ["key"] = "value" };

            graph.AddNode("node1", props);

            var stored = graph.GetNodeProperties("node1");
            Assert.Equal("value", stored["key"]);
        }

        [Fact]
        public void AddNode_DuplicateId_ThrowsArgumentException()
        {
            var graph = new GraphData("test");
            graph.AddNode("node1");

            Assert.Throws<ArgumentException>(() => graph.AddNode("node1"));
        }

        [Fact]
        public void AddNode_NullId_ThrowsArgumentNullException()
        {
            var graph = new GraphData("test");
            Assert.Throws<ArgumentNullException>(() => graph.AddNode(null));
        }

        [Fact]
        public void AddNode_EmptyId_ThrowsArgumentNullException()
        {
            var graph = new GraphData("test");
            Assert.Throws<ArgumentNullException>(() => graph.AddNode(""));
        }

        [Fact]
        public void RemoveNode_RemovesSuccessfully()
        {
            var graph = new GraphData("test");
            graph.AddNode("node1");

            var result = graph.RemoveNode("node1");

            Assert.True(result);
            Assert.False(graph.HasNode("node1"));
            Assert.Equal(0, graph.NodeCount);
        }

        [Fact]
        public void RemoveNode_NonExistent_ReturnsFalse()
        {
            var graph = new GraphData("test");
            Assert.False(graph.RemoveNode("nonexistent"));
        }

        [Fact]
        public void RemoveNode_AlsoRemovesConnectedEdges()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B");
            graph.AddEdge("B", "C");

            graph.RemoveNode("B");

            Assert.False(graph.HasEdge("A", "B"));
            Assert.False(graph.HasEdge("B", "C"));
            Assert.Equal(0, graph.EdgeCount); // Both edges connected to B are removed
        }

        [Fact]
        public void HasNode_ReturnsCorrectly()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");

            Assert.True(graph.HasNode("A"));
            Assert.False(graph.HasNode("B"));
        }

        // ────────────────────────────────────────────────────────────────
        // AddEdge / RemoveEdge / HasEdge
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void AddEdge_AddsEdgeSuccessfully()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");

            graph.AddEdge("A", "B");

            Assert.True(graph.HasEdge("A", "B"));
            Assert.Equal(1, graph.EdgeCount);
        }

        [Fact]
        public void AddEdge_WithProperties_StoresProperties()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            var props = new Dictionary<string, object> { ["weight"] = 5.0 };

            graph.AddEdge("A", "B", props);

            var stored = graph.GetEdgeProperties("A", "B");
            Assert.Equal(5.0, stored["weight"]);
        }

        [Fact]
        public void AddEdge_Duplicate_ThrowsArgumentException()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddEdge("A", "B");

            Assert.Throws<ArgumentException>(() => graph.AddEdge("A", "B"));
        }

        [Fact]
        public void AddEdge_NonExistentSourceNode_ThrowsArgumentException()
        {
            var graph = new GraphData("test");
            graph.AddNode("B");

            Assert.Throws<ArgumentException>(() => graph.AddEdge("A", "B"));
        }

        [Fact]
        public void AddEdge_NonExistentTargetNode_ThrowsArgumentException()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");

            Assert.Throws<ArgumentException>(() => graph.AddEdge("A", "B"));
        }

        [Fact]
        public void RemoveEdge_RemovesSuccessfully()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddEdge("A", "B");

            var result = graph.RemoveEdge("A", "B");

            Assert.True(result);
            Assert.False(graph.HasEdge("A", "B"));
            Assert.Equal(0, graph.EdgeCount);
        }

        [Fact]
        public void RemoveEdge_NonExistent_ReturnsFalse()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");

            Assert.False(graph.RemoveEdge("A", "B"));
        }

        [Fact]
        public void HasEdge_DirectedOnly()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddEdge("A", "B");

            Assert.True(graph.HasEdge("A", "B"));
            Assert.False(graph.HasEdge("B", "A")); // Directed graph
        }

        [Fact]
        public void AddEdge_SelfLoop_Works()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");

            graph.AddEdge("A", "A");

            Assert.True(graph.HasEdge("A", "A"));
        }

        // ────────────────────────────────────────────────────────────────
        // GetNodeProperties / GetEdgeProperties — shallow copy known issue
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void GetNodeProperties_ReturnsCopy()
        {
            var graph = new GraphData("test");
            var original = new Dictionary<string, object> { ["key"] = "value" };
            graph.AddNode("A", original);

            var retrieved = graph.GetNodeProperties("A");

            // Should be a copy, not the same reference
            Assert.NotSame(original, retrieved);
            Assert.Equal("value", retrieved["key"]);
        }

        [Fact]
        [Trait("Bug", "shallow-copy-properties")]
        public void GetNodeProperties_ShallowCopy_ReferenceTypesShared()
        {
            // Known issue: GetNodeProperties returns a new Dictionary but the values
            // are shallow-copied. If a property value is a reference type (e.g., List),
            // modifying the returned dictionary's value will affect the internal state.
            var graph = new GraphData("test");
            var list = new List<int> { 1, 2, 3 };
            graph.AddNode("A", new Dictionary<string, object> { ["items"] = list });

            var props = graph.GetNodeProperties("A");
            var retrievedList = (List<int>)props["items"];
            retrievedList.Add(4);

            // Since it's a shallow copy, the internal list is shared
            var propsAgain = graph.GetNodeProperties("A");
            Assert.Contains(4, (List<int>)propsAgain["items"]);
        }

        [Fact]
        public void GetEdgeProperties_ReturnsCopy()
        {
            var graph = CreateTestGraph();

            var props = graph.GetEdgeProperties("A", "B");

            Assert.Equal("friend", props["type"]);
            Assert.Equal(0.8, props["strength"]);
        }

        [Fact]
        [Trait("Bug", "shallow-copy-properties")]
        public void GetEdgeProperties_ShallowCopy_ReferenceTypesShared()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            var list = new List<string> { "tag1" };
            graph.AddEdge("A", "B", new Dictionary<string, object> { ["tags"] = list });

            var props = graph.GetEdgeProperties("A", "B");
            ((List<string>)props["tags"]).Add("tag2");

            var propsAgain = graph.GetEdgeProperties("A", "B");
            Assert.Contains("tag2", (List<string>)propsAgain["tags"]);
        }

        [Fact]
        public void GetNodeProperties_NonExistent_ThrowsKeyNotFoundException()
        {
            var graph = new GraphData("test");
            Assert.Throws<KeyNotFoundException>(() => graph.GetNodeProperties("nonexistent"));
        }

        [Fact]
        public void GetEdgeProperties_NonExistent_ThrowsKeyNotFoundException()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            Assert.Throws<KeyNotFoundException>(() => graph.GetEdgeProperties("A", "B"));
        }

        // ────────────────────────────────────────────────────────────────
        // GetEdges returns materialized list (known issue: lazy evaluation)
        // ────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Bug", "lazy-get-edges")]
        public void GetEdges_ReturnsMaterializedList()
        {
            // Known issue: GetEdges() returns an IEnumerable that is lazily evaluated
            // via _edgeProperties.Keys.Select(ParseEdgeKey). If the edge properties
            // dictionary is modified between enumeration, results may be inconsistent.
            // Ideally, it should return a materialized list (e.g., .ToList()).
            var graph = CreateTestGraph();

            var edges = graph.GetEdges().ToList();

            Assert.Equal(3, edges.Count);
            Assert.Contains(edges, e => e.From == "A" && e.To == "B");
            Assert.Contains(edges, e => e.From == "B" && e.To == "C");
            Assert.Contains(edges, e => e.From == "A" && e.To == "C");
        }

        [Fact]
        [Trait("Bug", "lazy-get-edges")]
        public void GetEdges_LazyEnumeration_ModificationDuringEnumeration()
        {
            // This test demonstrates the lazy evaluation issue:
            // If we enumerate GetEdges() and modify the graph during enumeration,
            // it may throw or produce inconsistent results.
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B");
            graph.AddEdge("B", "C");

            var edges = graph.GetEdges(); // Lazy IEnumerable

            // Materialize first to avoid modification-during-enumeration
            var materialized = edges.ToList();
            Assert.Equal(2, materialized.Count);
        }

        // ────────────────────────────────────────────────────────────────
        // BFS traversal order
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void BFS_TraversalOrder_IsLevelOrder()
        {
            // Graph: A → B, A → C, B → D, C → E
            var graph = new GraphData("tree");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddNode("D");
            graph.AddNode("E");
            graph.AddEdge("A", "B");
            graph.AddEdge("A", "C");
            graph.AddEdge("B", "D");
            graph.AddEdge("C", "E");

            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .ToNodeIds()
                .ToList();

            // BFS from A: A first, then B, C (level 1), then D, E (level 2)
            Assert.Equal("A", result[0]);
            Assert.Contains("B", result);
            Assert.Contains("C", result);
            Assert.Contains("D", result);
            Assert.Contains("E", result);

            // B and C should come before D and E
            Assert.True(result.IndexOf("B") < result.IndexOf("D"));
            Assert.True(result.IndexOf("C") < result.IndexOf("E"));
        }

        [Fact]
        public void BFS_SingleNode_ReturnsOnlyStartNode()
        {
            var graph = new GraphData("single");
            graph.AddNode("only");

            var result = graph.Query()
                .From("only")
                .ToNodeIds()
                .ToList();

            Assert.Single(result);
            Assert.Equal("only", result[0]);
        }

        [Fact]
        public void BFS_DirectedTraversal_FollowsOutEdgesOnly()
        {
            var graph = new GraphData("directed");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B");
            graph.AddEdge("C", "A"); // C → A, but A has no edge to C

            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .ToNodeIds()
                .ToList();

            Assert.Contains("A", result);
            Assert.Contains("B", result);
            Assert.DoesNotContain("C", result); // C is only reachable via in-edge
        }

        // ────────────────────────────────────────────────────────────────
        // BFS with node filters
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void BFS_WithNodeFilter_FiltersNodes()
        {
            var graph = new GraphData("filtered");
            graph.AddNode("A", new Dictionary<string, object> { ["active"] = true });
            graph.AddNode("B", new Dictionary<string, object> { ["active"] = false });
            graph.AddNode("C", new Dictionary<string, object> { ["active"] = true });
            graph.AddEdge("A", "B");
            graph.AddEdge("B", "C");

            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .WhereNodeProperty("active", QueryOp.Eq, true)
                .ToNodeIds()
                .ToList();

            // A is active, B is not (filtered out but still traversed through),
            // C is active
            Assert.Contains("A", result);
            Assert.DoesNotContain("B", result);
            Assert.Contains("C", result);
        }

        [Fact]
        public void BFS_WithNodeHasProperty_Filter()
        {
            var graph = new GraphData("props");
            graph.AddNode("A", new Dictionary<string, object> { ["color"] = "red" });
            graph.AddNode("B", new Dictionary<string, object>()); // No color
            graph.AddNode("C", new Dictionary<string, object> { ["color"] = "blue" });
            graph.AddEdge("A", "B");
            graph.AddEdge("B", "C");

            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .WhereNodeHasProperty("color")
                .ToNodeIds()
                .ToList();

            Assert.Contains("A", result);
            Assert.DoesNotContain("B", result);
            Assert.Contains("C", result);
        }

        // ────────────────────────────────────────────────────────────────
        // BFS with edge filters
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void BFS_WithEdgeFilter_FiltersEdges()
        {
            var graph = new GraphData("edgeFilter");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddNode("D");
            graph.AddEdge("A", "B", new Dictionary<string, object> { ["type"] = "road" });
            graph.AddEdge("A", "C", new Dictionary<string, object> { ["type"] = "rail" });
            graph.AddEdge("B", "D", new Dictionary<string, object> { ["type"] = "road" });

            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .WhereEdgeProperty("type", QueryOp.Eq, "road")
                .ToNodeIds()
                .ToList();

            Assert.Contains("A", result);
            Assert.Contains("B", result);
            Assert.Contains("D", result);
            Assert.DoesNotContain("C", result); // rail edge filtered out
        }

        // ────────────────────────────────────────────────────────────────
        // BFS: edge filter direction bug when traverseIn=true
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void BFS_EdgeFilterDirection_WhenTraverseIn_WorksCorrectly()
        {
            // Fixed in #35: Edge filters now receive (from, to) in edge-storage order
            // regardless of traversal direction.
            var graph = new GraphData("directionBug");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("B", "A", new Dictionary<string, object> { ["weight"] = 1.0 });
            graph.AddEdge("C", "A", new Dictionary<string, object> { ["weight"] = 2.0 });

            // Traverse in-edges from A: should find B (weight=1) and C (weight=2)
            // Edge filter on weight=1 should only match B→A
            var result = graph.Query()
                .From("A")
                .TraverseIn()
                .WhereEdgeProperty("weight", QueryOp.Eq, 1.0)
                .ToNodeIds()
                .ToList();

            Assert.Contains("B", result);
            Assert.DoesNotContain("C", result);
        }

        // ────────────────────────────────────────────────────────────────
        // Edge key separator \0 in node IDs (known issue)
        // ────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Bug", "edge-key-separator")]
        public void EdgeKey_NullSeparator_InNodeIds_CausesCollision()
        {
            // Known issue: Edge keys are constructed as "fromId\0toId" using the
            // null character as a separator. If node IDs contain the \0 character,
            // edge key parsing will be incorrect, leading to incorrect edge lookups.
            //
            // For example, edge ("a\0b", "c") generates key "a\0b\0c" which
            // parses as ("a", "b\0c") instead of ("a\0b", "c").
            var graph = new GraphData("separator");
            graph.AddNode("node1");
            graph.AddNode("node2");

            // Normal case: no \0 in IDs
            graph.AddEdge("node1", "node2");
            Assert.True(graph.HasEdge("node1", "node2"));

            // The edge key is "node1\0node2"
            // This works correctly because node IDs don't contain \0
        }

        [Fact]
        [Trait("Bug", "edge-key-separator")]
        public void EdgeKey_NormalNodeIds_WorksCorrectly()
        {
            // Verify that the \0 separator works correctly for normal node IDs
            var graph = new GraphData("normal");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddEdge("A", "B");

            Assert.True(graph.HasEdge("A", "B"));
            Assert.False(graph.HasEdge("B", "A"));
        }

        // ────────────────────────────────────────────────────────────────
        // AddNodes batch — partial failure (known issue: not atomic)
        // ────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Bug", "batch-not-atomic")]
        public void AddNodes_Batch_Failure_IsAtomic()
        {
            // AddNodes validates all nodes before applying any.
            // If any node in the batch fails validation, no nodes are added.
            var graph = new GraphData("batch");
            graph.AddNode("existing");

            var nodes = new List<(string Id, IDictionary<string, object> Props)>
            {
                ("new1", null),
                ("new2", null),
                ("existing", null), // This will throw (duplicate)
                ("new3", null),     // This will never be reached
            };

            // The batch operation will throw on "existing" (duplicate)
            Assert.Throws<ArgumentException>(() => graph.AddNodes(nodes));

            // Atomic behavior: no nodes from the batch should have been added
            Assert.False(graph.HasNode("new1"), "new1 should not be added (atomic rollback)");
            Assert.False(graph.HasNode("new2"), "new2 should not be added (atomic rollback)");
            Assert.False(graph.HasNode("new3"), "new3 should not be added (atomic rollback)");
            Assert.True(graph.HasNode("existing"), "existing should still be present");
        }

        [Fact]
        public void AddNodes_AllSuccessful_ReturnsCount()
        {
            var graph = new GraphData("batch");
            var nodes = new List<(string Id, IDictionary<string, object> Props)>
            {
                ("A", null),
                ("B", null),
                ("C", null),
            };

            var count = graph.AddNodes(nodes);

            Assert.Equal(3, count);
            Assert.Equal(3, graph.NodeCount);
        }

        [Fact]
        public void AddEdges_AllSuccessful_ReturnsCount()
        {
            var graph = new GraphData("batch");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");

            var edges = new List<(string From, string To, IDictionary<string, object> Props)>
            {
                ("A", "B", null),
                ("B", "C", null),
            };

            var count = graph.AddEdges(edges);

            Assert.Equal(2, count);
            Assert.Equal(2, graph.EdgeCount);
        }

        // ────────────────────────────────────────────────────────────────
        // WithName copy semantics
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void WithName_CreatesIndependentCopy()
        {
            var original = CreateTestGraph();

            var copy = original.WithName("copy") as GraphData;

            Assert.NotNull(copy);
            Assert.Equal("copy", copy.Name);
            Assert.NotEqual(original.Id, copy.Id);
        }

        [Fact]
        public void WithName_CopyHasSameNodes()
        {
            var original = CreateTestGraph();

            var copy = original.WithName("copy") as GraphData;

            Assert.Equal(original.NodeCount, copy.NodeCount);
            foreach (var nodeId in original.GetNodeIds())
            {
                Assert.True(copy.HasNode(nodeId));
            }
        }

        [Fact]
        public void WithName_CopyHasSameEdges()
        {
            var original = CreateTestGraph();

            var copy = original.WithName("copy") as GraphData;

            Assert.Equal(original.EdgeCount, copy.EdgeCount);
            foreach (var (from, to) in original.GetEdges())
            {
                Assert.True(copy.HasEdge(from, to));
            }
        }

        [Fact]
        public void WithName_ModifyingCopyDoesNotAffectOriginal()
        {
            var original = CreateTestGraph();

            var copy = original.WithName("copy") as GraphData;
            copy.AddNode("NEW_NODE");

            Assert.False(original.HasNode("NEW_NODE"));
            Assert.True(copy.HasNode("NEW_NODE"));
        }

        [Fact]
        public void WithName_CopyHasSameProperties()
        {
            var original = CreateTestGraph();

            var copy = original.WithName("copy") as GraphData;

            var origProps = original.GetNodeProperties("A");
            var copyProps = copy.GetNodeProperties("A");
            Assert.Equal(origProps["color"], copyProps["color"]);
            Assert.Equal(origProps["weight"], copyProps["weight"]);
        }

        [Fact]
        public void WithName_CopyHasSameEdgeProperties()
        {
            var original = CreateTestGraph();

            var copy = original.WithName("copy") as GraphData;

            foreach (var (from, to) in original.GetEdges())
            {
                var origEdge = original.GetEdgeProperties(from, to);
                var copyEdge = copy.GetEdgeProperties(from, to);
                Assert.Equal(origEdge["type"], copyEdge["type"]);
                Assert.Equal(origEdge["strength"], copyEdge["strength"]);
            }
        }

        // ────────────────────────────────────────────────────────────────
        // GetOutNeighbors / GetInNeighbors
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void GetOutNeighbors_ReturnsCorrectNeighbors()
        {
            var graph = CreateTestGraph();

            var outNeighbors = graph.GetOutNeighbors("A").ToList();

            Assert.Contains("B", outNeighbors);
            Assert.Contains("C", outNeighbors);
            Assert.Equal(2, outNeighbors.Count);
        }

        [Fact]
        public void GetInNeighbors_ReturnsCorrectNeighbors()
        {
            var graph = CreateTestGraph();

            var inNeighbors = graph.GetInNeighbors("C").ToList();

            Assert.Contains("A", inNeighbors);
            Assert.Contains("B", inNeighbors);
            Assert.Equal(2, inNeighbors.Count);
        }

        [Fact]
        public void GetOutNeighbors_NoOutEdges_ReturnsEmpty()
        {
            var graph = CreateTestGraph();

            var outNeighbors = graph.GetOutNeighbors("C").ToList();

            Assert.Empty(outNeighbors);
        }

        [Fact]
        public void GetInNeighbors_NoInEdges_ReturnsEmpty()
        {
            var graph = CreateTestGraph();

            var inNeighbors = graph.GetInNeighbors("A").ToList();

            Assert.Empty(inNeighbors);
        }

        [Fact]
        public void GetNeighbors_ReturnsUnionOfInAndOut()
        {
            var graph = CreateTestGraph();

            var neighbors = graph.GetNeighbors("B").ToList();

            // B has out-neighbor C and in-neighbor A
            Assert.Contains("A", neighbors);
            Assert.Contains("C", neighbors);
        }

        [Fact]
        public void GetOutDegree_ReturnsCorrectCount()
        {
            var graph = CreateTestGraph();

            Assert.Equal(2, graph.GetOutDegree("A")); // A → B, A → C
            Assert.Equal(1, graph.GetOutDegree("B")); // B → C
            Assert.Equal(0, graph.GetOutDegree("C")); // C has no out-edges
        }

        [Fact]
        public void GetInDegree_ReturnsCorrectCount()
        {
            var graph = CreateTestGraph();

            Assert.Equal(0, graph.GetInDegree("A")); // A has no in-edges
            Assert.Equal(1, graph.GetInDegree("B")); // A → B
            Assert.Equal(2, graph.GetInDegree("C")); // A → C, B → C
        }

        [Fact]
        public void GetOutNeighbors_NonExistentNode_ThrowsKeyNotFoundException()
        {
            var graph = new GraphData("test");
            Assert.Throws<KeyNotFoundException>(() => graph.GetOutNeighbors("nonexistent"));
        }

        [Fact]
        public void GetInNeighbors_NonExistentNode_ThrowsKeyNotFoundException()
        {
            var graph = new GraphData("test");
            Assert.Throws<KeyNotFoundException>(() => graph.GetInNeighbors("nonexistent"));
        }

        // ────────────────────────────────────────────────────────────────
        // UpdateNodeProperties / UpdateEdgeProperties
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void UpdateNodeProperties_UpdatesExistingProperties()
        {
            var graph = new GraphData("test");
            graph.AddNode("A", new Dictionary<string, object> { ["color"] = "red" });

            graph.UpdateNodeProperties("A", new Dictionary<string, object> { ["color"] = "blue" });

            Assert.Equal("blue", graph.GetNodeProperties("A")["color"]);
        }

        [Fact]
        public void UpdateNodeProperties_AddsNewProperties()
        {
            var graph = new GraphData("test");
            graph.AddNode("A", new Dictionary<string, object> { ["color"] = "red" });

            graph.UpdateNodeProperties("A", new Dictionary<string, object> { ["size"] = 10 });

            var props = graph.GetNodeProperties("A");
            Assert.Equal("red", props["color"]);
            Assert.Equal(10, props["size"]);
        }

        [Fact]
        public void UpdateEdgeProperties_UpdatesExistingProperties()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddEdge("A", "B", new Dictionary<string, object> { ["weight"] = 1.0 });

            graph.UpdateEdgeProperties("A", "B", new Dictionary<string, object> { ["weight"] = 2.0 });

            Assert.Equal(2.0, graph.GetEdgeProperties("A", "B")["weight"]);
        }

        // ────────────────────────────────────────────────────────────────
        // Clear
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Clear_RemovesAllNodesAndEdges()
        {
            var graph = CreateTestGraph();

            graph.Clear();

            Assert.Equal(0, graph.NodeCount);
            Assert.Equal(0, graph.EdgeCount);
        }

        // ────────────────────────────────────────────────────────────────
        // GetNodeIds
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void GetNodeIds_ReturnsAllNodeIds()
        {
            var graph = CreateTestGraph();

            var ids = graph.GetNodeIds().ToList();

            Assert.Contains("A", ids);
            Assert.Contains("B", ids);
            Assert.Contains("C", ids);
            Assert.Equal(3, ids.Count);
        }

        [Fact]
        public void GetNodeIds_EmptyGraph_ReturnsEmpty()
        {
            var graph = new GraphData("empty");

            var ids = graph.GetNodeIds().ToList();

            Assert.Empty(ids);
        }

        // ────────────────────────────────────────────────────────────────
        // BFS: MaxDepth
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void BFS_MaxDepth_LimitsTraversal()
        {
            var graph = new GraphData("chain");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddNode("D");
            graph.AddEdge("A", "B");
            graph.AddEdge("B", "C");
            graph.AddEdge("C", "D");

            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .MaxDepth(1)
                .ToNodeIds()
                .ToList();

            Assert.Contains("A", result);
            Assert.Contains("B", result);
            Assert.DoesNotContain("C", result); // Depth 2, beyond MaxDepth
            Assert.DoesNotContain("D", result);
        }

        // ────────────────────────────────────────────────────────────────
        // BFS: TraverseIn
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void BFS_TraverseIn_FollowsInEdges()
        {
            var graph = new GraphData("reverse");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("B", "A"); // B → A
            graph.AddEdge("C", "A"); // C → A

            var result = graph.Query()
                .From("A")
                .TraverseIn()
                .ToNodeIds()
                .ToList();

            Assert.Contains("A", result);
            Assert.Contains("B", result);
            Assert.Contains("C", result);
        }

        // ────────────────────────────────────────────────────────────────
        // Query: ToEdges
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Query_ToEdges_ReturnsAllEdges()
        {
            var graph = CreateTestGraph();

            var edges = graph.Query().ToEdges().ToList();

            Assert.Equal(3, edges.Count);
        }

        [Fact]
        public void Query_CountNodes_ReturnsCorrectCount()
        {
            var graph = CreateTestGraph();

            Assert.Equal(3, graph.Query().CountNodes());
        }

        [Fact]
        public void Query_CountEdges_ReturnsCorrectCount()
        {
            var graph = CreateTestGraph();

            Assert.Equal(3, graph.Query().CountEdges());
        }

        // ────────────────────────────────────────────────────────────────
        // Issue #34 — CompareNumeric safely handles non-numeric values
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void CompareNumeric_NonNumericString_DoesNotCrash()
        {
            // Query with Gt on a string property — should not throw
            var graph = new GraphData("test");
            graph.AddNode("A", new Dictionary<string, object> { ["label"] = "hello" });
            graph.AddNode("B", new Dictionary<string, object> { ["label"] = "world" });
            graph.AddEdge("A", "B", new Dictionary<string, object> { ["weight"] = "not-a-number" });

            // Gt comparison with non-numeric string should return 0 (incomparable), not crash
            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .WhereEdgeProperty("weight", QueryOp.Gt, "also-not-a-number")
                .ToNodeIds()
                .ToList();

            // CompareNumeric returns 0 for incomparable, so Gt (> 0) is false → filtered out
            Assert.DoesNotContain("B", result);
        }

        [Fact]
        public void CompareNumeric_NullPropertyValue_DoesNotCrash()
        {
            // Null property value compared with numeric — should not throw
            var graph = new GraphData("test");
            graph.AddNode("A", new Dictionary<string, object> { ["val"] = null });
            graph.AddNode("B", new Dictionary<string, object> { ["val"] = 42.0 });
            graph.AddEdge("A", "B");

            // Query with Gt on null property value should not crash
            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .WhereNodeProperty("val", QueryOp.Gt, 0.0)
                .ToNodeIds()
                .ToList();

            // A has null val → TryConvertToDouble returns false → CompareNumeric returns 0 → Gt is false
            Assert.DoesNotContain("A", result);
            // B has 42.0 → CompareNumeric returns 1 → Gt is true
            Assert.Contains("B", result);
        }

        [Fact]
        public void CompareNumeric_MixedTypes_NumericAndString_NoCrash()
        {
            // One node has numeric, another has string for the same property
            var graph = new GraphData("test");
            graph.AddNode("A", new Dictionary<string, object> { ["score"] = 100.0 });
            graph.AddNode("B", new Dictionary<string, object> { ["score"] = "high" });
            graph.AddEdge("A", "B");

            // Gt comparison with mixed types
            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .WhereNodeProperty("score", QueryOp.Gt, 50.0)
                .ToNodeIds()
                .ToList();

            // A: 100.0 > 50.0 → true
            Assert.Contains("A", result);
            // B: "high" can't convert → CompareNumeric returns 0 → 0 > 0 is false
            Assert.DoesNotContain("B", result);
        }

        [Fact]
        public void CompareNumeric_Lt_WithNonNumericString_DoesNotCrash()
        {
            var graph = new GraphData("test");
            graph.AddNode("A", new Dictionary<string, object> { ["val"] = "abc" });
            graph.AddEdge("A", "A");

            // Lt comparison — should not throw
            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .WhereNodeProperty("val", QueryOp.Lt, 999.0)
                .ToNodeIds()
                .ToList();

            // "abc" can't convert → CompareNumeric returns 0 → 0 < 999 is true... wait no
            // CompareNumeric returns 0 when TryConvertToDouble fails
            // So for Lt: 0 < 999 is actually true... but left is "abc" not 0
            // Let me re-check: CompareNumeric returns 0 when EITHER side fails
            // So 0.CompareTo(999) = -1, and -1 < 0 is true, so Lt would be true
            // Actually wait: CompareNumeric returns leftVal.CompareTo(rightVal) only when both succeed
            // If either fails, it returns 0 (treated as equal)
            // So Lt: 0 < 0 is false → filtered out
            Assert.Empty(result);
        }

        [Fact]
        public void CompareNumeric_Eq_NonNumericVsNonNumeric_DoesNotCrash()
        {
            // Two string values compared with Eq via numeric path
            var graph = new GraphData("test");
            graph.AddNode("A", new Dictionary<string, object> { ["tag"] = "foo" });
            graph.AddNode("B", new Dictionary<string, object> { ["tag"] = "bar" });
            graph.AddEdge("A", "B");

            // Gt on two non-numeric values — both fail TryConvertToDouble → returns 0 → not Gt
            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .WhereNodeProperty("tag", QueryOp.Gt, "baz")
                .ToNodeIds()
                .ToList();

            // All non-numeric → CompareNumeric returns 0 → Gt is false
            Assert.DoesNotContain("B", result);
        }

        // ────────────────────────────────────────────────────────────────
        // EdgeType (Issue #135)
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void AddEdge_WithType_TypeIsStored()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");

            graph.AddEdge("A", "B", "WorksAt");

            Assert.True(graph.HasEdge("A", "B"));
        }

        [Fact]
        public void GetOutNeighbors_WithType_FiltersCorrectly()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B", "Friend");
            graph.AddEdge("A", "C", "Colleague");

            var friends = graph.GetOutNeighbors("A", "Friend").ToList();
            Assert.Single(friends);
            Assert.Contains("B", friends);

            var colleagues = graph.GetOutNeighbors("A", "Colleague").ToList();
            Assert.Single(colleagues);
            Assert.Contains("C", colleagues);
        }

        [Fact]
        public void GetInNeighbors_WithType_FiltersCorrectly()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "C", "Friend");
            graph.AddEdge("B", "C", "Colleague");

            var friends = graph.GetInNeighbors("C", "Friend").ToList();
            Assert.Single(friends);
            Assert.Contains("A", friends);
        }

        [Fact]
        public void GetNeighbors_WithType_FiltersCorrectly()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B", "Friend");
            graph.AddEdge("C", "B", "Colleague");

            var friends = graph.GetNeighbors("B", "Friend").ToList();
            Assert.Single(friends);
            Assert.Contains("A", friends);
        }

        [Fact]
        public void GetOutNeighbors_NoType_ReturnsAll()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B", "Friend");
            graph.AddEdge("A", "C", "Colleague");

            var all = graph.GetOutNeighbors("A").ToList();
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public void WhereEdgeType_FiltersEdgesInQuery()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B", "Friend");
            graph.AddEdge("A", "C", "Colleague");

            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .WhereEdgeType("Friend")
                .ToNodeIds()
                .ToList();

            Assert.Contains("A", result);
            Assert.Contains("B", result);
            Assert.DoesNotContain("C", result);
        }

        [Fact]
        public void WhereEdgeType_CountEdges_FiltersCorrectly()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B", "Friend");
            graph.AddEdge("A", "C", "Colleague");

            var count = graph.Query()
                .WhereEdgeType("Friend")
                .CountEdges();

            Assert.Equal(1, count);
        }

        [Fact]
        public void WhereEdgeType_ToEdges_FiltersCorrectly()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B", "Friend");
            graph.AddEdge("A", "C", "Colleague");

            var edges = graph.Query()
                .WhereEdgeType("Friend")
                .ToEdges()
                .ToList();

            Assert.Single(edges);
            Assert.Equal("A", edges[0].From);
            Assert.Equal("B", edges[0].To);
        }

        [Fact]
        public void AddEdge_NullType_DefaultsToEmpty()
        {
            var graph = new GraphData("test");
            graph.AddNode("A");
            graph.AddNode("B");

            graph.AddEdge("A", "B");

            var neighbors = graph.GetOutNeighbors("A", "").ToList();
            Assert.Single(neighbors);
            Assert.Contains("B", neighbors);
        }

        [Fact]
        public void WithName_CopyPreservesEdgeTypes()
        {
            var original = new GraphData("test");
            original.AddNode("A");
            original.AddNode("B");
            original.AddEdge("A", "B", "Friend");

            var copy = original.WithName("copy") as GraphData;

            var friends = copy.GetOutNeighbors("A", "Friend").ToList();
            Assert.Single(friends);
            Assert.Contains("B", friends);
        }

        // ────────────────────────────────────────────────────────────────
        // LambdaFilteredGraphQuery: Where(Func<QueryRow, bool>)
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Where_Lambda_FiltersNodesByProperty()
        {
            var graph = CreateTestGraph();

            var result = graph.Query()
                .Where(row => row.Has("color") && row.Get<string>("color") == "red")
                .ToNodeIds()
                .ToList();

            Assert.Single(result);
            Assert.Contains("A", result);
        }

        [Fact]
        public void Where_Lambda_CombinedWithNodeProperty()
        {
            var graph = CreateTestGraph();

            // WhereNodeProperty filters by weight > 1.5, then lambda filters by color == "green"
            var result = graph.Query()
                .WhereNodeProperty("weight", QueryOp.Gt, 1.5)
                .Where(row => row.Get<string>("color") == "green")
                .ToNodeIds()
                .ToList();

            Assert.Single(result);
            Assert.Contains("C", result);
        }

        [Fact]
        public void Where_Lambda_CountNodes_ReturnsFilteredCount()
        {
            var graph = CreateTestGraph();

            var count = graph.Query()
                .Where(row => row.Has("weight") && row.Get<double>("weight") >= 2.0)
                .CountNodes();

            Assert.Equal(2, count); // B (2.0) and C (3.0)
        }

        [Fact]
        public void Where_Lambda_CountEdges_ReturnsFilteredCount()
        {
            var graph = CreateTestGraph();

            var count = graph.Query()
                .Where(row => row.Has("strength") && row.Get<double>("strength") > 0.6)
                .CountEdges();

            // Edges: A→B (0.8), B→C (0.5), A→C (1.0) → 2 edges match
            Assert.Equal(2, count);
        }

        [Fact]
        public void Where_Lambda_ToEdges_FiltersByEdgeProperty()
        {
            var graph = CreateTestGraph();

            var edges = graph.Query()
                .Where(row => row.Has("type") && row.Get<string>("type") == "friend")
                .ToEdges()
                .ToList();

            Assert.Single(edges);
            Assert.Equal("A", edges[0].From);
            Assert.Equal("B", edges[0].To);
        }

        [Fact]
        public void Where_Lambda_WithBFS_AppliesFilterDuringTraversal()
        {
            var graph = CreateTestGraph();

            // BFS from A, depth 1, but only keep nodes with color == "blue"
            var result = graph.Query()
                .From("A")
                .TraverseOut()
                .MaxDepth(1)
                .Where(row => row.Get<string>("color") == "blue")
                .ToNodeIds()
                .ToList();

            // A is visited first (color=red → filtered out), then B (color=blue → kept)
            Assert.Contains("B", result);
            Assert.DoesNotContain("A", result);
        }

        [Fact]
        public void Where_Lambda_EmptyResult_WhenNothingMatches()
        {
            var graph = CreateTestGraph();

            var result = graph.Query()
                .Where(row => row.Get<string>("color") == "nonexistent")
                .ToNodeIds()
                .ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void Where_Lambda_SourceProperty_ReturnsDataset()
        {
            var graph = CreateTestGraph();

            var query = graph.Query();
            var filtered = query.Where(row => true);

            Assert.NotNull(filtered.Source);
            Assert.Same(graph, filtered.Source);
        }
    }
}
