using System;

namespace AroAro.DataCore
{
    /// <summary>
    /// DataCore 统一查询入口。
    /// 提供 <c>From("tableName")</c> 和 <c>Graph("graphName")</c> 风格的查询起点。
    /// </summary>
    /// <example>
    /// <code>
    /// // 表格查询
    /// var result = DataCoreQuery.From(store, "Sales")
    ///     .Where(row => row.Get&lt;double&gt;("Revenue") > 1000)
    ///     .OrderBy("Revenue", SortDirection.Descending)
    ///     .Limit(100)
    ///     .Execute();
    ///
    /// // 图查询
    /// var nodes = DataCoreQuery.Graph(store, "DependencyGraph")
    ///     .From("root-node")
    ///     .TraverseOut()
    ///     .MaxDepth(3)
    ///     .ToNodeIds();
    /// </code>
    /// </example>
    public static class DataCoreQuery
    {
        /// <summary>
        /// 从指定 DataCoreStore 创建表格查询。
        /// </summary>
        /// <param name="store">DataCoreStore 实例</param>
        /// <param name="tableName">表格数据集名称</param>
        /// <returns>可链式调用的 ITabularQuery</returns>
        /// <exception cref="KeyNotFoundException">表格不存在</exception>
        public static ITabularQuery From(DataCoreStore store, string tableName)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name is required", nameof(tableName));

            var tabular = store.GetTabular(tableName);
            return tabular.Query();
        }

        /// <summary>
        /// 从指定 IDataStore 创建表格查询。
        /// </summary>
        public static ITabularQuery From(IDataStore store, string tableName)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name is required", nameof(tableName));

            var tabular = store.GetTabular(tableName);
            return tabular.Query();
        }

        /// <summary>
        /// 从指定 DataCoreStore 创建图查询。
        /// </summary>
        /// <param name="store">DataCoreStore 实例</param>
        /// <param name="graphName">图数据集名称</param>
        /// <returns>可链式调用的 IGraphQuery</returns>
        /// <exception cref="KeyNotFoundException">图不存在</exception>
        public static IGraphQuery Graph(DataCoreStore store, string graphName)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (string.IsNullOrWhiteSpace(graphName))
                throw new ArgumentException("Graph name is required", nameof(graphName));

            var graph = store.GetGraph(graphName);
            return graph.Query();
        }

        /// <summary>
        /// 从指定 IDataStore 创建图查询。
        /// </summary>
        public static IGraphQuery Graph(IDataStore store, string graphName)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (string.IsNullOrWhiteSpace(graphName))
                throw new ArgumentException("Graph name is required", nameof(graphName));

            var graph = store.GetGraph(graphName);
            return graph.Query();
        }

        /// <summary>
        /// 从 ITabularDataset 直接创建查询（快捷方式）。
        /// </summary>
        public static ITabularQuery From(ITabularDataset dataset)
        {
            if (dataset == null) throw new ArgumentNullException(nameof(dataset));
            return dataset.Query();
        }

        /// <summary>
        /// 从 IGraphDataset 直接创建查询（快捷方式）。
        /// </summary>
        public static IGraphQuery From(IGraphDataset dataset)
        {
            if (dataset == null) throw new ArgumentNullException(nameof(dataset));
            return dataset.Query();
        }
    }
}
