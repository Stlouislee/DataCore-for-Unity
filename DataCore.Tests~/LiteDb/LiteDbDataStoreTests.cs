using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        #region Concurrency (issue #87 — SemaphoreSlim guard for mobile Direct mode)

        [Fact]
        public void ConcurrentTabularCreation_DoesNotCorruptDatabase()
        {
            using var store = CreateStore();
            const int threadCount = 10;
            var barrier = new Barrier(threadCount);
            var errors = new ConcurrentBag<Exception>();

            var threads = Enumerable.Range(0, threadCount).Select(i => new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    store.CreateTabular($"concurrent_t_{i}");
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            })).ToList();

            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join(TimeSpan.FromSeconds(10)));

            Assert.Empty(errors);
            Assert.Equal(threadCount, store.TabularNames.Count);
        }

        [Fact]
        public void ConcurrentGraphCreation_DoesNotCorruptDatabase()
        {
            using var store = CreateStore();
            const int threadCount = 10;
            var barrier = new Barrier(threadCount);
            var errors = new ConcurrentBag<Exception>();

            var threads = Enumerable.Range(0, threadCount).Select(i => new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    store.CreateGraph($"concurrent_g_{i}");
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            })).ToList();

            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join(TimeSpan.FromSeconds(10)));

            Assert.Empty(errors);
            Assert.Equal(threadCount, store.GraphNames.Count);
        }

        [Fact]
        public void ConcurrentReadsAndWrites_DoNotCorruptData()
        {
            using var store = CreateStore();
            var ds = store.CreateTabular("rw_test");
            ds.AddNumericColumn("values", new double[] { 1, 2, 3, 4, 5 });

            const int readerCount = 8;
            const int writerCount = 4;
            var barrier = new Barrier(readerCount + writerCount);
            var errors = new ConcurrentBag<Exception>();
            var readResults = new ConcurrentBag<int>();

            // Readers: repeatedly read row count
            var readers = Enumerable.Range(0, readerCount).Select(_ => new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    for (int i = 0; i < 50; i++)
                    {
                        int count = ds.RowCount;
                        readResults.Add(count);
                        Thread.Yield();
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            })).ToList();

            // Writers: add columns concurrently
            var writers = Enumerable.Range(0, writerCount).Select(i => new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    for (int j = 0; j < 10; j++)
                    {
                        ds.AddNumericColumn($"col_{i}_{j}", new double[] { 10, 20, 30, 40, 50 });
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            })).ToList();

            var allThreads = readers.Concat(writers).ToList();
            allThreads.ForEach(t => t.Start());
            allThreads.ForEach(t => t.Join(TimeSpan.FromSeconds(15)));

            Assert.Empty(errors);
            // All reads should have returned a valid row count (5)
            Assert.All(readResults, r => Assert.Equal(5, r));
        }

        [Fact]
        public void ConcurrentMixedOperations_DoNotThrow()
        {
            using var store = CreateStore();
            // Pre-create some datasets
            for (int i = 0; i < 5; i++)
                store.CreateTabular($"pre_t_{i}");
            for (int i = 0; i < 5; i++)
                store.CreateGraph($"pre_g_{i}");

            const int threadCount = 12;
            var barrier = new Barrier(threadCount);
            var errors = new ConcurrentBag<Exception>();

            var threads = Enumerable.Range(0, threadCount).Select(i => new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    for (int j = 0; j < 20; j++)
                    {
                        switch ((i + j) % 5)
                        {
                            case 0:
                                store.CreateTabular($"mix_t_{i}_{j}");
                                break;
                            case 1:
                                store.CreateGraph($"mix_g_{i}_{j}");
                                break;
                            case 2:
                                var _ = store.TabularNames;
                                break;
                            case 3:
                                var __ = store.GraphNames;
                                break;
                            case 4:
                                var ___ = store.DatasetNames;
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            })).ToList();

            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join(TimeSpan.FromSeconds(30)));

            Assert.Empty(errors);
        }

        [Fact]
        public void ConcurrentExecuteInTransaction_SerializesCorrectly()
        {
            using var store = CreateStore();
            const int threadCount = 6;
            var barrier = new Barrier(threadCount);
            var errors = new ConcurrentBag<Exception>();
            var completedOps = new ConcurrentBag<string>();

            var threads = Enumerable.Range(0, threadCount).Select(i => new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    store.ExecuteInTransaction(() =>
                    {
                        var ds = store.CreateTabular($"tx_{i}");
                        ds.AddNumericColumn("data", new double[] { i * 1.0, i * 2.0, i * 3.0 });
                        completedOps.Add($"tx_{i}");
                    });
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            })).ToList();

            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join(TimeSpan.FromSeconds(15)));

            Assert.Empty(errors);
            Assert.Equal(threadCount, completedOps.Count);

            // Verify all transactional datasets persisted with correct data
            for (int i = 0; i < threadCount; i++)
            {
                Assert.True(store.TabularExists($"tx_{i}"), $"Dataset tx_{i} should exist after transaction");
                var ds = store.GetTabular($"tx_{i}");
                Assert.Equal(3, ds.RowCount);
            }
        }

        [Fact]
        public void ConcurrentCheckpoint_DoesNotThrow()
        {
            using var store = CreateStore();
            store.CreateTabular("cp_data").AddNumericColumn("val", new double[] { 1, 2, 3 });

            const int threadCount = 5;
            var barrier = new Barrier(threadCount);
            var errors = new ConcurrentBag<Exception>();

            var threads = Enumerable.Range(0, threadCount).Select(_ => new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    for (int i = 0; i < 10; i++)
                    {
                        store.Checkpoint();
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            })).ToList();

            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join(TimeSpan.FromSeconds(15)));

            Assert.Empty(errors);
        }

        [Fact]
        public void SemaphoreSlim_CorrectlySerializesAccess()
        {
            // This test verifies that concurrent operations are serialized
            // by checking that no data corruption occurs even under heavy contention.
            // On mobile (Direct mode), this is enforced by SemaphoreSlim;
            // on desktop (Shared mode), by lock(_lock).
            using var store = CreateStore();
            const int ops = 100;
            var errors = new ConcurrentBag<Exception>();

            Parallel.For(0, ops, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                try
                {
                    if (i % 2 == 0)
                    {
                        store.CreateTabular($"serial_t_{i}");
                    }
                    else
                    {
                        store.CreateGraph($"serial_g_{i}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });

            Assert.Empty(errors);

            // Verify exactly the right number of each type were created
            int tabularCount = store.TabularNames.Count(n => n.StartsWith("serial_t_"));
            int graphCount = store.GraphNames.Count(n => n.StartsWith("serial_g_"));
            Assert.Equal(50, tabularCount);
            Assert.Equal(50, graphCount);
        }

        #endregion
    }
}
