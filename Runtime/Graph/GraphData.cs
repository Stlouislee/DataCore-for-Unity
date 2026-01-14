using System;
using System.Collections.Generic;
using System.Linq;

namespace AroAro.DataCore.Graph
{
    /// <summary>
    /// 内存中的图数据类 - 用于Session内部的图操作
    /// 这是一个轻量级的内存实现，不支持持久化
    /// </summary>
    public class GraphData : IGraphDataset
    {
        private readonly string _name;
        private readonly string _id;
        private readonly Dictionary<string, Dictionary<string, object>> _nodes = new();
        private readonly Dictionary<string, Dictionary<string, object>> _edgeProperties = new();
        private readonly Dictionary<string, HashSet<string>> _outAdjacency = new();
        private readonly Dictionary<string, HashSet<string>> _inAdjacency = new();

        public GraphData(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _id = Guid.NewGuid().ToString("N");
        }

        #region IDataSet Implementation

        public string Name => _name;
        public DataSetKind Kind => DataSetKind.Graph;
        public string Id => _id;

        public IDataSet WithName(string newName)
        {
            var copy = new GraphData(newName);
            
            // Copy nodes
            foreach (var kvp in _nodes)
            {
                copy._nodes[kvp.Key] = new Dictionary<string, object>(kvp.Value);
                copy._outAdjacency[kvp.Key] = new HashSet<string>(_outAdjacency[kvp.Key]);
                copy._inAdjacency[kvp.Key] = new HashSet<string>(_inAdjacency[kvp.Key]);
            }
            
            // Copy edge properties
            foreach (var kvp in _edgeProperties)
            {
                copy._edgeProperties[kvp.Key] = new Dictionary<string, object>(kvp.Value);
            }
            
            return copy;
        }

        #endregion

        #region IGraphDataset Implementation

        public int NodeCount => _nodes.Count;
        public int EdgeCount => _edgeProperties.Count;

        public void AddNode(string id, IDictionary<string, object> properties = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id));
            
            if (_nodes.ContainsKey(id))
                throw new ArgumentException($"Node '{id}' already exists");

