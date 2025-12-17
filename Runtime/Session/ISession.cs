using System;
using System.Collections.Generic;
using AroAro.DataCore.Events;

namespace AroAro.DataCore.Session
{
    /// <summary>
    /// 会话接口，用于管理临时数据集和操作
    /// </summary>
    public interface ISession : IDisposable
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 会话名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 会话创建时间
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// 会话最后活动时间
        /// </summary>
        DateTime LastActivityAt { get; }

        /// <summary>
        /// 会话中的数据集数量
        /// </summary>
        int DatasetCount { get; }

        /// <summary>
        /// 获取会话中所有数据集的名称
        /// </summary>
        IReadOnlyCollection<string> DatasetNames { get; }

        /// <summary>
        /// 打开数据集（从全局存储加载到会话）
        /// </summary>
        /// <param name="name">数据集名称</param>
        /// <param name="copyName">在会话中的副本名称（可选）</param>
        /// <returns>数据集副本</returns>
        IDataSet OpenDataset(string name, string copyName = null);

        /// <summary>
        /// 创建新的数据集
        /// </summary>
        /// <param name="name">数据集名称</param>
        /// <param name="kind">数据集类型</param>
        /// <returns>新创建的数据集</returns>
        IDataSet CreateDataset(string name, DataSetKind kind);

        /// <summary>
        /// 获取会话中的数据集
        /// </summary>
        /// <param name="name">数据集名称</param>
        /// <returns>数据集</returns>
        IDataSet GetDataset(string name);

        /// <summary>
        /// 检查会话中是否存在指定名称的数据集
        /// </summary>
        /// <param name="name">数据集名称</param>
        /// <returns>是否存在</returns>
        bool HasDataset(string name);

        /// <summary>
        /// 从会话中移除数据集（不删除原始数据）
        /// </summary>
        /// <param name="name">数据集名称</param>
        /// <returns>是否成功移除</returns>
        bool RemoveDataset(string name);

        /// <summary>
        /// 将查询结果保存为新的数据集
        /// </summary>
        /// <param name="sourceName">源数据集名称</param>
        /// <param name="query">查询条件</param>
        /// <param name="newName">新数据集名称</param>
        /// <returns>新数据集</returns>
        IDataSet SaveQueryResult(string sourceName, Func<IDataSet, IDataSet> query, string newName);

        /// <summary>
        /// 将数据集持久化到全局存储
        /// </summary>
        /// <param name="name">数据集名称</param>
        /// <param name="targetName">目标名称（可选，如果不指定则使用原名称）</param>
        /// <returns>是否成功持久化</returns>
        bool PersistDataset(string name, string targetName = null);

        /// <summary>
        /// 清空会话中的所有临时数据
        /// </summary>
        void Clear();

        /// <summary>
        /// 更新最后活动时间
        /// </summary>
        void Touch();
    }
}