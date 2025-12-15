using System;
using System.Collections.Generic;

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
        }
    }
}