namespace AroAro.DataCore.Workspace
{
    /// <summary>
    /// 数据集内存保留策略
    /// </summary>
    public enum WorkspaceRetentionPolicy
    {
        /// <summary>始终强引用</summary>
        Strong,

        /// <summary>弱引用，GC 可回收</summary>
        Weak,

        /// <summary>自动：&lt; 100K 行强引用，&gt;= 100K 行弱引用</summary>
        Auto
    }
}
