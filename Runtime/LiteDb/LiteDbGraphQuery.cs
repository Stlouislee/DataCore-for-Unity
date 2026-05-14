using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace AroAro.DataCore.LiteDb
{
    /// <summary>
    /// LiteDB 图查询实现
    /// </summary>
    public sealed class LiteDbGraphQuery : IGraphQuery
    {
        private readonly LiteDbGraphDataset _dataset;
        private readonly List<Func<GraphNode, bool>> _nodeFilters;
        private readonly List<Func<GraphEdge, bool>> _edgeFilters;
        private string _startNodeId;
        private bool _traverseOut;
        private bool _traverseIn;
        private int _maxDepth = int.MaxValue;
        private bool _useDFS;

        internal LiteDbGraphQuery(LiteDbGraphDataset dataset)
        {
            _dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
            _nodeFilters = new List<Func<GraphNode, bool>>();
            _edgeFilters = new List<Func<GraphEdge, bool>>();
        }

        public IGraphDataset Source => _dataset;

        #region IGraphQuery 实现 - 节点过滤

        public IGraphQuery WhereNodeProperty(string property, QueryOp op, object value)
        {
            _nodeFilters.Add(node =>
            {
                if (!node.Properties.TryGetValue(property, out var bsonValue))
                    return false;
                return BsonValueComparer.Evaluate(bsonValue, op, value);
            });
            return this;
        }

        public IGraphQuery WhereNodeHasProperty(string property)
        {
            _nodeFilters.Add(node => node.Properties.ContainsKey(property) && !node.Properties[property].IsNull);
            return this;
        }

        #endregion

        #region IGraphQuery 实现 - 边过滤

        public IGraphQuery WhereEdgeProperty(string property, QueryOp op, object value)
        {
            _edgeFilters.Add(edge =>
            {
                if (!edge.Properties.TryGetValue(property, out var bsonValue))
                    return false;
                return BsonValueComparer.Evaluate(bsonValue, op, value);
            });
            return this;
        }

        public IGraphQuery WhereEdgeType(string edgeType)
        {
            var type = edgeType ?? string.Empty;
            _edgeFilters.Add(edge => edge.Type == type);
            return this;
        }

        #endregion

        #region IGraphQuery 实现 - 遍历

        public IGraphQuery From(string nodeId)
        {
            _startNodeId = nodeId;
            return this;
        }

        public IGraphQuery TraverseOut()
        {
            _traverseOut = true;
            return this;
        }

        public IGraphQuery TraverseIn()
        {
            _traverseIn = true;
            return this;
        }

        public IGraphQuery MaxDepth(int depth)
        {
            _maxDepth = depth;
            return this;
        }

        public IGraphQuery UseBFS()
        {
            _useDFS = false;
            return this;
        }

        public IGraphQuery UseDFS()
        {
            _useDFS = true;
            return this;
        }

        #endregion

        #region IGraphQuery 实现 - 执行

        public IEnumerable<string> ToNodeIds()
        {
            if (!string.IsNullOrEmpty(_startNodeId))
            {
                return TraverseFromNode();
            }

            return ExecuteNodeFilters().Select(n => n.NodeId);
        }

        public IEnumerable<(string From, string To)> ToEdges()
        {
            return ExecuteEdgeFilters().Select(e => (e.FromNodeId, e.ToNodeId));
        }

        public int CountNodes()
        {
            if (!string.IsNullOrEmpty(_startNodeId))
            {
                return TraverseFromNode().Count();
            }
            return ExecuteNodeFilters().Count();
        }

        public int CountEdges()
        {
            return ExecuteEdgeFilters().Count();
        }

        #endregion

        #region 内部方法

        private IEnumerable<string> TraverseFromNode()
        {
            if (!_dataset.HasNode(_startNodeId))
                yield break;

            // Pre-load all nodes into dictionary for O(1) lookup
            var nodeMap = _dataset.GetAllNodesInternal()
                .ToDictionary(n => n.NodeId, n => n);

            if (_useDFS)
            {
                // DFS using explicit stack
                var visited = new HashSet<string>();
                var stack = new Stack<(string nodeId, int depth)>();
                stack.Push((_startNodeId, 0));
                visited.Add(_startNodeId);

                while (stack.Count > 0)
                {
                    var (current, depth) = stack.Pop();

                    if (nodeMap.TryGetValue(current, out var node))
                    {
                        bool passesFilter = true;
                        foreach (var filter in _nodeFilters)
                        {
                            if (!filter(node))
                            {
                                passesFilter = false;
                                break;
                            }
                        }
                        if (passesFilter)
                            yield return current;
                    }

                    if (depth >= _maxDepth)
                        continue;

                    // Push neighbors in reverse order so first neighbor is processed first
                    var neighbors = GetNeighborNodes(current).Distinct().Reverse().ToList();
                    foreach (var neighbor in neighbors)
                    {
                        if (visited.Contains(neighbor))
                            continue;

                        if (!PassesEdgeFilter(current, neighbor))
                            continue;

                        visited.Add(neighbor);
                        stack.Push((neighbor, depth + 1));
                    }
                }
            }
            else
            {
                // BFS using queue
                var visited = new HashSet<string>();
                var queue = new Queue<(string nodeId, int depth)>();
                queue.Enqueue((_startNodeId, 0));
                visited.Add(_startNodeId);

                while (queue.Count > 0)
                {
                    var (current, depth) = queue.Dequeue();

                    if (nodeMap.TryGetValue(current, out var node))
                    {
                        bool passesFilter = true;
                        foreach (var filter in _nodeFilters)
                        {
                            if (!filter(node))
                            {
                                passesFilter = false;
                                break;
                            }
                        }
                        if (passesFilter)
                            yield return current;
                    }

                    if (depth >= _maxDepth)
                        continue;

                    foreach (var neighbor in GetNeighborNodes(current).Distinct())
                    {
                        if (visited.Contains(neighbor))
                            continue;

                        if (!PassesEdgeFilter(current, neighbor))
                            continue;

                        visited.Add(neighbor);
                        queue.Enqueue((neighbor, depth + 1));
                    }
                }
            }
        }

        private IEnumerable<string> GetNeighborNodes(string nodeId)
        {
            if (_traverseOut)
                foreach (var n in _dataset.GetOutNeighbors(nodeId))
                    yield return n;
            if (_traverseIn)
                foreach (var n in _dataset.GetInNeighbors(nodeId))
                    yield return n;
            if (!_traverseOut && !_traverseIn)
                foreach (var n in _dataset.GetNeighbors(nodeId))
                    yield return n;
        }

        private bool PassesEdgeFilter(string current, string neighbor)
        {
            if (_edgeFilters.Count == 0)
                return true;

            GraphEdge edge = null;
            if (_traverseOut && !_traverseIn)
                edge = _dataset.FindEdgeInternal(current, neighbor);
            else if (_traverseIn && !_traverseOut)
                edge = _dataset.FindEdgeInternal(neighbor, current);
            else
                edge = _dataset.FindEdgeInternal(current, neighbor)
                    ?? _dataset.FindEdgeInternal(neighbor, current);

            if (edge == null)
                return false;

            return _edgeFilters.All(f => f(edge));
        }

        private IEnumerable<GraphNode> ExecuteNodeFilters()
        {
            IEnumerable<GraphNode> nodes = _dataset.GetAllNodesInternal();

            foreach (var filter in _nodeFilters)
            {
                nodes = nodes.Where(filter);
            }

            return nodes;
        }

        private IEnumerable<GraphEdge> ExecuteEdgeFilters()
        {
            IEnumerable<GraphEdge> edges = _dataset.GetAllEdgesInternal();

            foreach (var filter in _edgeFilters)
            {
                edges = edges.Where(filter);
            }

            return edges;
        }



        #endregion
    }
}
