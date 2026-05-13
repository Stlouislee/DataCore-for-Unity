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
    public class ToolSchemaTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;

        public ToolSchemaTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dc_schema_" + Guid.NewGuid().ToString("N"));
            _store = new DataCoreStore(Path.Combine(_tempDir, "test.db"));
            DataCoreTools.Initialize(_store);
        }

        public void Dispose()
        {
            _store?.Dispose();
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Fact]
        public void GetToolSchemas_ReturnsValidJson()
        {
            var json = DataCoreTools.GetToolSchemas();
            var docs = JsonSerializer.Deserialize<JsonElement[]>(json);
            Assert.NotNull(docs);
            Assert.True(docs.Length > 40, $"Expected >40 schemas, got {docs.Length}");
        }

        [Fact]
        public void GetToolSchemas_EachHasRequiredFields()
        {
            var json = DataCoreTools.GetToolSchemas();
            var docs = JsonSerializer.Deserialize<JsonElement[]>(json);

            foreach (var schema in docs)
            {
                Assert.True(schema.TryGetProperty("name", out _), "Missing 'name'");
                Assert.True(schema.TryGetProperty("description", out _), "Missing 'description'");
                Assert.True(schema.TryGetProperty("parameters", out var p), "Missing 'parameters'");
                Assert.True(p.TryGetProperty("type", out _), "Missing parameters.type");
                Assert.True(p.TryGetProperty("properties", out _), "Missing parameters.properties");
            }
        }

        [Fact]
        public void GetToolNames_ReturnsAllTools()
        {
            var names = DataCoreTools.GetToolNames();
            Assert.True(names.Count > 40);
            Assert.Contains("workspace_filter", names);
            Assert.Contains("workspace_open_graph", names);
            Assert.Contains("workspace_dataframe_create", names);
            Assert.Contains("workspace_describe_graph", names);
        }

        [Fact]
        public void GetToolSchemas_MatchesGetToolNames()
        {
            var schemaJson = DataCoreTools.GetToolSchemas();
            var schemas = JsonSerializer.Deserialize<JsonElement[]>(schemaJson);
            var schemaNames = schemas.Select(s => s.GetProperty("name").GetString()).OrderBy(x => x).ToList();

            var toolNames = DataCoreTools.GetToolNames().OrderBy(x => x).ToList();

            Assert.Equal(toolNames, schemaNames);
        }

        [Fact]
        public void GetToolSchemas_FilterHasCorrectParameters()
        {
            var json = DataCoreTools.GetToolSchemas();
            var docs = JsonSerializer.Deserialize<JsonElement[]>(json);
            var filter = docs.First(d => d.GetProperty("name").GetString() == "workspace_filter");

            var parameters = filter.GetProperty("parameters");
            var properties = parameters.GetProperty("properties");

            Assert.True(properties.TryGetProperty("source", out _));
            Assert.True(properties.TryGetProperty("filter", out _));

            var required = parameters.GetProperty("required");
            var requiredList = required.EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.Contains("source", requiredList);
            Assert.Contains("filter", requiredList);
        }
    }
}
