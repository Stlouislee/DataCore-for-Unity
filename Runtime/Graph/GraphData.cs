using System;
using System.Collections.Generic;
using AroAro.DataCore.Events;

namespace AroAro.DataCore.Graph
{
    public sealed class GraphData : IDataSet
    {
        private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<(string From, string To), Edge> _edges = new();

        public GraphData(string name)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name required", nameof(name)) : name;
        }

        public string Name { get; }
        public DataSetKind Kind => DataSetKind.Graph;

        public int NodeCount => _nodes.Count;
        public int EdgeCount => _edges.Count;

        public IDataSet WithName(string name)
        {
            var g = new GraphData(name);
            foreach (var n in _nodes.Values)
                g._nodes[n.Id] = n.Clone();
            foreach (var e in _edges.Values)
                g._edges[(e.From, e.To)] = e.Clone();
            return g;
        }

        public void AddNode(string id, Dictionary<string, string> properties = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Node id required", nameof(id));
            if (_nodes.ContainsKey(id)) throw new InvalidOperationException($"Node already exists: {id}");
            _nodes[id] = new Node(id, properties);

            DataCoreEventManager.RaiseDatasetModified(this, "AddNode", id);
        }

        public bool RemoveNode(string id)
        {
            if (!_nodes.Remove(id)) return false;

            // remove incident edges
            var toRemove = new List<(string, string)>();
            foreach (var k in _edges.Keys)
            {
                if (k.From == id || k.To == id)
                    toRemove.Add((k.From, k.To));
            }
            for (var i = 0; i < toRemove.Count; i++)
                _edges.Remove(toRemove[i]);

            DataCoreEventManager.RaiseDatasetModified(this, "RemoveNode", id);

            return true;
        }

        public bool HasNode(string id) => _nodes.ContainsKey(id);

        public Node GetNode(string id)
        {
            if (!_nodes.TryGetValue(id, out var n))
                throw new KeyNotFoundException($"Node not found: {id}");
            return n;
        }

        public void AddEdge(string from, string to, Dictionary<string, string> properties = null)
        {
            if (string.IsNullOrWhiteSpace(from)) throw new ArgumentException("From required", nameof(from));
            if (string.IsNullOrWhiteSpace(to)) throw new ArgumentException("To required", nameof(to));
            if (!_nodes.ContainsKey(from)) throw new InvalidOperationException($"Missing node: {from}");
            if (!_nodes.ContainsKey(to)) throw new InvalidOperationException($"Missing node: {to}");

            var key = (From: from, To: to);
            if (_edges.ContainsKey(key)) throw new InvalidOperationException($"Edge already exists: {from}->{to}");
            _edges[key] = new Edge(from, to, properties);

            DataCoreEventManager.RaiseDatasetModified(this, "AddEdge", new { from, to });
        }

        public bool RemoveEdge(string from, string to)
        {
            var removed = _edges.Remove((from, to));
            if (removed)
            {
                DataCoreEventManager.RaiseDatasetModified(this, "RemoveEdge", new { from, to });
            }
            return removed;
        }

        public bool HasEdge(string from, string to) => _edges.ContainsKey((from, to));

        public IEnumerable<string> NeighborsOut(string from)
        {
            foreach (var e in _edges.Values)
                if (e.From == from)
                    yield return e.To;
        }

        public IEnumerable<string> NeighborsIn(string to)
        {
            foreach (var e in _edges.Values)
                if (e.To == to)
                    yield return e.From;
        }

        public IEnumerable<Edge> Edges()
        {
            foreach (var e in _edges.Values)
                yield return e;
        }

        public IEnumerable<string> GetNodeIds()
        {
            foreach (var nodeId in _nodes.Keys)
                yield return nodeId;
        }

        public GraphQuery Query() => new GraphQuery(this);

        public sealed class Node
        {
            internal Node(string id, Dictionary<string, string> properties)
            {
                Id = id;
                Properties = properties != null
                    ? new Dictionary<string, string>(properties, StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal);
            }

            public string Id { get; }
            public Dictionary<string, string> Properties { get; }

            internal Node Clone() => new Node(Id, Properties);
        }

        public sealed class Edge
        {
            internal Edge(string from, string to, Dictionary<string, string> properties)
            {
                From = from;
                To = to;
                Properties = properties != null
                    ? new Dictionary<string, string>(properties, StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal);
            }

            public string From { get; }
            public string To { get; }
            public Dictionary<string, string> Properties { get; }

            internal Edge Clone() => new Edge(From, To, Properties);
        }

        internal IEnumerable<Node> NodesInternal()
        {
            foreach (var n in _nodes.Values)
                yield return n;
        }

        internal IEnumerable<Edge> EdgesInternal()
        {
            foreach (var e in _edges.Values)
                yield return e;
        }
    }
}
