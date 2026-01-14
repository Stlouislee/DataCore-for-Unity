using System;
using System.Collections.Generic;
using System.Linq;

namespace AroAro.DataCore.Memory
{
    /// <summary>
    /// 内存图数据集实现
    /// </summary>
    public sealed class MemoryGraphDataset : IGraphDataset
    {
        private readonly string _name;
        private readonly string _id;
        private readonly Dictionary<string, Dictionary<string, object>> _nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<(string From, string To), Dictionary<string, object>> _edges = new();

        internal MemoryGraphDataset(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _id = Guid.NewGuid().ToString("N");
        }

        #region IDataSet 实现

        public string Name => _name;
        public DataSetKind Kind => DataSetKind.Graph;
        public string Id => _id;

        public IDataSet WithName(string name)
        {
            var clone = new MemoryGraphDataset(name);
            foreach (var kv in _nodes)
                clone._nodes[kv.Key] = new Dictionary<string, object>(kv.Value);
            foreach (var kv in _edges)
                clone._edges[kv.Key] = new Dictionary<string, object>(kv.Value);
            return clone;
        }

        #endregion

        #region IGraphDataset 实现

        public int NodeCount => _nodes.Count;
        public int EdgeCount => _edges.Count;

        #endregion

        #region 节点操作

        public void AddNode(string id, IDictionary<string, object> properties = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Node id is required", nameof(id));
            if (_nodes.ContainsKey(id))
                throw new InvalidOperationException($"Node '{id}' already exists");

            _nodes[id] = properties != null
                ? new Dictionary<string, object>(properties)
                : new Dictionary<string, object>();
        }

        public bool RemoveNode(string id)
        {
            if (!_nodes.Remove(id)) return false;

            // 移除相关的边
            var edgesToRemove = _edges.Keys.Where(k => k.From == id || k.To == id).ToList();
            foreach (var key in edgesToRemove)
                _edges.Remove(key);

            return true;
        }

        public bool HasNode(string id) => _nodes.ContainsKey(id);

        public IDictionary<string, object> GetNodeProperties(string id)
        {
            if (!_nodes.TryGetValue(id, out var props))
                throw new KeyNotFoundException($"Node '{id}' not found");
            return new Dictionary<string, object>(props);
        }

        public void UpdateNodeProperties(string id, IDictionary<string, object> properties)
        {
            if (!_nodes.TryGetValue(id, out var props))
                throw new KeyNotFoundException($"Node '{id}' not found");

            foreach (var kv in properties)
                props[kv.Key] = kv.Value;
        }

        public IEnumerable<string> GetNodeIds() => _nodes.Keys;

        #endregion

        #region 边操作

        public void AddEdge(string fromId, string toId, IDictionary<string, object> properties = null)
        {
            if (string.IsNullOrWhiteSpace(fromId))
                throw new ArgumentException("From node id is required", nameof(fromId));
            if (string.IsNullOrWhiteSpace(toId))
                throw new ArgumentException("To node id is required", nameof(toId));
            if (!_nodes.ContainsKey(fromId))
                throw new InvalidOperationException($"Node '{fromId}' not found");
            if (!_nodes.ContainsKey(toId))
                throw new InvalidOperationException($"Node '{toId}' not found");

            var key = (fromId, toId);
            if (_edges.ContainsKey(key))
                throw new InvalidOperationException($"Edge '{fromId}' -> '{toId}' already exists");

            _edges[key] = properties != null
                ? new Dictionary<string, object>(properties)
                : new Dictionary<string, object>();
        }

        public bool RemoveEdge(string fromId, string toId)
        {
            return _edges.Remove((fromId, toId));
        }

        public bool HasEdge(string fromId, string toId)
        {
            return _edges.ContainsKey((fromId, toId));
        }

        public IDictionary<string, object> GetEdgeProperties(string fromId, string toId)
        {
            if (!_edges.TryGetValue((fromId, toId), out var props))
                throw new KeyNotFoundException($"Edge '{fromId}' -> '{toId}' not found");
            return new Dictionary<string, object>(props);
        }

        public void UpdateEdgeProperties(string fromId, string toId, IDictionary<string, object> properties)
        {
            if (!_edges.TryGetValue((fromId, toId), out var props))
                throw new KeyNotFoundException($"Edge '{fromId}' -> '{toId}' not found");

            foreach (var kv in properties)
                props[kv.Key] = kv.Value;
        }

        public IEnumerable<(string From, string To)> GetEdges() => _edges.Keys;

        #endregion

        #region 邻接查询

        public IEnumerable<string> GetOutNeighbors(string nodeId)
        {
            return _edges.Keys.Where(k => k.From == nodeId).Select(k => k.To);
        }

        public IEnumerable<string> GetInNeighbors(string nodeId)
        {
            return _edges.Keys.Where(k => k.To == nodeId).Select(k => k.From);
        }

        public IEnumerable<string> GetNeighbors(string nodeId)
        {
            return GetOutNeighbors(nodeId).Concat(GetInNeighbors(nodeId)).Distinct();
        }

        public int GetOutDegree(string nodeId) => GetOutNeighbors(nodeId).Count();

        public int GetInDegree(string nodeId) => GetInNeighbors(nodeId).Count();

