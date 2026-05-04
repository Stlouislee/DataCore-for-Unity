namespace AroAro.DataCore
{
    /// <summary>
    /// 排序方向枚举。
    /// 用于 OrderBy(column, direction) 统一 API。
    /// </summary>
    public enum SortDirection
    {
        /// <summary>升序（默认）</summary>
        Ascending = 0,

        /// <summary>降序</summary>
        Descending = 1
    }
}
