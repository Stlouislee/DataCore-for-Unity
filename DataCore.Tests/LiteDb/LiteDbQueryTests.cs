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
    public class LiteDbQueryTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly LiteDbDataStore _store;

        public LiteDbQueryTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"datacore_query_test_{Guid.NewGuid():N}.db");
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

        /// <summary>
        /// Creates a sample tabular dataset with 5 rows for query testing.
        /// Columns: name (string), age (numeric), city (string)
        /// </summary>
        private ITabularDataset CreateSampleTable()
        {
            var ds = _store.CreateTabular("people");
            ds.AddStringColumn("name", new string[] { "Alice", "Bob", "Charlie", "Diana", "Eve" });
            ds.AddNumericColumn("age", new double[] { 30, 25, 35, 28, 42 });
            ds.AddStringColumn("city", new string[] { "Beijing", "Shanghai", "Beijing", "Shenzhen", "Shanghai" });
            return ds;
        }

        /// <summary>
        /// Creates a sample graph for query testing.
        /// Structure: a -> b -> c, a -> d, c -> e
        /// </summary>
        private IGraphDataset CreateSampleGraph()
        {
            var g = _store.CreateGraph("social");
            g.AddNode("a", new Dictionary<string, object> { { "role", "admin" }, { "level", 1 } });
            g.AddNode("b", new Dictionary<string, object> { { "role", "user" }, { "level", 2 } });
            g.AddNode("c", new Dictionary<string, object> { { "role", "user" }, { "level", 3 } });
            g.AddNode("d", new Dictionary<string, object> { { "role", "moderator" }, { "level", 2 } });
            g.AddNode("e", new Dictionary<string, object> { { "role", "user" }, { "level", 1 } });

            g.AddEdge("a", "b", new Dictionary<string, object> { { "type", "follows" } });
            g.AddEdge("b", "c", new Dictionary<string, object> { { "type", "follows" } });
            g.AddEdge("a", "d", new Dictionary<string, object> { { "type", "follows" } });
            g.AddEdge("c", "e", new Dictionary<string, object> { { "type", "follows" } });
            g.AddEdge("d", "b", new Dictionary<string, object> { { "type", "blocks" } });

            return g;
        }

        #region TabularQuery: Where with Eq

        [Fact]
        public void Where_Eq_FiltersExactMatch()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().Where("city", QueryOp.Eq, "Beijing").ToRowIndices();

            Assert.Equal(2, result.Length);
            // Alice (index 0) and Charlie (index 2) are in Beijing
            Assert.Contains(0, result);
            Assert.Contains(2, result);
        }

        [Fact]
        public void Where_Eq_Numeric_FiltersExactValue()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().Where("age", QueryOp.Eq, 25).ToRowIndices();

            Assert.Single(result);
            Assert.Equal(1, result[0]); // Bob
        }

        #endregion

        #region TabularQuery: Where with NotEq

        [Fact]
        public void Where_NotEq_ExcludesMatch()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().Where("city", QueryOp.Ne, "Shanghai").ToRowIndices();

            // Should include Beijing (0, 2) and Shenzhen (3)
            Assert.Equal(3, result.Length);
            Assert.DoesNotContain(1, result); // Bob in Shanghai
            Assert.DoesNotContain(4, result); // Eve in Shanghai
        }

        #endregion

        #region TabularQuery: Where with Gt, Lt, Ge, Le

        [Fact]
        public void Where_Gt_FiltersCorrectly()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().Where("age", QueryOp.Gt, 30).ToRowIndices();

            // Charlie (35) and Eve (42)
            Assert.Equal(2, result.Length);
            Assert.Contains(2, result);
            Assert.Contains(4, result);
        }

        [Fact]
        public void Where_Lt_FiltersCorrectly()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().Where("age", QueryOp.Lt, 30).ToRowIndices();

            // Bob (25) and Diana (28)
            Assert.Equal(2, result.Length);
            Assert.Contains(1, result);
            Assert.Contains(3, result);
        }

        [Fact]
        public void Where_Ge_FiltersCorrectly()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().Where("age", QueryOp.Ge, 30).ToRowIndices();

            // Alice (30), Charlie (35), Eve (42)
            Assert.Equal(3, result.Length);
            Assert.Contains(0, result);
            Assert.Contains(2, result);
            Assert.Contains(4, result);
        }

        [Fact]
        public void Where_Le_FiltersCorrectly()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().Where("age", QueryOp.Le, 30).ToRowIndices();

            // Alice (30), Bob (25), Diana (28)
            Assert.Equal(3, result.Length);
            Assert.Contains(0, result);
            Assert.Contains(1, result);
            Assert.Contains(3, result);
        }

        #endregion

        #region TabularQuery: Where with Contains, StartsWith

        [Fact]
        public void Where_Contains_FiltersSubstring()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().Where("city", QueryOp.Contains, "hai").ToRowIndices();

            // Shanghai matches
            Assert.Equal(2, result.Length);
            Assert.Contains(1, result); // Bob -> Shanghai
            Assert.Contains(4, result); // Eve -> Shanghai
        }

        [Fact]
        public void Where_StartsWith_FiltersPrefix()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().Where("name", QueryOp.StartsWith, "D").ToRowIndices();

            Assert.Single(result);
            Assert.Equal(3, result[0]); // Diana
        }

        #endregion

        #region TabularQuery: Where convenience methods

        [Fact]
        public void WhereEquals_FiltersCorrectly()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().WhereEquals("name", "Eve").ToRowIndices();

            Assert.Single(result);
            Assert.Equal(4, result[0]);
        }

        [Fact]
        public void WhereGreaterThan_FiltersCorrectly()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().WhereGreaterThan("age", 35).ToRowIndices();

            Assert.Single(result);
            Assert.Equal(4, result[0]); // Eve (42)
        }

        [Fact]
        public void WhereContains_FiltersCorrectly()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().WhereContains("name", "li").ToRowIndices();

            // Alice and Charlie contain "li"
            Assert.Equal(2, result.Length);
            Assert.Contains(0, result);
            Assert.Contains(2, result);
        }

        [Fact]
        public void WhereStartsWith_FiltersCorrectly()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().WhereStartsWith("name", "A").ToRowIndices();

            Assert.Single(result);
            Assert.Equal(0, result[0]); // Alice
        }

        [Fact]
        public void WhereBetween_FiltersRange()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().WhereBetween("age", 25, 30).ToRowIndices();

            // Alice (30), Bob (25), Diana (28)
            Assert.Equal(3, result.Length);
            Assert.Contains(0, result);
            Assert.Contains(1, result);
            Assert.Contains(3, result);
        }

        [Fact]
        public void WhereIn_FiltersMultipleValues()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().WhereIn("city", new[] { "Beijing", "Shenzhen" }).ToRowIndices();

            // Alice (Beijing), Charlie (Beijing), Diana (Shenzhen)
            Assert.Equal(3, result.Length);
            Assert.Contains(0, result);
            Assert.Contains(2, result);
            Assert.Contains(3, result);
        }

        [Fact]
        public void WhereIsNull_FiltersNullValues()
        {
            var ds = _store.CreateTabular("nullable");
            ds.AddStringColumn("val", new string[] { "a", null, "c" });

            var result = ds.Query().WhereIsNull("val").ToRowIndices();

            Assert.Single(result);
            Assert.Equal(1, result[0]);
        }

        [Fact]
        public void WhereIsNotNull_FiltersNonNullValues()
        {
            var ds = _store.CreateTabular("nullable2");
            ds.AddStringColumn("val", new string[] { "a", null, "c" });

            var result = ds.Query().WhereIsNotNull("val").ToRowIndices();

            Assert.Equal(2, result.Length);
            Assert.Contains(0, result);
            Assert.Contains(2, result);
        }

        #endregion

        #region TabularQuery: OrderBy / OrderByDescending

        [Fact]
        public void OrderBy_SortsAscending()
        {
            var ds = CreateSampleTable();
            var ages = ds.Query().OrderBy("age").ToDictionaries()
                .Select(d => (double)d["age"]).ToArray();

            Assert.Equal(new double[] { 25, 28, 30, 35, 42 }, ages);
        }

        [Fact]
        public void OrderByDescending_SortsDescending()
        {
            var ds = CreateSampleTable();
            var ages = ds.Query().OrderByDescending("age").ToDictionaries()
                .Select(d => (double)d["age"]).ToArray();

            Assert.Equal(new double[] { 42, 35, 30, 28, 25 }, ages);
        }

        [Fact]
        public void OrderBy_StringColumn_SortsAlphabetically()
        {
            var ds = CreateSampleTable();
            var names = ds.Query().OrderBy("name").ToDictionaries()
                .Select(d => (string)d["name"]).ToArray();

            Assert.Equal(new[] { "Alice", "Bob", "Charlie", "Diana", "Eve" }, names);
        }

        #endregion

        #region TabularQuery: Limit / Skip

        [Fact]
        public void Limit_RestrictsResultCount()
        {
            var ds = CreateSampleTable();
            var result = ds.Query().OrderBy("age").Limit(3).ToRowIndices();

            Assert.Equal(3, result.Length);
        }

        [Fact]
        public void Skip_SkipsFirstNRows()
        {
            var ds = CreateSampleTable();
            var ages = ds.Query().OrderBy("age").Skip(2).ToDictionaries()
                .Select(d => (double)d["age"]).ToArray();

            // After sorting: 25, 28, 30, 35, 42. Skip 2 -> 30, 35, 42
            Assert.Equal(new double[] { 30, 35, 42 }, ages);
        }

        [Fact]
        public void Skip_Limit_CombinedWorkCorrectly()
        {
            var ds = CreateSampleTable();
            var ages = ds.Query().OrderBy("age").Skip(1).Limit(2).ToDictionaries()
                .Select(d => (double)d["age"]).ToArray();

            // After sorting: 25, 28, 30, 35, 42. Skip 1, Take 2 -> 28, 30
            Assert.Equal(new double[] { 28, 30 }, ages);
        }

        [Fact]
        public void Page_ReturnsCorrectPage()
        {
            var ds = CreateSampleTable();
            // Page 2 with page size 2, sorted by age
            // All sorted: 25(Bob), 28(Diana), 30(Alice), 35(Charlie), 42(Eve)
            // Page 2 = items 3-4: 30(Alice), 35(Charlie)
            var ages = ds.Query().OrderBy("age").Page(2, 2).ToDictionaries()
                .Select(d => (double)d["age"]).ToArray();

            Assert.Equal(new double[] { 30, 35 }, ages);
        }

        // Known issue: Skip(0) should be a no-op, Limit(int.MaxValue) should return all.
        // These are the default values, so no issues expected. However, if the Skip/Limit
        // implementation uses Tail() or other non-standard semantics, results may differ.
        [Fact]
        public void Skip_Zero_IsNoOp()
        {
            var ds = CreateSampleTable();
            var withSkip = ds.Query().Skip(0).ToRowIndices();
            var withoutSkip = ds.Query().ToRowIndices();

            Assert.Equal(withoutSkip.Length, withSkip.Length);
        }

        #endregion

        #region TabularQuery: Select column subset

        [Fact]
        public void Select_RestrictsColumnsInOutput()
        {
            var ds = CreateSampleTable();
            var rows = ds.Query().Select("name", "city").ToDictionaries();

            foreach (var row in rows)
            {
                Assert.True(row.ContainsKey("name"), "Selected column 'name' should be present");
                Assert.True(row.ContainsKey("city"), "Selected column 'city' should be present");
                Assert.False(row.ContainsKey("age"), "Non-selected column 'age' should not be present");
            }
        }

        [Fact]
        public void Select_SingleColumn_Works()
        {
            var ds = CreateSampleTable();
            var rows = ds.Query().Select("name").ToDictionaries();

            Assert.All(rows, row =>
            {
                Assert.Single(row);
                Assert.True(row.ContainsKey("name"));
            });
        }

        #endregion

        #region TabularQuery: ToRowIndices / ToDictionaries

        [Fact]
        public void ToRowIndices_ReturnsFilteredIndices()
        {
            var ds = CreateSampleTable();
            var indices = ds.Query().Where("city", QueryOp.Eq, "Beijing").ToRowIndices();

            Assert.All(indices, i => Assert.True(i >= 0 && i < ds.RowCount));
        }

        [Fact]
        public void ToDictionaries_ReturnsFilteredDictionaries()
        {
            var ds = CreateSampleTable();
            var dicts = ds.Query().Where("age", QueryOp.Gt, 30).ToDictionaries();

            Assert.Equal(2, dicts.Count);
            Assert.All(dicts, d => Assert.True((double)d["age"] > 30));
        }

        [Fact]
        public void ToDictionaries_EmptyResult_ReturnsEmptyList()
        {
            var ds = CreateSampleTable();
            var dicts = ds.Query().Where("age", QueryOp.Gt, 100).ToDictionaries();

            Assert.Empty(dicts);
        }

        #endregion

        #region TabularQuery: Count / Any / FirstOrDefault

        [Fact]
        public void Count_ReturnsMatchingRowCount()
        {
            var ds = CreateSampleTable();
            int count = ds.Query().Where("city", QueryOp.Eq, "Shanghai").Count();

            Assert.Equal(2, count);
        }

        [Fact]
        public void Any_TrueWhenMatchesExist()
        {
            var ds = CreateSampleTable();

            Assert.True(ds.Query().Where("age", QueryOp.Gt, 40).Any());
        }

        [Fact]
        public void Any_FalseWhenNoMatches()
        {
            var ds = CreateSampleTable();

            Assert.False(ds.Query().Where("age", QueryOp.Gt, 100).Any());
        }

        [Fact]
        public void FirstOrDefault_ReturnsFirstMatch()
        {
            var ds = CreateSampleTable();
            var first = ds.Query().OrderBy("age").FirstOrDefault();

            Assert.NotNull(first);
            Assert.Equal("Bob", first["name"]);
            Assert.Equal(25.0, first["age"]);
        }

        [Fact]
        public void FirstOrDefault_NoMatches_ReturnsNull()
        {
            var ds = CreateSampleTable();

            var result = ds.Query().Where("age", QueryOp.Gt, 100).FirstOrDefault();

            Assert.Null(result);
        }

        #endregion

        #region TabularQuery: Chained operations

        [Fact]
        public void Chained_Where_OrderBy_Limit_Works()
        {
            var ds = CreateSampleTable();

            // Where city == Beijing, OrderBy age descending, Limit 1
            var result = ds.Query()
                .Where("city", QueryOp.Eq, "Beijing")
                .OrderByDescending("age")
                .Limit(1)
                .ToDictionaries();

            Assert.Single(result);
            Assert.Equal("Charlie", result[0]["name"]);
            Assert.Equal(35.0, result[0]["age"]);
        }

        [Fact]
        public void Chained_MultipleWhereConditions()
        {
            var ds = CreateSampleTable();

            var result = ds.Query()
                .Where("age", QueryOp.Ge, 25)
                .Where("age", QueryOp.Le, 30)
                .Where("city", QueryOp.Eq, "Shanghai")
                .ToRowIndices();

            // Bob (25, Shanghai)
            Assert.Single(result);
            Assert.Equal(1, result[0]);
        }

        [Fact]
        public void Chained_Where_OrderBy_Select()
        {
            var ds = CreateSampleTable();

            var result = ds.Query()
                .Where("city", QueryOp.Eq, "Beijing")
                .OrderBy("name")
                .Select("name", "age")
                .ToDictionaries();

            Assert.Equal(2, result.Count);
            Assert.Equal("Alice", result[0]["name"]);
            Assert.Equal("Charlie", result[1]["name"]);
            Assert.All(result, r => Assert.False(r.ContainsKey("city")));
        }

        #endregion

        #region TabularQuery: Aggregation

        [Fact]
        public void Sum_CalculatesCorrectly()
        {
            var ds = CreateSampleTable();
            double total = ds.Query().Sum("age");

            // 30 + 25 + 35 + 28 + 42 = 160
            Assert.Equal(160.0, total);
        }

        [Fact]
        public void Average_CalculatesCorrectly()
        {
            var ds = CreateSampleTable();
            double avg = ds.Query().Average("age");

            // 160 / 5 = 32
            Assert.Equal(32.0, avg);
        }

        [Fact]
        public void Max_ReturnsHighestValue()
        {
            var ds = CreateSampleTable();
            double max = ds.Query().Max("age");

            Assert.Equal(42.0, max);
        }

        [Fact]
        public void Min_ReturnsLowestValue()
        {
            var ds = CreateSampleTable();
            double min = ds.Query().Min("age");

            Assert.Equal(25.0, min);
        }

        [Fact]
        public void Sum_WithFilter_CalculatesFilteredSum()
        {
            var ds = CreateSampleTable();
            double total = ds.Query()
                .Where("city", QueryOp.Eq, "Beijing")
                .Sum("age");

            // Alice (30) + Charlie (35) = 65
            Assert.Equal(65.0, total);
        }

        #endregion

        #region GraphQuery: BFS traversal

        [Fact]
        public void GraphQuery_From_TraverseOut_BFS()
        {
            var g = CreateSampleGraph();

            // From "a", traverse out: a -> b, a -> d; then b -> c, d -> b (already visited); then c -> e
            var visited = g.Query()
                .From("a")
                .TraverseOut()
                .ToNodeIds()
                .ToList();

            Assert.Contains("a", visited);
            Assert.Contains("b", visited);
            Assert.Contains("d", visited);
            Assert.Contains("c", visited);
            Assert.Contains("e", visited);
            Assert.Equal(5, visited.Count);
        }

        [Fact]
        public void GraphQuery_From_TraverseOut_MaxDepth()
        {
            var g = CreateSampleGraph();

            // From "a", traverse out with max depth 1:
            // depth 0: a
            // depth 1: b, d (out-neighbors of a)
            var visited = g.Query()
                .From("a")
                .TraverseOut()
                .MaxDepth(1)
                .ToNodeIds()
                .ToList();

            Assert.Contains("a", visited);
            Assert.Contains("b", visited);
            Assert.Contains("d", visited);
            // Should NOT include c or e (depth 2+)
            Assert.DoesNotContain("c", visited);
            Assert.DoesNotContain("e", visited);
        }

        [Fact]
        public void GraphQuery_From_TraverseIn()
        {
            var g = CreateSampleGraph();

            // From "c", traverse in: c <- b, b <- a, a <- d, d <- (nothing incoming besides a->d)
            // But also d -> b (edge from d to b, so in-neighbor of b is d)
            var visited = g.Query()
                .From("c")
                .TraverseIn()
                .ToNodeIds()
                .ToList();

            // c's in-neighbor: b; b's in-neighbors: a, d; a's in-neighbors: (none); d's in-neighbors: a (already visited)
            Assert.Contains("c", visited);
            Assert.Contains("b", visited);
            Assert.Contains("a", visited);
            Assert.Contains("d", visited);
        }

        [Fact]
        public void GraphQuery_From_SingleNode_ReturnsSelf()
        {
            var g = CreateSampleGraph();

            var result = g.Query().From("a").MaxDepth(0).ToNodeIds().ToList();

            Assert.Single(result);
            Assert.Equal("a", result[0]);
        }

        [Fact]
        public void GraphQuery_From_NonExistingNode_ReturnsEmpty()
        {
            var g = CreateSampleGraph();

            var result = g.Query().From("nonexistent").TraverseOut().ToNodeIds().ToList();

            Assert.Empty(result);
        }

        #endregion

        #region GraphQuery: Node/Edge filters

        [Fact]
        public void GraphQuery_WhereNodeProperty_FiltersNodes()
        {
            var g = CreateSampleGraph();

            var admins = g.Query()
                .WhereNodeProperty("role", QueryOp.Eq, "admin")
                .ToNodeIds()
                .ToList();

            Assert.Single(admins);
            Assert.Equal("a", admins[0]);
        }

        [Fact]
        public void GraphQuery_WhereNodeProperty_Gt_FiltersCorrectly()
        {
            var g = CreateSampleGraph();

            var highLevel = g.Query()
                .WhereNodeProperty("level", QueryOp.Gt, 1)
                .ToNodeIds()
                .ToList();

            // b (level 2), c (level 3), d (level 2)
            Assert.Equal(3, highLevel.Count);
            Assert.Contains("b", highLevel);
            Assert.Contains("c", highLevel);
            Assert.Contains("d", highLevel);
        }

        [Fact]
        public void GraphQuery_WhereNodeHasProperty_FiltersCorrectly()
        {
            var g = _store.CreateGraph("hasprop");
            g.AddNode("n1", new Dictionary<string, object> { { "tag", "yes" } });
            g.AddNode("n2"); // no properties
            g.AddNode("n3", new Dictionary<string, object> { { "tag", "also yes" } });

            var result = g.Query()
                .WhereNodeHasProperty("tag")
                .ToNodeIds()
                .ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains("n1", result);
            Assert.Contains("n3", result);
        }

        [Fact]
        public void GraphQuery_WhereEdgeProperty_FiltersEdges()
        {
            var g = CreateSampleGraph();

            var followEdges = g.Query()
                .WhereEdgeProperty("type", QueryOp.Eq, "follows")
                .ToEdges()
                .ToList();

            Assert.Equal(4, followEdges.Count);
            Assert.DoesNotContain(("d", "b"), followEdges); // that's a "blocks" edge
        }

        [Fact]
        public void GraphQuery_WhereEdgeProperty_MultipleFilters()
        {
            var g = CreateSampleGraph();

            var result = g.Query()
                .WhereEdgeProperty("type", QueryOp.Eq, "blocks")
                .ToEdges()
                .ToList();

            Assert.Single(result);
            Assert.Equal(("d", "b"), result[0]);
        }

        #endregion

        #region GraphQuery: ToNodeIds / ToEdgeIds / CountNodes / CountEdges

        [Fact]
        public void GraphQuery_CountNodes_AllNodes()
        {
            var g = CreateSampleGraph();

            int count = g.Query().CountNodes();

            Assert.Equal(5, count);
        }

        [Fact]
        public void GraphQuery_CountNodes_Filtered()
        {
            var g = CreateSampleGraph();

            int count = g.Query()
                .WhereNodeProperty("role", QueryOp.Eq, "user")
                .CountNodes();

            // b, c, e are "user"
            Assert.Equal(3, count);
        }

        [Fact]
        public void GraphQuery_CountEdges_AllEdges()
        {
            var g = CreateSampleGraph();

            int count = g.Query().CountEdges();

            Assert.Equal(5, count);
        }

        [Fact]
        public void GraphQuery_CountEdges_Filtered()
        {
            var g = CreateSampleGraph();

            int count = g.Query()
                .WhereEdgeProperty("type", QueryOp.Eq, "follows")
                .CountEdges();

            Assert.Equal(4, count);
        }

        [Fact]
        public void GraphQuery_ToNodeIds_FilteredReturnsCorrectIds()
        {
            var g = CreateSampleGraph();

            var ids = g.Query()
                .WhereNodeProperty("level", QueryOp.Eq, 2)
                .ToNodeIds()
                .ToList();

            Assert.Equal(2, ids.Count);
            Assert.Contains("b", ids);
            Assert.Contains("d", ids);
        }

        [Fact]
        public void GraphQuery_ToEdges_ReturnsAllEdges()
        {
            var g = CreateSampleGraph();

            var edges = g.Query().ToEdges().ToList();

            Assert.Equal(5, edges.Count);
        }

        #endregion

        #region GraphQuery: Chained traversal with filters

        [Fact]
        public void GraphQuery_TraverseOut_WithNodeFilter()
        {
            var g = CreateSampleGraph();

            // From "a", traverse out, but only include "user" role nodes
            var result = g.Query()
                .From("a")
                .TraverseOut()
                .WhereNodeProperty("role", QueryOp.Eq, "user")
                .ToNodeIds()
                .ToList();

            // BFS from a: a (admin, filtered out), b (user), d (moderator, filtered out),
            // c (user, via b), e (user, via c)
            Assert.Contains("b", result);
            Assert.Contains("c", result);
            Assert.Contains("e", result);
            Assert.DoesNotContain("a", result);
            Assert.DoesNotContain("d", result);
        }

        [Fact]
        public void GraphQuery_TraverseOut_WithMaxDepth_WithFilter()
        {
            var g = CreateSampleGraph();

            // From "a", traverse out max depth 1, only users
            var result = g.Query()
                .From("a")
                .TraverseOut()
                .MaxDepth(1)
                .WhereNodeProperty("role", QueryOp.Eq, "user")
                .ToNodeIds()
                .ToList();

            // a at depth 0: admin (filtered out)
            // b, d at depth 1: b is user (included), d is moderator (filtered out)
            Assert.Single(result);
            Assert.Contains("b", result);
        }

        #endregion
    }
}
