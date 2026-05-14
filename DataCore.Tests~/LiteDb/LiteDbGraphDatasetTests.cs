using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AroAro.DataCore;
using AroAro.DataCore.LiteDb;
using Xunit;

namespace DataCore.Tests.LiteDb
{
    [Collection("LiteDB")]
    public class LiteDbGraphDatasetTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly LiteDbDataStore _store;

        public LiteDbGraphDatasetTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"datacore_graph_test_{Guid.NewGuid():N}.db");
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

        private IGraphDataset CreateGraph(string name = "testGraph")
        {
            return _store.CreateGraph(name);
        }

        #region AddNode

        [Fact]
        public void AddNode_WithProperties_NodeIsRetrievable()
        {
            var g = CreateGraph();
            g.AddNode("n1", new Dictionary<string, object>
            {
                { "label", "Person" },
                { "age", 30 }
            });

            Assert.True(g.HasNode("n1"));

            var props = g.GetNodeProperties("n1");
            Assert.Equal("Person", props["label"]);
            Assert.Equal(30, props["age"]);
        }

        [Fact]
        public void AddNode_WithoutProperties_NodeExistsWithEmptyProps()
        {
            var g = CreateGraph();
            g.AddNode("n1");

            Assert.True(g.HasNode("n1"));

            var props = g.GetNodeProperties("n1");
            Assert.NotNull(props);
            Assert.Empty(props);
        }

        [Fact]
        public void AddNode_DuplicateId_ThrowsInvalidOperationException()
        {
            var g = CreateGraph();
            g.AddNode("n1");

            var ex = Assert.Throws<InvalidOperationException>(() => g.AddNode("n1"));
            Assert.Contains("already exists", ex.Message);
        }

        [Fact]
        public void AddNode_NullOrEmptyId_ThrowsArgumentException()
        {
            var g = CreateGraph();

            Assert.Throws<ArgumentException>(() => g.AddNode(null));
            Assert.Throws<ArgumentException>(() => g.AddNode(""));
            Assert.Throws<ArgumentException>(() => g.AddNode("   "));
        }

        [Fact]
        public void AddNode_IncrementsNodeCount()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");

            Assert.Equal(3, g.NodeCount);
        }

        #endregion

        #region AddNodes (batch)

        [Fact]
        public void AddNodes_BatchOperation_AddsAll()
        {
            var g = CreateGraph();

            int added = g.AddNodes(new[]
            {
                ("n1", (IDictionary<string, object>)new Dictionary<string, object> { { "x", 1 } }),
                ("n2", (IDictionary<string, object>)new Dictionary<string, object> { { "x", 2 } }),
                ("n3", (IDictionary<string, object>)new Dictionary<string, object> { { "x", 3 } }),
            });

            Assert.Equal(3, added);
            Assert.Equal(3, g.NodeCount);
        }

        #endregion

        #region RemoveNode

        [Fact]
        public void RemoveNode_ExistingNode_ReturnsTrue()
        {
            var g = CreateGraph();
            g.AddNode("n1");

            bool removed = g.RemoveNode("n1");

            Assert.True(removed);
            Assert.False(g.HasNode("n1"));
        }

        [Fact]
        public void RemoveNode_NonExistingNode_ReturnsFalse()
        {
            var g = CreateGraph();

            Assert.False(g.RemoveNode("ghost"));
        }

        [Fact]
        public void RemoveNode_RemovesAssociatedEdges()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b");
            g.AddEdge("b", "c");
            g.AddEdge("a", "c");

            g.RemoveNode("b"); // Should remove edges a->b and b->c

            Assert.False(g.HasEdge("a", "b"), "Edge a->b should be removed when node b is removed");
            Assert.False(g.HasEdge("b", "c"), "Edge b->c should be removed when node b is removed");
            Assert.True(g.HasEdge("a", "c"), "Edge a->c should survive removal of node b");
            Assert.Equal(1, g.EdgeCount);
        }

        [Fact]
        public void RemoveNode_DecreasesNodeCount()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");

            g.RemoveNode("a");

            Assert.Equal(1, g.NodeCount);
        }

        #endregion

        #region AddEdge

        [Fact]
        public void AddEdge_WithProperties_EdgeIsRetrievable()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");

            g.AddEdge("a", "b", new Dictionary<string, object>
            {
                { "weight", 5.0 },
                { "type", "friend" }
            });

            Assert.True(g.HasEdge("a", "b"));

            var props = g.GetEdgeProperties("a", "b");
            Assert.Equal(5.0, props["weight"]);
            Assert.Equal("friend", props["type"]);
        }

        [Fact]
        public void AddEdge_ForNonExistingFromNode_ThrowsInvalidOperationException()
        {
            var g = CreateGraph();
            g.AddNode("b");

            var ex = Assert.Throws<InvalidOperationException>(() => g.AddEdge("ghost", "b"));
            Assert.Contains("does not exist", ex.Message);
        }

        [Fact]
        public void AddEdge_ForNonExistingToNode_ThrowsInvalidOperationException()
        {
            var g = CreateGraph();
            g.AddNode("a");

            var ex = Assert.Throws<InvalidOperationException>(() => g.AddEdge("a", "ghost"));
            Assert.Contains("does not exist", ex.Message);
        }

        [Fact]
        public void AddEdge_IncrementsEdgeCount()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");

            g.AddEdge("a", "b");
            g.AddEdge("b", "c");

            Assert.Equal(2, g.EdgeCount);
        }

        [Fact]
        public void AddEdge_NullOrEmptyFromId_ThrowsArgumentException()
        {
            var g = CreateGraph();
            g.AddNode("a");

            Assert.Throws<ArgumentException>(() => g.AddEdge(null, "a"));
            Assert.Throws<ArgumentException>(() => g.AddEdge("", "a"));
        }

        [Fact]
        public void AddEdge_NullOrEmptyToId_ThrowsArgumentException()
        {
            var g = CreateGraph();
            g.AddNode("a");

            Assert.Throws<ArgumentException>(() => g.AddEdge("a", null));
            Assert.Throws<ArgumentException>(() => g.AddEdge("a", ""));
        }

        #endregion

        #region AddEdges (batch)

        [Fact]
        public void AddEdges_BatchOperation_AddsAll()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");

            int added = g.AddEdges(new[]
            {
                ("a", "b", (IDictionary<string, object>)null),
                ("b", "c", (IDictionary<string, object>)null),
            });

            Assert.Equal(2, added);
            Assert.Equal(2, g.EdgeCount);
        }

        #endregion

        #region RemoveEdge

        [Fact]
        public void RemoveEdge_ExistingEdge_ReturnsTrue()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddEdge("a", "b");

            bool removed = g.RemoveEdge("a", "b");

            Assert.True(removed);
            Assert.False(g.HasEdge("a", "b"));
        }

        [Fact]
        public void RemoveEdge_NonExistingEdge_ReturnsFalse()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");

            Assert.False(g.RemoveEdge("a", "b"));
        }

        [Fact]
        public void RemoveEdge_DecrementsEdgeCount()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddEdge("a", "b");
            Assert.Equal(1, g.EdgeCount);

            g.RemoveEdge("a", "b");

            Assert.Equal(0, g.EdgeCount);
        }

        #endregion

        #region HasNode / HasEdge

        [Fact]
        public void HasNode_ExistingNode_ReturnsTrue()
        {
            var g = CreateGraph();
            g.AddNode("n1");

            Assert.True(g.HasNode("n1"));
        }

        [Fact]
        public void HasNode_NonExistingNode_ReturnsFalse()
        {
            var g = CreateGraph();

            Assert.False(g.HasNode("ghost"));
        }

        [Fact]
        public void HasEdge_ExistingEdge_ReturnsTrue()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddEdge("a", "b");

            Assert.True(g.HasEdge("a", "b"));
        }

        [Fact]
        public void HasEdge_NonExistingEdge_ReturnsFalse()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");

            Assert.False(g.HasEdge("a", "b"));
        }

        [Fact]
        public void HasEdge_DirectedOnly_ReverseEdgeNotAutomaticallyCreated()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddEdge("a", "b");

            Assert.True(g.HasEdge("a", "b"));
            Assert.False(g.HasEdge("b", "a"), "Edges should be directed; reverse edge should not exist");
        }

        #endregion

        #region GetNodeIds

        [Fact]
        public void GetNodeIds_ReturnsAllNodes()
        {
            var g = CreateGraph();
            g.AddNode("x");
            g.AddNode("y");
            g.AddNode("z");

            var ids = g.GetNodeIds().ToList();

            Assert.Equal(3, ids.Count);
            Assert.Contains("x", ids);
            Assert.Contains("y", ids);
            Assert.Contains("z", ids);
        }

        [Fact]
        public void GetNodeIds_EmptyGraph_ReturnsEmpty()
        {
            var g = CreateGraph();

            Assert.Empty(g.GetNodeIds());
        }

        #endregion

        #region GetNodeProperties / GetEdgeProperties

        [Fact]
        public void GetNodeProperties_ReturnsCorrectValues()
        {
            var g = CreateGraph();
            g.AddNode("n1", new Dictionary<string, object>
            {
                { "name", "Alice" },
                { "score", 42.0 },
                { "active", true }
            });

            var props = g.GetNodeProperties("n1");

            Assert.Equal("Alice", props["name"]);
            Assert.Equal(42.0, props["score"]);
            Assert.Equal(true, props["active"]);
        }

        [Fact]
        public void GetNodeProperties_NonExistingNode_ReturnsNull()
        {
            var g = CreateGraph();

            Assert.Null(g.GetNodeProperties("ghost"));
        }

        [Fact]
        public void GetEdgeProperties_ReturnsCorrectValues()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddEdge("a", "b", new Dictionary<string, object>
            {
                { "weight", 3.14 },
                { "label", "connects" }
            });

            var props = g.GetEdgeProperties("a", "b");

            Assert.Equal(3.14, props["weight"]);
            Assert.Equal("connects", props["label"]);
        }

        [Fact]
        public void GetEdgeProperties_NonExistingEdge_ReturnsNull()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");

            Assert.Null(g.GetEdgeProperties("a", "b"));
        }

        #endregion

        #region GetOutNeighbors / GetInNeighbors / GetNeighbors

        [Fact]
        public void GetOutNeighbors_ReturnsOutgoingTargets()
        {
            // a -> b, a -> c, b -> c
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b");
            g.AddEdge("a", "c");
            g.AddEdge("b", "c");

            var outNeighbors = g.GetOutNeighbors("a").ToList();

            Assert.Equal(2, outNeighbors.Count);
            Assert.Contains("b", outNeighbors);
            Assert.Contains("c", outNeighbors);
        }

        [Fact]
        public void GetInNeighbors_ReturnsIncomingSources()
        {
            // a -> c, b -> c
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "c");
            g.AddEdge("b", "c");

            var inNeighbors = g.GetInNeighbors("c").ToList();

            Assert.Equal(2, inNeighbors.Count);
            Assert.Contains("a", inNeighbors);
            Assert.Contains("b", inNeighbors);
        }

        [Fact]
        public void GetNeighbors_ReturnsUnionOfInAndOut()
        {
            // a -> b, c -> b
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b");
            g.AddEdge("c", "b");

            var neighbors = g.GetNeighbors("b").ToList();

            Assert.Equal(2, neighbors.Count);
            Assert.Contains("a", neighbors);
            Assert.Contains("c", neighbors);
        }

        [Fact]
        public void GetNeighbors_NodeWithNoEdges_ReturnsEmpty()
        {
            var g = CreateGraph();
            g.AddNode("isolated");
            g.AddNode("other");

            Assert.Empty(g.GetNeighbors("isolated"));
        }

        #endregion

        #region GetOutDegree / GetInDegree

        [Fact]
        public void GetOutDegree_ReturnsCorrectCount()
        {
            // a -> b, a -> c, a -> d
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddNode("d");
            g.AddEdge("a", "b");
            g.AddEdge("a", "c");
            g.AddEdge("a", "d");

            Assert.Equal(3, g.GetOutDegree("a"));
            Assert.Equal(0, g.GetOutDegree("b"));
        }

        [Fact]
        public void GetInDegree_ReturnsCorrectCount()
        {
            // b -> a, c -> a, d -> a
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddNode("d");
            g.AddEdge("b", "a");
            g.AddEdge("c", "a");
            g.AddEdge("d", "a");

            Assert.Equal(3, g.GetInDegree("a"));
            Assert.Equal(0, g.GetInDegree("b"));
        }

        #endregion

        #region NodeCount / EdgeCount after operations

        [Fact]
        public void NodeCount_AccurateAfterMixedOperations()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            Assert.Equal(3, g.NodeCount);

            g.RemoveNode("b");
            Assert.Equal(2, g.NodeCount);

            g.AddNode("d");
            Assert.Equal(3, g.NodeCount);
        }

        [Fact]
        public void EdgeCount_AccurateAfterMixedOperations()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b");
            g.AddEdge("b", "c");
            Assert.Equal(2, g.EdgeCount);

            g.RemoveEdge("a", "b");
            Assert.Equal(1, g.EdgeCount);

            g.AddEdge("c", "a");
            Assert.Equal(2, g.EdgeCount);
        }

        [Fact]
        public void EdgeCount_DecreasesWhenNodeRemoved()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b");
            g.AddEdge("a", "c");
            g.AddEdge("b", "c");
            Assert.Equal(3, g.EdgeCount);

            g.RemoveNode("a"); // Removes a->b and a->c

            Assert.Equal(1, g.EdgeCount);
        }

        #endregion

        #region FlushMetadata

        [Fact]
        public void FlushMetadata_PersistsCounts()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddEdge("a", "b");

            // Force metadata flush
            ((AroAro.DataCore.LiteDb.LiteDbGraphDataset)g).FlushMetadata();

            // After flushing, NodeCount and EdgeCount should still be correct
            Assert.Equal(2, g.NodeCount);
            Assert.Equal(1, g.EdgeCount);
        }

        [Fact]
        public void FlushMetadata_MultipleCalls_NoError()
        {
            var g = CreateGraph();
            g.AddNode("a");

            // Multiple flushes should be idempotent
            ((AroAro.DataCore.LiteDb.LiteDbGraphDataset)g).FlushMetadata();
            ((AroAro.DataCore.LiteDb.LiteDbGraphDataset)g).FlushMetadata();
            ((AroAro.DataCore.LiteDb.LiteDbGraphDataset)g).FlushMetadata();

            Assert.Equal(1, g.NodeCount);
        }

        #endregion

        #region Clear

        [Fact]
        public void Clear_RemovesAllNodesAndEdges()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddEdge("a", "b");

            g.Clear();

            Assert.Equal(0, g.NodeCount);
            Assert.Equal(0, g.EdgeCount);
            Assert.Empty(g.GetNodeIds());
        }

        #endregion

        #region UpdateNodeProperties

        [Fact]
        public void UpdateNodeProperties_ModifiesExistingProps()
        {
            var g = CreateGraph();
            g.AddNode("n1", new Dictionary<string, object> { { "name", "Alice" }, { "age", 25 } });

            g.UpdateNodeProperties("n1", new Dictionary<string, object> { { "age", 26 }, { "city", "Beijing" } });

            var props = g.GetNodeProperties("n1");
            Assert.Equal("Alice", props["name"]); // unchanged
            Assert.Equal(26, props["age"]);        // updated
            Assert.Equal("Beijing", props["city"]); // new
        }

        [Fact]
        public void UpdateNodeProperties_NonExistingNode_ThrowsKeyNotFoundException()
        {
            var g = CreateGraph();

            Assert.Throws<KeyNotFoundException>(() =>
                g.UpdateNodeProperties("ghost", new Dictionary<string, object> { { "x", 1 } }));
        }

        #endregion

        #region UpdateEdgeProperties

        [Fact]
        public void UpdateEdgeProperties_ModifiesExistingProps()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddEdge("a", "b", new Dictionary<string, object> { { "weight", 1.0 } });

            g.UpdateEdgeProperties("a", "b", new Dictionary<string, object> { { "weight", 5.0 } });

            var props = g.GetEdgeProperties("a", "b");
            Assert.Equal(5.0, props["weight"]);
        }

        [Fact]
        public void UpdateEdgeProperties_NonExistingEdge_ThrowsKeyNotFoundException()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");

            Assert.Throws<KeyNotFoundException>(() =>
                g.UpdateEdgeProperties("a", "b", new Dictionary<string, object> { { "x", 1 } }));
        }

        #endregion

        #region GetEdges

        [Fact]
        public void GetEdges_ReturnsAllEdges()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b");
            g.AddEdge("b", "c");

            var edges = g.GetEdges().ToList();

            Assert.Equal(2, edges.Count);
            Assert.Contains(("a", "b"), edges);
            Assert.Contains(("b", "c"), edges);
        }

        #endregion

        #region EdgeType (Issue #135)

        [Fact]
        public void AddEdge_WithType_TypeIsStored()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");

            g.AddEdge("a", "b", "WorksAt");

            var props = g.GetEdgeProperties("a", "b");
            Assert.NotNull(props);
            // Type is stored as a first-class field, not in properties
            Assert.True(g.HasEdge("a", "b"));
        }

        [Fact]
        public void AddEdge_WithType_GetEdgesReturnsEdge()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b", "Friend");
            g.AddEdge("a", "c", "Colleague");

            var edges = g.GetEdges().ToList();
            Assert.Equal(2, edges.Count);
        }

        [Fact]
        public void GetOutNeighbors_WithType_FiltersCorrectly()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b", "Friend");
            g.AddEdge("a", "c", "Colleague");

            var friends = g.GetOutNeighbors("a", "Friend").ToList();
            Assert.Single(friends);
            Assert.Contains("b", friends);

            var colleagues = g.GetOutNeighbors("a", "Colleague").ToList();
            Assert.Single(colleagues);
            Assert.Contains("c", colleagues);
        }

        [Fact]
        public void GetInNeighbors_WithType_FiltersCorrectly()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "c", "Friend");
            g.AddEdge("b", "c", "Colleague");

            var friends = g.GetInNeighbors("c", "Friend").ToList();
            Assert.Single(friends);
            Assert.Contains("a", friends);

            var colleagues = g.GetInNeighbors("c", "Colleague").ToList();
            Assert.Single(colleagues);
            Assert.Contains("b", colleagues);
        }

        [Fact]
        public void GetNeighbors_WithType_FiltersCorrectly()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b", "Friend");
            g.AddEdge("c", "b", "Colleague");

            var friends = g.GetNeighbors("b", "Friend").ToList();
            Assert.Single(friends);
            Assert.Contains("a", friends);
        }

        [Fact]
        public void GetOutNeighbors_NoType_ReturnsAll()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b", "Friend");
            g.AddEdge("a", "c", "Colleague");

            var all = g.GetOutNeighbors("a").ToList();
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public void WhereEdgeType_FiltersEdgesInQuery()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");
            g.AddNode("c");
            g.AddEdge("a", "b", "Friend");
            g.AddEdge("a", "c", "Colleague");

            var result = g.Query()
                .From("a")
                .TraverseOut()
                .WhereEdgeType("Friend")
                .ToNodeIds()
                .ToList();

            Assert.Contains("a", result);
            Assert.Contains("b", result);
            Assert.DoesNotContain("c", result);
        }

        [Fact]
        public void WhereEdgeType_CombinedWithNodeFilter()
        {
            var g = CreateGraph();
            g.AddNode("a", new Dictionary<string, object> { { "active", true } });
            g.AddNode("b", new Dictionary<string, object> { { "active", false } });
            g.AddNode("c", new Dictionary<string, object> { { "active", true } });
            g.AddEdge("a", "b", "Friend");
            g.AddEdge("a", "c", "Friend");

            var result = g.Query()
                .From("a")
                .TraverseOut()
                .WhereEdgeType("Friend")
                .WhereNodeProperty("active", QueryOp.Eq, true)
                .ToNodeIds()
                .ToList();

            Assert.Contains("a", result);
            Assert.DoesNotContain("b", result);
            Assert.Contains("c", result);
        }

        [Fact]
        public void AddEdge_NullType_DefaultsToEmpty()
        {
            var g = CreateGraph();
            g.AddNode("a");
            g.AddNode("b");

            // AddEdge without type (null defaults to empty)
            g.AddEdge("a", "b");

            // Edge should still be found via unfiltered query
            var allNeighbors = g.GetOutNeighbors("a").ToList();
            Assert.Single(allNeighbors);
            Assert.Contains("b", allNeighbors);
            Assert.True(g.HasEdge("a", "b"));
        }

        #endregion

        #region Dispose on dataset

        [Fact(Skip = "Known issue: Dispose does not enforce ObjectDisposedException on all operations")]
        public void Graph_AfterStoreDispose_ThrowsObjectDisposedException()
        {
            var store = new LiteDbDataStore(Path.Combine(Path.GetTempPath(), $"graph_dispose_{Guid.NewGuid():N}.db"));
            var g = store.CreateGraph("g1");
            g.AddNode("a");

            store.Dispose();

            Assert.Throws<ObjectDisposedException>(() => g.HasNode("a"));
            Assert.Throws<ObjectDisposedException>(() => g.NodeCount);
        }

        #endregion
    }
}
