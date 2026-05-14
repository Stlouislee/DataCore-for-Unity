using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AroAro.DataCore;
using AroAro.DataCore.Workspace;
using Xunit;

namespace DataCore.Tests.Workspace
{
    public class WorkspaceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;
        private int _counter;

        public WorkspaceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dc_ws_test_" + Guid.NewGuid().ToString("N"));
            _store = new DataCoreStore(Path.Combine(_tempDir, "test.db"));
        }

        public void Dispose()
        {
            _store?.Dispose();
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private string UniqueName(string prefix) => $"{prefix}_{++_counter}";

        [Fact]
        public void WorkspaceExistsOnStore()
        {
            Assert.NotNull(_store.Workspace);
            Assert.Equal(0, _store.Workspace.DatasetCount);
        }

        [Fact]
        public void RegisterAndGet()
        {
            var name = UniqueName("tbl");
            var tabular = _store.CreateTabular(name);
            tabular.AddStringColumn("name", new[] { "Alice", "Bob" });
            tabular.AddNumericColumn("age", new[] { 25.0, 30.0 });

            _store.Workspace.Register(name, tabular, DataSource.Store);

            Assert.Equal(1, _store.Workspace.DatasetCount);
            Assert.True(_store.Workspace.Has(name));

            var retrieved = _store.Workspace.Get(name);
            Assert.Equal(DataSetKind.Tabular, retrieved.Kind);
        }

        [Fact]
        public void RegisterFromDictionaries()
        {
            var name = UniqueName("cities");
            var data = new List<Dictionary<string, object>>
            {
                new() { ["city"] = "Beijing", ["pop"] = 21.5 },
                new() { ["city"] = "Shanghai", ["pop"] = 24.9 }
            };

            _store.Workspace.Register(name, data);

            Assert.True(_store.Workspace.Has(name));
            var ds = _store.Workspace.Get(name);
            Assert.Equal(DataSetKind.Tabular, ds.Kind);

            if (ds is ITabularDataset tabular)
            {
                Assert.Equal(2, tabular.RowCount);
            }
        }

        [Fact]
        public void RegisterAutoNaming()
        {
            var baseName = UniqueName("result");
            var data1 = new List<Dictionary<string, object>>
            {
                new() { ["x"] = 1 }
            };

            _store.Workspace.Register(baseName, data1);
            _store.Workspace.RegisterAuto(baseName, _store.CreateTabular(UniqueName("tmp")));

            Assert.True(_store.Workspace.Has(baseName));
            Assert.True(_store.Workspace.Has(baseName + "_2"));
        }

        [Fact]
        public void GetFallbackToStore()
        {
            var name = UniqueName("storeonly");
            // Create in store only, not in workspace
            var tabular = _store.CreateTabular(name);
            tabular.AddStringColumn("col", new[] { "val" });

            // Get should fallback to store and auto-load
            var ds = _store.Workspace.Get(name);
            Assert.Equal(DataSetKind.Tabular, ds.Kind);

            // Now it should be in workspace
            Assert.Equal(1, _store.Workspace.DatasetCount);
        }

        [Fact]
        public void HasChecksWorkspaceOnly()
        {
            var wsName = UniqueName("ws");
            var storeName = UniqueName("store");

            _store.CreateTabular(storeName);
            _store.Workspace.Register(wsName, _store.CreateTabular(UniqueName("inner")));

            Assert.True(_store.Workspace.Has(wsName));
            Assert.False(_store.Workspace.Has(storeName)); // Has = workspace only

            // TryPeek checks both layers
            Assert.True(_store.Workspace.TryPeek(storeName, out _));
        }

        [Fact]
        public void TryPeekReturnsMetadata()
        {
            var name = UniqueName("peek");
            var tabular = _store.CreateTabular(name);
            tabular.AddStringColumn("name", new[] { "A", "B", "C", "D" });
            tabular.AddNumericColumn("val", new[] { 1.0, 2.0, 3.0, 4.0 });

            _store.Workspace.Register(name, tabular, DataSource.Derived);

            bool found = _store.Workspace.TryPeek(name, out var entry);
            Assert.True(found);
            Assert.Equal(name, entry.Name);
            Assert.Equal(DataSource.Derived, entry.Source);
            Assert.Equal(4, entry.Rows);
            Assert.Equal(2, entry.Columns);
            Assert.Equal(2, entry.Schema.Count);
            Assert.Equal(3, entry.Sample.Count); // max 3 rows

            bool notFound = _store.Workspace.TryPeek("nonexistent_" + Guid.NewGuid(), out var noEntry);
            Assert.False(notFound);
            Assert.Null(noEntry);
        }

        [Fact]
        public void DescribeReturnsEntry()
        {
            var name = UniqueName("desc");
            var tabular = _store.CreateTabular(name);
            tabular.AddStringColumn("name", new[] { "X" });
            _store.Workspace.Register(name, tabular);

            var entry = _store.Workspace.Describe(name);
            Assert.Equal(name, entry.Name);
            Assert.Equal(DataSetKind.Tabular, entry.Kind);
        }

        [Fact]
        public void DescribeAllIncludesBothLayers()
        {
            var storeName = UniqueName("store");
            var wsName = UniqueName("ws");

            _store.CreateTabular(storeName);
            _store.Workspace.Register(wsName, _store.CreateTabular(UniqueName("inner")));

            var all = _store.Workspace.DescribeAll();
            Assert.True(all.Count >= 2);

            var names = all.Select(e => e.Name).ToList();
            Assert.Contains(storeName, names);
            Assert.Contains(wsName, names);
        }

        [Fact]
        public void DescribeAllCaching()
        {
            var name1 = UniqueName("cache");
            _store.Workspace.Register(name1, _store.CreateTabular(UniqueName("inner1")));

            var first = _store.Workspace.DescribeAll();
            var second = _store.Workspace.DescribeAll();

            // Should be same cached instance
            Assert.Same(first, second);

            // Register should invalidate cache
            var name2 = UniqueName("cache2");
            _store.Workspace.Register(name2, _store.CreateTabular(UniqueName("inner2")));
            var third = _store.Workspace.DescribeAll();

            Assert.True(third.Count >= 2); // Cache invalidated
        }

        [Fact]
        public void SummaryShowsCorrectCounts()
        {
            // Fresh isolated store
            var tmpDir = Path.Combine(Path.GetTempPath(), "dc_ws_summary_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);
            try
            {
                using var store = new DataCoreStore(Path.Combine(tmpDir, "summary.db"));

                Assert.Equal("Workspace: empty", store.Workspace.Summary());

                // Create a store-only dataset
                var storeName = "StoreTbl";
                store.CreateTabular(storeName);

                // Create a derived dataset (not in store)
                var derivedData = new List<Dictionary<string, object>>
                {
                    new() { ["x"] = 1 }
                };
                store.Workspace.Register("DerivedTbl", derivedData, DataSource.Derived);

                var summary = store.Workspace.Summary();
                Assert.True(summary.Contains("2 datasets"), $"Expected '2 datasets', got: '{summary}'");
                Assert.Contains("1 store", summary);
                Assert.Contains("1 derived", summary);
            }
            finally
            {
                try { Directory.Delete(tmpDir, true); } catch { }
            }
        }

        [Fact]
        public void RemoveDataset()
        {
            var name = UniqueName("remove");
            _store.Workspace.Register(name, _store.CreateTabular(UniqueName("inner")));
            Assert.True(_store.Workspace.Has(name));

            bool removed = _store.Workspace.Remove(name);
            Assert.True(removed);
            Assert.False(_store.Workspace.Has(name));
            Assert.Equal(0, _store.Workspace.DatasetCount);

            // Removing non-existent should return false
            Assert.False(_store.Workspace.Remove("nonexistent_" + Guid.NewGuid()));
        }

        [Fact]
        public void RenameDataset()
        {
            var oldName = UniqueName("old");
            var newName = UniqueName("new");

            var tabular = _store.CreateTabular(oldName);
            tabular.AddStringColumn("col", new[] { "val" });
            _store.Workspace.Register(oldName, tabular);

            bool renamed = _store.Workspace.Rename(oldName, newName);
            Assert.True(renamed);
            Assert.False(_store.Workspace.Has(oldName));
            Assert.True(_store.Workspace.Has(newName));
        }

        [Fact]
        public void CloneDataset()
        {
            var origName = UniqueName("orig");
            var copyName = UniqueName("copy");

            var tabular = _store.CreateTabular(origName);
            tabular.AddStringColumn("name", new[] { "A", "B" });
            _store.Workspace.Register(origName, tabular);

            var cloned = _store.Workspace.Clone(origName, copyName);
            Assert.Equal(DataSetKind.Tabular, cloned.Kind);
            Assert.True(_store.Workspace.Has(origName));
            Assert.True(_store.Workspace.Has(copyName));
            Assert.Equal(2, _store.Workspace.DatasetCount);
        }

        [Fact]
        public void ClearWorkspace()
        {
            var name1 = UniqueName("a");
            var name2 = UniqueName("b");

            _store.Workspace.Register(name1, _store.CreateTabular(UniqueName("inner1")));
            _store.Workspace.Register(name2, _store.CreateTabular(UniqueName("inner2")));
            Assert.Equal(2, _store.Workspace.DatasetCount);

            _store.Workspace.Clear();
            Assert.Equal(0, _store.Workspace.DatasetCount);
        }

        [Fact]
        public void AllNamesIncludesBothLayers()
        {
            var storeName = UniqueName("store");
            var wsName = UniqueName("ws");

            _store.CreateTabular(storeName);
            _store.Workspace.Register(wsName, _store.CreateTabular(UniqueName("inner")));

            var all = _store.Workspace.AllNames;
            Assert.Contains(storeName, all);
            Assert.Contains(wsName, all);
        }

        [Fact]
        public void SourceTracking()
        {
            var storeName = UniqueName("src_store");
            var derivedName = UniqueName("src_derived");
            var importedName = UniqueName("src_imported");

            var storeTbl = _store.CreateTabular(storeName);
            _store.Workspace.Register(storeName, storeTbl, DataSource.Store);
            _store.Workspace.Register(derivedName, _store.CreateTabular(UniqueName("inner1")), DataSource.Derived);
            _store.Workspace.Register(importedName, _store.CreateTabular(UniqueName("inner2")), DataSource.Imported);

            var all = _store.Workspace.DescribeAll();
            Assert.Equal(DataSource.Store, all.First(e => e.Name == storeName).Source);
            Assert.Equal(DataSource.Derived, all.First(e => e.Name == derivedName).Source);
            Assert.Equal(DataSource.Imported, all.First(e => e.Name == importedName).Source);
        }

        [Fact]
        public void DisposeWorkspaceThrows()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), "dc_ws_disp_" + Guid.NewGuid().ToString("N"));
            var store = new DataCoreStore(Path.Combine(tmpDir, "disp.db"));
            var workspace = store.Workspace;

            workspace.Register(UniqueName("test"), store.CreateTabular(UniqueName("inner")));
            Assert.Equal(1, workspace.DatasetCount); // verify it works before dispose

            workspace.Dispose();

            // After dispose, all operations should throw
            var ex = Assert.Throws<ObjectDisposedException>(() => workspace.DatasetCount);
            Assert.Contains("Workspace", ex.ObjectName);

            store.Dispose();
            try { Directory.Delete(tmpDir, true); } catch { }
        }

        [Fact]
        public void BackwardCompatibility()
        {
#pragma warning disable CS0618 // Obsolete
            var sessionManager = _store.SessionManager;
            Assert.NotNull(sessionManager);

            var session = sessionManager.CreateSession("CompatTest");
            Assert.Equal("CompatTest", session.Name);

            sessionManager.CloseAllSessions();
#pragma warning restore CS0618
        }

        [Fact]
        public void RegisterDuplicateNameThrows()
        {
            var name = UniqueName("dup");
            _store.Workspace.Register(name, _store.CreateTabular(UniqueName("inner1")));
            Assert.Throws<InvalidOperationException>(() =>
                _store.Workspace.Register(name, _store.CreateTabular(UniqueName("inner2"))));
        }

        [Fact]
        public void GetNonExistentThrows()
        {
            Assert.Throws<KeyNotFoundException>(() =>
                _store.Workspace.Get("nonexistent_" + Guid.NewGuid()));
        }

        [Fact]
        public void DescribeNonExistentThrows()
        {
            Assert.Throws<KeyNotFoundException>(() =>
                _store.Workspace.Describe("nonexistent_" + Guid.NewGuid()));
        }

        // ══════════════════════════════════════════════════════════════
        //  数据内容验证
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void RegisterFromDictionaries_PreservesDataContent()
        {
            var name = UniqueName("data");
            var data = new List<Dictionary<string, object>>
            {
                new() { ["city"] = "Beijing", ["pop"] = 21.5 },
                new() { ["city"] = "Shanghai", ["pop"] = 24.9 },
                new() { ["city"] = "Shenzhen", ["pop"] = 17.6 }
            };

            _store.Workspace.Register(name, data);

            var ds = _store.Workspace.Get(name);
            var tabular = Assert.IsAssignableFrom<ITabularDataset>(ds);

            Assert.Equal(3, tabular.RowCount);
            Assert.True(tabular.ColumnCount >= 2);
            Assert.True(tabular.HasColumn("city"));
            Assert.True(tabular.HasColumn("pop"));

            // Verify actual values
            var row0 = tabular.GetRow(0);
            Assert.Equal("Beijing", row0["city"]);

            var row1 = tabular.GetRow(1);
            Assert.Equal("Shanghai", row1["city"]);
        }

        [Fact]
        public void RegisterFromDictionaries_EmptyDataCreatesEmptyDataset()
        {
            var name = UniqueName("empty");
            var data = new List<Dictionary<string, object>>();

            _store.Workspace.Register(name, data);

            var ds = _store.Workspace.Get(name);
            var tabular = Assert.IsAssignableFrom<ITabularDataset>(ds);
            Assert.Equal(0, tabular.RowCount);
        }

        [Fact]
        public void DescribeAll_SampleRowsContainCorrectData()
        {
            var name = UniqueName("sample");
            var data = new List<Dictionary<string, object>>
            {
                new() { ["x"] = 1.0, ["label"] = "a" },
                new() { ["x"] = 2.0, ["label"] = "b" },
                new() { ["x"] = 3.0, ["label"] = "c" },
                new() { ["x"] = 4.0, ["label"] = "d" }
            };

            _store.Workspace.Register(name, data);

            var entry = _store.Workspace.Describe(name);
            Assert.Equal(4, entry.Rows);
            Assert.Equal(3, entry.Sample.Count); // capped at 3

            // Sample should be the first 3 rows
            Assert.Equal("a", entry.Sample[0]["label"]);
            Assert.Equal("b", entry.Sample[1]["label"]);
            Assert.Equal("c", entry.Sample[2]["label"]);
        }

        [Fact]
        public void DescribeAll_SchemaContainsCorrectTypes()
        {
            var name = UniqueName("schema");
            var tabular = _store.CreateTabular(name);
            tabular.AddStringColumn("name", new[] { "A" });
            tabular.AddNumericColumn("score", new[] { 95.5 });
            _store.Workspace.Register(name, tabular);

            var entry = _store.Workspace.Describe(name);
            Assert.Equal(2, entry.Schema.Count);

            var nameCol = entry.Schema.First(c => c.Name == "name");
            Assert.Equal(ColumnType.String, nameCol.Type);

            var scoreCol = entry.Schema.First(c => c.Name == "score");
            Assert.Equal(ColumnType.Numeric, scoreCol.Type);
        }

        // ══════════════════════════════════════════════════════════════
        //  Clone 独立性
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Clone_DataIsIndependent()
        {
            var origName = UniqueName("orig");
            var copyName = UniqueName("copy");

            var tabular = _store.CreateTabular(origName);
            tabular.AddNumericColumn("val", new[] { 1.0, 2.0 });
            _store.Workspace.Register(origName, tabular);

            var cloned = _store.Workspace.Clone(origName, copyName);
            var origTabular = Assert.IsAssignableFrom<ITabularDataset>(_store.Workspace.Get(origName));
            var copyTabular = Assert.IsAssignableFrom<ITabularDataset>(cloned);

            // Same row count
            Assert.Equal(origTabular.RowCount, copyTabular.RowCount);

            // Same column names
            Assert.Equal(origTabular.ColumnNames.OrderBy(n => n), copyTabular.ColumnNames.OrderBy(n => n));

            // Modify original, copy should be unaffected
            origTabular.AddRow(new Dictionary<string, object> { ["val"] = 3.0 });
            Assert.Equal(3, origTabular.RowCount);
            Assert.Equal(2, copyTabular.RowCount); // unchanged
        }

        // ══════════════════════════════════════════════════════════════
        //  Rename 后行为
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Rename_OldNameWorkspaceSlotRemoved()
        {
            var oldName = UniqueName("old");
            var newName = UniqueName("new");

            var data = new List<Dictionary<string, object>> { new() { ["x"] = 1 } };
            _store.Workspace.Register(oldName, data);

            _store.Workspace.Rename(oldName, newName);

            // Old name should not be in workspace slots
            Assert.False(_store.Workspace.Has(oldName));

            // But it may still exist in store (LiteDB entry persists)
            // Get falls back to store — that's expected
            // New name should be in workspace
            Assert.True(_store.Workspace.Has(newName));
            var ds = _store.Workspace.Get(newName);
            Assert.NotNull(ds);
        }

        [Fact]
        public void Rename_ToSameNameIsNoOp()
        {
            var name = UniqueName("same");
            _store.Workspace.Register(name, _store.CreateTabular(UniqueName("inner")));

            bool result = _store.Workspace.Rename(name, name);
            Assert.True(result);
            Assert.True(_store.Workspace.Has(name));
        }

        [Fact]
        public void Rename_NonExistentReturnsFalse()
        {
            Assert.False(_store.Workspace.Rename("nope_" + Guid.NewGuid(), "new_" + Guid.NewGuid()));
        }

        // ══════════════════════════════════════════════════════════════
        //  Clear 后行为
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Clear_DescribeAllReturnsEmpty()
        {
            _store.Workspace.Register(UniqueName("a"), _store.CreateTabular(UniqueName("i1")));
            _store.Workspace.Register(UniqueName("b"), _store.CreateTabular(UniqueName("i2")));

            _store.Workspace.Clear();

            // DescribeAll should only show store datasets, no workspace entries
            var wsEntries = _store.Workspace.DescribeAll().Where(e => e.Source != DataSource.Store).ToList();
            Assert.Empty(wsEntries);
        }

        [Fact]
        public void Clear_TwiceIsHarmless()
        {
            _store.Workspace.Register(UniqueName("a"), _store.CreateTabular(UniqueName("i")));
            _store.Workspace.Clear();
            _store.Workspace.Clear(); // should not throw
            Assert.Equal(0, _store.Workspace.DatasetCount);
        }

        [Fact]
        public void Clear_DoesNotAffectStore()
        {
            var storeName = UniqueName("persist");
            _store.CreateTabular(storeName);
            _store.Workspace.Register(UniqueName("ws"), _store.CreateTabular(UniqueName("i")));

            _store.Workspace.Clear();

            // Store dataset should survive
            Assert.True(_store.HasDataset(storeName));
        }

        // ══════════════════════════════════════════════════════════════
        //  Get 从 store 加载后数据可读
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void GetFromStore_DataIsReadable()
        {
            var name = UniqueName("readable");
            var tabular = _store.CreateTabular(name);
            tabular.AddStringColumn("fruit", new[] { "apple", "banana" });
            tabular.AddNumericColumn("price", new[] { 3.5, 7.0 });

            // Get via workspace (fallback to store)
            var ds = _store.Workspace.Get(name);
            var t = Assert.IsAssignableFrom<ITabularDataset>(ds);

            Assert.Equal(2, t.RowCount);
            Assert.Equal("apple", t.GetRow(0)["fruit"]);
            Assert.Equal(7.0, t.GetRow(1)["price"]);
        }

        // ══════════════════════════════════════════════════════════════
        //  TryPeek 不污染 workspace
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void TryPeek_DoesNotLoadIntoWorkspace()
        {
            var name = UniqueName("peekonly");
            _store.CreateTabular(name);

            Assert.Equal(0, _store.Workspace.DatasetCount);

            // TryPeek should NOT add to workspace
            _store.Workspace.TryPeek(name, out _);
            Assert.Equal(0, _store.Workspace.DatasetCount);

            // But Get should
            _store.Workspace.Get(name);
            Assert.Equal(1, _store.Workspace.DatasetCount);
        }

        // ══════════════════════════════════════════════════════════════
        //  Graph 数据集
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void RegisterAndDescribeGraphDataset()
        {
            var name = UniqueName("graph");
            var graph = _store.CreateGraph(name);
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddEdge("A", "B");
            _store.Workspace.Register(name, graph, DataSource.Store);

            var entry = _store.Workspace.Describe(name);
            Assert.Equal(DataSetKind.Graph, entry.Kind);
            Assert.Equal(DataSource.Store, entry.Source);
        }

        // ══════════════════════════════════════════════════════════════
        //  混合操作压力测试
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void MixedOperations_MaintainConsistency()
        {
            // Register several
            for (int i = 0; i < 5; i++)
            {
                _store.Workspace.Register(UniqueName("item"), new List<Dictionary<string, object>>
                {
                    new() { ["i"] = (double)i }
                });
            }
            Assert.Equal(5, _store.Workspace.DatasetCount);

            // Remove some
            var names = _store.Workspace.DatasetNames.ToList();
            _store.Workspace.Remove(names[0]);
            _store.Workspace.Remove(names[2]);
            Assert.Equal(3, _store.Workspace.DatasetCount);

            // Rename one
            var toRename = _store.Workspace.DatasetNames.First();
            var newName = UniqueName("renamed");
            _store.Workspace.Rename(toRename, newName);
            Assert.True(_store.Workspace.Has(newName));
            Assert.False(_store.Workspace.Has(toRename));

            // Clone one
            var toClone = _store.Workspace.DatasetNames.First();
            _store.Workspace.Clone(toClone, UniqueName("clone"));
            Assert.Equal(4, _store.Workspace.DatasetCount);

            // DescribeAll should reflect all changes
            var all = _store.Workspace.DescribeAll();
            var wsEntries = all.Where(e => e.Source != DataSource.Store).ToList();
            Assert.Equal(4, wsEntries.Count);

            // Clear
            _store.Workspace.Clear();
            Assert.Equal(0, _store.Workspace.DatasetCount);
        }

        // ══════════════════════════════════════════════════════════════
        //  边界情况
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Register_NullDatasetThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _store.Workspace.Register("x", (IDataSet)null));
        }

        [Fact]
        public void Register_EmptyNameThrows()
        {
            Assert.Throws<ArgumentException>(() =>
                _store.Workspace.Register("", _store.CreateTabular(UniqueName("i"))));
        }

        [Fact]
        public void Register_NullNameThrows()
        {
            Assert.Throws<ArgumentException>(() =>
                _store.Workspace.Register(null, _store.CreateTabular(UniqueName("i"))));
        }

        [Fact]
        public void Get_EmptyNameThrows()
        {
            Assert.Throws<ArgumentException>(() => _store.Workspace.Get(""));
        }

        [Fact]
        public void Rename_NullOldNameThrows()
        {
            Assert.Throws<ArgumentException>(() => _store.Workspace.Rename(null, "x"));
        }

        [Fact]
        public void Rename_NullNewNameThrows()
        {
            var name = UniqueName("r");
            _store.Workspace.Register(name, _store.CreateTabular(UniqueName("i")));
            Assert.Throws<ArgumentException>(() => _store.Workspace.Rename(name, null));
        }

        [Fact]
        public void Clone_NullSourceNameThrows()
        {
            Assert.Throws<ArgumentException>(() => _store.Workspace.Clone(null, "x"));
        }

        [Fact]
        public void Clone_NullNewNameThrows()
        {
            var name = UniqueName("c");
            _store.Workspace.Register(name, _store.CreateTabular(UniqueName("i")));
            Assert.Throws<ArgumentException>(() => _store.Workspace.Clone(name, null));
        }

        // ══════════════════════════════════════════════════════════════
        //  AllNames 去重
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void AllNames_NoDuplicatesWhenNameInBothLayers()
        {
            var name = UniqueName("both");
            var tabular = _store.CreateTabular(name);
            _store.Workspace.Register(name, tabular, DataSource.Store);

            var all = _store.Workspace.AllNames;
            Assert.Equal(1, all.Count(n => n == name));
        }

        // ══════════════════════════════════════════════════════════════
        //  AutoName 连续冲突
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void RegisterAutoNaming_MultipleCollisions()
        {
            var baseName = UniqueName("multi");
            _store.Workspace.Register(baseName, new List<Dictionary<string, object>> { new() { ["x"] = 1.0 } });
            _store.Workspace.RegisterAuto(baseName, _store.CreateTabular(UniqueName("t1")));
            _store.Workspace.RegisterAuto(baseName, _store.CreateTabular(UniqueName("t2")));

            Assert.True(_store.Workspace.Has(baseName));
            Assert.True(_store.Workspace.Has(baseName + "_2"));
            Assert.True(_store.Workspace.Has(baseName + "_3"));
        }

        // ══════════════════════════════════════════════════════════════
        //  Weak retention policy
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void WeakRetentionPolicy_RegisterAndGet()
        {
            var name = UniqueName("weak");
            var tabular = _store.CreateTabular(name);
            tabular.AddStringColumn("col", new[] { "val" });

            _store.Workspace.Register(name, tabular, DataSource.Derived, WorkspaceRetentionPolicy.Weak);

            // Should still be accessible immediately
            var ds = _store.Workspace.Get(name);
            Assert.NotNull(ds);
            Assert.Equal(DataSetKind.Tabular, ds.Kind);
        }

        [Fact]
        public void StrongRetentionPolicy_RegisterAndGet()
        {
            var name = UniqueName("strong");
            var tabular = _store.CreateTabular(name);
            tabular.AddStringColumn("col", new[] { "val" });

            _store.Workspace.Register(name, tabular, DataSource.Derived, WorkspaceRetentionPolicy.Strong);

            var ds = _store.Workspace.Get(name);
            Assert.NotNull(ds);
        }

        // ══════════════════════════════════════════════════════════════
        //  Remove 后 DescribeAll 更新
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Remove_InvalidatesDescribeAllCache()
        {
            var name = UniqueName("cached");
            _store.Workspace.Register(name, _store.CreateTabular(UniqueName("i")));

            var before = _store.Workspace.DescribeAll();
            Assert.Single(before.Where(e => e.Name == name));

            _store.Workspace.Remove(name);

            var after = _store.Workspace.DescribeAll();
            Assert.Empty(after.Where(e => e.Name == name));
        }

        // ══════════════════════════════════════════════════════════════
        //  Summary 各种状态
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Summary_OnlyDerived()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), "dc_ws_sum2_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);
            try
            {
                using var store = new DataCoreStore(Path.Combine(tmpDir, "s.db"));
                store.Workspace.Register("d1", new List<Dictionary<string, object>> { new() { ["x"] = 1.0 } });
                store.Workspace.Register("d2", new List<Dictionary<string, object>> { new() { ["x"] = 2.0 } });

                var summary = store.Workspace.Summary();
                Assert.Contains("2 datasets", summary);
                Assert.Contains("2 derived", summary);
                Assert.DoesNotContain("store", summary);
            }
            finally { try { Directory.Delete(tmpDir, true); } catch { } }
        }

        [Fact]
        public void Summary_WithImported()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), "dc_ws_sum3_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);
            try
            {
                using var store = new DataCoreStore(Path.Combine(tmpDir, "s.db"));
                store.Workspace.Register("imp", new List<Dictionary<string, object>> { new() { ["x"] = 1.0 } }, DataSource.Imported);

                var summary = store.Workspace.Summary();
                Assert.Contains("1 imported", summary);
            }
            finally { try { Directory.Delete(tmpDir, true); } catch { } }
        }
    }
}
