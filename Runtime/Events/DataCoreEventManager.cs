using System;
using System.Collections.Generic;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Events
{
    /// <summary>
    /// DataCore事件管理器
    /// </summary>
    public static class DataCoreEventManager
    {
        // 数据集创建事件
        public static event EventHandler<DatasetCreatedEventArgs> DatasetCreated;
        
        // 数据集删除事件
        public static event EventHandler<DatasetDeletedEventArgs> DatasetDeleted;
        
        // 数据集加载事件
        public static event EventHandler<DatasetLoadedEventArgs> DatasetLoaded;
        
        // 数据集保存事件
        public static event EventHandler<DatasetSavedEventArgs> DatasetSaved;
        
        // 数据集修改事件
        public static event EventHandler<DatasetModifiedEventArgs> DatasetModified;
        
        // 数据集查询事件
        public static event EventHandler<DatasetQueriedEventArgs> DatasetQueried;

        // 会话数据集添加事件
        public static event EventHandler<SessionDatasetAddedEventArgs> SessionDatasetAdded;
        
        // 会话数据集创建事件
        public static event EventHandler<SessionDatasetCreatedEventArgs> SessionDatasetCreated;
        
        // 会话数据集移除事件
        public static event EventHandler<SessionDatasetRemovedEventArgs> SessionDatasetRemoved;
        
        // 会话查询结果保存事件
        public static event EventHandler<SessionQueryResultSavedEventArgs> SessionQueryResultSaved;

        /// <summary>
        /// 触发数据集创建事件
        /// </summary>
        public static void RaiseDatasetCreated(IDataSet dataset)
        {
            DatasetCreated?.Invoke(null, new DatasetCreatedEventArgs(dataset));
        }

        /// <summary>
        /// 触发数据集删除事件
        /// </summary>
        public static void RaiseDatasetDeleted(string datasetName, DataSetKind datasetKind)
        {
            DatasetDeleted?.Invoke(null, new DatasetDeletedEventArgs(datasetName, datasetKind));
        }

        /// <summary>
        /// 触发数据集加载事件
        /// </summary>
        public static void RaiseDatasetLoaded(IDataSet dataset)
        {
            DatasetLoaded?.Invoke(null, new DatasetLoadedEventArgs(dataset));
        }

        /// <summary>
        /// 触发数据集保存事件
        /// </summary>
        public static void RaiseDatasetSaved(IDataSet dataset, string filePath)
        {
            DatasetSaved?.Invoke(null, new DatasetSavedEventArgs(dataset, filePath));
        }

        /// <summary>
        /// 触发数据集修改事件
        /// </summary>
        public static void RaiseDatasetModified(IDataSet dataset, string operation, object additionalData = null)
        {
            DatasetModified?.Invoke(null, new DatasetModifiedEventArgs(dataset, operation, additionalData));
        }

        /// <summary>
        /// 触发数据集查询事件
        /// </summary>
        public static void RaiseDatasetQueried(IDataSet dataset, string queryType, object queryResult)
        {
            DatasetQueried?.Invoke(null, new DatasetQueriedEventArgs(dataset, queryType, queryResult));
        }

        /// <summary>
        /// 触发会话数据集添加事件
        /// </summary>
        public static void RaiseSessionDatasetAdded(ISession session, IDataSet dataset)
        {
            SessionDatasetAdded?.Invoke(null, new SessionDatasetAddedEventArgs(session, dataset));
        }

        /// <summary>
        /// 触发会话数据集创建事件
        /// </summary>
        public static void RaiseSessionDatasetCreated(ISession session, IDataSet dataset)
        {
            SessionDatasetCreated?.Invoke(null, new SessionDatasetCreatedEventArgs(session, dataset));
        }

        /// <summary>
        /// 触发会话数据集移除事件
        /// </summary>
        public static void RaiseSessionDatasetRemoved(ISession session, IDataSet dataset)
        {
            SessionDatasetRemoved?.Invoke(null, new SessionDatasetRemovedEventArgs(session, dataset));
        }

        /// <summary>
        /// 触发会话查询结果保存事件
        /// </summary>
        public static void RaiseSessionQueryResultSaved(ISession session, IDataSet source, IDataSet result)
        {
            SessionQueryResultSaved?.Invoke(null, new SessionQueryResultSavedEventArgs(session, source, result));
        }

        #region DataFrame Events

        // DataFrame创建事件
        public static event EventHandler<DataFrameCreatedEventArgs> DataFrameCreated;
        
        // DataFrame移除事件
        public static event EventHandler<DataFrameRemovedEventArgs> DataFrameRemoved;
        
        // DataFrame查询事件
        public static event EventHandler<DataFrameQueriedEventArgs> DataFrameQueried;

        /// <summary>
        /// 触发DataFrame创建事件
        /// </summary>
        public static void RaiseSessionDataFrameCreated(ISession session, string dataFrameName)
        {
            DataFrameCreated?.Invoke(null, new DataFrameCreatedEventArgs(session, dataFrameName));
        }

        /// <summary>
        /// 触发DataFrame移除事件
        /// </summary>
        public static void RaiseSessionDataFrameRemoved(ISession session, string dataFrameName)
        {
            DataFrameRemoved?.Invoke(null, new DataFrameRemovedEventArgs(session, dataFrameName));
        }

        /// <summary>
        /// 触发DataFrame查询事件
        /// </summary>
        public static void RaiseDataFrameQueried(ISession session, string sourceDataFrame, string resultDataset, string queryDescription)
        {
            DataFrameQueried?.Invoke(null, new DataFrameQueriedEventArgs(session, sourceDataFrame, resultDataset, queryDescription));
        }

        #endregion

        #region Algorithm Events

        // Algorithm started event
        public static event EventHandler<AlgorithmStartedEventArgs> AlgorithmStarted;

        // Algorithm completed event
        public static event EventHandler<AlgorithmCompletedEventArgs> AlgorithmCompleted;

        // Pipeline completed event
        public static event EventHandler<PipelineCompletedEventArgs> PipelineCompleted;

        /// <summary>
        /// Fire when an algorithm execution begins.
        /// </summary>
        public static void RaiseAlgorithmStarted(string algorithmName, IDataSet inputDataset)
        {
            AlgorithmStarted?.Invoke(null, new AlgorithmStartedEventArgs(algorithmName, inputDataset));
        }

        /// <summary>
        /// Fire when an algorithm execution completes (success or failure).
        /// </summary>
        public static void RaiseAlgorithmCompleted(
            string algorithmName, IDataSet inputDataset, IDataSet outputDataset,
            bool success, TimeSpan duration, string error = null)
        {
            AlgorithmCompleted?.Invoke(null, new AlgorithmCompletedEventArgs(
                algorithmName, inputDataset, outputDataset, success, duration, error));
        }

        /// <summary>
        /// Fire when an algorithm pipeline completes.
        /// </summary>
        public static void RaisePipelineCompleted(
            string pipelineName, int stepCount, bool success,
            TimeSpan duration, int failedStepIndex = -1)
        {
            PipelineCompleted?.Invoke(null, new PipelineCompletedEventArgs(
                pipelineName, stepCount, success, duration, failedStepIndex));
        }

        #endregion

        /// <summary>
        /// 清除所有事件订阅
        /// </summary>
        public static void ClearAllSubscriptions()
        {
            DatasetCreated = null;
            DatasetDeleted = null;
            DatasetLoaded = null;
            DatasetSaved = null;
            DatasetModified = null;
            DatasetQueried = null;
            SessionDatasetAdded = null;
            SessionDatasetCreated = null;
            SessionDatasetRemoved = null;
            SessionQueryResultSaved = null;
            DataFrameCreated = null;
            DataFrameRemoved = null;
            DataFrameQueried = null;
            AlgorithmStarted = null;
            AlgorithmCompleted = null;
            PipelineCompleted = null;
        }
    }
}