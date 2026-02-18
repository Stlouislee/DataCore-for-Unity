using System;
using AroAro.DataCore.Session;

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

    /// <summary>
    /// 会话数据集添加事件参数
    /// </summary>
    public class SessionDatasetAddedEventArgs : EventArgs
    {
        public ISession Session { get; }
        public IDataSet Dataset { get; }
        
        public SessionDatasetAddedEventArgs(ISession session, IDataSet dataset)
        {
            Session = session;
            Dataset = dataset;
        }
    }

    /// <summary>
    /// 会话数据集创建事件参数
    /// </summary>
    public class SessionDatasetCreatedEventArgs : EventArgs
    {
        public ISession Session { get; }
        public IDataSet Dataset { get; }
        
        public SessionDatasetCreatedEventArgs(ISession session, IDataSet dataset)
        {
            Session = session;
            Dataset = dataset;
        }
    }

    /// <summary>
    /// 会话数据集移除事件参数
    /// </summary>
    public class SessionDatasetRemovedEventArgs : EventArgs
    {
        public ISession Session { get; }
        public IDataSet Dataset { get; }
        
        public SessionDatasetRemovedEventArgs(ISession session, IDataSet dataset)
        {
            Session = session;
            Dataset = dataset;
        }
    }

    /// <summary>
    /// 会话查询结果保存事件参数
    /// </summary>
    public class SessionQueryResultSavedEventArgs : EventArgs
    {
        public ISession Session { get; }
        public IDataSet Source { get; }
        public IDataSet Result { get; }
        
        public SessionQueryResultSavedEventArgs(ISession session, IDataSet source, IDataSet result)
        {
            Session = session;
            Source = source;
            Result = result;
        }
    }

    #region DataFrame Events

    /// <summary>
    /// DataFrame创建事件参数
    /// </summary>
    public class DataFrameCreatedEventArgs : EventArgs
    {
        public ISession Session { get; }
        public string DataFrameName { get; }
        
        public DataFrameCreatedEventArgs(ISession session, string dataFrameName)
        {
            Session = session;
            DataFrameName = dataFrameName;
        }
    }

    /// <summary>
    /// DataFrame移除事件参数
    /// </summary>
    public class DataFrameRemovedEventArgs : EventArgs
    {
        public ISession Session { get; }
        public string DataFrameName { get; }
        
        public DataFrameRemovedEventArgs(ISession session, string dataFrameName)
        {
            Session = session;
            DataFrameName = dataFrameName;
        }
    }

    /// <summary>
    /// DataFrame查询事件参数
    /// </summary>
    public class DataFrameQueriedEventArgs : EventArgs
    {
        public ISession Session { get; }
        public string SourceDataFrame { get; }
        public string ResultDataset { get; }
        public string QueryDescription { get; }
        
        public DataFrameQueriedEventArgs(ISession session, string sourceDataFrame, string resultDataset, string queryDescription)
        {
            Session = session;
            SourceDataFrame = sourceDataFrame;
            ResultDataset = resultDataset;
            QueryDescription = queryDescription;
        }
    }

    #endregion

    #region Algorithm Events

    /// <summary>
    /// Algorithm execution started event arguments.
    /// </summary>
    public class AlgorithmStartedEventArgs : EventArgs
    {
        public string AlgorithmName { get; }
        public IDataSet InputDataset { get; }

        public AlgorithmStartedEventArgs(string algorithmName, IDataSet inputDataset)
        {
            AlgorithmName = algorithmName;
            InputDataset = inputDataset;
        }
    }

    /// <summary>
    /// Algorithm execution completed event arguments.
    /// </summary>
    public class AlgorithmCompletedEventArgs : EventArgs
    {
        public string AlgorithmName { get; }
        public IDataSet InputDataset { get; }
        public IDataSet OutputDataset { get; }
        public bool Success { get; }
        public TimeSpan Duration { get; }
        public string Error { get; }

        public AlgorithmCompletedEventArgs(
            string algorithmName,
            IDataSet inputDataset,
            IDataSet outputDataset,
            bool success,
            TimeSpan duration,
            string error = null)
        {
            AlgorithmName = algorithmName;
            InputDataset = inputDataset;
            OutputDataset = outputDataset;
            Success = success;
            Duration = duration;
            Error = error;
        }
    }

    /// <summary>
    /// Pipeline execution completed event arguments.
    /// </summary>
    public class PipelineCompletedEventArgs : EventArgs
    {
        public string PipelineName { get; }
        public int StepCount { get; }
        public bool Success { get; }
        public TimeSpan Duration { get; }
        public int FailedStepIndex { get; }

        public PipelineCompletedEventArgs(
            string pipelineName, int stepCount, bool success,
            TimeSpan duration, int failedStepIndex = -1)
        {
            PipelineName = pipelineName;
            StepCount = stepCount;
            Success = success;
            Duration = duration;
            FailedStepIndex = failedStepIndex;
        }
    }

    #endregion
}