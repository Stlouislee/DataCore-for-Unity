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

        #endregion

        #region 节点操作

        public void AddNode(string id, IDictionary<string, object> properties = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Node ID cannot be null or empty", nameof(id));

            if (_nodes.Exists(n => n.NodeId == id))
                throw new InvalidOperationException($"Node '{id}' already exists");

            var node = new GraphNode
            {
                NodeId = id,
                Properties = ConvertToBsonDocument(properties)
            };

            _nodes.Insert(node);
            _metadata.NodeCount++;
            UpdateMetadata();
        }

        public int AddNodes(IEnumerable<(string Id, IDictionary<string, object> Properties)> nodes)
        {
            int count = 0;
            var nodeList = nodes.Select(n => new GraphNode
            {
                NodeId = n.Id,
                Properties = ConvertToBsonDocument(n.Properties)
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

        public void UpdateNodeProperties(string id, IDictionary<string, object> properties)
        {
            var node = _nodes.FindOne(n => n.NodeId == id);
            if (node == null) 
                throw new KeyNotFoundException($"Node '{id}' not found");

            foreach (var kv in properties)
            {
                node.Properties[kv.Key] = ConvertToBsonValue(kv.Value);
            }

            _nodes.Update(node);
            UpdateMetadata();
        }

        public bool RemoveNode(string id)
        {
            var node = _nodes.FindOne(n => n.NodeId == id);
            if (node == null) return false;

            // 删除相关边
            var relatedEdges = _edges.Find(e => e.FromNodeId == id || e.ToNodeId == id).ToList();
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

        public bool HasNode(string id)
        {
            return _nodes.Exists(n => n.NodeId == id);
        }

        public IDictionary<string, object> GetNodeProperties(string id)
        {
            var node = _nodes.FindOne(n => n.NodeId == id);
            if (node == null) return null;

            var result = new Dictionary<string, object>();
            foreach (var kv in node.Properties)
            {
                result[kv.Key] = ConvertFromBsonValue(kv.Value);
            }
            return result;
        }

        public IEnumerable<string> GetNodeIds()
        {
            return _nodes.FindAll().Select(n => n.NodeId);
        }

        #endregion

        #region 边操作

        public void AddEdge(string fromId, string toId, IDictionary<string, object> properties = null)
        {
            if (string.IsNullOrWhiteSpace(fromId))
                throw new ArgumentException("From node ID cannot be null or empty", nameof(fromId));
            if (string.IsNullOrWhiteSpace(toId))
                throw new ArgumentException("To node ID cannot be null or empty", nameof(toId));

            if (!HasNode(fromId))
                throw new InvalidOperationException($"Node '{fromId}' does not exist");
            if (!HasNode(toId))
                throw new InvalidOperationException($"Node '{toId}' does not exist");

            var edge = new GraphEdge
            {
                FromNodeId = fromId,
                ToNodeId = toId,
                Weight = 1.0,
                Properties = ConvertToBsonDocument(properties)
            };

            _edges.Insert(edge);
            _metadata.EdgeCount++;
            UpdateMetadata();
        }

        public int AddEdges(IEnumerable<(string From, string To, IDictionary<string, object> Properties)> edges)
        {
            int count = 0;
            var edgeList = edges.Select(e => new GraphEdge
            {
                FromNodeId = e.From,
                ToNodeId = e.To,
                Weight = 1.0,
                Properties = ConvertToBsonDocument(e.Properties)
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

        public bool RemoveEdge(string fromId, string toId)
        {
            var edge = _edges.FindOne(e => e.FromNodeId == fromId && e.ToNodeId == toId);
            if (edge == null) return false;

            _edges.Delete(edge.Id);
            _metadata.EdgeCount--;
            UpdateMetadata();
            return true;
        }

        public bool HasEdge(string fromId, string toId)
        {
            return _edges.Exists(e => e.FromNodeId == fromId && e.ToNodeId == toId);
        }

        public IDictionary<string, object> GetEdgeProperties(string fromId, string toId)
        {
            var edge = _edges.FindOne(e => e.FromNodeId == fromId && e.ToNodeId == toId);
            if (edge == null) return null;

            var result = new Dictionary<string, object>();
            foreach (var kv in edge.Properties)
            {
                result[kv.Key] = ConvertFromBsonValue(kv.Value);
            }
            return result;
        }

        public void UpdateEdgeProperties(string fromId, string toId, IDictionary<string, object> properties)
        {
            var edge = _edges.FindOne(e => e.FromNodeId == fromId && e.ToNodeId == toId);
            if (edge == null)
                throw new KeyNotFoundException($"Edge from '{fromId}' to '{toId}' not found");

            foreach (var kv in properties)
            {
                edge.Properties[kv.Key] = ConvertToBsonValue(kv.Value);
            }

            _edges.Update(edge);
            UpdateMetadata();
        }

        public IEnumerable<(string From, string To)> GetEdges()
        {
            return _edges.FindAll().Select(e => (e.FromNodeId, e.ToNodeId));
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

        public IEnumerable<string> GetNeighbors(string nodeId)
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

        #endregion
    }
}
