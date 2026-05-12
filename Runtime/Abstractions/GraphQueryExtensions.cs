using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AroAro.DataCore
{
    /// <summary>
    /// IGraphQuery 扩展方法 — 提供 LINQ-like lambda 语法。
    /// </summary>
    /// <example>
    /// <code>
    /// var nodes = graph.Query()
    ///     .From("root")
    ///     .TraverseOut()
    ///     .MaxDepth(3)
    ///     .Where(node => node.Has("label") && node.Get&lt;string&gt;("label") == "Active")
    ///     .ToNodeIds();
    /// </code>
    /// </example>
    public static class GraphQueryExtensions
    {
        /// <summary>
        /// 使用 lambda 表达式过滤节点。
        /// <code>
        /// .Where(node => node.Get&lt;string&gt;("type") == "Person")
        /// </code>
        /// </summary>
        public static IGraphQuery Where(this IGraphQuery query, Func<QueryRow, bool> predicate)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            // 直接包装为 LambdaFilteredGraphQuery，不添加 dummy 过滤器
            return new LambdaFilteredGraphQuery(query, predicate);
        }

        /// <summary>
        /// 语义化：检查节点是否具有指定标签。
        /// 等价于 WhereNodeProperty("label", QueryOp.Eq, label)。
        /// </summary>
        public static IGraphQuery WhereHasLabel(this IGraphQuery query, string label)
        {
            return query.WhereNodeProperty("label", QueryOp.Eq, label);
        }

        /// <summary>
        /// 语义化：按边类型过滤遍历。
        /// 等价于 WhereEdgeProperty("type", QueryOp.Eq, edgeType)。
        /// </summary>
        public static IGraphQuery Traverse(this IGraphQuery query, string edgeType)
        {
            return query.WhereEdgeProperty("type", QueryOp.Eq, edgeType);
        }
    }

    /// <summary>
    /// 包装图查询 — 在现有查询之上叠加 lambda 节点过滤器。
    /// </summary>
    internal sealed class LambdaFilteredGraphQuery : IGraphQuery
    {
        private readonly IGraphQuery _inner;
        private readonly Func<QueryRow, bool> _predicate;

        public LambdaFilteredGraphQuery(IGraphQuery inner, Func<QueryRow, bool> predicate)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        public IGraphDataset Source => _inner.Source;

        // ── 节点过滤：继续叠加 ──
        public IGraphQuery WhereNodeProperty(string property, QueryOp op, object value)
            => new LambdaFilteredGraphQuery(_inner.WhereNodeProperty(property, op, value), _predicate);

        public IGraphQuery WhereNodeHasProperty(string property)
            => new LambdaFilteredGraphQuery(_inner.WhereNodeHasProperty(property), _predicate);

        // ── 边过滤：委托 ──
        public IGraphQuery WhereEdgeProperty(string property, QueryOp op, object value)
            => new LambdaFilteredGraphQuery(_inner.WhereEdgeProperty(property, op, value), _predicate);

        // ── 遍历：委托 ──
        public IGraphQuery From(string nodeId) => _inner.From(nodeId);
        public IGraphQuery TraverseOut() => _inner.TraverseOut();
        public IGraphQuery TraverseIn() => _inner.TraverseIn();
        public IGraphQuery MaxDepth(int depth) => _inner.MaxDepth(depth);

        // ── 执行：inner 执行后再用 lambda 过滤 ──

        public IEnumerable<string> ToNodeIds()
        {
            var source = _inner.Source;
            foreach (var nodeId in _inner.ToNodeIds())
            {
                // 获取节点属性，构建 QueryRow，应用 lambda 过滤
                if (source != null)
                {
                    var props = source.GetNodeProperties(nodeId);
                    var row = new QueryRow(new Dictionary<string, object>(props));
                    if (!_predicate(row))
                        continue;
                }
                yield return nodeId;
            }
        }

        public IEnumerable<(string From, string To)> ToEdges()
        {
            var source = _inner.Source;
            foreach (var edge in _inner.ToEdges())
            {
                // 获取边属性，构建 QueryRow，应用 lambda 过滤
                if (source != null)
                {
                    var props = source.GetEdgeProperties(edge.From, edge.To);
                    var row = new QueryRow(new Dictionary<string, object>(props));
                    if (!_predicate(row))
                        continue;
                }
                yield return edge;
            }
        }

        public int CountNodes() => ToNodeIds().Count();
        public int CountEdges() => ToEdges().Count();

        // ── 异步执行 ──

        public Task<IEnumerable<string>> ToNodeIdsAsync(CancellationToken ct = default)
            => Task.Run(() => ToNodeIds(), ct);

        public Task<IEnumerable<(string From, string To)>> ToEdgesAsync(CancellationToken ct = default)
            => Task.Run(() => ToEdges(), ct);

        public Task<int> CountNodesAsync(CancellationToken ct = default)
            => Task.Run(() => CountNodes(), ct);

        public Task<int> CountEdgesAsync(CancellationToken ct = default)
            => Task.Run(() => CountEdges(), ct);
    }
}
