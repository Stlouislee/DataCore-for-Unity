using System;
using System.Collections.Generic;
using System.IO;
using AroAro.DataCore;
using Xunit;

namespace DataCore.Tests
{
    /// <summary>
    /// Tests for DataCoreStore properties and error handling.
    /// </summary>
    public class DataCoreStoreTests
    {
        #region DatabasePath

        [Fact]
        public void DatabasePath_LiteDbStore_ReturnsNonNullPath()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                using var store = new DataCoreStore(Path.Combine(tempDir, "test.db"));
                Assert.NotNull(store.DatabasePath);
                Assert.Contains("test.db", store.DatabasePath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void DatabasePath_NonLiteDbStore_ThrowsNotSupportedException()
        {
            using var store = new DataCoreStore(new FakeDataStore());
            Assert.Throws<NotSupportedException>(() => store.DatabasePath);
        }

        #endregion

        #region Mock IDataStore

        /// <summary>
        /// Minimal IDataStore implementation that is NOT LiteDbDataStore.
        /// Used to test type-dependent behavior in DataCoreStore.
        /// </summary>
        private class FakeDataStore : IDataStore
        {
            public StorageBackend Backend => (StorageBackend)99;
            public IReadOnlyCollection<string> DatasetNames => Array.Empty<string>();
            public IReadOnlyCollection<string> TabularNames => Array.Empty<string>();
            public IReadOnlyCollection<string> GraphNames => Array.Empty<string>();

            public ITabularDataset CreateTabular(string name) => throw new NotImplementedException();
            public ITabularDataset GetTabular(string name) => throw new NotImplementedException();
            public ITabularDataset GetOrCreateTabular(string name) => throw new NotImplementedException();
            public bool TryGetTabular(string name, out ITabularDataset tabular) { tabular = null; return false; }
            public bool TabularExists(string name) => false;
            public bool DeleteTabular(string name) => false;

            public IGraphDataset CreateGraph(string name) => throw new NotImplementedException();
            public IGraphDataset GetGraph(string name) => throw new NotImplementedException();
            public IGraphDataset GetOrCreateGraph(string name) => throw new NotImplementedException();
            public bool TryGetGraph(string name, out IGraphDataset graph) { graph = null; return false; }
            public bool GraphExists(string name) => false;
            public bool DeleteGraph(string name) => false;

            public bool BeginTransaction() => true;
            public bool Commit() => true;
            public bool Rollback() => true;
            public void ExecuteInTransaction(Action action) => action();
            public T ExecuteInTransaction<T>(Func<T> action) => action();

            public void Checkpoint() { }
            public void ClearAll() { }
            public void Dispose() { }
        }

        #endregion
    }
}
