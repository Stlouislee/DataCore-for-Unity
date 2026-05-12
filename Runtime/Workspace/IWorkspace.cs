using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AroAro.DataCore.Workspace
{
    /// <summary>
    /// 统一内存工作区接口 — DataCore 的"桌面"
    /// 
    /// Workspace 是 store 的一等成员，构造即存在，永远可用。
    /// 它同时包含从持久层加载的数据和临时计算产生的数据，
    /// 提供统一的自省、管理和生命周期操作。
    /// </summary>
    public interface IWorkspace : IDisposable
    {
        #region 基础查询

        /// <summary>工作区中的数据集名称</summary>
        IReadOnlyCollection<string> DatasetNames { get; }

        /// <summary>工作区中的数据集数量</summary>
        int DatasetCount { get; }

        /// <summary>统一视图：store ∪ workspace 的所有数据集名称</summary>
        IReadOnlyCollection<string> AllNames { get; }

        #endregion

        #region 注册

        /// <summary>
        /// 将数据集注册到工作区
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="dataset">数据集</param>
        /// <param name="source">来源</param>
        /// <param name="retention">内存保留策略</param>
        void Register(string name, IDataSet dataset,
            DataSource source = DataSource.Derived,
            WorkspaceRetentionPolicy retention = WorkspaceRetentionPolicy.Auto);

        /// <summary>
        /// 将字典数据注册为表格数据集（自动创建 ITabularDataset）
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="data">行数据</param>
        /// <param name="source">来源</param>
        /// <param name="retention">内存保留策略</param>
        void Register(string name, IEnumerable<Dictionary<string, object>> data,
            DataSource source = DataSource.Derived,
            WorkspaceRetentionPolicy retention = WorkspaceRetentionPolicy.Auto);

        /// <summary>
        /// 自动命名注册（名称冲突时自动加后缀）
        /// </summary>
        void RegisterAuto(string baseName, IDataSet dataset,
            DataSource source = DataSource.Derived,
            WorkspaceRetentionPolicy retention = WorkspaceRetentionPolicy.Auto);

        #endregion

        #region 获取

        /// <summary>
        /// 获取数据集。优先查 workspace，fallback 到 store（自动加载）。
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns>数据集</returns>
        /// <exception cref="KeyNotFoundException">两边都不存在时抛出</exception>
        IDataSet Get(string name);

        /// <summary>
        /// 检查数据集是否存在（workspace 或 store）
        /// </summary>
        bool Has(string name);

        /// <summary>
        /// 只查元数据，不触发数据加载。适合 AI agent 快速扫描上下文。
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="entry">元数据（不存在时为 null）</param>
        /// <returns>是否存在</returns>
        bool TryPeek(string name, out WorkspaceEntry entry);

        #endregion

        #region 自省（AI 友好）

        /// <summary>单个数据集详情</summary>
        /// <param name="name">名称</param>
        /// <returns>元数据</returns>
        /// <exception cref="KeyNotFoundException">不存在时抛出</exception>
        WorkspaceEntry Describe(string name);

        /// <summary>全部数据集详情（懒加载 + 缓存）</summary>
        IReadOnlyList<WorkspaceEntry> DescribeAll();

        /// <summary>一句话描述整个工作区状态</summary>
        string Summary();

        #endregion

        #region 生命周期

        /// <summary>移除数据集（不影响 store）</summary>
        bool Remove(string name);

        /// <summary>重命名</summary>
        bool Rename(string oldName, string newName);

        /// <summary>克隆为新数据集</summary>
        IDataSet Clone(string name, string newName);

        /// <summary>清空工作区（不影响 store）</summary>
        void Clear();

        #endregion

        #region 异步 API

        /// <summary>异步全部数据集详情</summary>
        Task<IReadOnlyList<WorkspaceEntry>> DescribeAllAsync(CancellationToken ct = default);

        /// <summary>异步注册</summary>
        Task RegisterAsync(string name, IDataSet dataset,
            DataSource source = DataSource.Derived,
            WorkspaceRetentionPolicy retention = WorkspaceRetentionPolicy.Auto,
            CancellationToken ct = default);

        #endregion
    }
}