        #endregion

        #region 图查询

        public IGraphQuery Query() => new MemoryGraphQuery(this);

        #endregion

        #region 批量操作

        public int AddNodes(IEnumerable<(string Id, IDictionary<string, object> Properties)> nodes)
        {
            int count = 0;
            foreach (var (id, props) in nodes)
            {
                AddNode(id, props);
                count++;
            }
            return count;
        }

        public int AddEdges(IEnumerable<(string From, string To, IDictionary<string, object> Properties)> edges)
        {
            int count = 0;
            foreach (var (from, to, props) in edges)
            {
                AddEdge(from, to, props);
                count++;
            }
            return count;
        }

        public void Clear()
        {
            _nodes.Clear();
            _edges.Clear();
        }

        #endregion

        #region 内部方法

        internal Dictionary<string, object> GetNodePropertiesInternal(string id)
        {
            return _nodes.TryGetValue(id, out var props) ? props : null;
        }

        internal Dictionary<string, object> GetEdgePropertiesInternal(string fromId, string toId)
        {
            return _edges.TryGetValue((fromId, toId), out var props) ? props : null;
        }

        #endregion
    }

    /// <summary>
    /// 内存图查询实现
    /// </summary>
    public sealed class MemoryGraphQuery : IGraphQuery
    {
        private readonly MemoryGraphDataset _graph;
        private readonly List<Func<string, bool>> _nodePredicates = new();
        private readonly List<Func<(string, string), bool>> _edgePredicates = new();
        private string _startNode;
        private bool? _traverseOut;
        private int? _maxDepth;

        internal MemoryGraphQuery(MemoryGraphDataset graph)
        {
            _graph = graph;
        }

        public IGraphQuery WhereNodeProperty(string property, QueryOp op, object value)
        {
            _nodePredicates.Add(nodeId =>
            {
                var props = _graph.GetNodePropertiesInternal(nodeId);
                if (props == null || !props.TryGetValue(property, out var propValue))
                    return false;
                return CompareValues(propValue, op, value);
            });
            return this;
        }

        public IGraphQuery WhereNodeHasProperty(string property)
        {
            _nodePredicates.Add(nodeId =>
            {
                var props = _graph.GetNodePropertiesInternal(nodeId);
                return props != null && props.ContainsKey(property);
            });
            return this;
        }

        public IGraphQuery WhereEdgeProperty(string property, QueryOp op, object value)
        {
            _edgePredicates.Add(edge =>
            {
                var props = _graph.GetEdgePropertiesInternal(edge.Item1, edge.Item2);
                if (props == null || !props.TryGetValue(property, out var propValue))
                    return false;
                return CompareValues(propValue, op, value);
            });
            return this;
        }

        public IGraphQuery From(string nodeId)
        {
            _startNode = nodeId;
            return this;
        }

        public IGraphQuery TraverseOut()
        {
            _traverseOut = true;
            return this;
        }

        public IGraphQuery TraverseIn()
        {
            _traverseOut = false;
            return this;
        }

        public IGraphQuery MaxDepth(int depth)
        {
            _maxDepth = depth;
            return this;
        }

        public IEnumerable<string> ToNodeIds()
        {
            IEnumerable<string> nodes;

            if (_startNode != null && _traverseOut.HasValue)
            {
                nodes = TraverseNodes(_startNode, _traverseOut.Value, _maxDepth ?? int.MaxValue);
            }
            else
            {
                nodes = _graph.GetNodeIds();
            }

            return nodes.Where(n => _nodePredicates.All(p => p(n)));
        }

        public IEnumerable<(string From, string To)> ToEdges()
        {
            return _graph.GetEdges().Where(e => _edgePredicates.All(p => p(e)));
        }

        public int CountNodes() => ToNodeIds().Count();

        public int CountEdges() => ToEdges().Count();

        private IEnumerable<string> TraverseNodes(string start, bool outward, int maxDepth)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<(string Node, int Depth)>();
            queue.Enqueue((start, 0));

            while (queue.Count > 0)
            {
                var (node, depth) = queue.Dequeue();
                if (visited.Contains(node) || depth > maxDepth) continue;

                visited.Add(node);
                yield return node;

                var neighbors = outward ? _graph.GetOutNeighbors(node) : _graph.GetInNeighbors(node);
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                        queue.Enqueue((neighbor, depth + 1));
                }
            }
        }

        private bool CompareValues(object left, QueryOp op, object right)
        {
            return op switch
            {
                QueryOp.Eq => Equals(left, right),
                QueryOp.Ne => !Equals(left, right),
                QueryOp.Gt => Convert.ToDouble(left) > Convert.ToDouble(right),
                QueryOp.Ge => Convert.ToDouble(left) >= Convert.ToDouble(right),
                QueryOp.Lt => Convert.ToDouble(left) < Convert.ToDouble(right),
                QueryOp.Le => Convert.ToDouble(left) <= Convert.ToDouble(right),
                QueryOp.Contains => (left?.ToString() ?? "").Contains(right?.ToString() ?? ""),
                QueryOp.StartsWith => (left?.ToString() ?? "").StartsWith(right?.ToString() ?? ""),
                _ => false
            };
        }
    }
}
