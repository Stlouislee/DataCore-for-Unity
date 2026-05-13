using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using AroAro.DataCore;
using AroAro.DataCore.Tools;
using Xunit;

namespace DataCore.Tests.Tools
{
    public class PerformanceOptimizationTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;

        public PerformanceOptimizationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dc_perf_" + Guid.NewGuid().ToString("N"));
            _store = new DataCoreStore(Path.Combine(_tempDir, "test.db"));
            DataCoreTools.Initialize(_store);
            FilterExpressionParser.ClearCache();
        }

        public void Dispose()
        {
            _store?.Dispose();
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private void SeedLargeDataset(int rows)
        {
            var tabular = _store.CreateTabular("LargeData");
            var ids = Enumerable.Range(1, rows).Select(i => (double)i).ToArray();
            var names = Enumerable.Range(1, rows).Select(i => $"User_{i}").ToArray();
            var ages = Enumerable.Range(1, rows).Select(i => (double)(20 + i % 50)).ToArray();
            var cities = Enumerable.Range(1, rows).Select(i => new[] { "Shanghai", "Beijing", "Guangzhou", "Shenzhen" }[i % 4]).ToArray();

            tabular.AddNumericColumn("id", ids);
            tabular.AddStringColumn("name", names);
            tabular.AddNumericColumn("age", ages);
            tabular.AddStringColumn("city", cities);
        }

        [Fact]
        public void WorkspaceOpen_DirectCopy_WorksCorrectly()
        {
            SeedLargeDataset(100);

            var result = Parse(DataCoreTools.Execute("workspace_open",
                new Dictionary<string, object>
                {
                    ["dataset"] = "LargeData"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(100, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void WorkspaceOpen_DirectCopy_PreservesData()
        {
            var tabular = _store.CreateTabular("Source");
            tabular.AddNumericColumn("val", new double[] { 1.5, 2.5, 3.5 });
            tabular.AddStringColumn("label", new[] { "a", "b", "c" });

            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["dataset"] = "Source"
            });

            var sample = Parse(DataCoreTools.Execute("workspace_sample",
                new Dictionary<string, object>
                {
                    ["dataset"] = "Source",
                    ["rows"] = 10
                }));

            Assert.True(sample.GetProperty("success").GetBoolean());
            var rows = sample.GetProperty("result").GetProperty("data");
            Assert.Equal(3, rows.GetArrayLength());
        }

        [Fact]
        public void FilterExpressionParser_CachesPredicates()
        {
            FilterExpressionParser.ClearCache();
            Assert.Equal(0, FilterExpressionParser.CacheSize);

            var pred1 = FilterExpressionParser.Parse("age > 18");
            Assert.Equal(1, FilterExpressionParser.CacheSize);

            // Same expression returns cached predicate
            var pred2 = FilterExpressionParser.Parse("age > 18");
            Assert.Equal(1, FilterExpressionParser.CacheSize);
            Assert.Same(pred1, pred2);

            // Different expression creates new entry
            var pred3 = FilterExpressionParser.Parse("city == Shanghai");
            Assert.Equal(2, FilterExpressionParser.CacheSize);
        }

        [Fact]
        public void FilterExpressionParser_CachedPredicate_WorksCorrectly()
        {
            var pred = FilterExpressionParser.Parse("age > 25 AND city == Shanghai");

            var row1 = new Dictionary<string, object> { ["age"] = 30.0, ["city"] = "Shanghai" };
            var row2 = new Dictionary<string, object> { ["age"] = 20.0, ["city"] = "Shanghai" };
            var row3 = new Dictionary<string, object> { ["age"] = 30.0, ["city"] = "Beijing" };

            Assert.True(pred(row1));
            Assert.False(pred(row2));
            Assert.False(pred(row3));
        }

        [Fact]
        public void FilterExpressionParser_ClearCache_Works()
        {
            FilterExpressionParser.Parse("x > 1");
            FilterExpressionParser.Parse("y < 2");
            Assert.True(FilterExpressionParser.CacheSize >= 2);

            FilterExpressionParser.ClearCache();
            Assert.Equal(0, FilterExpressionParser.CacheSize);
        }

        [Fact]
        public void Join_WithHashIndex_WorksCorrectly()
        {
            var users = _store.CreateTabular("Users");
            users.AddNumericColumn("id", new double[] { 1, 2, 3 });
            users.AddStringColumn("name", new[] { "Alice", "Bob", "Charlie" });

            var orders = _store.CreateTabular("Orders");
            orders.AddNumericColumn("order_id", new double[] { 101, 102, 103 });
            orders.AddNumericColumn("user_id", new double[] { 1, 2, 1 });
            orders.AddNumericColumn("amount", new double[] { 100, 200, 150 });

            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "Users" });
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object> { ["dataset"] = "Orders" });

            var result = Parse(DataCoreTools.Execute("workspace_join",
                new Dictionary<string, object>
                {
                    ["left"] = "Users",
                    ["right"] = "Orders",
                    ["leftKey"] = "id",
                    ["rightKey"] = "user_id",
                    ["resultName"] = "Joined"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(3, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        private JsonElement Parse(string json) => JsonSerializer.Deserialize<JsonElement>(json);
    }
}
