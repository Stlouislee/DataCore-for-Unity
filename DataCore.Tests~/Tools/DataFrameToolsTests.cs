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
    public class DataFrameToolsTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;

        public DataFrameToolsTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dc_df_" + Guid.NewGuid().ToString("N"));
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
            tabular.AddNumericColumn("id", new double[] { 1, 2, 3 });
            tabular.AddStringColumn("name", new[] { "Alice", "Bob", "Charlie" });
            tabular.AddNumericColumn("age", new double[] { 25, 30, 35 });
        }

        private JsonElement Parse(string json) => JsonSerializer.Deserialize<JsonElement>(json);

        [Fact]
        public void DataFrameCreate_AndList()
        {
            var create = Parse(DataCoreTools.Execute("workspace_dataframe_create",
                new Dictionary<string, object> { ["name"] = "mydf" }));
            Assert.True(create.GetProperty("success").GetBoolean());

            var list = Parse(DataCoreTools.Execute("workspace_dataframe_list",
                new Dictionary<string, object>()));
            Assert.True(list.GetProperty("success").GetBoolean());
            Assert.Equal(1, list.GetProperty("result").GetProperty("count").GetInt32());
            Assert.Equal("mydf", list.GetProperty("result").GetProperty("dataFrames")[0].GetString());
        }

        [Fact]
        public void DataFrameConvert_FromWorkspaceDataset()
        {
            SeedUsers();
            // Load into workspace
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            var result = Parse(DataCoreTools.Execute("workspace_dataframe_convert",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(3, result.GetProperty("result").GetProperty("rows").GetInt32());
            Assert.Equal(3, result.GetProperty("result").GetProperty("columns").GetInt32());
        }

        [Fact]
        public void DataFrameRemove()
        {
            DataCoreTools.Execute("workspace_dataframe_create",
                new Dictionary<string, object> { ["name"] = "temp" });

            var result = Parse(DataCoreTools.Execute("workspace_dataframe_remove",
                new Dictionary<string, object> { ["name"] = "temp" }));
            Assert.True(result.GetProperty("success").GetBoolean());

            var list = Parse(DataCoreTools.Execute("workspace_dataframe_list",
                new Dictionary<string, object>()));
            Assert.Equal(0, list.GetProperty("result").GetProperty("count").GetInt32());
        }

        [Fact]
        public void DataFrameRemove_NotFound_ReturnsError()
        {
            var result = Parse(DataCoreTools.Execute("workspace_dataframe_remove",
                new Dictionary<string, object> { ["name"] = "nonexistent" }));
            Assert.False(result.GetProperty("success").GetBoolean());
        }

        [Fact]
        public void DataFrameToDataset_RoundTrip()
        {
            SeedUsers();
            DataCoreTools.Execute("workspace_open", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["dataset"] = "Users"
            });

            // Convert to DataFrame
            DataCoreTools.Execute("workspace_dataframe_convert", new Dictionary<string, object>
            {
                ["workspace"] = "default",
                ["source"] = "Users"
            });

            // Convert back to dataset
            var result = Parse(DataCoreTools.Execute("workspace_dataframe_to_dataset",
                new Dictionary<string, object>
                {
                    ["workspace"] = "default",
                    ["source"] = "Users",
                    ["resultName"] = "UsersFromDf"
                }));

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(3, result.GetProperty("result").GetProperty("rows").GetInt32());
        }

        [Fact]
        public void DataFrameCreate_Duplicate_ReturnsError()
        {
            DataCoreTools.Execute("workspace_dataframe_create",
                new Dictionary<string, object> { ["name"] = "dup" });

            var result = Parse(DataCoreTools.Execute("workspace_dataframe_create",
                new Dictionary<string, object> { ["name"] = "dup" }));
            Assert.False(result.GetProperty("success").GetBoolean());
        }
    }
}
