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
        private int _skip;
        private int _limit = int.MaxValue;

        internal LiteDbGraphQuery(LiteDbGraphDataset dataset)
        {
            _dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
            _nodeFilters = new List<Func<GraphNode, bool>>();
            _edgeFilters = new List<Func<GraphEdge, bool>>();
        }

        #region IGraphQuery 实现

        public IGraphQuery WhereNodeProperty(string property, QueryOp op, object value)
        {
            _nodeFilters.Add(node =>
            {
                if (!node.Properties.TryGetValue(property, out var bsonValue))
                    return false;
                return EvaluateCondition(bsonValue, op, value);
            });
            return this;
        }

        public IGraphQuery WhereNodeId(string nodeId)
        {
            _nodeFilters.Add(node => node.NodeId == nodeId);
            return this;
        }

        public IGraphQuery WhereNodeIdIn(IEnumerable<string> nodeIds)
        {
            var idSet = new HashSet<string>(nodeIds);
            _nodeFilters.Add(node => idSet.Contains(node.NodeId));
            return this;
        }

        public IGraphQuery WhereHasOutgoingEdge()
        {
            _nodeFilters.Add(node => _dataset.GetOutDegree(node.NodeId) > 0);
            return this;
        }

        public IGraphQuery WhereHasIncomingEdge()
        {
            _nodeFilters.Add(node => _dataset.GetInDegree(node.NodeId) > 0);
            return this;
        }

        public IGraphQuery WhereDegreeGreaterThan(int degree)
        {
            _nodeFilters.Add(node => _dataset.GetDegree(node.NodeId) > degree);
            return this;
        }

        public IGraphQuery WhereDegreeLessThan(int degree)
        {
            _nodeFilters.Add(node => _dataset.GetDegree(node.NodeId) < degree);
            return this;
        }

        public IGraphQuery WhereEdgeWeight(QueryOp op, double value)
        {
            _edgeFilters.Add(edge =>
            {
                return op switch
                {
                    QueryOp.Equal => Math.Abs(edge.Weight - value) < 0.0001,
                    QueryOp.NotEqual => Math.Abs(edge.Weight - value) >= 0.0001,
                    QueryOp.GreaterThan => edge.Weight > value,
                    QueryOp.GreaterOrEqual => edge.Weight >= value,
                    QueryOp.LessThan => edge.Weight < value,
                    QueryOp.LessOrEqual => edge.Weight <= value,
                    _ => false
                };
            });
            return this;
        }

        public IGraphQuery WhereEdgeProperty(string property, QueryOp op, object value)
        {
            _edgeFilters.Add(edge =>
            {
                if (!edge.Properties.TryGetValue(property, out var bsonValue))
                    return false;
                return EvaluateCondition(bsonValue, op, value);
            });
            return this;
        }

        public IGraphQuery Skip(int count)
        {
            _skip = Math.Max(0, count);
            return this;
        }

        public IGraphQuery Limit(int count)
        {
            _limit = Math.Max(0, count);
            return this;
        }

        #endregion

        #region 执行查询

        public int CountNodes() => ExecuteNodeFilters().Count();

        public int CountEdges() => ExecuteEdgeFilters().Count();

        public IEnumerable<(string nodeId, Dictionary<string, object> properties)> ToNodeResults()
        {
            var nodes = ExecuteNodeFilters()
                .Skip(_skip)
                .Take(_limit);

            foreach (var node in nodes)
            {
                var props = new Dictionary<string, object>();
                foreach (var kv in node.Properties)
                {
                    props[kv.Key] = ConvertFromBsonValue(kv.Value);
                }
                yield return (node.NodeId, props);
            }
        }

        public IEnumerable<(string from, string to, double weight, Dictionary<string, object> properties)> ToEdgeResults()
        {
            var edges = ExecuteEdgeFilters()
                .Skip(_skip)
                .Take(_limit);

            foreach (var edge in edges)
            {
                var props = new Dictionary<string, object>();
                foreach (var kv in edge.Properties)
                {
                    props[kv.Key] = ConvertFromBsonValue(kv.Value);
                }
                yield return (edge.FromNodeId, edge.ToNodeId, edge.Weight, props);
            }
        }

        public IEnumerable<string> ToNodeIds()
        {
            return ExecuteNodeFilters()
                .Skip(_skip)
                .Take(_limit)
                .Select(n => n.NodeId);
        }

        public (string nodeId, Dictionary<string, object> properties)? FirstNodeOrDefault()
        {
            var node = ExecuteNodeFilters().FirstOrDefault();
            if (node == null) return null;

            var props = new Dictionary<string, object>();
            foreach (var kv in node.Properties)
            {
                props[kv.Key] = ConvertFromBsonValue(kv.Value);
            }
            return (node.NodeId, props);
        }

        public (string from, string to, double weight, Dictionary<string, object> properties)? FirstEdgeOrDefault()
        {
            var edge = ExecuteEdgeFilters().FirstOrDefault();
            if (edge == null) return null;

            var props = new Dictionary<string, object>();
            foreach (var kv in edge.Properties)
            {
                props[kv.Key] = ConvertFromBsonValue(kv.Value);
            }
            return (edge.FromNodeId, edge.ToNodeId, edge.Weight, props);
        }

        #endregion

        #region 内部方法

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

        private bool EvaluateCondition(BsonValue bsonValue, QueryOp op, object value)
        {
            switch (op)
            {
                case QueryOp.Equal:
                    return BsonValueEquals(bsonValue, value);

                case QueryOp.NotEqual:
                    return !BsonValueEquals(bsonValue, value);

                case QueryOp.GreaterThan:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble > Convert.ToDouble(value);

                case QueryOp.GreaterOrEqual:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble >= Convert.ToDouble(value);

                case QueryOp.LessThan:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble < Convert.ToDouble(value);

                case QueryOp.LessOrEqual:
                    if (!bsonValue.IsNumber) return false;
                    return bsonValue.AsDouble <= Convert.ToDouble(value);

                case QueryOp.Contains:
                    if (!bsonValue.IsString) return false;
                    return bsonValue.AsString?.Contains(value?.ToString()) ?? false;

                case QueryOp.StartsWith:
                    if (!bsonValue.IsString) return false;
                    return bsonValue.AsString?.StartsWith(value?.ToString()) ?? false;

                case QueryOp.EndsWith:
                    if (!bsonValue.IsString) return false;
                    return bsonValue.AsString?.EndsWith(value?.ToString()) ?? false;

                case QueryOp.IsNull:
                    return bsonValue.IsNull;

                case QueryOp.IsNotNull:
                    return !bsonValue.IsNull;

                default:
                    return false;
            }
        }

        private bool BsonValueEquals(BsonValue bsonValue, object value)
        {
            if (value == null) return bsonValue.IsNull;
            if (bsonValue.IsNull) return false;

            if (bsonValue.IsNumber && (value is int || value is long || value is float || value is double))
                return Math.Abs(bsonValue.AsDouble - Convert.ToDouble(value)) < 0.0001;

            if (bsonValue.IsString && value is string s)
                return bsonValue.AsString == s;

            if (bsonValue.IsBoolean && value is bool b)
                return bsonValue.AsBoolean == b;

            return bsonValue.ToString() == value.ToString();
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
