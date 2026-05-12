using System;
using System.Threading;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Events
{
    /// <summary>
    /// DataCore事件管理器（线程安全）
    /// </summary>
    public static class DataCoreEventManager
    {
        // ── 事件字段（Interlocked 保护） ──────────────────────────────

        private static EventHandler<DatasetCreatedEventArgs> _datasetCreated;
        private static EventHandler<DatasetDeletedEventArgs> _datasetDeleted;
        private static EventHandler<DatasetLoadedEventArgs> _datasetLoaded;
        private static EventHandler<DatasetSavedEventArgs> _datasetSaved;
        private static EventHandler<DatasetModifiedEventArgs> _datasetModified;
        private static EventHandler<DatasetQueriedEventArgs> _datasetQueried;
        private static EventHandler<DatasetImportCompletedEventArgs> _datasetImportCompleted;

        private static EventHandler<SessionDatasetAddedEventArgs> _sessionDatasetAdded;
        private static EventHandler<SessionDatasetCreatedEventArgs> _sessionDatasetCreated;
        private static EventHandler<SessionDatasetRemovedEventArgs> _sessionDatasetRemoved;
        private static EventHandler<SessionQueryResultSavedEventArgs> _sessionQueryResultSaved;

        private static EventHandler<DataFrameCreatedEventArgs> _dataFrameCreated;
        private static EventHandler<DataFrameRemovedEventArgs> _dataFrameRemoved;
        private static EventHandler<DataFrameQueriedEventArgs> _dataFrameQueried;

        private static EventHandler<AlgorithmStartedEventArgs> _algorithmStarted;
        private static EventHandler<AlgorithmCompletedEventArgs> _algorithmCompleted;
        private static EventHandler<PipelineCompletedEventArgs> _pipelineCompleted;

        private static EventHandler<WorkspaceDatasetRegisteredEventArgs> _workspaceDatasetRegistered;

        // ── 订阅（CAS loop） ──────────────────────────────────────────

        public static void SubscribeDatasetCreated(EventHandler<DatasetCreatedEventArgs> handler)
            => Subscribe(ref _datasetCreated, handler);
        public static void UnsubscribeDatasetCreated(EventHandler<DatasetCreatedEventArgs> handler)
            => Unsubscribe(ref _datasetCreated, handler);

        public static void SubscribeDatasetDeleted(EventHandler<DatasetDeletedEventArgs> handler)
            => Subscribe(ref _datasetDeleted, handler);
        public static void UnsubscribeDatasetDeleted(EventHandler<DatasetDeletedEventArgs> handler)
            => Unsubscribe(ref _datasetDeleted, handler);

        public static void SubscribeDatasetLoaded(EventHandler<DatasetLoadedEventArgs> handler)
            => Subscribe(ref _datasetLoaded, handler);
        public static void UnsubscribeDatasetLoaded(EventHandler<DatasetLoadedEventArgs> handler)
            => Unsubscribe(ref _datasetLoaded, handler);

        public static void SubscribeDatasetSaved(EventHandler<DatasetSavedEventArgs> handler)
            => Subscribe(ref _datasetSaved, handler);
        public static void UnsubscribeDatasetSaved(EventHandler<DatasetSavedEventArgs> handler)
            => Unsubscribe(ref _datasetSaved, handler);

        public static void SubscribeDatasetModified(EventHandler<DatasetModifiedEventArgs> handler)
            => Subscribe(ref _datasetModified, handler);
        public static void UnsubscribeDatasetModified(EventHandler<DatasetModifiedEventArgs> handler)
            => Unsubscribe(ref _datasetModified, handler);

        public static void SubscribeDatasetQueried(EventHandler<DatasetQueriedEventArgs> handler)
            => Subscribe(ref _datasetQueried, handler);
        public static void UnsubscribeDatasetQueried(EventHandler<DatasetQueriedEventArgs> handler)
            => Unsubscribe(ref _datasetQueried, handler);

        public static void SubscribeDatasetImportCompleted(EventHandler<DatasetImportCompletedEventArgs> handler)
            => Subscribe(ref _datasetImportCompleted, handler);
        public static void UnsubscribeDatasetImportCompleted(EventHandler<DatasetImportCompletedEventArgs> handler)
            => Unsubscribe(ref _datasetImportCompleted, handler);

        public static void SubscribeSessionDatasetAdded(EventHandler<SessionDatasetAddedEventArgs> handler)
            => Subscribe(ref _sessionDatasetAdded, handler);
        public static void UnsubscribeSessionDatasetAdded(EventHandler<SessionDatasetAddedEventArgs> handler)
            => Unsubscribe(ref _sessionDatasetAdded, handler);

        public static void SubscribeSessionDatasetCreated(EventHandler<SessionDatasetCreatedEventArgs> handler)
            => Subscribe(ref _sessionDatasetCreated, handler);
        public static void UnsubscribeSessionDatasetCreated(EventHandler<SessionDatasetCreatedEventArgs> handler)
            => Unsubscribe(ref _sessionDatasetCreated, handler);

        public static void SubscribeSessionDatasetRemoved(EventHandler<SessionDatasetRemovedEventArgs> handler)
            => Subscribe(ref _sessionDatasetRemoved, handler);
        public static void UnsubscribeSessionDatasetRemoved(EventHandler<SessionDatasetRemovedEventArgs> handler)
            => Unsubscribe(ref _sessionDatasetRemoved, handler);

        public static void SubscribeSessionQueryResultSaved(EventHandler<SessionQueryResultSavedEventArgs> handler)
            => Subscribe(ref _sessionQueryResultSaved, handler);
        public static void UnsubscribeSessionQueryResultSaved(EventHandler<SessionQueryResultSavedEventArgs> handler)
            => Unsubscribe(ref _sessionQueryResultSaved, handler);

        public static void SubscribeDataFrameCreated(EventHandler<DataFrameCreatedEventArgs> handler)
            => Subscribe(ref _dataFrameCreated, handler);
        public static void UnsubscribeDataFrameCreated(EventHandler<DataFrameCreatedEventArgs> handler)
            => Unsubscribe(ref _dataFrameCreated, handler);

        public static void SubscribeDataFrameRemoved(EventHandler<DataFrameRemovedEventArgs> handler)
            => Subscribe(ref _dataFrameRemoved, handler);
        public static void UnsubscribeDataFrameRemoved(EventHandler<DataFrameRemovedEventArgs> handler)
            => Unsubscribe(ref _dataFrameRemoved, handler);

        public static void SubscribeDataFrameQueried(EventHandler<DataFrameQueriedEventArgs> handler)
            => Subscribe(ref _dataFrameQueried, handler);
        public static void UnsubscribeDataFrameQueried(EventHandler<DataFrameQueriedEventArgs> handler)
            => Unsubscribe(ref _dataFrameQueried, handler);

        public static void SubscribeAlgorithmStarted(EventHandler<AlgorithmStartedEventArgs> handler)
            => Subscribe(ref _algorithmStarted, handler);
        public static void UnsubscribeAlgorithmStarted(EventHandler<AlgorithmStartedEventArgs> handler)
            => Unsubscribe(ref _algorithmStarted, handler);

        public static void SubscribeAlgorithmCompleted(EventHandler<AlgorithmCompletedEventArgs> handler)
            => Subscribe(ref _algorithmCompleted, handler);
        public static void UnsubscribeAlgorithmCompleted(EventHandler<AlgorithmCompletedEventArgs> handler)
            => Unsubscribe(ref _algorithmCompleted, handler);

        public static void SubscribePipelineCompleted(EventHandler<PipelineCompletedEventArgs> handler)
            => Subscribe(ref _pipelineCompleted, handler);
        public static void UnsubscribePipelineCompleted(EventHandler<PipelineCompletedEventArgs> handler)
            => Unsubscribe(ref _pipelineCompleted, handler);

        public static void SubscribeWorkspaceDatasetRegistered(EventHandler<WorkspaceDatasetRegisteredEventArgs> handler)
            => Subscribe(ref _workspaceDatasetRegistered, handler);
        public static void UnsubscribeWorkspaceDatasetRegistered(EventHandler<WorkspaceDatasetRegisteredEventArgs> handler)
            => Unsubscribe(ref _workspaceDatasetRegistered, handler);

        // ── 触发事件（原子快照） ──────────────────────────────────────

        public static void RaiseDatasetCreated(IDataSet dataset)
        {
            var handler = Volatile.Read(ref _datasetCreated);
            handler?.Invoke(null, new DatasetCreatedEventArgs(dataset));
        }

        public static void RaiseDatasetDeleted(string datasetName, DataSetKind datasetKind)
        {
            var handler = Volatile.Read(ref _datasetDeleted);
            handler?.Invoke(null, new DatasetDeletedEventArgs(datasetName, datasetKind));
        }

        public static void RaiseDatasetLoaded(IDataSet dataset)
        {
            var handler = Volatile.Read(ref _datasetLoaded);
            handler?.Invoke(null, new DatasetLoadedEventArgs(dataset));
        }

        public static void RaiseDatasetSaved(IDataSet dataset, string filePath)
        {
            var handler = Volatile.Read(ref _datasetSaved);
            handler?.Invoke(null, new DatasetSavedEventArgs(dataset, filePath));
        }

        public static void RaiseDatasetModified(IDataSet dataset, string operation, object additionalData = null)
        {
            var handler = Volatile.Read(ref _datasetModified);
            handler?.Invoke(null, new DatasetModifiedEventArgs(dataset, operation, additionalData));
        }

        public static void RaiseDatasetQueried(IDataSet dataset, string queryType, object queryResult)
        {
            var handler = Volatile.Read(ref _datasetQueried);
            handler?.Invoke(null, new DatasetQueriedEventArgs(dataset, queryType, queryResult));
        }

        /// <summary>
        /// 触发数据集导入完成事件（数据已填充）
        /// </summary>
        public static void RaiseDatasetImportCompleted(IDataSet dataset)
        {
            var handler = Volatile.Read(ref _datasetImportCompleted);
            handler?.Invoke(null, new DatasetImportCompletedEventArgs(dataset));
        }

        public static void RaiseSessionDatasetAdded(ISession session, IDataSet dataset)
        {
            var handler = Volatile.Read(ref _sessionDatasetAdded);
            handler?.Invoke(null, new SessionDatasetAddedEventArgs(session, dataset));
        }

        public static void RaiseSessionDatasetCreated(ISession session, IDataSet dataset)
        {
            var handler = Volatile.Read(ref _sessionDatasetCreated);
            handler?.Invoke(null, new SessionDatasetCreatedEventArgs(session, dataset));
        }

        public static void RaiseSessionDatasetRemoved(ISession session, IDataSet dataset)
        {
            var handler = Volatile.Read(ref _sessionDatasetRemoved);
            handler?.Invoke(null, new SessionDatasetRemovedEventArgs(session, dataset));
        }

        public static void RaiseSessionQueryResultSaved(ISession session, IDataSet source, IDataSet result)
        {
            var handler = Volatile.Read(ref _sessionQueryResultSaved);
            handler?.Invoke(null, new SessionQueryResultSavedEventArgs(session, source, result));
        }

        public static void RaiseSessionDataFrameCreated(ISession session, string dataFrameName)
        {
            var handler = Volatile.Read(ref _dataFrameCreated);
            handler?.Invoke(null, new DataFrameCreatedEventArgs(session, dataFrameName));
        }

        public static void RaiseSessionDataFrameRemoved(ISession session, string dataFrameName)
        {
            var handler = Volatile.Read(ref _dataFrameRemoved);
            handler?.Invoke(null, new DataFrameRemovedEventArgs(session, dataFrameName));
        }

        public static void RaiseDataFrameQueried(ISession session, string sourceDataFrame, string resultDataset, string queryDescription)
        {
            var handler = Volatile.Read(ref _dataFrameQueried);
            handler?.Invoke(null, new DataFrameQueriedEventArgs(session, sourceDataFrame, resultDataset, queryDescription));
        }

        public static void RaiseAlgorithmStarted(string algorithmName, IDataSet inputDataset)
        {
            var handler = Volatile.Read(ref _algorithmStarted);
            handler?.Invoke(null, new AlgorithmStartedEventArgs(algorithmName, inputDataset));
        }

        public static void RaiseAlgorithmCompleted(
            string algorithmName, IDataSet inputDataset, IDataSet outputDataset,
            bool success, TimeSpan duration, string error = null)
        {
            var handler = Volatile.Read(ref _algorithmCompleted);
            handler?.Invoke(null, new AlgorithmCompletedEventArgs(
                algorithmName, inputDataset, outputDataset, success, duration, error));
        }

        public static void RaisePipelineCompleted(
            string pipelineName, int stepCount, bool success,
            TimeSpan duration, int failedStepIndex = -1)
        {
            var handler = Volatile.Read(ref _pipelineCompleted);
            handler?.Invoke(null, new PipelineCompletedEventArgs(
                pipelineName, stepCount, success, duration, failedStepIndex));
        }

        public static void RaiseWorkspaceDatasetRegistered(Workspace.IWorkspace workspace, IDataSet dataset)
        {
            var handler = Volatile.Read(ref _workspaceDatasetRegistered);
            handler?.Invoke(null, new WorkspaceDatasetRegisteredEventArgs(workspace, dataset));
        }

        // ── 清除所有订阅（原子操作） ─────────────────────────────────

        public static void ClearAllSubscriptions()
        {
            Interlocked.Exchange(ref _datasetCreated, null);
            Interlocked.Exchange(ref _datasetDeleted, null);
            Interlocked.Exchange(ref _datasetLoaded, null);
            Interlocked.Exchange(ref _datasetSaved, null);
            Interlocked.Exchange(ref _datasetModified, null);
            Interlocked.Exchange(ref _datasetQueried, null);
            Interlocked.Exchange(ref _datasetImportCompleted, null);
            Interlocked.Exchange(ref _sessionDatasetAdded, null);
            Interlocked.Exchange(ref _sessionDatasetCreated, null);
            Interlocked.Exchange(ref _sessionDatasetRemoved, null);
            Interlocked.Exchange(ref _sessionQueryResultSaved, null);
            Interlocked.Exchange(ref _dataFrameCreated, null);
            Interlocked.Exchange(ref _dataFrameRemoved, null);
            Interlocked.Exchange(ref _dataFrameQueried, null);
            Interlocked.Exchange(ref _algorithmStarted, null);
            Interlocked.Exchange(ref _algorithmCompleted, null);
            Interlocked.Exchange(ref _pipelineCompleted, null);
            Interlocked.Exchange(ref _workspaceDatasetRegistered, null);
        }

        // ── 辅助方法 ─────────────────────────────────────────────────

        private static void Subscribe<T>(ref EventHandler<T> field, EventHandler<T> handler) where T : EventArgs
        {
            EventHandler<T> current, updated;
            do
            {
                current = field;
                updated = (EventHandler<T>)Delegate.Combine(current, handler);
            }
            while (Interlocked.CompareExchange(ref field, updated, current) != current);
        }

        private static void Unsubscribe<T>(ref EventHandler<T> field, EventHandler<T> handler) where T : EventArgs
        {
            EventHandler<T> current, updated;
            do
            {
                current = field;
                updated = (EventHandler<T>)Delegate.Remove(current, handler);
            }
            while (Interlocked.CompareExchange(ref field, updated, current) != current);
        }
    }
}