            _nodes[id] = properties != null 
                ? new Dictionary<string, object>(properties) 
                : new Dictionary<string, object>();
            _outAdjacency[id] = new HashSet<string>();
            _inAdjacency[id] = new HashSet<string>();
        }

        public bool RemoveNode(string id)
        {
            if (!_nodes.ContainsKey(id))
                return false;

            // Remove all edges connected to this node
            var outEdgesToRemove = _outAdjacency[id].ToList();
            foreach (var toId in outEdgesToRemove)
            {
                RemoveEdge(id, toId);
            }

            var inEdgesToRemove = _inAdjacency[id].ToList();
            foreach (var fromId in inEdgesToRemove)
            {
                RemoveEdge(fromId, id);
            }

            _nodes.Remove(id);
            _outAdjacency.Remove(id);
            _inAdjacency.Remove(id);
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
            if (!_nodes.ContainsKey(id))
                throw new KeyNotFoundException($"Node '{id}' not found");

            foreach (var kvp in properties)
            {
                _nodes[id][kvp.Key] = kvp.Value;
            }
        }

        public IEnumerable<string> GetNodeIds() => _nodes.Keys;

        public void AddEdge(string fromId, string toId, IDictionary<string, object> properties = null)
        {
            if (!_nodes.ContainsKey(fromId))
                throw new ArgumentException($"Source node '{fromId}' not found");
            if (!_nodes.ContainsKey(toId))
                throw new ArgumentException($"Target node '{toId}' not found");

            var edgeKey = GetEdgeKey(fromId, toId);
            if (_edgeProperties.ContainsKey(edgeKey))
                throw new ArgumentException($"Edge from '{fromId}' to '{toId}' already exists");

            _edgeProperties[edgeKey] = properties != null 
                ? new Dictionary<string, object>(properties) 
                : new Dictionary<string, object>();
            _outAdjacency[fromId].Add(toId);
            _inAdjacency[toId].Add(fromId);
        }

        public bool RemoveEdge(string fromId, string toId)
        {
            var edgeKey = GetEdgeKey(fromId, toId);
            if (!_edgeProperties.ContainsKey(edgeKey))
                return false;

            _edgeProperties.Remove(edgeKey);
            _outAdjacency[fromId].Remove(toId);
            _inAdjacency[toId].Remove(fromId);
            return true;
        }

        public bool HasEdge(string fromId, string toId)
        {
            var edgeKey = GetEdgeKey(fromId, toId);
            return _edgeProperties.ContainsKey(edgeKey);
        }

        public IDictionary<string, object> GetEdgeProperties(string fromId, string toId)
        {
            var edgeKey = GetEdgeKey(fromId, toId);
            if (!_edgeProperties.TryGetValue(edgeKey, out var props))
                throw new KeyNotFoundException($"Edge from '{fromId}' to '{toId}' not found");
            return new Dictionary<string, object>(props);
        }

        public void UpdateEdgeProperties(string fromId, string toId, IDictionary<string, object> properties)
        {
            var edgeKey = GetEdgeKey(fromId, toId);
            if (!_edgeProperties.ContainsKey(edgeKey))
                throw new KeyNotFoundException($"Edge from '{fromId}' to '{toId}' not found");

            foreach (var kvp in properties)
            {
                _edgeProperties[edgeKey][kvp.Key] = kvp.Value;
            }
        }

        public IEnumerable<(string From, string To)> GetEdges()
        {
            return _edgeProperties.Keys.Select(ParseEdgeKey);
        }

        public IEnumerable<string> GetOutNeighbors(string nodeId)
        {
            if (!_outAdjacency.TryGetValue(nodeId, out var neighbors))
                throw new KeyNotFoundException($"Node '{nodeId}' not found");
            return neighbors;
        }

        public IEnumerable<string> GetInNeighbors(string nodeId)
        {
            if (!_inAdjacency.TryGetValue(nodeId, out var neighbors))
                throw new KeyNotFoundException($"Node '{nodeId}' not found");
            return neighbors;
        }

        public IEnumerable<string> GetNeighbors(string nodeId)
        {
            var outNeighbors = GetOutNeighbors(nodeId);
            var inNeighbors = GetInNeighbors(nodeId);
            return outNeighbors.Union(inNeighbors);
        }

        public int GetOutDegree(string nodeId)
        {
            if (!_outAdjacency.TryGetValue(nodeId, out var neighbors))
                throw new KeyNotFoundException($"Node '{nodeId}' not found");
            return neighbors.Count;
        }

        public int GetInDegree(string nodeId)
        {
            if (!_inAdjacency.TryGetValue(nodeId, out var neighbors))
                throw new KeyNotFoundException($"Node '{nodeId}' not found");
            return neighbors.Count;
        }

        public IGraphQuery Query()
        {
            return new InMemoryGraphQuery(this);
        }

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
            _edgeProperties.Clear();
            _outAdjacency.Clear();
            _inAdjacency.Clear();
        }

        #endregion

        #region Private Helper Methods

        private static string GetEdgeKey(string fromId, string toId) => $"{fromId}\0{toId}";

        private static (string From, string To) ParseEdgeKey(string key)
        {
            var parts = key.Split('\0');
            return (parts[0], parts[1]);
        }

        #endregion

        /// <summary>
        /// 简单的内存图查询实现
        /// </summary>
        private class InMemoryGraphQuery : IGraphQuery
        {
            private readonly GraphData _source;
            private string _startNode;
            private int _maxDepth = int.MaxValue;
            private bool _traverseOut = true;
            private bool _traverseIn = false;
            private List<Func<string, bool>> _nodeFilters = new();
            private List<Func<string, string, bool>> _edgeFilters = new();

            public InMemoryGraphQuery(GraphData source)
            {
                _source = source;
            }

            public IGraphQuery WhereNodeProperty(string property, QueryOp op, object value)
            {
                _nodeFilters.Add(nodeId =>
                {
                    var props = _source.GetNodeProperties(nodeId);
                    if (!props.TryGetValue(property, out var propValue))
                        return false;
                    return CompareValues(propValue, op, value);
                });
                return this;
            }

            public IGraphQuery WhereNodeHasProperty(string property)
            {
                _nodeFilters.Add(nodeId =>
                {
                    var props = _source.GetNodeProperties(nodeId);
                    return props.ContainsKey(property);
                });
                return this;
            }

            public IGraphQuery WhereEdgeProperty(string property, QueryOp op, object value)
            {
                _edgeFilters.Add((from, to) =>
                {
                    var props = _source.GetEdgeProperties(from, to);
                    if (!props.TryGetValue(property, out var propValue))
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
                _traverseIn = false;
                return this;
            }

            public IGraphQuery TraverseIn()
            {
                _traverseIn = true;
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
                if (string.IsNullOrEmpty(_startNode))
                {
                    var nodes = _source.GetNodeIds();
                    return ApplyNodeFilters(nodes);
                }

                return BFS();
            }

            public IEnumerable<(string From, string To)> ToEdges()
            {
                var edges = _source.GetEdges();
                return ApplyEdgeFilters(edges);
            }

            public int CountNodes() => ToNodeIds().Count();

            public int CountEdges() => ToEdges().Count();

            private IEnumerable<string> ApplyNodeFilters(IEnumerable<string> nodes)
            {
                foreach (var filter in _nodeFilters)
                {
                    nodes = nodes.Where(filter);
                }
                return nodes;
            }

            private IEnumerable<(string From, string To)> ApplyEdgeFilters(IEnumerable<(string From, string To)> edges)
            {
                foreach (var filter in _edgeFilters)
                {
                    edges = edges.Where(e => filter(e.From, e.To));
                }
                return edges;
            }

            private IEnumerable<string> BFS()
            {
                var visited = new HashSet<string> { _startNode };
                var queue = new Queue<(string node, int depth)>();
                queue.Enqueue((_startNode, 0));

                while (queue.Count > 0)
                {
                    var (current, depth) = queue.Dequeue();

                    // Apply node filters
                    bool passesFilter = _nodeFilters.All(f => f(current));
                    if (passesFilter)
                        yield return current;

                    if (depth >= _maxDepth)
                        continue;

                    IEnumerable<string> neighbors;
                    if (_traverseOut && _traverseIn)
                        neighbors = _source.GetNeighbors(current);
                    else if (_traverseOut)
                        neighbors = _source.GetOutNeighbors(current);
                    else
                        neighbors = _source.GetInNeighbors(current);

                    foreach (var neighbor in neighbors)
                    {
                        if (visited.Contains(neighbor))
                            continue;

                        // Check edge filters
                        bool edgePassesFilter = _edgeFilters.All(f => f(current, neighbor));
                        if (!edgePassesFilter)
                            continue;

                        visited.Add(neighbor);
                        queue.Enqueue((neighbor, depth + 1));
                    }
                }
            }

            private static bool CompareValues(object left, QueryOp op, object right)
            {
                if (left == null || right == null)
                    return false;

                return op switch
                {
                    QueryOp.Eq => Equals(left, right),
                    QueryOp.Ne => !Equals(left, right),
                    QueryOp.Gt => CompareNumeric(left, right) > 0,
                    QueryOp.Ge => CompareNumeric(left, right) >= 0,
                    QueryOp.Lt => CompareNumeric(left, right) < 0,
                    QueryOp.Le => CompareNumeric(left, right) <= 0,
                    QueryOp.Contains => left.ToString().Contains(right.ToString()),
                    QueryOp.StartsWith => left.ToString().StartsWith(right.ToString()),
                    QueryOp.EndsWith => left.ToString().EndsWith(right.ToString()),
                    _ => false
                };
            }

            private static int CompareNumeric(object left, object right)
            {
                var leftVal = Convert.ToDouble(left);
                var rightVal = Convert.ToDouble(right);
                return leftVal.CompareTo(rightVal);
            }
        }
    }
}
