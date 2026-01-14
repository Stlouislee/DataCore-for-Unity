using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace AroAro.DataCore.LiteDb
{
    /// <summary>
    /// LiteDB 图数据集实现
    /// </summary>
    public sealed class LiteDbGraphDataset : IGraphDataset
    {
        private readonly LiteDatabase _database;
        private readonly GraphMetadata _metadata;
        private readonly ILiteCollection<GraphNode> _nodes;
        private readonly ILiteCollection<GraphEdge> _edges;

        internal LiteDbGraphDataset(LiteDatabase database, GraphMetadata metadata)
        {
            _database = database;
            _metadata = metadata;
            _nodes = _database.GetCollection<GraphNode>($"graph_{metadata.Id}_nodes");
            _edges = _database.GetCollection<GraphEdge>($"graph_{metadata.Id}_edges");

            _nodes.EnsureIndex(n => n.NodeId, true);
            _edges.EnsureIndex(e => e.FromNodeId);
            _edges.EnsureIndex(e => e.ToNodeId);
        }

        #region IDataSet 实现

        public string Name => _metadata.Name;
        public DataSetKind Kind => DataSetKind.Graph;
        public string Id => _metadata.DatasetId;

        public IDataSet WithName(string name)
        {
            throw new NotSupportedException("Use IDataStore.CreateGraph and copy data instead");
        }

        #endregion

        #region IGraphDataset 实现

        public int NodeCount => _metadata.NodeCount;
        public int EdgeCount => _metadata.EdgeCount;
        public bool IsDirected => _metadata.IsDirected;

        #endregion

        #region 节点操作

        public void AddNode(string nodeId, IDictionary<string, object> properties = null)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Node ID cannot be null or empty", nameof(nodeId));

            if (_nodes.Exists(n => n.NodeId == nodeId))
                throw new InvalidOperationException($"Node '{nodeId}' already exists");

            var node = new GraphNode
            {
                NodeId = nodeId,
                Properties = ConvertToBsonDocument(properties)
            };

            _nodes.Insert(node);
            _metadata.NodeCount++;
            UpdateMetadata();
        }

        public int AddNodes(IEnumerable<(string nodeId, IDictionary<string, object> properties)> nodes)
        {
            int count = 0;
            var nodeList = nodes.Select(n => new GraphNode
            {
                NodeId = n.nodeId,
                Properties = ConvertToBsonDocument(n.properties)
            }).ToList();

            if (nodeList.Count > 0)
            {
                _nodes.InsertBulk(nodeList);
                _metadata.NodeCount += nodeList.Count;
                count = nodeList.Count;
                UpdateMetadata();
            }

            return count;
        }

        public bool UpdateNodeProperties(string nodeId, IDictionary<string, object> properties)
        {
            var node = _nodes.FindOne(n => n.NodeId == nodeId);
            if (node == null) return false;

            foreach (var kv in properties)
            {
                node.Properties[kv.Key] = ConvertToBsonValue(kv.Value);
            }

            _nodes.Update(node);
            UpdateMetadata();
            return true;
        }

        public bool RemoveNode(string nodeId)
        {
            var node = _nodes.FindOne(n => n.NodeId == nodeId);
            if (node == null) return false;

            // 删除相关边
            var relatedEdges = _edges.Find(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId).ToList();
            foreach (var edge in relatedEdges)
            {
                _edges.Delete(edge.Id);
            }
            _metadata.EdgeCount -= relatedEdges.Count;

            _nodes.Delete(node.Id);
            _metadata.NodeCount--;
            UpdateMetadata();
            return true;
        }

        public bool HasNode(string nodeId)
        {
            return _nodes.Exists(n => n.NodeId == nodeId);
        }

        public Dictionary<string, object> GetNodeProperties(string nodeId)
        {
            var node = _nodes.FindOne(n => n.NodeId == nodeId);
            if (node == null) return null;

            var result = new Dictionary<string, object>();
            foreach (var kv in node.Properties)
            {
                result[kv.Key] = ConvertFromBsonValue(kv.Value);
            }
            return result;
        }

        public IEnumerable<string> GetAllNodeIds()
        {
            return _nodes.FindAll().Select(n => n.NodeId);
        }

        public IEnumerable<(string nodeId, Dictionary<string, object> properties)> GetAllNodes()
        {
            foreach (var node in _nodes.FindAll())
            {
                var props = new Dictionary<string, object>();
                foreach (var kv in node.Properties)
                {
                    props[kv.Key] = ConvertFromBsonValue(kv.Value);
                }
                yield return (node.NodeId, props);
            }
        }

        #endregion

        #region 边操作

        public void AddEdge(string fromNodeId, string toNodeId, IDictionary<string, object> properties = null, double weight = 1.0)
        {
            if (string.IsNullOrWhiteSpace(fromNodeId))
                throw new ArgumentException("From node ID cannot be null or empty", nameof(fromNodeId));
            if (string.IsNullOrWhiteSpace(toNodeId))
                throw new ArgumentException("To node ID cannot be null or empty", nameof(toNodeId));

            if (!HasNode(fromNodeId))
                throw new InvalidOperationException($"Node '{fromNodeId}' does not exist");
            if (!HasNode(toNodeId))
                throw new InvalidOperationException($"Node '{toNodeId}' does not exist");

            var edge = new GraphEdge
            {
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                Weight = weight,
                Properties = ConvertToBsonDocument(properties)
            };

            _edges.Insert(edge);
            _metadata.EdgeCount++;
            UpdateMetadata();
        }

        public int AddEdges(IEnumerable<(string from, string to, IDictionary<string, object> properties, double weight)> edges)
        {
            int count = 0;
            var edgeList = edges.Select(e => new GraphEdge
            {
                FromNodeId = e.from,
                ToNodeId = e.to,
                Weight = e.weight,
                Properties = ConvertToBsonDocument(e.properties)
            }).ToList();

            if (edgeList.Count > 0)
            {
                _edges.InsertBulk(edgeList);
                _metadata.EdgeCount += edgeList.Count;
                count = edgeList.Count;
                UpdateMetadata();
            }

            return count;
        }

        public bool RemoveEdge(string fromNodeId, string toNodeId)
        {
            var edge = _edges.FindOne(e => e.FromNodeId == fromNodeId && e.ToNodeId == toNodeId);
            if (edge == null) return false;

            _edges.Delete(edge.Id);
            _metadata.EdgeCount--;
            UpdateMetadata();
            return true;
        }

        public bool HasEdge(string fromNodeId, string toNodeId)
        {
            if (IsDirected)
            {
                return _edges.Exists(e => e.FromNodeId == fromNodeId && e.ToNodeId == toNodeId);
            }
            else
            {
                return _edges.Exists(e =>
                    (e.FromNodeId == fromNodeId && e.ToNodeId == toNodeId) ||
                    (e.FromNodeId == toNodeId && e.ToNodeId == fromNodeId));
            }
        }

        public double GetEdgeWeight(string fromNodeId, string toNodeId)
        {
            var edge = _edges.FindOne(e => e.FromNodeId == fromNodeId && e.ToNodeId == toNodeId);
            return edge?.Weight ?? 0.0;
        }

        public Dictionary<string, object> GetEdgeProperties(string fromNodeId, string toNodeId)
        {
            var edge = _edges.FindOne(e => e.FromNodeId == fromNodeId && e.ToNodeId == toNodeId);
            if (edge == null) return null;

            var result = new Dictionary<string, object>();
            foreach (var kv in edge.Properties)
            {
                result[kv.Key] = ConvertFromBsonValue(kv.Value);
            }
            return result;
        }

        public IEnumerable<(string from, string to, double weight, Dictionary<string, object> properties)> GetAllEdges()
        {
            foreach (var edge in _edges.FindAll())
            {
                var props = new Dictionary<string, object>();
                foreach (var kv in edge.Properties)
                {
                    props[kv.Key] = ConvertFromBsonValue(kv.Value);
                }
                yield return (edge.FromNodeId, edge.ToNodeId, edge.Weight, props);
            }
        }

        #endregion

        #region 邻居查询

        public IEnumerable<string> GetOutNeighbors(string nodeId)
        {
            return _edges.Find(e => e.FromNodeId == nodeId).Select(e => e.ToNodeId).Distinct();
        }

        public IEnumerable<string> GetInNeighbors(string nodeId)
        {
            return _edges.Find(e => e.ToNodeId == nodeId).Select(e => e.FromNodeId).Distinct();
        }

        public IEnumerable<string> GetAllNeighbors(string nodeId)
        {
            var outNeighbors = GetOutNeighbors(nodeId);
            var inNeighbors = GetInNeighbors(nodeId);
            return outNeighbors.Union(inNeighbors).Distinct();
        }

        public int GetOutDegree(string nodeId)
        {
            return _edges.Count(e => e.FromNodeId == nodeId);
        }

        public int GetInDegree(string nodeId)
        {
            return _edges.Count(e => e.ToNodeId == nodeId);
        }

        public int GetDegree(string nodeId)
        {
            if (IsDirected)
            {
                return GetOutDegree(nodeId) + GetInDegree(nodeId);
            }
            else
            {
                return _edges.Count(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId);
            }
        }

        #endregion

        #region 图算法

        public IEnumerable<string> BreadthFirstSearch(string startNodeId)
        {
            if (!HasNode(startNodeId))
                throw new ArgumentException($"Node '{startNodeId}' not found");

            var visited = new HashSet<string>();
            var queue = new Queue<string>();

            queue.Enqueue(startNodeId);
            visited.Add(startNodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                yield return current;

                foreach (var neighbor in GetAllNeighbors(current))
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        public IEnumerable<string> DepthFirstSearch(string startNodeId)
        {
            if (!HasNode(startNodeId))
                throw new ArgumentException($"Node '{startNodeId}' not found");

            var visited = new HashSet<string>();
            var stack = new Stack<string>();

            stack.Push(startNodeId);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (visited.Contains(current))
                    continue;

                visited.Add(current);
                yield return current;

                foreach (var neighbor in GetAllNeighbors(current).Reverse())
                {
                    if (!visited.Contains(neighbor))
                    {
                        stack.Push(neighbor);
                    }
                }
            }
        }

        public (bool found, IEnumerable<string> path) FindShortestPath(string fromNodeId, string toNodeId)
        {
            if (!HasNode(fromNodeId) || !HasNode(toNodeId))
                return (false, null);

            if (fromNodeId == toNodeId)
                return (true, new[] { fromNodeId });

            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            var parent = new Dictionary<string, string>();

            queue.Enqueue(fromNodeId);
            visited.Add(fromNodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var neighbor in GetAllNeighbors(current))
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        parent[neighbor] = current;
                        queue.Enqueue(neighbor);

                        if (neighbor == toNodeId)
                        {
                            var path = ReconstructPath(parent, fromNodeId, toNodeId);
                            return (true, path);
                        }
                    }
                }
            }

            return (false, null);
        }

        #endregion

        #region 查询

        public IGraphQuery Query() => new LiteDbGraphQuery(this);

        #endregion

        #region 清空

        public void Clear()
        {
            _nodes.DeleteAll();
            _edges.DeleteAll();
            _metadata.NodeCount = 0;
            _metadata.EdgeCount = 0;
            UpdateMetadata();
        }

        #endregion

        #region 内部方法

        internal IEnumerable<GraphNode> GetAllNodesInternal() => _nodes.FindAll();
        internal IEnumerable<GraphEdge> GetAllEdgesInternal() => _edges.FindAll();

        private void UpdateMetadata()
        {
            _metadata.ModifiedAt = DateTime.UtcNow;
            var meta = _database.GetCollection<GraphMetadata>("graph_meta");
            meta.Update(_metadata);
        }

        private BsonDocument ConvertToBsonDocument(IDictionary<string, object> values)
        {
            var doc = new BsonDocument();
            if (values != null)
            {
                foreach (var kv in values)
                {
                    doc[kv.Key] = ConvertToBsonValue(kv.Value);
                }
            }
            return doc;
        }

        private BsonValue ConvertToBsonValue(object value)
        {
            if (value == null) return BsonValue.Null;
            return value switch
            {
                double d => new BsonValue(d),
                float f => new BsonValue(f),
                int i => new BsonValue(i),
                long l => new BsonValue(l),
                bool b => new BsonValue(b),
                DateTime dt => new BsonValue(dt),
                string s => new BsonValue(s),
                _ => new BsonValue(value.ToString())
            };
        }

        private object ConvertFromBsonValue(BsonValue value)
        {
            if (value.IsNull) return null;
            if (value.IsDouble) return value.AsDouble;
            if (value.IsInt32) return value.AsInt32;
            if (value.IsInt64) return value.AsInt64;
            if (value.IsBoolean) return value.AsBoolean;
            if (value.IsDateTime) return value.AsDateTime;
            if (value.IsString) return value.AsString;
            return value.ToString();
        }

        private List<string> ReconstructPath(Dictionary<string, string> parent, string from, string to)
        {
            var path = new List<string>();
            var current = to;
            while (current != from)
            {
                path.Add(current);
                current = parent[current];
            }
            path.Add(from);
            path.Reverse();
            return path;
        }

        #endregion
    }
}
