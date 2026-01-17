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
        private readonly object _lock = new object();
        private bool _disposed;
        private int _pendingMetadataUpdates;
        private const int MetadataUpdateBatchSize = 100; // Batch metadata updates

        internal LiteDbGraphDataset(LiteDatabase database, GraphMetadata metadata)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _nodes = _database.GetCollection<GraphNode>($"graph_{metadata.Id}_nodes");
            _edges = _database.GetCollection<GraphEdge>($"graph_{metadata.Id}_edges");

            _nodes.EnsureIndex(n => n.NodeId, true);
            _edges.EnsureIndex(e => e.FromNodeId);
            _edges.EnsureIndex(e => e.ToNodeId);
        }

        /// <summary>
        /// Check if the database is still accessible
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LiteDbGraphDataset), $"Graph dataset '{_metadata.Name}' has been disposed");
        }

        /// <summary>
        /// Mark this dataset as disposed (called by parent store)
        /// </summary>
        internal void MarkDisposed()
        {
            _disposed = true;
        }

        /// <summary>
        /// Force flush any pending metadata updates
        /// </summary>
        public void FlushMetadata()
        {
            lock (_lock)
            {
                if (_pendingMetadataUpdates > 0)
                {
                    ForceUpdateMetadata();
                    _pendingMetadataUpdates = 0;
                }
            }
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
            ThrowIfDisposed();
            
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Node ID cannot be null or empty", nameof(id));

            lock (_lock)
            {
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
        }

        public int AddNodes(IEnumerable<(string Id, IDictionary<string, object> Properties)> nodes)
        {
            ThrowIfDisposed();
            
            lock (_lock)
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
                    ForceUpdateMetadata(); // Force update after bulk operation
                }

                return count;
            }
        }

        public void UpdateNodeProperties(string id, IDictionary<string, object> properties)
        {
            ThrowIfDisposed();
            
            lock (_lock)
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
        }

        public bool RemoveNode(string id)
        {
            ThrowIfDisposed();
            
            lock (_lock)
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
                ForceUpdateMetadata();
                return true;
            }
        }

        public bool HasNode(string id)
        {
            ThrowIfDisposed();
            return _nodes.Exists(n => n.NodeId == id);
        }

        public IDictionary<string, object> GetNodeProperties(string id)
        {
            ThrowIfDisposed();
            
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
            ThrowIfDisposed();
            return _nodes.FindAll().Select(n => n.NodeId);
        }

        #endregion

        #region 边操作

        public void AddEdge(string fromId, string toId, IDictionary<string, object> properties = null)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrWhiteSpace(fromId))
                throw new ArgumentException("From node ID cannot be null or empty", nameof(fromId));
            if (string.IsNullOrWhiteSpace(toId))
                throw new ArgumentException("To node ID cannot be null or empty", nameof(toId));

            lock (_lock)
            {
                if (!HasNodeInternal(fromId))
                    throw new InvalidOperationException($"Node '{fromId}' does not exist");
                if (!HasNodeInternal(toId))
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
        }

        public int AddEdges(IEnumerable<(string From, string To, IDictionary<string, object> Properties)> edges)
        {
            ThrowIfDisposed();
            
            lock (_lock)
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
                    ForceUpdateMetadata(); // Force update after bulk operation
                }

                return count;
            }
        }

        public bool RemoveEdge(string fromId, string toId)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                var edge = _edges.FindOne(e => e.FromNodeId == fromId && e.ToNodeId == toId);
                if (edge == null) return false;

                _edges.Delete(edge.Id);
                _metadata.EdgeCount--;
                UpdateMetadata();
                return true;
            }
        }

        public bool HasEdge(string fromId, string toId)
        {
            ThrowIfDisposed();
            return _edges.Exists(e => e.FromNodeId == fromId && e.ToNodeId == toId);
        }

        public IDictionary<string, object> GetEdgeProperties(string fromId, string toId)
        {
            ThrowIfDisposed();
            
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
            ThrowIfDisposed();
            
            lock (_lock)
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
        }

        public IEnumerable<(string From, string To)> GetEdges()
        {
            ThrowIfDisposed();
            return _edges.FindAll().Select(e => (e.FromNodeId, e.ToNodeId));
        }

        #endregion

        #region 邻居查询

        public IEnumerable<string> GetOutNeighbors(string nodeId)
        {
            ThrowIfDisposed();
            return _edges.Find(e => e.FromNodeId == nodeId).Select(e => e.ToNodeId).Distinct();
        }

        public IEnumerable<string> GetInNeighbors(string nodeId)
        {
            ThrowIfDisposed();
            return _edges.Find(e => e.ToNodeId == nodeId).Select(e => e.FromNodeId).Distinct();
        }

        public IEnumerable<string> GetNeighbors(string nodeId)
        {
            ThrowIfDisposed();
            var outNeighbors = GetOutNeighbors(nodeId);
            var inNeighbors = GetInNeighbors(nodeId);
            return outNeighbors.Union(inNeighbors).Distinct();
        }

        public int GetOutDegree(string nodeId)
        {
            ThrowIfDisposed();
            return _edges.Count(e => e.FromNodeId == nodeId);
        }

        public int GetInDegree(string nodeId)
        {
            ThrowIfDisposed();
            return _edges.Count(e => e.ToNodeId == nodeId);
        }

        #endregion

        #region 查询

        public IGraphQuery Query()
        {
            ThrowIfDisposed();
            return new LiteDbGraphQuery(this);
        }

        #endregion

        #region 清空

        public void Clear()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                _nodes.DeleteAll();
                _edges.DeleteAll();
                _metadata.NodeCount = 0;
                _metadata.EdgeCount = 0;
                ForceUpdateMetadata();
            }
        }

        #endregion

        #region 内部方法

        internal IEnumerable<GraphNode> GetAllNodesInternal()
        {
            ThrowIfDisposed();
            return _nodes.FindAll();
        }
        
        internal IEnumerable<GraphEdge> GetAllEdgesInternal()
        {
            ThrowIfDisposed();
            return _edges.FindAll();
        }

        /// <summary>
        /// Internal HasNode check without locking (for use inside locked sections)
        /// </summary>
        private bool HasNodeInternal(string id)
        {
            return _nodes.Exists(n => n.NodeId == id);
        }

        /// <summary>
        /// Batched metadata update - only writes to DB after threshold or on ForceUpdateMetadata
        /// </summary>
        private void UpdateMetadata()
        {
            _metadata.ModifiedAt = DateTime.UtcNow;
            _pendingMetadataUpdates++;
            
            if (_pendingMetadataUpdates >= MetadataUpdateBatchSize)
            {
                ForceUpdateMetadata();
                _pendingMetadataUpdates = 0;
            }
        }

        /// <summary>
        /// Force write metadata to database immediately
        /// </summary>
        private void ForceUpdateMetadata()
        {
            try
            {
                _metadata.ModifiedAt = DateTime.UtcNow;
                var meta = _database.GetCollection<GraphMetadata>("graph_meta");
                meta.Update(_metadata);
                _pendingMetadataUpdates = 0;
            }
            catch (ObjectDisposedException)
            {
                // Database was disposed, mark self as disposed
                _disposed = true;
                throw;
            }
            catch (LiteException ex) when (ex.Message.Contains("disposed"))
            {
                _disposed = true;
                throw new ObjectDisposedException(nameof(LiteDbGraphDataset), "Database has been disposed", ex);
            }
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
