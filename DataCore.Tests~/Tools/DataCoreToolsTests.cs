using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AroAro.DataCore;
using AroAro.DataCore.Tools;
using AroAro.DataCore.Workspace;
using Xunit;

namespace DataCore.Tests.Tools
{
    public class DataCoreToolsTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;

        public DataCoreToolsTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dc_tools_" + Guid.NewGuid().ToString("N"));
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
            var tabular = _store.CreateTabular("Users");
            tabular.AddNumericColumn("id", new double[] { 1, 2, 3, 4, 5 });
            tabular.AddStringColumn("name", new[] { "Alice", "Bob", "Charlie", "Diana", "Eve" });
            tabular.AddNumericColumn("age", new double[] { 25, 30, 35, 28, 22 });
            tabular.AddStringColumn("city", new[] { "Shanghai", "Beijing", "Shanghai", "Guangzhou", "Beijing" });
            tabular.AddNumericColumn("score", new double[] { 88.5, 92.0, 75.0, 95.5, 60.0 });
        }

        private void SeedOrders()
        {
            var tabular = _store.CreateTabular("Orders");
            tabular.AddNumericColumn("order_id", new double[] { 101, 102, 103, 104, 105 });
            tabular.AddNumericColumn("user_id", new double[] { 1, 2, 1, 3, 4 });
            tabular.AddNumericColumn("amount", new double[] { 250.0, 180.0, 320.0, 90.0, 410.0 });
            tabular.AddStringColumn("status", new[] { "completed", "pending", "completed", "completed", "pending" });
        }

        private JsonElement ParseResult(string json)
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        // ── 3.1 管理 ──

        [Fact]
        public void WorkspaceCreate_AndList()
        {
            var result = ParseResult(DataCoreTools.Execute("workspace_create",
                new Dictionary<string, object> { ["name"] = "analysis" }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal("analysis", result.GetProperty("result").GetProperty("name").GetString());

            var list = ParseResult(DataCoreTools.Execute("workspace_list", new Dictionary<string, object>()));
            Assert.True(list.GetProperty("success").GetBoolean());
            var workspaces = list.GetProperty("result").GetProperty("workspaces");
            Assert.Equal(2, workspaces.GetArrayLength()); // default + analysis
        }

        [Fact]
        public void WorkspaceDestroy()
        {
            DataCoreTools.Execute("workspace_create", new Dictionary<string, object> { ["name"] = "temp" });
            var result = ParseResult(DataCoreTools.Execute("workspace_destroy",
                new Dictionary<string, object> { ["name"] = "temp" }));

            Assert.True(result.GetProperty("success").GetBoolean());
        }

        // ── 3.2 加载 ──

        [Fact]
        public void WorkspaceOpen_CopiesData()
        {
            SeedUsers();
            var result = ParseResult(DataCoreTools.Execute("workspace_open",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "Users"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(5, result.GetProperty("result").GetProperty("rows").GetInt32());
            Assert.Equal("Store", result.GetProperty("result").GetProperty("source").GetString());
        }

        [Fact]
        public void WorkspaceOpen_NonexistentDataset_ReturnsError()
        {
            var result = ParseResult(DataCoreTools.Execute("workspace_open",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "Nonexistent"
                }));

            Assert.False(result.GetProperty("success").GetBoolean());
            Assert.Contains("not found", result.GetProperty("error").GetString());
        }

        // ── 3.3 检视 ──

        [Fact]
        public void WorkspaceDescribe_All()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_describe",
                new Dictionary<string, object> { ["workspace"] = "default" }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Contains("datasets", result.GetProperty("result").ToString());
        }

        [Fact]
        public void WorkspaceSample()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_sample",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "Users",
                    ["rows"] = 3
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(3, result.GetProperty("result").GetProperty("rowsRequested").GetInt32());
        }

        [Fact]
        public void WorkspaceSchema()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_schema",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "Users"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            var columns = result.GetProperty("result").GetProperty("columns");
            Assert.Equal(5, columns.GetArrayLength());
        }

        [Fact]
        public void WorkspaceStatistics()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_statistics",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "Users"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            var cols = result.GetProperty("result").GetProperty("columns");
            Assert.True(cols.TryGetProperty("age", out _));
        }

        // ── 3.4 变换 ──

        [Fact]
        public void WorkspaceFilter()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_filter",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["filter"] = "age > 25",
                    ["resultName"] = "adults"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(3, result.GetProperty("result").GetProperty("rows").GetInt32());
            Assert.Equal("adults", result.GetProperty("result").GetProperty("name").GetString());
        }

        [Fact]
        public void WorkspaceFilter_ComplexExpression()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_filter",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["filter"] = "age > 24 AND city == Shanghai",
                    ["resultName"] = "shanghai_adults"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            // Alice (25, Shanghai) and Charlie (35, Shanghai)
            Assert.Equal(2, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void WorkspaceFilter_OR()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_filter",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["filter"] = "city == Shanghai OR city == Guangzhou",
                    ["resultName"] = "south_cities"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            // Alice (Shanghai), Charlie (Shanghai), Diana (Guangzhou)
            Assert.Equal(3, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void WorkspaceFilter_Contains()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_filter",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["filter"] = "name contains Ali",
                    ["resultName"] = "matched"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(1, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void WorkspaceFilter_Between()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_filter",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["filter"] = "age between 25 30",
                    ["resultName"] = "mid_age"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            // Alice (25), Bob (30), Diana (28) = 3
            Assert.Equal(3, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void WorkspaceSelect()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_select",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["columns"] = new[] { "name", "age" },
                    ["resultName"] = "name_age"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(2, result.GetProperty("result").GetProperty("columns").GetInt32());
            Assert.Equal(5, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void WorkspaceSort()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_sort",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["by"] = "age",
                    ["order"] = "desc",
                    ["resultName"] = "by_age_desc"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            var sample = result.GetProperty("result").GetProperty("sample");
            // First row should be Charlie (35)
            Assert.Equal("Charlie", sample[0].GetProperty("name").GetString());
        }

        [Fact]
        public void WorkspaceDistinct()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_distinct",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["columns"] = new[] { "city" },
                    ["resultName"] = "unique_cities"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            // 3 unique cities: Shanghai, Beijing, Guangzhou
            Assert.Equal(3, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void WorkspaceAddColumn()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var json = DataCoreTools.Execute("workspace_add_column", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["source"] = "Users",
                ["columnName"] = "age_group",
                ["expression"] = "age >= 18 ? adult : minor",
                ["resultName"] = "with_age_group"
            });
            var result = ParseResult(json);
            Assert.True(result.GetProperty("success").GetBoolean(), json);
            Assert.Equal(6, result.GetProperty("result").GetProperty("columns").GetInt32());
        }

        [Fact]
        public void WorkspaceDropColumns()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_drop_columns",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["columns"] = new[] { "score" },
                    ["resultName"] = "no_score"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(4, result.GetProperty("result").GetProperty("columns").GetInt32());
        }

        [Fact]
        public void WorkspaceLimit()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_limit",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["count"] = 2,
                    ["offset"] = 1,
                    ["resultName"] = "limited"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(2, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void WorkspaceRandomSample()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_random_sample",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["count"] = 3,
                    ["seed"] = 42,
                    ["resultName"] = "sampled"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(3, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        // ── 3.5 组合 ──

        [Fact]
        public void WorkspaceJoin_InnerJoin()
        {
            SeedUsers();
            SeedOrders();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Orders"
            });

            var json = DataCoreTools.Execute("workspace_join", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["left"] = "Users",
                ["right"] = "Orders",
                ["leftKey"] = "id",
                ["rightKey"] = "user_id",
                ["joinType"] = "inner",
                ["resultName"] = "user_orders"
            });
            var result = ParseResult(json);
            Assert.True(result.GetProperty("success").GetBoolean(), json);
            Assert.Equal(5, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void WorkspaceJoin_LeftJoin()
        {
            SeedUsers();
            SeedOrders();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Orders"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_join",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["left"] = "Users",
                    ["right"] = "Orders",
                    ["leftKey"] = "id",
                    ["rightKey"] = "user_id",
                    ["joinType"] = "left",
                    ["resultName"] = "user_orders_left"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            // Users 1,2,3,4 have orders (user_id 1,2,1,3,4) → 5 matched + Eve unmatched = 6
            Assert.Equal(6, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void WorkspaceUnion()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });
            DataCoreTools.Execute("workspace_clone", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["source"] = "Users",
                ["newName"] = "Users2"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_union",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["sources"] = new[] { "Users", "Users2" },
                    ["resultName"] = "all_users"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(10, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        // ── 3.6 聚合 ──

        [Fact]
        public void WorkspaceAggregate()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_aggregate",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["groupBy"] = "city",
                    ["aggregations"] = new List<Dictionary<string, object>>
                    {
                        new() { ["column"] = "age", ["op"] = "mean", ["alias"] = "avg_age" },
                        new() { ["column"] = "id", ["op"] = "count", ["alias"] = "user_count" }
                    },
                    ["resultName"] = "city_stats"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(3, result.GetProperty("result").GetProperty("rows").GetInt32()); // 3 cities
        }

        [Fact]
        public void WorkspaceCount()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_count",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["filter"] = "age > 25"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(3, result.GetProperty("result").GetProperty("count").GetInt32());
        }

        // ── 3.7 持久化 ──

        [Fact]
        public void WorkspaceSave()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });
            DataCoreTools.Execute("workspace_filter", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["source"] = "Users",
                ["filter"] = "age > 25",
                ["resultName"] = "adults"
            });

            var json = DataCoreTools.Execute("workspace_save", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "adults"
            });
            var result = ParseResult(json);
            Assert.True(result.GetProperty("success").GetBoolean(), json);
            Assert.Equal(3, result.GetProperty("result").GetProperty("rows").GetInt32());
            Assert.True(_store.HasDataset("adults"));
        }

        [Fact]
        public void WorkspaceExportCsv()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_export_csv",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "Users"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            var content = result.GetProperty("result").GetProperty("content").GetString();
            Assert.Contains("Alice", content);
        }

        // ── 3.8 修改 ──

        [Fact]
        public void WorkspaceUpdate()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_update",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "Users",
                    ["filter"] = "name == Alice",
                    ["set"] = new Dictionary<string, object> { ["city"] = "Beijing" }
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(1, result.GetProperty("result").GetProperty("updatedRows").GetInt32());
        }

        [Fact]
        public void WorkspaceDeleteRows()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_delete_rows",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "Users",
                    ["filter"] = "age < 25"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            // Eve (22) removed
            Assert.Equal(1, result.GetProperty("result").GetProperty("deletedRows").GetInt32());
            Assert.Equal(4, result.GetProperty("result").GetProperty("remainingRows").GetInt32());
        }

        [Fact]
        public void WorkspaceAppend()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_append",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "Users",
                    ["rows"] = new List<Dictionary<string, object>>
                    {
                        new() { ["id"] = 6, ["name"] = "Frank", ["age"] = 40, ["city"] = "Shenzhen", ["score"] = 70.0 }
                    }
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(1, result.GetProperty("result").GetProperty("addedRows").GetInt32());
            Assert.Equal(6, result.GetProperty("result").GetProperty("totalRows").GetInt32());
        }

        [Fact]
        public void WorkspaceClear()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_clear",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "Users"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(5, result.GetProperty("result").GetProperty("clearedRows").GetInt32());
        }

        // ── 3.10 元操作 ──

        [Fact]
        public void WorkspaceClone()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_clone",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["newName"] = "UsersCopy"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(5, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void WorkspaceRenameDataset()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_rename_dataset",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["oldName"] = "Users",
                    ["newName"] = "People"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());

            // Verify it's renamed
            var describe = ParseResult(DataCoreTools.Execute("workspace_describe",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "People"
                }));
            Assert.True(describe.GetProperty("success").GetBoolean());
        }

        [Fact]
        public void WorkspaceRemove()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_remove",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["dataset"] = "Users"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(0, _store.Workspace.DatasetCount);
        }

        [Fact]
        public void WorkspaceSearch()
        {
            SeedUsers();
            SeedOrders();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_search",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["query"] = "User"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            var matches = result.GetProperty("result").GetProperty("matches");
            Assert.True(matches.GetArrayLength() >= 1);
        }

        [Fact]
        public void WorkspaceDiff()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });
            DataCoreTools.Execute("workspace_clone", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["source"] = "Users",
                ["newName"] = "UsersV2"
            });

            var result = ParseResult(DataCoreTools.Execute("workspace_diff",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["left"] = "Users",
                    ["right"] = "UsersV2"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
        }

        // ── 多 Workspace ──

        [Fact]
        public void MultiWorkspace_Isolation()
        {
            SeedUsers();

            // Open in default
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            // Create separate workspace
            DataCoreTools.Execute("workspace_create", new Dictionary<string, object> { ["name"] = "analysis" });

            // Open in analysis
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "analysis",
                ["dataset"] = "Users"
            });

            // Filter in analysis only
            DataCoreTools.Execute("workspace_filter", new Dictionary<string, object>
            {
                ["workspace"] = "analysis",
                ["source"] = "Users",
                ["filter"] = "age > 30",
                ["resultName"] = "seniors"
            });

            // Default should have 1 dataset (Users), analysis should have 2 (Users, seniors)
            Assert.Equal(1, _store.Workspace.DatasetCount);
            Assert.Equal(2, _store.GetWorkspace("analysis").DatasetCount);
        }

        // ── Filter Debug ──

        [Fact]
        public void Debug_FilterExpression()
        {
            // Quick smoke test for AND filter
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default", ["dataset"] = "Users"
            });

            var r2Json = DataCoreTools.Execute("workspace_filter", new Dictionary<string, object>
            {
                ["workspace"] = "default", ["source"] = "Users",
                ["filter"] = "age > 24 AND city == Shanghai", ["resultName"] = "f2"
            });
            var r2 = ParseResult(r2Json);
            Assert.True(r2.GetProperty("success").GetBoolean(), r2Json);
            Assert.Equal(2, r2.GetProperty("result").GetProperty("rows").GetInt32());
        }

        // ── Error handling ──

        [Fact]
        public void UnknownTool_ReturnsError()
        {
            var result = ParseResult(DataCoreTools.Execute("nonexistent_tool",
                new Dictionary<string, object>()));

            Assert.False(result.GetProperty("success").GetBoolean());
        }

        [Fact]
        public void MissingParameter_ReturnsError()
        {
            var result = ParseResult(DataCoreTools.Execute("workspace_filter",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default"
                    // Missing source, filter, resultName
                }));

            Assert.False(result.GetProperty("success").GetBoolean());
        }
    }
}
