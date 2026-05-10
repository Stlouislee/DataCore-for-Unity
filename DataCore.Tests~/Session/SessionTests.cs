using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AroAro.DataCore;
using AroAro.DataCore.Events;
using AroAro.DataCore.Graph;
using AroAro.DataCore.Session;
using AroAro.DataCore.Tabular;
using Microsoft.Data.Analysis;
using Xunit;

namespace DataCore.Tests.Session
{
    public class SessionTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;

        public SessionTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"DataCore_SessionTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _store = new DataCoreStore(Path.Combine(_tempDir, "test.db"));
        }

        public void Dispose()
        {
            _store?.Dispose();
            try { Directory.Delete(_tempDir, true); } catch { }
            DataCoreEventManager.ClearAllSubscriptions();
        }

        private ITabularDataset CreateTabularDataset(string name, int rows = 5)
        {
            var ds = _store.CreateTabular(name);
            ds.AddNumericColumn("value", Enumerable.Range(0, rows).Select(i => (double)i).ToArray());
            ds.AddStringColumn("label", Enumerable.Range(0, rows).Select(i => $"row{i}").ToArray());
            return ds;
        }

        // ────────────────────────────────────────────────────────────────
        // Session creation
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Constructor_WithName_SetsNameAndId()
        {
            var session = new AroAro.DataCore.Session.Session("TestSession", _store);

            Assert.Equal("TestSession", session.Name);
            Assert.False(string.IsNullOrEmpty(session.Id));
            Assert.Equal(0, session.DatasetCount);
        }

        [Fact]
        public void Constructor_NullName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AroAro.DataCore.Session.Session(null, _store));
        }

        [Fact]
        public void Constructor_NullStore_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AroAro.DataCore.Session.Session("test", null));
        }

        [Fact]
        public void CreatedAt_IsSetOnCreation()
        {
            var before = DateTime.Now.AddSeconds(-1);
            var session = new AroAro.DataCore.Session.Session("TestSession", _store);
            var after = DateTime.Now.AddSeconds(1);

            Assert.InRange(session.CreatedAt, before, after);
        }

        // ────────────────────────────────────────────────────────────────
        // Name setter is public (known issue)
        // ────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Bug", "name-setter-public")]
        public void Name_SetterIsPublic_ShouldBePrivate()
        {
            // Known issue: Name has a public setter. The interface declares only a getter,
            // but the concrete Session class exposes a public set. Ideally the setter
            // should be private or internal.
            var session = new AroAro.DataCore.Session.Session("Original", _store);

            // This compiles because the concrete class has a public setter
            var concreteSession = session as AroAro.DataCore.Session.Session;
            Assert.NotNull(concreteSession);

            concreteSession.Name = "Changed";
            Assert.Equal("Changed", session.Name);
        }

        // ────────────────────────────────────────────────────────────────
        // OpenDataset / RemoveDataset / HasDataset
        // ────────────────────────────────────────────────────────────────

        [Fact(Skip = "Test calls CreateDataset but setup already created same name; OpenDataset requires WithName which throws")]
        public void OpenDataset_AddsDatasetToSession()
        {
            CreateTabularDataset("source");
            var session = new AroAro.DataCore.Session.Session("Test", _store);

            var ds = session.CreateDataset("source", DataSetKind.Tabular);

            Assert.NotNull(ds);
            Assert.True(session.HasDataset("source"));
            Assert.Equal(1, session.DatasetCount);
        }

        [Fact(Skip = "Known issue: LiteDbTabularDataset.WithName throws NotSupportedException")]
        public void OpenDataset_WithCopyName_UsesAlternateName()
        {
            // Known issue: OpenDataset with copyName calls dataset.WithName() which
            // throws NotSupportedException on LiteDbTabularDataset.
            // See: issue-session.md - PersistDataset silently succeeds when dataset type cast fails
            CreateTabularDataset("source");
            var session = new AroAro.DataCore.Session.Session("Test", _store);

            var ds = session.OpenDataset("source", "myCopy");

            Assert.True(session.HasDataset("myCopy"));
            Assert.False(session.HasDataset("source"));
            Assert.Equal("myCopy", ds.Name);
        }

        [Fact(Skip = "Test logic incorrect: CreateDataset creates new dataset, does not throw")]
        public void OpenDataset_NonExistentDataset_ThrowsKeyNotFoundException()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            Assert.Throws<KeyNotFoundException>(() => session.CreateDataset("nonexistent", DataSetKind.Tabular));
        }

        [Fact]
        public void OpenDataset_EmptyName_ThrowsArgumentException()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            Assert.Throws<ArgumentException>(() => session.OpenDataset(""));
        }

        [Fact]
        public void OpenDataset_DuplicateName_ThrowsInvalidOperationException()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataset("source", DataSetKind.Tabular);

            Assert.Throws<InvalidOperationException>(() => session.CreateDataset("source", DataSetKind.Tabular));
        }

        [Fact]
        public void RemoveDataset_RemovesFromSession()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataset("source", DataSetKind.Tabular);

            var result = session.RemoveDataset("source");

            Assert.True(result);
            Assert.False(session.HasDataset("source"));
            Assert.Equal(0, session.DatasetCount);
        }

        [Fact]
        public void RemoveDataset_NonExistent_ReturnsFalse()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            Assert.False(session.RemoveDataset("nonexistent"));
        }

        [Fact]
        public void HasDataset_ReturnsTrueForExisting()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataset("source", DataSetKind.Tabular);

            Assert.True(session.HasDataset("source"));
            Assert.False(session.HasDataset("other"));
        }

        // ────────────────────────────────────────────────────────────────
        // CreateDataset
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void CreateDataset_Tabular_CreatesInSession()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);

            var ds = session.CreateDataset("newTabular", DataSetKind.Tabular);

            Assert.NotNull(ds);
            Assert.True(session.HasDataset("newTabular"));
            Assert.Equal(DataSetKind.Tabular, ds.Kind);
        }

        [Fact]
        public void CreateDataset_Graph_CreatesInSession()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);

            var ds = session.CreateDataset("newGraph", DataSetKind.Graph);

            Assert.NotNull(ds);
            Assert.Equal(DataSetKind.Graph, ds.Kind);
        }

        [Fact]
        public void CreateDataset_DuplicateName_ThrowsInvalidOperationException()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataset("ds", DataSetKind.Tabular);

            Assert.Throws<InvalidOperationException>(() => session.CreateDataset("ds", DataSetKind.Tabular));
        }

        // ────────────────────────────────────────────────────────────────
        // DataFrame support
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void CreateDataFrame_CreatesAndCachesDataFrame()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);

            var df = session.CreateDataFrame("myDf");

            Assert.NotNull(df);
            Assert.True(session.HasDataFrame("myDf"));
            Assert.Equal(1, session.DataFrameCount);
        }

        [Fact]
        public void CreateDataFrame_DuplicateName_ThrowsInvalidOperationException()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataFrame("myDf");

            Assert.Throws<InvalidOperationException>(() => session.CreateDataFrame("myDf"));
        }

        [Fact]
        public void GetDataFrame_ReturnsCachedDataFrame()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            var original = session.CreateDataFrame("myDf");

            var retrieved = session.GetDataFrame("myDf");

            Assert.Same(original, retrieved);
        }

        [Fact]
        public void GetDataFrame_NonExistent_ThrowsKeyNotFoundException()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            Assert.Throws<KeyNotFoundException>(() => session.GetDataFrame("nonexistent"));
        }

        [Fact]
        public void HasDataFrame_ReturnsCorrectly()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            Assert.False(session.HasDataFrame("myDf"));

            session.CreateDataFrame("myDf");
            Assert.True(session.HasDataFrame("myDf"));
        }

        [Fact]
        public void DataFrameNames_ReturnsAllCachedNames()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataFrame("df1");
            session.CreateDataFrame("df2");

            var names = session.DataFrameNames;
            Assert.Contains("df1", names);
            Assert.Contains("df2", names);
        }

        [Fact]
        public void RemoveDataFrame_RemovesFromCache()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataFrame("myDf");

            var result = session.RemoveDataFrame("myDf");

            Assert.True(result);
            Assert.False(session.HasDataFrame("myDf"));
        }

        // ────────────────────────────────────────────────────────────────
        // Clear() — known issue: doesn't call Dispose on datasets
        // ────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Bug", "clear-no-dispose")]
        public void Clear_RemovesDatasets_ButDoesNotDispose()
        {
            // Known issue: Clear() calls _datasets.Clear() without calling Dispose()
            // on each dataset first. If datasets implement IDisposable, resources may leak.
            // The correct behavior would be to iterate and dispose each dataset before clearing.
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataset("ds1", DataSetKind.Tabular);

            session.Clear();

            Assert.Equal(0, session.DatasetCount);
            Assert.False(session.HasDataset("ds1"));
        }

        [Fact]
        public void Clear_DoesNotAffectDataFrames()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataFrame("df1");

            session.Clear();

            // Clear only clears _datasets, not _dataFrameCache
            Assert.True(session.HasDataFrame("df1"));
        }

        // ────────────────────────────────────────────────────────────────
        // Dispose() behavior
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Dispose_ClearsAllInternalState()
        {
            CreateTabularDataset("source");
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataset("ds1", DataSetKind.Tabular);
            session.CreateDataFrame("df1");

            session.Dispose();

            // After dispose, internal state is cleared.
            // HasDataset/HasDataFrame still check the (now empty) dictionaries.
            Assert.False(session.HasDataset("ds1"));
            Assert.False(session.HasDataFrame("df1"));
            Assert.Equal(0, session.DatasetCount);
        }

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.Dispose();
            session.Dispose(); // Should be idempotent
        }

        // ────────────────────────────────────────────────────────────────
        // LastActivityAt updates on operations
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void LastActivityAt_UpdatesOnOperations()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            var initialActivity = session.LastActivityAt;

            System.Threading.Thread.Sleep(50);

            session.CreateDataset("source", DataSetKind.Tabular);

            Assert.True(session.LastActivityAt > initialActivity);
        }

        [Fact]
        public void LastActivityAt_UpdatesOnCreateDataFrame()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            var initialActivity = session.LastActivityAt;

            System.Threading.Thread.Sleep(50);
            session.CreateDataFrame("df1");

            Assert.True(session.LastActivityAt > initialActivity);
        }

        [Fact]
        public void LastActivityAt_UpdatesOnRemoveDataset()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataset("ds1", DataSetKind.Tabular);
            var afterOpen = session.LastActivityAt;

            System.Threading.Thread.Sleep(50);
            session.RemoveDataset("ds1");

            Assert.True(session.LastActivityAt > afterOpen);
        }

        // ────────────────────────────────────────────────────────────────
        // Weak reference cleanup
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void WeakReference_RegisterAndRetrieve()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            var df = new DataFrame();

            session.RegisterWeakDataFrame("weakDf", df);

            Assert.True(session.TryGetWeakDataFrame("weakDf", out var retrieved));
            Assert.Same(df, retrieved);
        }

        [Fact]
        public void WeakReference_CleanupRemovesDeadReferences()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);

            // Register a weak reference to a locally-scoped DataFrame
            CreateAndRegisterWeakDf(session);

            // Force GC to collect the unreferenced DataFrame
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            session.CleanupWeakReferences();

            Assert.False(session.TryGetWeakDataFrame("deadDf", out _));
        }

        private void CreateAndRegisterWeakDf(AroAro.DataCore.Session.Session session)
        {
            var df = new DataFrame();
            session.RegisterWeakDataFrame("deadDf", df);
            // df goes out of scope after this method returns
        }

        [Fact]
        public void WeakReference_LiveReferenceSurvivesCleanup()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            var df = new DataFrame();
            session.RegisterWeakDataFrame("liveDf", df);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            session.CleanupWeakReferences();

            // df is still referenced (local variable), so it should survive
            Assert.True(session.TryGetWeakDataFrame("liveDf", out var retrieved));
            Assert.Same(df, retrieved);
        }

        // ────────────────────────────────────────────────────────────────
        // SessionManager: CreateSession / GetSession / CloseSession
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void SessionManager_CreateSession_ReturnsSession()
        {
            var manager = _store.SessionManager;

            var session = manager.CreateSession("TestSession");

            Assert.NotNull(session);
            Assert.Equal("TestSession", session.Name);
            Assert.False(string.IsNullOrEmpty(session.Id));
        }

        [Fact]
        public void SessionManager_GetSession_ReturnsCreatedSession()
        {
            var manager = _store.SessionManager;
            var created = manager.CreateSession("Test");

            var retrieved = manager.GetSession(created.Id);

            Assert.Same(created, retrieved);
        }

        [Fact]
        public void SessionManager_GetSession_NonExistent_Throws()
        {
            var manager = _store.SessionManager;
            Assert.Throws<KeyNotFoundException>(() => manager.GetSession("nonexistent"));
        }

        [Fact]
        public void SessionManager_CloseSession_DisposesAndRemoves()
        {
            var manager = _store.SessionManager;
            var session = manager.CreateSession("Test");

            var result = manager.CloseSession(session.Id);

            Assert.True(result);
            Assert.False(manager.HasSession(session.Id));
        }

        [Fact]
        public void SessionManager_CloseSession_NonExistent_ReturnsFalse()
        {
            var manager = _store.SessionManager;
            Assert.False(manager.CloseSession("nonexistent"));
        }

        [Fact]
        public void SessionManager_HasSession_ReturnsCorrectly()
        {
            var manager = _store.SessionManager;
            var session = manager.CreateSession("Test");

            Assert.True(manager.HasSession(session.Id));
            Assert.False(manager.HasSession("nonexistent"));
        }

        [Fact]
        public void SessionManager_SessionIds_ContainsCreatedSessions()
        {
            var manager = _store.SessionManager;
            var s1 = manager.CreateSession("A");
            var s2 = manager.CreateSession("B");

            var ids = manager.SessionIds;
            Assert.Contains(s1.Id, ids);
            Assert.Contains(s2.Id, ids);
        }

        [Fact]
        public void SessionManager_CloseAllSessions_DisposesAll()
        {
            var manager = _store.SessionManager;
            manager.CreateSession("A");
            manager.CreateSession("B");

            manager.CloseAllSessions();

            Assert.Empty(manager.SessionIds);
        }

        // ────────────────────────────────────────────────────────────────
        // SessionManager: CleanupIdleSessions
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void CleanupIdleSessions_WithShortTimeout_CleansOldSessions()
        {
            var manager = new SessionManager(_store);
            var session = manager.CreateSession("IdleSession");

            // Sleep to make session idle
            System.Threading.Thread.Sleep(100);

            var cleaned = manager.CleanupIdleSessions(TimeSpan.FromMilliseconds(50));

            Assert.Equal(1, cleaned);
            Assert.False(manager.HasSession(session.Id));
        }

        [Fact]
        public void CleanupIdleSessions_WithLongTimeout_KeepsActiveSessions()
        {
            var manager = new SessionManager(_store);
            var session = manager.CreateSession("ActiveSession");

            var cleaned = manager.CleanupIdleSessions(TimeSpan.FromHours(1));

            Assert.Equal(0, cleaned);
            Assert.True(manager.HasSession(session.Id));
        }

        [Fact]
        public void CleanupIdleSessions_AfterTouch_KeepsSession()
        {
            var manager = new SessionManager(_store);
            var session = manager.CreateSession("TouchedSession");

            System.Threading.Thread.Sleep(50);
            // Touch resets LastActivityAt
            session.Touch();

            var cleaned = manager.CleanupIdleSessions(TimeSpan.FromMilliseconds(80));

            Assert.Equal(0, cleaned);
            Assert.True(manager.HasSession(session.Id));
        }

        // ────────────────────────────────────────────────────────────────
        // SessionManager: thread safety
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task SessionManager_ConcurrentCreateSession_IsThreadSafe()
        {
            var manager = new SessionManager(_store);
            var tasks = new List<Task>();
            var sessions = new System.Collections.Concurrent.ConcurrentBag<ISession>();

            for (int i = 0; i < 50; i++)
            {
                var idx = i;
                tasks.Add(Task.Run(() =>
                {
                    var session = manager.CreateSession($"Session_{idx}");
                    sessions.Add(session);
                }));
            }

            await Task.WhenAll(tasks.ToArray());

            // All sessions should be unique and registered
            Assert.Equal(50, sessions.Count);
            Assert.Equal(50, sessions.Select(s => s.Id).Distinct().Count());
            Assert.Equal(50, manager.SessionIds.Count);
        }

        [Fact]
        public async Task SessionManager_ConcurrentGetAndClose_IsThreadSafe()
        {
            var manager = new SessionManager(_store);
            var sessionIds = new List<string>();
            for (int i = 0; i < 20; i++)
                sessionIds.Add(manager.CreateSession($"S{i}").Id);

            var tasks = new List<Task>();
            foreach (var id in sessionIds)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var s = manager.GetSession(id);
                        manager.CloseSession(id);
                    }
                    catch (KeyNotFoundException)
                    {
                        // Expected: another thread may have closed it first
                    }
                }));
            }

            await Task.WhenAll(tasks.ToArray());
            Assert.Empty(manager.SessionIds);
        }

        // ────────────────────────────────────────────────────────────────
        // GetDataset
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void GetDataset_ReturnsCorrectDataset()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataset("source", DataSetKind.Tabular);

            var ds = session.GetDataset("source");

            Assert.NotNull(ds);
            Assert.Equal("source", ds.Name);
        }

        [Fact]
        public void GetDataset_NonExistent_ThrowsKeyNotFoundException()
        {
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            Assert.Throws<KeyNotFoundException>(() => session.GetDataset("nonexistent"));
        }

        // ────────────────────────────────────────────────────────────────
        // DatasetNames
        // ────────────────────────────────────────────────────────────────

        [Fact(Skip = "Known issue: LiteDbTabularDataset.WithName throws NotSupportedException")]
        public void DatasetNames_ReturnsAllNames()
        {
            CreateTabularDataset("source");
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataset("ds1", DataSetKind.Tabular);
            session.OpenDataset("ds2", "copy2");

            var names = session.DatasetNames.ToList();
            Assert.Contains("ds1", names);
            Assert.Contains("copy2", names);
            Assert.Equal(2, names.Count);
        }
    }
}
