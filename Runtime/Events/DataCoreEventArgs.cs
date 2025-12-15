using System;

namespace AroAro.DataCore.Events
{
    /// <summary>
    /// 数据集事件参数基类
    /// </summary>
    public abstract class DataCoreEventArgs : EventArgs
    {
        public string DatasetName { get; }
        public DataSetKind DatasetKind { get; }
        
        protected DataCoreEventArgs(string datasetName, DataSetKind datasetKind)
        {
            DatasetName = datasetName;
            DatasetKind = datasetKind;
        }
    }

    /// <summary>
    /// 数据集创建事件参数
    /// </summary>
    public class DatasetCreatedEventArgs : DataCoreEventArgs
    {
        public IDataSet Dataset { get; }
        
        public DatasetCreatedEventArgs(IDataSet dataset) 
            : base(dataset.Name, dataset.Kind)
        {
            Dataset = dataset;
        }
    }

    /// <summary>
    /// 数据集删除事件参数
    /// </summary>
    public class DatasetDeletedEventArgs : DataCoreEventArgs
    {
        public DatasetDeletedEventArgs(string datasetName, DataSetKind datasetKind) 
            : base(datasetName, datasetKind)
        {
        }
    }

    /// <summary>
    /// 数据集加载事件参数
    /// </summary>
    public class DatasetLoadedEventArgs : DataCoreEventArgs
    {
        public IDataSet Dataset { get; }
        
        public DatasetLoadedEventArgs(IDataSet dataset) 
            : base(dataset.Name, dataset.Kind)
        {
            Dataset = dataset;
        }
    }

    /// <summary>
    /// 数据集保存事件参数
    /// </summary>
    public class DatasetSavedEventArgs : DataCoreEventArgs
    {
        public string FilePath { get; }
        
        public DatasetSavedEventArgs(IDataSet dataset, string filePath) 
            : base(dataset.Name, dataset.Kind)
        {
            FilePath = filePath;
        }
    }

    /// <summary>
    /// 数据集修改事件参数
    /// </summary>
    public class DatasetModifiedEventArgs : DataCoreEventArgs
    {
        public string Operation { get; }
        public object AdditionalData { get; }
        
        public DatasetModifiedEventArgs(IDataSet dataset, string operation, object additionalData = null) 
            : base(dataset.Name, dataset.Kind)
        {
            Operation = operation;
            AdditionalData = additionalData;
        }
    }

    /// <summary>
    /// 数据集查询事件参数
    /// </summary>
    public class DatasetQueriedEventArgs : DataCoreEventArgs
    {
        public string QueryType { get; }
        public object QueryResult { get; }
        
        public DatasetQueriedEventArgs(IDataSet dataset, string queryType, object queryResult) 
            : base(dataset.Name, dataset.Kind)
        {
            QueryType = queryType;
            QueryResult = queryResult;
        }
    }
}