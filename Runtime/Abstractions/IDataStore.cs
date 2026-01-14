using System;
using System.Collections.Generic;

namespace AroAro.DataCore
{
    /// <summary>
    /// 存储后端类型
    /// </summary>
    public enum StorageBackend
    {
        /// <summary>
        /// LiteDB 嵌入式数据库
        /// </summary>
        LiteDb = 1,

        /// <summary>
        /// 内存存储（不持久化）
        /// </summary>
        Memory = 2,

        /// <summary>
        /// 文件存储（原有的 Arrow/JSON 格式）
        /// </summary>
        File = 3
    }

    /// <summary>
    /// 数据存储接口 - 所有数据集的统一入口
    /// </summary>
    public interface IDataStore : IDisposable
    {
        /// <summary>
        /// 存储后端类型
        /// </summary>
        StorageBackend Backend { get; }

        /// <summary>
        /// 所有数据集名称
        /// </summary>
        IReadOnlyCollection<string> DatasetNames { get; }

        /// <summary>
        /// 所有表格数据集名称
        /// </summary>
        IReadOnlyCollection<string> TabularNames { get; }

        /// <summary>
        /// 所有图数据集名称
        /// </summary>
        IReadOnlyCollection<string> GraphNames { get; }

        #region 表格操作

        /// <summary>
        /// 创建新的表格数据集
        /// </summary>
        ITabularDataset CreateTabular(string name);

        /// <summary>
        /// 获取表格数据集
        /// </summary>
        ITabularDataset GetTabular(string name);

        /// <summary>
        /// 获取或创建表格数据集
        /// </summary>
        ITabularDataset GetOrCreateTabular(string name);

        /// <summary>
        /// 尝试获取表格数据集
        /// </summary>
        bool TryGetTabular(string name, out ITabularDataset tabular);

        /// <summary>
        /// 检查表格是否存在
        /// </summary>
        bool TabularExists(string name);

        /// <summary>
        /// 删除表格数据集
        /// </summary>
        bool DeleteTabular(string name);

        #endregion

        #region 图操作

        /// <summary>
        /// 创建新的图数据集
        /// </summary>
        IGraphDataset CreateGraph(string name);

        /// <summary>
        /// 获取图数据集
        /// </summary>
        IGraphDataset GetGraph(string name);

        /// <summary>
        /// 获取或创建图数据集
        /// </summary>
        IGraphDataset GetOrCreateGraph(string name);

        /// <summary>
        /// 尝试获取图数据集
        /// </summary>
        bool TryGetGraph(string name, out IGraphDataset graph);

        /// <summary>
        /// 检查图是否存在
        /// </summary>
        bool GraphExists(string name);

        /// <summary>
        /// 删除图数据集
        /// </summary>
        bool DeleteGraph(string name);

        #endregion

        #region 事务

        /// <summary>
        /// 开始事务
        /// </summary>
        bool BeginTransaction();

        /// <summary>
        /// 提交事务
        /// </summary>
        bool Commit();

        /// <summary>
        /// 回滚事务
        /// </summary>
        bool Rollback();

        /// <summary>
        /// 在事务中执行操作
        /// </summary>
        void ExecuteInTransaction(Action action);

        /// <summary>
        /// 在事务中执行操作并返回结果
        /// </summary>
        T ExecuteInTransaction<T>(Func<T> action);

        #endregion

        #region 维护

        /// <summary>
        /// 执行检查点（刷新数据）
        /// </summary>
        void Checkpoint();

        /// <summary>
        /// 清空所有数据
        /// </summary>
        void ClearAll();

        #endregion
    }
}
