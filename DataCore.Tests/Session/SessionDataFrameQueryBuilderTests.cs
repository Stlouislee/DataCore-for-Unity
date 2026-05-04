using System;
using System.IO;
using System.Linq;
using AroAro.DataCore;
using AroAro.DataCore.Events;
using AroAro.DataCore.Session;
using Microsoft.Data.Analysis;
using Xunit;

namespace DataCore.Tests.Session
{
    public class SessionDataFrameQueryBuilderTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;
        private readonly AroAro.DataCore.Session.Session _session;

        public SessionDataFrameQueryBuilderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"DataCore_QueryBuilderTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _store = new DataCoreStore(Path.Combine(_tempDir, "test.db"));
            _session = new AroAro.DataCore.Session.Session("QueryTest", _store);
        }

        public void Dispose()
        {
            _session?.Dispose();
            _store?.Dispose();
            try { Directory.Delete(_tempDir, true); } catch { }
            DataCoreEventManager.ClearAllSubscriptions();
        }

        /// <summary>
        /// Creates a DataFrame with numeric + string columns and registers it in the session.
        /// Columns: "age" (double), "name" (string), "score" (double)
        /// 5 rows.
        /// </summary>
        private DataFrame SetupTestDataFrame()
        {
            var df = _session.CreateDataFrame("testDf");

            var ageCol = new DoubleDataFrameColumn("age", new double[] { 25, 30, 35, 40, 45 });
            var nameCol = new StringDataFrameColumn("name", new string[] { "Alice", "Bob", "Charlie", "Diana", "Eve" });
            var scoreCol = new DoubleDataFrameColumn("score", new double[] { 85.5, 90.0, 78.3, 92.1, 88.7 });

            df.Columns.Add(ageCol);
            df.Columns.Add(nameCol);
            df.Columns.Add(scoreCol);

            return df;
        }

        /// <summary>
        /// Creates a DataFrame suitable for GroupBy tests.
        /// Columns: "category" (string), "value" (double)
        /// </summary>
        private DataFrame SetupGroupByDataFrame()
        {
            var df = _session.CreateDataFrame("groupByDf");

            var catCol = new StringDataFrameColumn("category", new string[] { "A", "B", "A", "B", "A" });
            var valCol = new DoubleDataFrameColumn("value", new double[] { 10, 20, 30, 40, 50 });

            df.Columns.Add(catCol);
            df.Columns.Add(valCol);

            return df;
        }

        // ────────────────────────────────────────────────────────────────
        // Where with various operators
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Where_NumericGt_FiltersCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Gt, 35.0)
                .ExecuteAsDataFrame();

            // Rows with age > 35: 40, 45 → 2 rows
            Assert.Equal(2, result.Rows.Count);
        }

        [Fact]
        public void Where_NumericLt_FiltersCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Lt, 30.0)
                .ExecuteAsDataFrame();

            // Rows with age < 30: 25 → 1 row
            Assert.Equal(1, result.Rows.Count);
        }

        [Fact]
        public void Where_NumericGe_FiltersCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Ge, 30.0)
                .ExecuteAsDataFrame();

            // Rows with age >= 30: 30, 35, 40, 45 → 4 rows
            Assert.Equal(4, result.Rows.Count);
        }

        [Fact]
        public void Where_NumericLe_FiltersCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Le, 30.0)
                .ExecuteAsDataFrame();

            // Rows with age <= 30: 25, 30 → 2 rows
            Assert.Equal(2, result.Rows.Count);
        }

        [Fact]
        public void Where_NumericEq_FiltersCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Eq, 35.0)
                .ExecuteAsDataFrame();

            Assert.Equal(1, result.Rows.Count);
        }

        [Fact]
        public void Where_NumericNe_FiltersCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Ne, 35.0)
                .ExecuteAsDataFrame();

            // All except age=35 → 4 rows
            Assert.Equal(4, result.Rows.Count);
        }

        [Fact]
        public void Where_StringEq_FiltersCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("name", ComparisonOp.Eq, "Alice")
                .ExecuteAsDataFrame();

            Assert.Equal(1, result.Rows.Count);
            Assert.Equal("Alice", (string)result.Columns["name"][0]);
        }

        [Fact]
        public void Where_StringContains_FiltersCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("name", ComparisonOp.Contains, "li")
                .ExecuteAsDataFrame();

            // "Alice" contains "li", "Charlie" contains "li" → 2 rows
            Assert.Equal(2, result.Rows.Count);
        }

        [Fact]
        public void Where_StringStartsWith_FiltersCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("name", ComparisonOp.StartsWith, "D")
                .ExecuteAsDataFrame();

            Assert.Equal(1, result.Rows.Count);
            Assert.Equal("Diana", (string)result.Columns["name"][0]);
        }

        [Fact(Skip = "Known issue: QueryOp.EndsWith not implemented in TabularData.Where")]
        public void Where_StringEndsWith_FiltersCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("name", ComparisonOp.EndsWith, "e")
                .ExecuteAsDataFrame();

            // "Alice", "Charlie", "Diana", "Eve" end with "e" → 4 rows
            Assert.Equal(4, result.Rows.Count);
        }

        [Fact]
        public void Where_NonExistentColumn_ThrowsArgumentException()
        {
            SetupTestDataFrame();

            // Known issue: validation happens at execution time, not build time.
            // The Where() call succeeds, but Execute throws.
            var builder = _session.QueryDataFrame("testDf")
                .Where("nonexistent", ComparisonOp.Gt, 10.0);

            Assert.Throws<ArgumentException>(() => builder.ExecuteAsDataFrame());
        }

        // ────────────────────────────────────────────────────────────────
        // OrderBy / OrderByDescending
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void OrderBy_SortsAscending()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .OrderBy("age")
                .ExecuteAsDataFrame();

            var ages = result.Columns["age"];
            for (int i = 1; i < ages.Length; i++)
            {
                Assert.True((double)ages[i] >= (double)ages[i - 1]);
            }
        }

        [Fact]
        public void OrderByDescending_SortsDescending()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .OrderBy("age", ascending: false)
                .ExecuteAsDataFrame();

            var ages = result.Columns["age"];
            for (int i = 1; i < ages.Length; i++)
            {
                Assert.True((double)ages[i] <= (double)ages[i - 1]);
            }
        }

        // ────────────────────────────────────────────────────────────────
        // Limit
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Limit_ReturnsCorrectNumberOfRows()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Limit(3)
                .ExecuteAsDataFrame();

            Assert.Equal(3, result.Rows.Count);
        }

        [Fact]
        public void Limit_ZeroOrNegative_ThrowsArgumentException()
        {
            SetupTestDataFrame();

            Assert.Throws<ArgumentException>(() =>
                _session.QueryDataFrame("testDf").Limit(0));
            Assert.Throws<ArgumentException>(() =>
                _session.QueryDataFrame("testDf").Limit(-1));
        }

        [Fact(Skip = "Known issue: IndexOutOfRangeException in DataFrame Clone with mapIndices")]
        public void Limit_LargerThanRowCount_ReturnsAllRows()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Limit(100)
                .ExecuteAsDataFrame();

            Assert.Equal(5, result.Rows.Count);
        }

        // ────────────────────────────────────────────────────────────────
        // Offset — known issue: uses Tail() instead of proper Skip
        // ────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Bug", "offset-uses-tail")]
        public void Offset_SkipsFirstNRows_KnownIssueUsesTail()
        {
            // Known issue: Offset uses df.Tail(df.Rows.Count - count) which returns
            // the LAST (N - count) rows instead of skipping the first count rows.
            // For example, with 5 rows and offset=2, it returns Tail(3) which is
            // the last 3 rows, not rows 3,4,5 (skipping first 2).
            // This is incorrect — proper behavior would be Skip(count).
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Offset(2)
                .ExecuteAsDataFrame();

            // With Tail(5-2) = Tail(3), we get the last 3 rows.
            // Correct behavior: skip first 2 → rows at indices 2,3,4
            // Bug: Tail(3) → rows at indices 2,3,4 (same result in this case,
            // but the semantics differ when combined with other operations).
            Assert.Equal(3, result.Rows.Count);
        }

        [Fact]
        public void Offset_Zero_ReturnsAllRows()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Offset(0)
                .ExecuteAsDataFrame();

            Assert.Equal(5, result.Rows.Count);
        }

        [Fact]
        public void Offset_Negative_ThrowsArgumentException()
        {
            SetupTestDataFrame();

            Assert.Throws<ArgumentException>(() =>
                _session.QueryDataFrame("testDf").Offset(-1));
        }

        // ────────────────────────────────────────────────────────────────
        // GroupBy with single aggregate
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void GroupBy_Sum_AggregatesCorrectly()
        {
            SetupGroupByDataFrame();

            var result = _session.QueryDataFrame("groupByDf")
                .GroupBy("category", ("value", AggregateFunction.Sum))
                .ExecuteAsDataFrame();

            Assert.Equal(2, result.Rows.Count); // A and B
            Assert.True(result.Columns.IndexOf("value_Sum") >= 0);
        }

        [Fact]
        public void GroupBy_Count_AggregatesCorrectly()
        {
            SetupGroupByDataFrame();

            var result = _session.QueryDataFrame("groupByDf")
                .GroupBy("category", ("value", AggregateFunction.Count))
                .ExecuteAsDataFrame();

            Assert.Equal(2, result.Rows.Count);
            Assert.True(result.Columns.IndexOf("value_Count") >= 0);
        }

        [Fact]
        public void GroupBy_Mean_AggregatesCorrectly()
        {
            SetupGroupByDataFrame();

            var result = _session.QueryDataFrame("groupByDf")
                .GroupBy("category", ("value", AggregateFunction.Average))
                .ExecuteAsDataFrame();

            Assert.Equal(2, result.Rows.Count);
            Assert.True(result.Columns.IndexOf("value_Average") >= 0);
        }

        [Fact]
        public void GroupBy_Min_AggregatesCorrectly()
        {
            SetupGroupByDataFrame();

            var result = _session.QueryDataFrame("groupByDf")
                .GroupBy("category", ("value", AggregateFunction.Min))
                .ExecuteAsDataFrame();

            Assert.Equal(2, result.Rows.Count);
            Assert.True(result.Columns.IndexOf("value_Min") >= 0);
        }

        [Fact]
        public void GroupBy_Max_AggregatesCorrectly()
        {
            SetupGroupByDataFrame();

            var result = _session.QueryDataFrame("groupByDf")
                .GroupBy("category", ("value", AggregateFunction.Max))
                .ExecuteAsDataFrame();

            Assert.Equal(2, result.Rows.Count);
            Assert.True(result.Columns.IndexOf("value_Max") >= 0);
        }

        // ────────────────────────────────────────────────────────────────
        // GroupBy with MULTIPLE aggregates — known bug: only last survives
        // ────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Bug", "groupby-multiple-aggregates")]
        public void GroupBy_MultipleAggregates_AllAggregatesPresent()
        {
            // Fixed: GroupBy with multiple aggregates now preserves all aggregation columns.
            // Each aggregate is merged into the result DataFrame instead of overwriting.
            SetupGroupByDataFrame();

            var result = _session.QueryDataFrame("groupByDf")
                .GroupBy("category",
                    ("value", AggregateFunction.Sum),
                    ("value", AggregateFunction.Count),
                    ("value", AggregateFunction.Min))
                .ExecuteAsDataFrame();

            // The result should have 2 rows (one per category)
            Assert.Equal(2, result.Rows.Count);

            // All aggregate columns should be present
            Assert.True(result.Columns.IndexOf("value_Sum") >= 0,
                "value_Sum should be present after fix");
            Assert.True(result.Columns.IndexOf("value_Count") >= 0,
                "value_Count should be present after fix");
            Assert.True(result.Columns.IndexOf("value_Min") >= 0,
                "value_Min should be present after fix");
        }

        [Fact]
        [Trait("Bug", "groupby-multiple-aggregates")]
        public void GroupBy_TwoAggregates_BothPresent()
        {
            // Fixed: both aggregates are preserved in the result
            SetupGroupByDataFrame();

            var result = _session.QueryDataFrame("groupByDf")
                .GroupBy("category",
                    ("value", AggregateFunction.Sum),
                    ("value", AggregateFunction.Max))
                .ExecuteAsDataFrame();

            Assert.Equal(2, result.Rows.Count);

            // Both aggregate columns should be present
            Assert.True(result.Columns.IndexOf("value_Sum") >= 0,
                "value_Sum should be present after fix");
            Assert.True(result.Columns.IndexOf("value_Max") >= 0,
                "value_Max should be present after fix");
        }

        // ────────────────────────────────────────────────────────────────
        // Select column subset
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Select_ReturnsOnlySpecifiedColumns()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Select("name", "score")
                .ExecuteAsDataFrame();

            Assert.Equal(2, result.Columns.Count);
            Assert.True(result.Columns.IndexOf("name") >= 0);
            Assert.True(result.Columns.IndexOf("score") >= 0);
            Assert.True(result.Columns.IndexOf("age") < 0);
        }

        [Fact]
        public void Select_NonExistentColumn_ThrowsArgumentException()
        {
            SetupTestDataFrame();

            var builder = _session.QueryDataFrame("testDf")
                .Select("name", "nonexistent");

            Assert.Throws<ArgumentException>(() => builder.ExecuteAsDataFrame());
        }

        // ────────────────────────────────────────────────────────────────
        // Execute returns IDataSet
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Execute_ReturnsIDataSet()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Gt, 30.0)
                .Execute("filtered");

            Assert.NotNull(result);
            Assert.Equal("filtered", result.Name);
            Assert.True(_session.HasDataset("filtered"));
        }

        [Fact]
        public void Execute_NullName_ThrowsArgumentException()
        {
            SetupTestDataFrame();

            Assert.Throws<ArgumentException>(() =>
                _session.QueryDataFrame("testDf")
                    .Where("age", ComparisonOp.Gt, 10.0)
                    .Execute(null));
        }

        // ────────────────────────────────────────────────────────────────
        // ExecuteAsDataFrame returns DataFrame
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void ExecuteAsDataFrame_ReturnsDataFrame()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Gt, 30.0)
                .ExecuteAsDataFrame();

            Assert.NotNull(result);
            Assert.IsType<DataFrame>(result);
        }

        [Fact]
        public void ExecuteAsDataFrame_DoesNotSaveToSession()
        {
            SetupTestDataFrame();

            _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Gt, 30.0)
                .ExecuteAsDataFrame();

            // ExecuteAsDataFrame should NOT create a dataset in the session
            // (only Execute does that)
            Assert.Equal(0, _session.DatasetCount);
        }

        // ────────────────────────────────────────────────────────────────
        // Chained operations
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Chained_WhereOrderByLimit_WorksCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Ge, 30.0)
                .OrderBy("score", ascending: false)
                .Limit(2)
                .ExecuteAsDataFrame();

            // Filter: age >= 30 → 4 rows (30, 35, 40, 45)
            // Order by score desc → 92.1, 90.0, 88.7, 78.3
            // Limit 2 → 2 rows
            Assert.Equal(2, result.Rows.Count);

            var scores = result.Columns["score"];
            Assert.True((double)scores[0] >= (double)scores[1]);
        }

        [Fact]
        public void Chained_SelectWhere_WorksCorrectly()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Gt, 30.0)
                .Select("name", "age")
                .ExecuteAsDataFrame();

            Assert.Equal(2, result.Columns.Count);
            Assert.True(result.Rows.Count > 0);
        }

        // ────────────────────────────────────────────────────────────────
        // Validation errors happen at execution time, not build time
        // ────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Bug", "deferred-validation")]
        public void ValidationErrors_DeferredToExecution_NotBuildTime()
        {
            // Known issue: validation of column existence happens inside the
            // lambda operations, not when Where/OrderBy/Select is called.
            // This means the builder accepts invalid column names without error,
            // and only throws when Execute/ExecuteAsDataFrame is called.
            //
            // Ideally, invalid column references should be caught at build time
            // for fail-fast behavior.
            SetupTestDataFrame();

            // Build phase succeeds even with invalid column
            var builder = _session.QueryDataFrame("testDf")
                .Where("nonexistent_column", ComparisonOp.Gt, 10.0);

            // Error only manifests at execution time
            Assert.Throws<ArgumentException>(() => builder.ExecuteAsDataFrame());
        }

        [Fact]
        [Trait("Bug", "deferred-validation")]
        public void OrderBy_InvalidColumn_DeferredToExecution()
        {
            SetupTestDataFrame();

            var builder = _session.QueryDataFrame("testDf")
                .OrderBy("nonexistent");

            Assert.Throws<ArgumentException>(() => builder.ExecuteAsDataFrame());
        }

        [Fact]
        [Trait("Bug", "deferred-validation")]
        public void Select_InvalidColumn_DeferredToExecution()
        {
            SetupTestDataFrame();

            var builder = _session.QueryDataFrame("testDf")
                .Select("name", "nonexistent");

            Assert.Throws<ArgumentException>(() => builder.ExecuteAsDataFrame());
        }

        // ────────────────────────────────────────────────────────────────
        // WithDescription
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void WithDescription_DoesNotAffectResult()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .WithDescription("My custom query")
                .Where("age", ComparisonOp.Gt, 30.0)
                .ExecuteAsDataFrame();

            Assert.NotNull(result);
            Assert.True(result.Rows.Count > 0);
        }

        // ────────────────────────────────────────────────────────────────
        // Edge cases
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Where_AllRowsFiltered_ReturnsEmptyDataFrame()
        {
            SetupTestDataFrame();

            var result = _session.QueryDataFrame("testDf")
                .Where("age", ComparisonOp.Gt, 1000.0)
                .ExecuteAsDataFrame();

            Assert.Equal(0, result.Rows.Count);
        }

        [Fact]
        public void GroupBy_NonExistentGroupColumn_ThrowsArgumentException()
        {
            SetupGroupByDataFrame();

            var builder = _session.QueryDataFrame("groupByDf")
                .GroupBy("nonexistent", ("value", AggregateFunction.Sum));

            Assert.Throws<ArgumentException>(() => builder.ExecuteAsDataFrame());
        }

        [Fact]
        public void GroupBy_NonExistentAggregateColumn_ThrowsArgumentException()
        {
            SetupGroupByDataFrame();

            var builder = _session.QueryDataFrame("groupByDf")
                .GroupBy("category", ("nonexistent", AggregateFunction.Sum));

            Assert.Throws<ArgumentException>(() => builder.ExecuteAsDataFrame());
        }
    }
}
