using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AroAro.DataCore;
using AroAro.DataCore.Events;
using AroAro.DataCore.Graph;
using AroAro.DataCore.Session;
using AroAro.DataCore.Tabular;
using Xunit;

namespace DataCore.Tests.Events
{
    public class EventManagerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DataCoreStore _store;

        public EventManagerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"DataCore_EventTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _store = new DataCoreStore(Path.Combine(_tempDir, "test.db"));
        }

        public void Dispose()
        {
            _store?.Dispose();
            try { Directory.Delete(_tempDir, true); } catch { }
            DataCoreEventManager.ClearAllSubscriptions();
        }

        // ────────────────────────────────────────────────────────────────
        // DatasetCreated event
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void DatasetCreated_FiresOnCreateTabular()
        {
            DatasetCreatedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeDatasetCreated((sender, args) => receivedArgs = args);

            _store.CreateTabular("testTable");

            Assert.NotNull(receivedArgs);
            Assert.Equal("testTable", receivedArgs.DatasetName);
            Assert.Equal(DataSetKind.Tabular, receivedArgs.DatasetKind);
        }

        [Fact]
        public void DatasetCreated_FiresOnCreateGraph()
        {
            DatasetCreatedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeDatasetCreated((sender, args) => receivedArgs = args);

            _store.CreateGraph("testGraph");

            Assert.NotNull(receivedArgs);
            Assert.Equal("testGraph", receivedArgs.DatasetName);
            Assert.Equal(DataSetKind.Graph, receivedArgs.DatasetKind);
        }

        // ────────────────────────────────────────────────────────────────
        // DatasetDeleted event
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void DatasetDeleted_FiresOnDelete()
        {
            _store.CreateTabular("toDelete");

            DatasetDeletedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeDatasetDeleted((sender, args) => receivedArgs = args);

            _store.Delete("toDelete");

            Assert.NotNull(receivedArgs);
            Assert.Equal("toDelete", receivedArgs.DatasetName);
            Assert.Equal(DataSetKind.Tabular, receivedArgs.DatasetKind);
        }

        [Fact]
        public void DatasetDeleted_GraphKind_IsCorrect()
        {
            _store.CreateGraph("graphToDelete");

            DatasetDeletedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeDatasetDeleted((sender, args) => receivedArgs = args);

            _store.Delete("graphToDelete");

            Assert.NotNull(receivedArgs);
            Assert.Equal(DataSetKind.Graph, receivedArgs.DatasetKind);
        }

        // ────────────────────────────────────────────────────────────────
        // DatasetModified event
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void DatasetModified_FiresWhenRaised()
        {
            DatasetModifiedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeDatasetModified((sender, args) => receivedArgs = args);

            var tabular = new TabularData("test");
            tabular.AddNumericColumn("x", new double[] { 1, 2, 3 });

            DataCoreEventManager.RaiseDatasetModified(tabular, "AddColumn", new { column = "x" });

            Assert.NotNull(receivedArgs);
            Assert.Equal("test", receivedArgs.DatasetName);
            Assert.Equal("AddColumn", receivedArgs.Operation);
            Assert.NotNull(receivedArgs.AdditionalData);
        }

        // ────────────────────────────────────────────────────────────────
        // DatasetQueried event
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void DatasetQueried_FiresWhenRaised()
        {
            DatasetQueriedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeDatasetQueried((sender, args) => receivedArgs = args);

            var tabular = new TabularData("test");
            tabular.AddNumericColumn("x", new double[] { 1, 2, 3 });

            DataCoreEventManager.RaiseDatasetQueried(tabular, "Filter", new[] { 0, 2 });

            Assert.NotNull(receivedArgs);
            Assert.Equal("test", receivedArgs.DatasetName);
            Assert.Equal("Filter", receivedArgs.QueryType);
        }

        // ────────────────────────────────────────────────────────────────
        // ClearAllSubscriptions removes all listeners
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void ClearAllSubscriptions_RemovesAllListeners()
        {
            bool called1 = false;
            bool called2 = false;

            DataCoreEventManager.SubscribeDatasetCreated((s, e) => called1 = true);
            DataCoreEventManager.SubscribeDatasetDeleted((s, e) => called2 = true);

            DataCoreEventManager.ClearAllSubscriptions();

            _store.CreateTabular("test");
            _store.Delete("test");

            Assert.False(called1, "DatasetCreated handler should have been removed");
            Assert.False(called2, "DatasetDeleted handler should have been removed");
        }

        [Fact]
        public void ClearAllSubscriptions_ClearsAlgorithmEvents()
        {
            bool startedCalled = false;
            bool completedCalled = false;

            DataCoreEventManager.SubscribeAlgorithmStarted((s, e) => startedCalled = true);
            DataCoreEventManager.SubscribeAlgorithmCompleted((s, e) => completedCalled = true);

            DataCoreEventManager.ClearAllSubscriptions();

            DataCoreEventManager.RaiseAlgorithmStarted("test", null);
            DataCoreEventManager.RaiseAlgorithmCompleted("test", null, null, true, TimeSpan.Zero);

            Assert.False(startedCalled);
            Assert.False(completedCalled);
        }

        // ────────────────────────────────────────────────────────────────
        // Multiple subscribers all receive events
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void MultipleSubscribers_AllReceiveEvents()
        {
            int callCount = 0;

            DataCoreEventManager.SubscribeDatasetCreated((s, e) => Interlocked.Increment(ref callCount));
            DataCoreEventManager.SubscribeDatasetCreated((s, e) => Interlocked.Increment(ref callCount));
            DataCoreEventManager.SubscribeDatasetCreated((s, e) => Interlocked.Increment(ref callCount));

            _store.CreateTabular("test");

            Assert.Equal(3, callCount);
        }

        [Fact]
        public void MultipleSubscribers_AllReceiveCorrectArgs()
        {
            DatasetCreatedEventArgs args1 = null;
            DatasetCreatedEventArgs args2 = null;

            DataCoreEventManager.SubscribeDatasetCreated((s, e) => args1 = e);
            DataCoreEventManager.SubscribeDatasetCreated((s, e) => args2 = e);

            _store.CreateTabular("shared");

            Assert.NotNull(args1);
            Assert.NotNull(args2);
            Assert.Equal("shared", args1.DatasetName);
            Assert.Equal("shared", args2.DatasetName);
        }

        // ────────────────────────────────────────────────────────────────
        // Event fires with correct args
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void DatasetCreated_CorrectDatasetName()
        {
            string receivedName = null;
            DataCoreEventManager.SubscribeDatasetCreated((s, e) => receivedName = e.DatasetName);

            _store.CreateTabular("MyDataset");

            Assert.Equal("MyDataset", receivedName);
        }

        [Fact]
        public void DatasetCreated_CorrectDatasetKind()
        {
            DataSetKind? receivedKind = null;
            DataCoreEventManager.SubscribeDatasetCreated((s, e) => receivedKind = e.DatasetKind);

            _store.CreateGraph("MyGraph");

            Assert.Equal(DataSetKind.Graph, receivedKind);
        }

        [Fact]
        public void DatasetDeleted_CorrectArgs()
        {
            _store.CreateTabular("toDelete");

            string receivedName = null;
            DataSetKind? receivedKind = null;
            DataCoreEventManager.SubscribeDatasetDeleted((s, e) =>
            {
                receivedName = e.DatasetName;
                receivedKind = e.DatasetKind;
            });

            _store.Delete("toDelete");

            Assert.Equal("toDelete", receivedName);
            Assert.Equal(DataSetKind.Tabular, receivedKind);
        }

        [Fact]
        public void DatasetModified_CorrectOperation()
        {
            string receivedOp = null;
            DataCoreEventManager.SubscribeDatasetModified((s, e) => receivedOp = e.Operation);

            var tabular = new TabularData("test");
            DataCoreEventManager.RaiseDatasetModified(tabular, "AddRow");

            Assert.Equal("AddRow", receivedOp);
        }

        // ────────────────────────────────────────────────────────────────
        // Session events
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void SessionDatasetCreated_FiresOnCreateDataset()
        {
            SessionDatasetCreatedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeSessionDatasetCreated((s, e) => receivedArgs = e);

            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataset("newDs", DataSetKind.Tabular);

            Assert.NotNull(receivedArgs);
            Assert.Same(session, receivedArgs.Session);
            Assert.Equal("newDs", receivedArgs.Dataset.Name);
        }

        [Fact(Skip = "Known issue: LiteDbTabularDataset.WithName throws NotSupportedException")]
        public void SessionDatasetRemoved_FiresOnRemoveDataset()
        {
            SessionDatasetRemovedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeSessionDatasetRemoved((s, e) => receivedArgs = e);

            _store.CreateTabular("source");
            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.OpenDataset("source");
            session.RemoveDataset("source");

            Assert.NotNull(receivedArgs);
            Assert.Same(session, receivedArgs.Session);
        }

        // ────────────────────────────────────────────────────────────────
        // DataFrame events
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void DataFrameCreated_FiresOnCreateDataFrame()
        {
            DataFrameCreatedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeDataFrameCreated((s, e) => receivedArgs = e);

            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataFrame("myDf");

            Assert.NotNull(receivedArgs);
            Assert.Same(session, receivedArgs.Session);
            Assert.Equal("myDf", receivedArgs.DataFrameName);
        }

        [Fact]
        public void DataFrameRemoved_FiresOnRemoveDataFrame()
        {
            DataFrameRemovedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeDataFrameRemoved((s, e) => receivedArgs = e);

            var session = new AroAro.DataCore.Session.Session("Test", _store);
            session.CreateDataFrame("myDf");
            session.RemoveDataFrame("myDf");

            Assert.NotNull(receivedArgs);
            Assert.Equal("myDf", receivedArgs.DataFrameName);
        }

        // ────────────────────────────────────────────────────────────────
        // Algorithm events
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void AlgorithmStarted_FiresOnExecution()
        {
            AlgorithmStartedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeAlgorithmStarted((s, e) => receivedArgs = e);

            var graph = new GraphData("test");
            graph.AddNode("A");
            DataCoreEventManager.RaiseAlgorithmStarted("PageRank", graph);

            Assert.NotNull(receivedArgs);
            Assert.Equal("PageRank", receivedArgs.AlgorithmName);
            Assert.Same(graph, receivedArgs.InputDataset);
        }

        [Fact]
        public void AlgorithmCompleted_FiresOnCompletion()
        {
            AlgorithmCompletedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeAlgorithmCompleted((s, e) => receivedArgs = e);

            DataCoreEventManager.RaiseAlgorithmCompleted("PageRank", null, null, true, TimeSpan.FromMilliseconds(100));

            Assert.NotNull(receivedArgs);
            Assert.Equal("PageRank", receivedArgs.AlgorithmName);
            Assert.True(receivedArgs.Success);
            Assert.Equal(TimeSpan.FromMilliseconds(100), receivedArgs.Duration);
        }

        [Fact]
        public void PipelineCompleted_FiresOnCompletion()
        {
            PipelineCompletedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribePipelineCompleted((s, e) => receivedArgs = e);

            DataCoreEventManager.RaisePipelineCompleted("MyPipeline", 3, true, TimeSpan.FromMilliseconds(500));

            Assert.NotNull(receivedArgs);
            Assert.Equal("MyPipeline", receivedArgs.PipelineName);
            Assert.Equal(3, receivedArgs.StepCount);
            Assert.True(receivedArgs.Success);
        }

        // ────────────────────────────────────────────────────────────────
        // Thread safety: concurrent subscribe/raise
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task ThreadSafety_ConcurrentSubscribeAndRaise_NoExceptions()
        {
            var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var receivedCount = 0;

            DataCoreEventManager.SubscribeDatasetCreated((s, e) => Interlocked.Increment(ref receivedCount));

            var tasks = new List<Task>();

            // Concurrent event raises
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        DataCoreEventManager.RaiseDatasetCreated(new TabularData("test"));
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                }));
            }

            // Concurrent subscribe/unsubscribe
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        EventHandler<DatasetCreatedEventArgs> handler = (s, e) => { };
                        DataCoreEventManager.SubscribeDatasetCreated(handler);
                        DataCoreEventManager.UnsubscribeDatasetCreated(handler);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                }));
            }

            await Task.WhenAll(tasks.ToArray());

            Assert.Empty(errors);
            Assert.Equal(20, receivedCount);
        }

        [Fact]
        public async Task ClearAllSubscriptions_CalledDuringRaise_NoExceptions()
        {
            var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var tasks = new List<Task>();

            DataCoreEventManager.SubscribeDatasetCreated((s, e) => { });

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        DataCoreEventManager.RaiseDatasetCreated(new TabularData("test"));
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                }));
            }

            tasks.Add(Task.Run(() =>
            {
                try
                {
                    DataCoreEventManager.ClearAllSubscriptions();
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }));

            await Task.WhenAll(tasks.ToArray());

            Assert.Empty(errors);
        }

        // ────────────────────────────────────────────────────────────────
        // Event not fired when no subscribers
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void NoSubscribers_DoesNotThrow()
        {
            DataCoreEventManager.ClearAllSubscriptions();

            var ex = Record.Exception(() =>
                DataCoreEventManager.RaiseDatasetCreated(new TabularData("test")));

            Assert.Null(ex);
        }

        // ────────────────────────────────────────────────────────────────
        // DatasetImportCompleted event (#111)
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void DatasetImportCompleted_FiresAfterImport()
        {
            DatasetImportCompletedEventArgs receivedArgs = null;
            DataCoreEventManager.SubscribeDatasetImportCompleted((s, e) => receivedArgs = e);

            var tabular = _store.CreateTabular("import-test");
            tabular.AddNumericColumn("x", new double[] { 1, 2, 3 });
            DataCoreEventManager.RaiseDatasetImportCompleted(tabular);

            Assert.NotNull(receivedArgs);
            Assert.Equal("import-test", receivedArgs.DatasetName);
            Assert.Equal(3, ((ITabularDataset)receivedArgs.Dataset).RowCount);
        }
    }
}
