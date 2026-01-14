using System;
using System.Collections.Generic;

namespace AroAro.DataCore
{
    /// <summary>
    /// 图数据集接口 - 提供图结构的操作
    /// </summary>
    public interface IGraphDataset : IDataSet
    {
        #region 属性

        /// <summary>
        /// 节点数量
        /// </summary>
        int NodeCount { get; }

        /// <summary>
        /// 边数量
        /// </summary>
        int EdgeCount { get; }

        #endregion

        #region 节点操作

        /// <summary>
        /// 添加节点
        /// </summary>
        void AddNode(string id, IDictionary<string, object> properties = null);

        /// <summary>
        /// 移除节点
        /// </summary>
        bool RemoveNode(string id);

        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        bool HasNode(string id);

        /// <summary>
        /// 获取节点属性
        /// </summary>
        IDictionary<string, object> GetNodeProperties(string id);

        /// <summary>
        /// 更新节点属性
        /// </summary>
        void UpdateNodeProperties(string id, IDictionary<string, object> properties);

        /// <summary>
        /// 获取所有节点 ID
        /// </summary>
        IEnumerable<string> GetNodeIds();

        #endregion

        #region 边操作

        /// <summary>
        /// 添加边
        /// </summary>
        void AddEdge(string fromId, string toId, IDictionary<string, object> properties = null);

        /// <summary>
        /// 移除边
        /// </summary>
        bool RemoveEdge(string fromId, string toId);

        /// <summary>
        /// 检查边是否存在
        /// </summary>
        bool HasEdge(string fromId, string toId);

        /// <summary>
        /// 获取边属性
        /// </summary>
        IDictionary<string, object> GetEdgeProperties(string fromId, string toId);

        /// <summary>
        /// 更新边属性
        /// </summary>
        void UpdateEdgeProperties(string fromId, string toId, IDictionary<string, object> properties);

        /// <summary>
        /// 获取所有边
        /// </summary>
        IEnumerable<(string From, string To)> GetEdges();

        #endregion

        #region 邻接查询

        /// <summary>
        /// 获取出边邻居
        /// </summary>
        IEnumerable<string> GetOutNeighbors(string nodeId);

        /// <summary>
        /// 获取入边邻居
        /// </summary>
        IEnumerable<string> GetInNeighbors(string nodeId);

        /// <summary>
        /// 获取所有邻居
        /// </summary>
        IEnumerable<string> GetNeighbors(string nodeId);

        /// <summary>
        /// 获取节点的出度
        /// </summary>
        int GetOutDegree(string nodeId);

        /// <summary>
        /// 获取节点的入度
        /// </summary>
        int GetInDegree(string nodeId);

        #endregion

        #region 图查询

        /// <summary>
        /// 创建图查询构建器
        /// </summary>
        IGraphQuery Query();

        #endregion

        #region 批量操作

        /// <summary>
        /// 批量添加节点
        /// </summary>
        int AddNodes(IEnumerable<(string Id, IDictionary<string, object> Properties)> nodes);

        /// <summary>
        /// 批量添加边
        /// </summary>
        int AddEdges(IEnumerable<(string From, string To, IDictionary<string, object> Properties)> edges);

        /// <summary>
        /// 清空所有数据
        /// </summary>
        void Clear();

        #endregion
    }

    /// <summary>
    /// 图查询接口
    /// </summary>
    public interface IGraphQuery
    {
        #region 节点过滤

        /// <summary>
        /// 过滤具有指定属性值的节点
        /// </summary>
        IGraphQuery WhereNodeProperty(string property, QueryOp op, object value);

        /// <summary>
        /// 过滤具有指定标签的节点
        /// </summary>
        IGraphQuery WhereNodeHasProperty(string property);

        #endregion

        #region 边过滤

        /// <summary>
        /// 过滤具有指定属性值的边
        /// </summary>
        IGraphQuery WhereEdgeProperty(string property, QueryOp op, object value);

        #endregion

        #region 遍历

        /// <summary>
        /// 从指定节点开始遍历
        /// </summary>
        IGraphQuery From(string nodeId);

        /// <summary>
        /// 遍历出边
        /// </summary>
        IGraphQuery TraverseOut();

        /// <summary>
        /// 遍历入边
        /// </summary>
        IGraphQuery TraverseIn();

        /// <summary>
        /// 限制遍历深度
        /// </summary>
        IGraphQuery MaxDepth(int depth);

        #endregion

        #region 执行

        /// <summary>
        /// 获取匹配的节点 ID
        /// </summary>
        IEnumerable<string> ToNodeIds();

        /// <summary>
        /// 获取匹配的边
        /// </summary>
        IEnumerable<(string From, string To)> ToEdges();

        /// <summary>
        /// 计算匹配节点数
        /// </summary>
        int CountNodes();

        /// <summary>
        /// 计算匹配边数
        /// </summary>
        int CountEdges();

        #endregion
    }
}
