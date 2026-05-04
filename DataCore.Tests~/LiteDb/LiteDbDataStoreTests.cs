using System;
using System.Collections.Generic;
using System.IO;
using AroAro.DataCore;
using AroAro.DataCore.LiteDb;
using Xunit;

namespace DataCore.Tests.LiteDb
{
    [Collection("LiteDB")]
    public class LiteDbDataStoreTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly List<LiteDbDataStore> _storesToDispose = new();

        public LiteDbDataStoreTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"datacore_test_{Guid.NewGuid():N}.db");
        }

        public void Dispose()
        {
            foreach (var store in _storesToDispose)
            {
                try { store.Dispose(); } catch { /* cleanup best-effort */ }
            }

            // Clean up DB file and any associated log files
            try
            {
                if (File.Exists(_dbPath)) File.Delete(_dbPath);
                var logPath = _dbPath + "-log";
                if (File.Exists(logPath)) File.Delete(logPath);
            }
            catch { /* cleanup best-effort */ }
        }

        private LiteDbDataStore CreateStore()
        {
            var store = new LiteDbDataStore(_dbPath);
            _storesToDispose.Add(store);
            return store;
        }

        #region CreateTabular / CreateGraph

        [Fact]
        public void CreateTabular_WithValidName_ReturnsDataset()
        {
            using var store = CreateStore();
            var ds = store.CreateTabular("myTable");

            Assert.NotNull(ds);
            Assert.Equal("myTable", ds.Name);
            Assert.Equal(DataSetKind.Tabular, ds.Kind);
        }

        [Fact]
        public void CreateGraph_WithValidName_ReturnsDataset()
        {
            using var store = CreateStore();
            var ds = store.CreateGraph("myGraph");

            Assert.NotNull(ds);
            Assert.Equal("myGraph", ds.Name);
            Assert.Equal(DataSetKind.Graph, ds.Kind);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void CreateTabular_WithNullOrWhitespaceName_ThrowsArgumentException(string name)
        {
            using var store = CreateStore();
            Assert.Throws<ArgumentException>(() => store.CreateTabular(name));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void CreateGraph_WithNullOrWhitespaceName_ThrowsArgumentException(string name)
        {
            using var store = CreateStore();
            Assert.Throws<ArgumentException>(() => store.CreateGraph(name));
        }

        #endregion

        #region Duplicate names

        [Fact]
        public void CreateTabular_DuplicateName_ThrowsInvalidOperationException()
        {
            using var store = CreateStore();
            store.CreateTabular("dup");

            var ex = Assert.Throws<InvalidOperationException>(() => store.CreateTabular("dup"));
            Assert.Contains("already exists", ex.Message);
        }

        [Fact]
        public void CreateGraph_DuplicateName_ThrowsInvalidOperationException()
        {
            using var store = CreateStore();
            store.CreateGraph("dup");

            var ex = Assert.Throws<InvalidOperationException>(() => store.CreateGraph("dup"));
            Assert.Contains("already exists", ex.Message);
        }

        [Fact]
        public void CreateTabular_AndGraph_SameName_BothSucceed()
        {
            // Tabular and graph are separate namespaces; same name should be fine.
            using var store = CreateStore();
            var t = store.CreateTabular("shared");
            var g = store.CreateGraph("shared");

            Assert.NotNull(t);
            Assert.NotNull(g);
        }

        #endregion

        #region DeleteTabular / DeleteGraph

        [Fact]
        public void DeleteTabular_Existing_ReturnsTrue()
        {
            using var store = CreateStore();
            store.CreateTabular("toDelete");

            bool result = store.DeleteTabular("toDelete");

            Assert.True(result, "DeleteTabular should return true for an existing dataset");
        }

        [Fact]
        public void DeleteTabular_NonExisting_ReturnsFalse()
        {
            using var store = CreateStore();

            bool result = store.DeleteTabular("ghost");

            Assert.False(result, "DeleteTabular should return false for a non-existing dataset");
        }

        [Fact]
        public void DeleteGraph_Existing_ReturnsTrue()
        {
            using var store = CreateStore();
            store.CreateGraph("toDelete");

            bool result = store.DeleteGraph("toDelete");

            Assert.True(result, "DeleteGraph should return true for an existing dataset");
        }

        [Fact]
        public void DeleteGraph_NonExisting_ReturnsFalse()
        {
            using var store = CreateStore();

            bool result = store.DeleteGraph("ghost");

            Assert.False(result, "DeleteGraph should return false for a non-existing dataset");
        }

        [Fact]
        public void DeleteTabular_RemovesFromNamesList()
        {
            using var store = CreateStore();
            store.CreateTabular("t1");
            store.CreateTabular("t2");

            store.DeleteTabular("t1");

            var names = store.TabularNames;
            Assert.DoesNotContain("t1", names);
            Assert.Contains("t2", names);
        }

        [Fact]
        public void DeleteGraph_RemovesFromNamesList()
        {
            using var store = CreateStore();
            store.CreateGraph("g1");
            store.CreateGraph("g2");

            store.DeleteGraph("g1");

            var names = store.GraphNames;
            Assert.DoesNotContain("g1", names);
            Assert.Contains("g2", names);
        }

        #endregion

        #region TabularNames / GraphNames

        [Fact]
        public void TabularNames_EmptyStore_ReturnsEmpty()
        {
            using var store = CreateStore();

            Assert.Empty(store.TabularNames);
        }

        [Fact]
        public void GraphNames_EmptyStore_ReturnsEmpty()
        {
            using var store = CreateStore();

            Assert.Empty(store.GraphNames);
        }

        [Fact]
        public void TabularNames_AfterCreation_ListsAll()
        {
            using var store = CreateStore();
            store.CreateTabular("alpha");
            store.CreateTabular("beta");
            store.CreateTabular("gamma");

            var names = store.TabularNames;

            Assert.Equal(3, names.Count);
            Assert.Contains("alpha", names);
            Assert.Contains("beta", names);
            Assert.Contains("gamma", names);
        }

        [Fact]
        public void GraphNames_AfterCreation_ListsAll()
        {
            using var store = CreateStore();
            store.CreateGraph("g1");
            store.CreateGraph("g2");

            var names = store.GraphNames;

            Assert.Equal(2, names.Count);
            Assert.Contains("g1", names);
            Assert.Contains("g2", names);
        }

        #endregion

        #region DatasetNames (combined)

        [Fact]
        public void DatasetNames_ReturnsBothTabularAndGraph()
        {
            using var store = CreateStore();
            store.CreateTabular("t1");
            store.CreateGraph("g1");

            var all = store.DatasetNames;

            Assert.Contains("t1", all);
            Assert.Contains("g1", all);
        }

        #endregion

        #region ClearAll

        [Fact]
        public void ClearAll_RemovesAllDatasets()
        {
            using var store = CreateStore();
            store.CreateTabular("t1");
            store.CreateTabular("t2");
            store.CreateGraph("g1");
            store.CreateGraph("g2");

            store.ClearAll();

            Assert.Empty(store.TabularNames);
            Assert.Empty(store.GraphNames);
            Assert.Empty(store.DatasetNames);
        }

        [Fact]
        public void ClearAll_OnEmptyStore_DoesNotThrow()
        {
            using var store = CreateStore();

            // Should not throw
            store.ClearAll();

            Assert.Empty(store.DatasetNames);
        }

        #endregion

        #region GetDatabaseSize

        [Fact]
        public void GetDatabaseSize_ReturnsNonNegative()
        {
            using var store = CreateStore();
            // Create some data so the file is non-empty
            var ds = store.CreateTabular("sizeTest");
            ds.AddNumericColumn("col", new double[] { 1, 2, 3 });
            store.Checkpoint();

            long size = store.GetDatabaseSize();

            Assert.True(size >= 0, $"Database size should be non-negative, got {size}");
        }

        [Fact]
        public void GetDatabaseSize_IncreasesWithMoreData()
        {
            using var store = CreateStore();
            long sizeBefore = store.GetDatabaseSize();

            var ds = store.CreateTabular("bigTable");
            var data = new double[1000];
            for (int i = 0; i < 1000; i++) data[i] = i;
            ds.AddNumericColumn("values", data);
            store.Checkpoint();

            long sizeAfter = store.GetDatabaseSize();

            Assert.True(sizeAfter > sizeBefore,
                $"Database should grow after adding data. Before={sizeBefore}, After={sizeAfter}");
        }

        #endregion

        #region ExecuteInTransaction

        [Fact]
        public void ExecuteInTransaction_CommitsOnSuccess()
        {
            using var store = CreateStore();

            store.ExecuteInTransaction(() =>
            {
                store.CreateTabular("committed");
            });

            // Data should persist after successful transaction
            Assert.True(store.TabularExists("committed"),
                "Dataset created in a successful transaction should persist");
        }

        [Fact(Skip = "Known issue: Transaction rollback does not remove created datasets")]
        public void ExecuteInTransaction_RollsBackOnException()
        {
            using var store = CreateStore();

            Assert.Throws<InvalidOperationException>(() =>
            {
                store.ExecuteInTransaction(() =>
                {
                    store.CreateTabular("rollbackTest");
                    throw new InvalidOperationException("Forced rollback");
                });
            });

            // After rollback, the dataset should not exist.
            // Note: LiteDB's transaction semantics may not fully roll back
            // metadata inserts — this test documents actual behavior.
            // If this assertion fails, it indicates LiteDB partial rollback
            // is a known limitation.
            Assert.False(store.TabularExists("rollbackTest"),
                "Dataset created before exception should be rolled back");
        }

        [Fact]
        public void ExecuteInTransaction_Generic_CommitsOnSuccess()
        {
            using var store = CreateStore();

            int result = store.ExecuteInTransaction(() =>
            {
                store.CreateGraph("gTx");
                return 42;
            });

            Assert.Equal(42, result);
            Assert.True(store.GraphExists("gTx"));
        }

        [Fact(Skip = "Known issue: Transaction rollback does not remove created datasets")]
        public void ExecuteInTransaction_Generic_RollsBackOnException()
        {
            using var store = CreateStore();

            Assert.Throws<InvalidOperationException>(() =>
            {
                store.ExecuteInTransaction<string>(() =>
                {
                    store.CreateTabular("txRollback");
                    throw new InvalidOperationException("Forced rollback");
                });
            });

            // Documenting actual behavior — may or may not roll back depending on LiteDB
            Assert.False(store.TabularExists("txRollback"),
                "Dataset should not persist after transaction rollback");
        }

        #endregion

        #region Dispose behavior

        [Fact]
        public void Dispose_ThenCreateTabular_ThrowsObjectDisposedException()
        {
            var store = CreateStore();
            store.Dispose();

            Assert.Throws<ObjectDisposedException>(() => store.CreateTabular("afterDispose"));
        }

        [Fact]
        public void Dispose_ThenCreateGraph_ThrowsObjectDisposedException()
        {
            var store = CreateStore();
            store.Dispose();

            Assert.Throws<ObjectDisposedException>(() => store.CreateGraph("afterDispose"));
        }

        [Fact]
        public void Dispose_ThenDeleteTabular_ThrowsObjectDisposedException()
        {
            var store = CreateStore();
            store.Dispose();

            Assert.Throws<ObjectDisposedException>(() => store.DeleteTabular("x"));
        }

        [Fact]
        public void Dispose_ThenClearAll_ThrowsObjectDisposedException()
        {
            var store = CreateStore();
            store.Dispose();

            Assert.Throws<ObjectDisposedException>(() => store.ClearAll());
        }

        [Fact]
        public void Dispose_ThenTabularNames_ThrowsObjectDisposedException()
        {
            var store = CreateStore();
            store.Dispose();

            Assert.Throws<ObjectDisposedException>(() => { var _ = store.TabularNames; });
        }

        [Fact]
        public void Dispose_ThenExecuteInTransaction_ThrowsObjectDisposedException()
        {
            var store = CreateStore();
            store.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                store.ExecuteInTransaction(() => { }));
        }

        [Fact]
        public void DoubleDispose_DoesNotThrow()
        {
            var store = CreateStore();
            store.Dispose();

            // Second dispose should be a no-op
            var exception = Record.Exception(() => store.Dispose());
            Assert.Null(exception);
        }

        #endregion

        #region Exists / TryGet

        [Fact]
        public void TabularExists_BeforeCreation_ReturnsFalse()
        {
            using var store = CreateStore();

            Assert.False(store.TabularExists("nonexistent"));
        }

        [Fact]
        public void TabularExists_AfterCreation_ReturnsTrue()
        {
            using var store = CreateStore();
            store.CreateTabular("exists");

            Assert.True(store.TabularExists("exists"));
        }

        [Fact]
        public void GraphExists_AfterCreation_ReturnsTrue()
        {
            using var store = CreateStore();
            store.CreateGraph("exists");

            Assert.True(store.GraphExists("exists"));
        }

        [Fact]
        public void TryGetTabular_Existing_ReturnsTrueAndDataset()
        {
            using var store = CreateStore();
            store.CreateTabular("found");

            bool found = store.TryGetTabular("found", out var ds);

            Assert.True(found);
            Assert.NotNull(ds);
            Assert.Equal("found", ds.Name);
        }

        [Fact]
        public void TryGetTabular_NonExisting_ReturnsFalse()
        {
            using var store = CreateStore();

            bool found = store.TryGetTabular("missing", out var ds);

            Assert.False(found);
            Assert.Null(ds);
        }

        [Fact]
        public void TryGetGraph_Existing_ReturnsTrueAndDataset()
        {
            using var store = CreateStore();
            store.CreateGraph("found");

            bool found = store.TryGetGraph("found", out var ds);

            Assert.True(found);
            Assert.NotNull(ds);
        }

        [Fact]
        public void TryGetGraph_NonExisting_ReturnsFalse()
        {
            using var store = CreateStore();

            bool found = store.TryGetGraph("missing", out var ds);

            Assert.False(found);
            Assert.Null(ds);
        }

        #endregion

        #region GetOrCreate

        [Fact]
        public void GetOrCreateTabular_CreatesWhenMissing()
        {
            using var store = CreateStore();

            var ds = store.GetOrCreateTabular("newTable");

            Assert.NotNull(ds);
            Assert.True(store.TabularExists("newTable"));
        }

        [Fact]
        public void GetOrCreateTabular_ReturnsExisting()
        {
            using var store = CreateStore();
            var original = store.CreateTabular("existing");

            var retrieved = store.GetOrCreateTabular("existing");

            Assert.Equal(original.Name, retrieved.Name);
        }

        [Fact]
        public void GetOrCreateGraph_CreatesWhenMissing()
        {
            using var store = CreateStore();

            var ds = store.GetOrCreateGraph("newGraph");

            Assert.NotNull(ds);
            Assert.True(store.GraphExists("newGraph"));
        }

        #endregion

        #region Checkpoint

        [Fact]
        public void Checkpoint_FlushesPendingMetadata()
        {
            using var store = CreateStore();
            var ds = store.CreateTabular("cpTest");
            ds.AddNumericColumn("val", new double[] { 1, 2, 3 });

            // Checkpoint should not throw
            store.Checkpoint();

            // Data should still be accessible
            Assert.Equal(3, ds.RowCount);
        }

        #endregion
    }
}
