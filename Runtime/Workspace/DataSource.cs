namespace AroAro.DataCore.Workspace
{
    /// <summary>
    /// 数据集来源
    /// </summary>
    public enum DataSource
    {
        /// <summary>从 LiteDB 持久层加载</summary>
        Store,

        /// <summary>计算/查询产生</summary>
        Derived,

        /// <summary>从外部导入（CSV、GraphML 等）</summary>
        Imported
    }
}
