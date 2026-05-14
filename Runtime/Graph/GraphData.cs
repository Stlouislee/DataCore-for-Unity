using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly Dictionary<string, string> _edgeTypes = new();
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
            
            // Copy nodes with properties
            foreach (var kvp in _nodes)
            {
                copy._nodes[kvp.Key] = new Dictionary<string, object>(kvp.Value);
            }
            
            // Copy edge properties, types and rebuild adjacency lists from edges
            foreach (var kvp in _edgeProperties)
            {
                var (from, to) = ParseEdgeKey(kvp.Key);
                
                // Only copy edges where both endpoints exist
                if (copy._nodes.ContainsKey(from) && copy._nodes.ContainsKey(to))
                {
                    copy._edgeProperties[kvp.Key] = new Dictionary<string, object>(kvp.Value);
                    if (_edgeTypes.TryGetValue(kvp.Key, out var edgeType))
                        copy._edgeTypes[kvp.Key] = edgeType;
                    
                    // Ensure adjacency lists exist for both nodes
                    if (!copy._outAdjacency.ContainsKey(from))
                        copy._outAdjacency[from] = new HashSet<string>();
                    if (!copy._inAdjacency.ContainsKey(to))
                        copy._inAdjacency[to] = new HashSet<string>();
                    
                    copy._outAdjacency[from].Add(to);
                    copy._inAdjacency[to].Add(from);
                }
            }
            
            // Ensure all nodes have adjacency entries (even isolated nodes)
            foreach (var nodeId in copy._nodes.Keys)
            {
                if (!copy._outAdjacency.ContainsKey(nodeId))
                    copy._outAdjacency[nodeId] = new HashSet<string>();
                if (!copy._inAdjacency.ContainsKey(nodeId))
                    copy._inAdjacency[nodeId] = new HashSet<string>();
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
            AddEdge(fromId, toId, null, properties);
        }

        public void AddEdge(string fromId, string toId, string edgeType, IDictionary<string, object> properties = null)
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
            _edgeTypes[edgeKey] = edgeType ?? string.Empty;
            _outAdjacency[fromId].Add(toId);
            _inAdjacency[toId].Add(fromId);
        }

        public bool RemoveEdge(string fromId, string toId)
        {
            var edgeKey = GetEdgeKey(fromId, toId);
            if (!_edgeProperties.ContainsKey(edgeKey))
                return false;

            _edgeProperties.Remove(edgeKey);
            _edgeTypes.Remove(edgeKey);
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
            return _edgeProperties.Keys.Select(ParseEdgeKey).ToList();
        }

        public IEnumerable<string> GetOutNeighbors(string nodeId)
        {
            if (!_outAdjacency.TryGetValue(nodeId, out var neighbors))
                throw new KeyNotFoundException($"Node '{nodeId}' not found");
            return neighbors;
        }

        public IEnumerable<string> GetOutNeighbors(string nodeId, string edgeType)
        {
            if (!_outAdjacency.TryGetValue(nodeId, out var neighbors))
                throw new KeyNotFoundException($"Node '{nodeId}' not found");
            var type = edgeType ?? string.Empty;
            return neighbors.Where(toId =>
            {
                var key = GetEdgeKey(nodeId, toId);
                return _edgeTypes.TryGetValue(key, out var t) && t == type;
            });
        }

        public IEnumerable<string> GetInNeighbors(string nodeId)
        {
            if (!_inAdjacency.TryGetValue(nodeId, out var neighbors))
                throw new KeyNotFoundException($"Node '{nodeId}' not found");
            return neighbors;
        }

        public IEnumerable<string> GetInNeighbors(string nodeId, string edgeType)
        {
            if (!_inAdjacency.TryGetValue(nodeId, out var neighbors))
                throw new KeyNotFoundException($"Node '{nodeId}' not found");
            var type = edgeType ?? string.Empty;
            return neighbors.Where(fromId =>
            {
                var key = GetEdgeKey(fromId, nodeId);
                return _edgeTypes.TryGetValue(key, out var t) && t == type;
            });
        }

        public IEnumerable<string> GetNeighbors(string nodeId)
        {
            var outNeighbors = GetOutNeighbors(nodeId);
            var inNeighbors = GetInNeighbors(nodeId);
            return outNeighbors.Union(inNeighbors);
        }

        public IEnumerable<string> GetNeighbors(string nodeId, string edgeType)
        {
            var outNeighbors = GetOutNeighbors(nodeId, edgeType);
            var inNeighbors = GetInNeighbors(nodeId, edgeType);
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
            var nodeList = nodes.ToList();
            // Phase 1: Validate all
            var seen = new HashSet<string>();
            foreach (var (id, _) in nodeList)
            {
                if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
                if (_nodes.ContainsKey(id) || !seen.Add(id))
                    throw new ArgumentException($"Duplicate node ID: {id}");
            }
            // Phase 2: Apply all
            foreach (var (id, props) in nodeList)
            {
                _nodes[id] = props != null ? new Dictionary<string, object>(props) : new Dictionary<string, object>();
                _outAdjacency[id] = new HashSet<string>();
                _inAdjacency[id] = new HashSet<string>();
            }
            return nodeList.Count;
        }

        public int AddEdges(IEnumerable<(string From, string To, IDictionary<string, object> Properties)> edges)
        {
            return AddEdges(edges.Select(e => (e.From, e.To, (string)null, e.Properties)));
        }

        public int AddEdges(IEnumerable<(string From, string To, string Type, IDictionary<string, object> Properties)> edges)
        {
            var edgeList = edges.ToList();
            // Phase 1: Validate all
            foreach (var (from, to, _, _) in edgeList)
            {
                if (!_nodes.ContainsKey(from)) throw new ArgumentException($"Source node '{from}' not found");
                if (!_nodes.ContainsKey(to)) throw new ArgumentException($"Target node '{to}' not found");
                var key = GetEdgeKey(from, to);
                if (_edgeProperties.ContainsKey(key))
                    throw new ArgumentException($"Edge from '{from}' to '{to}' already exists");
            }
            // Phase 2: Apply all
            foreach (var (from, to, edgeType, props) in edgeList)
            {
                var edgeKey = GetEdgeKey(from, to);
                _edgeProperties[edgeKey] = props != null ? new Dictionary<string, object>(props) : new Dictionary<string, object>();
                _edgeTypes[edgeKey] = edgeType ?? string.Empty;
                _outAdjacency[from].Add(to);
                _inAdjacency[to].Add(from);
            }
            return edgeList.Count;
        }

        public void Clear()
        {
            _nodes.Clear();
            _edgeProperties.Clear();
            _edgeTypes.Clear();
            _outAdjacency.Clear();
            _inAdjacency.Clear();
        }

        #endregion

        #region Private Helper Methods

        // Use a two-char separator that is extremely unlikely in node IDs.
        // \x01\x02 avoids the \0 issue (Android JNI treats \0 as C string terminator).
        private const string EdgeSeparator = "\x01\x02";

        private static string GetEdgeKey(string fromId, string toId)
        {
            // Escape any accidental occurrences of the separator in node IDs
            var safeFrom = fromId.Replace(EdgeSeparator, "\x01\x02\x01");
            var safeTo = toId.Replace(EdgeSeparator, "\x01\x02\x01");
            return $"{safeFrom}{EdgeSeparator}{safeTo}";
        }

        private static (string From, string To) ParseEdgeKey(string key)
        {
            var sepIndex = FindSeparator(key);
            var from = key.Substring(0, sepIndex).Replace("\x01\x02\x01", EdgeSeparator);
            var to = key.Substring(sepIndex + EdgeSeparator.Length).Replace("\x01\x02\x01", EdgeSeparator);
            return (from, to);
        }

        private static int FindSeparator(string key)
        {
            // Find the first unescaped separator
            for (int i = 0; i <= key.Length - EdgeSeparator.Length; i++)
            {
                if (key.Substring(i, EdgeSeparator.Length) == EdgeSeparator)
                {
                    // Check if it's an escaped separator (\x01\x02\x01)
                    if (i + EdgeSeparator.Length + 1 <= key.Length &&
                        key.Substring(i + EdgeSeparator.Length, 1) == "\x01")
                    {
                        i += EdgeSeparator.Length + 1; // skip escaped
                        continue;
                    }
                    return i;
                }
            }
            throw new FormatException("Invalid edge key format: separator not found");
        }

        #endregion

        #region 异步操作

        public Task AddNodeAsync(string id, IDictionary<string, object> properties = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            AddNode(id, properties);
            return Task.CompletedTask;
        }

        public Task<int> AddNodesAsync(IEnumerable<(string Id, IDictionary<string, object> Properties)> nodes, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(AddNodes(nodes));
        }

        public Task<IEnumerable<string>> GetOutNeighborsAsync(string nodeId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(GetOutNeighbors(nodeId));
        }

        public Task AddEdgeAsync(string fromId, string toId, IDictionary<string, object> properties = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            AddEdge(fromId, toId, properties);
            return Task.CompletedTask;
        }

        public Task<int> AddEdgesAsync(IEnumerable<(string From, string To, IDictionary<string, object> Properties)> edges, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(AddEdges(edges));
        }

        public Task<bool> RemoveNodeAsync(string id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(RemoveNode(id));
        }

        public Task ClearAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Clear();
            return Task.CompletedTask;
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

            public IGraphDataset Source => _source;

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

            public IGraphQuery WhereEdgeType(string edgeType)
            {
                var type = edgeType ?? string.Empty;
                _edgeFilters.Add((from, to) =>
                {
                    var key = GetEdgeKey(from, to);
                    return _source._edgeTypes.TryGetValue(key, out var t) && t == type;
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

                    // Apply node filters — nodes that fail filters are still traversed
                    // (neighbors are explored) but not yielded. This is intentional:
                    // filters control *output*, not traversal scope.
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

                        // Check edge filters — always pass (from, to) in edge-storage order
                        bool edgePassesFilter;
                        if (_traverseOut)
                            edgePassesFilter = _edgeFilters.All(f => f(current, neighbor));
                        else if (_traverseIn)
                            edgePassesFilter = _edgeFilters.All(f => f(neighbor, current));
                        else
                            edgePassesFilter = _edgeFilters.All(f => f(current, neighbor) || f(neighbor, current));

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

            private static bool TryConvertToDouble(object value, out double result)
            {
                result = 0;
                if (value == null) return false;
                if (value is double d) { result = d; return true; }
                if (value is int i) { result = i; return true; }
                if (value is float f) { result = f; return true; }
                if (value is long l) { result = l; return true; }
                if (value is string s) return double.TryParse(s, out result);
                try { result = Convert.ToDouble(value); return true; }
                catch { return false; }
            }

            private static int CompareNumeric(object left, object right)
            {
                if (!TryConvertToDouble(left, out var leftVal) ||
                    !TryConvertToDouble(right, out var rightVal))
                    return 0; // Incomparable values treated as equal
                return leftVal.CompareTo(rightVal);
            }
        }
    }
}
