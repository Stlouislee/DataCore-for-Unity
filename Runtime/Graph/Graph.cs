using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DataCore.Graph
{
    /// <summary>
    /// Graph direction type
    /// </summary>
    public enum GraphDirection
    {
        Directed,
        Undirected
    }
    
    /// <summary>
    /// Generic graph class supporting directed/undirected and weighted graphs
    /// </summary>
    public class Graph<TVertex, TEdge> where TEdge : IEdge<TVertex>
    {
        private readonly Dictionary<TVertex, VertexData> _vertices;
        private readonly List<TEdge> _edges;
        private readonly GraphDirection _direction;
        private readonly bool _allowParallelEdges;
        private readonly object _lock = new object();
        
        /// <summary>
        /// Number of vertices in the graph
        /// </summary>
        public int VertexCount => _vertices.Count;
        
        /// <summary>
        /// Number of edges in the graph
        /// </summary>
        public int EdgeCount => _edges.Count;
        
        /// <summary>
        /// Graph direction type
        /// </summary>
        public GraphDirection Direction => _direction;
        
        /// <summary>
        /// Whether parallel edges are allowed
        /// </summary>
        public bool AllowParallelEdges => _allowParallelEdges;
        
        /// <summary>
        /// All vertices in the graph
        /// </summary>
        public IEnumerable<TVertex> Vertices => _vertices.Keys;
        
        /// <summary>
        /// All edges in the graph
        /// </summary>
        public IEnumerable<TEdge> Edges => _edges;
        
        public Graph(GraphDirection direction = GraphDirection.Directed, bool allowParallelEdges = false)
        {
            _direction = direction;
            _allowParallelEdges = allowParallelEdges;
            _vertices = new Dictionary<TVertex, VertexData>();
            _edges = new List<TEdge>();
        }
        
        /// <summary>
        /// Add a vertex to the graph
        /// </summary>
        public bool AddVertex(TVertex vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException(nameof(vertex));
                
            lock (_lock)
            {
                if (_vertices.ContainsKey(vertex))
                    return false;
                    
                _vertices[vertex] = new VertexData();
                return true;
            }
        }
        
        /// <summary>
        /// Add multiple vertices to the graph
        /// </summary>
        public int AddVertices(IEnumerable<TVertex> vertices)
        {
            if (vertices == null)
                throw new ArgumentNullException(nameof(vertices));
                
            var count = 0;
            foreach (var vertex in vertices)
            {
                if (AddVertex(vertex))
                    count++;
            }
            return count;
        }
        
        /// <summary>
        /// Remove a vertex from the graph (and all connected edges)
        /// </summary>
        public bool RemoveVertex(TVertex vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException(nameof(vertex));
                
            lock (_lock)
            {
                if (!_vertices.ContainsKey(vertex))
                    return false;
                    
                // Remove all edges connected to this vertex
                var edgesToRemove = _edges.Where(e => e.Source.Equals(vertex) || e.Target.Equals(vertex)).ToList();
                foreach (var edge in edgesToRemove)
                {
                    RemoveEdgeInternal(edge);
                }
                
                _vertices.Remove(vertex);
                return true;
            }
        }
        
        /// <summary>
        /// Check if a vertex exists in the graph
        /// </summary>
        public bool ContainsVertex(TVertex vertex)
        {
            if (vertex == null)
                return false;
                
            lock (_lock)
            {
                return _vertices.ContainsKey(vertex);
            }
        }
        
        /// <summary>
        /// Add an edge to the graph
        /// </summary>
        public bool AddEdge(TEdge edge)
        {
            if (edge == null)
                throw new ArgumentNullException(nameof(edge));
                
            lock (_lock)
            {
                // Check if vertices exist
                if (!_vertices.ContainsKey(edge.Source) || !_vertices.ContainsKey(edge.Target))
                    return false;
                
                // Check for parallel edges
                if (!_allowParallelEdges && ContainsEdge(edge.Source, edge.Target))
                    return false;
                
                _edges.Add(edge);
                
                // Update vertex data
                _vertices[edge.Source].OutEdges.Add(edge);
                _vertices[edge.Target].InEdges.Add(edge);
                
                // For undirected graphs, add reverse connection
                if (_direction == GraphDirection.Undirected)
                {
                    _vertices[edge.Target].OutEdges.Add(edge);
                    _vertices[edge.Source].InEdges.Add(edge);
                }
                
                return true;
            }
        }
        
        /// <summary>
        /// Add an edge with source and target vertices
        /// </summary>
        public bool AddEdge(TVertex source, TVertex target, double weight = 1.0)
        {
            if (source == null || target == null)
                throw new ArgumentNullException("Source and target cannot be null");
                
            TEdge edge;
            if (_direction == GraphDirection.Undirected)
            {
                edge = (TEdge)(object)new UndirectedEdge<TVertex>(source, target, weight);
            }
            else
            {
                edge = (TEdge)(object)new Edge<TVertex>(source, target, weight);
            }
            
            return AddEdge(edge);
        }
        
        /// <summary>
        /// Remove an edge from the graph
        /// </summary>
        public bool RemoveEdge(TVertex source, TVertex target)
        {
            if (source == null || target == null)
                throw new ArgumentNullException("Source and target cannot be null");
                
            lock (_lock)
            {
                var edge = _edges.FirstOrDefault(e => e.Connects(source, target));
                if (edge == null)
                    return false;
                    
                return RemoveEdgeInternal(edge);
            }
        }
        
        /// <summary>
        /// Check if an edge exists between two vertices
        /// </summary>
        public bool ContainsEdge(TVertex source, TVertex target)
        {
            if (source == null || target == null)
                return false;
                
            lock (_lock)
            {
                return _edges.Any(e => e.Connects(source, target));
            }
        }
        
        /// <summary>
        /// Get the weight of an edge
        /// </summary>
        public double GetEdgeWeight(TVertex source, TVertex target)
        {
            if (source == null || target == null)
                throw new ArgumentNullException("Source and target cannot be null");
                
            lock (_lock)
            {
                var edge = _edges.FirstOrDefault(e => e.Connects(source, target));
                return edge?.Weight ?? double.NaN;
            }
        }
        
        /// <summary>
        /// Update the weight of an edge
        /// </summary>
        public bool UpdateEdgeWeight(TVertex source, TVertex target, double newWeight)
        {
            if (source == null || target == null)
                throw new ArgumentNullException("Source and target cannot be null");
                
            lock (_lock)
            {
                var edge = _edges.FirstOrDefault(e => e.Connects(source, target));
                if (edge == null)
                    return false;
                    
                edge.Weight = newWeight;
                return true;
            }
        }
        
        /// <summary>
        /// Get all outgoing edges from a vertex
        /// </summary>
        public IEnumerable<TEdge> GetOutEdges(TVertex vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException(nameof(vertex));
                
            lock (_lock)
            {
                if (!_vertices.TryGetValue(vertex, out var vertexData))
                    return Enumerable.Empty<TEdge>();
                    
                return vertexData.OutEdges;
            }
        }
        
        /// <summary>
        /// Get all incoming edges to a vertex
        /// </summary>
        public IEnumerable<TEdge> GetInEdges(TVertex vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException(nameof(vertex));
                
            lock (_lock)
            {
                if (!_vertices.TryGetValue(vertex, out var vertexData))
                    return Enumerable.Empty<TEdge>();
                    
                return vertexData.InEdges;
            }
        }
        
        /// <summary>
        /// Get all neighbors of a vertex
        /// </summary>
        public IEnumerable<TVertex> GetNeighbors(TVertex vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException(nameof(vertex));
                
            lock (_lock)
            {
                if (!_vertices.TryGetValue(vertex, out var vertexData))
                    return Enumerable.Empty<TVertex>();
                
                var neighbors = new HashSet<TVertex>();
                
                foreach (var edge in vertexData.OutEdges)
                {
                    neighbors.Add(edge.Target);
                }
                
                foreach (var edge in vertexData.InEdges)
                {
                    neighbors.Add(edge.Source);
                }
                
                return neighbors;
            }
        }
        
        /// <summary>
        /// Get the degree of a vertex
        /// </summary>
        public int GetDegree(TVertex vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException(nameof(vertex));
                
            lock (_lock)
            {
                if (!_vertices.TryGetValue(vertex, out var vertexData))
                    return 0;
                
                if (_direction == GraphDirection.Undirected)
                {
                    return vertexData.OutEdges.Count;
                }
                else
                {
                    return vertexData.OutEdges.Count + vertexData.InEdges.Count;
                }
            }
        }
        
        /// <summary>
        /// Get the in-degree of a vertex (for directed graphs)
        /// </summary>
        public int GetInDegree(TVertex vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException(nameof(vertex));
                
            lock (_lock)
            {
                if (!_vertices.TryGetValue(vertex, out var vertexData))
                    return 0;
                
                return vertexData.InEdges.Count;
            }
        }
        
        /// <summary>
        /// Get the out-degree of a vertex (for directed graphs)
        /// </summary>
        public int GetOutDegree(TVertex vertex)
        {
            if (vertex == null)
                throw new ArgumentNullException(nameof(vertex));
                
            lock (_lock)
            {
                if (!_vertices.TryGetValue(vertex, out var vertexData))
                    return 0;
                
                return vertexData.OutEdges.Count;
            }
        }
        
        /// <summary>
        /// Set a vertex property
        /// </summary>
        public void SetVertexProperty(TVertex vertex, string key, object value)
        {
            if (vertex == null)
                throw new ArgumentNullException(nameof(vertex));
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
                
            lock (_lock)
            {
                if (!_vertices.TryGetValue(vertex, out var vertexData))
                    throw new KeyNotFoundException($"Vertex {vertex} not found");
                
                vertexData.Properties[key] = value;
            }
        }
        
        /// <summary>
        /// Get a vertex property
        /// </summary>
        public object GetVertexProperty(TVertex vertex, string key)
        {
            if (vertex == null)
                throw new ArgumentNullException(nameof(vertex));
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
                
            lock (_lock)
            {
                if (!_vertices.TryGetValue(vertex, out var vertexData))
                    throw new KeyNotFoundException($"Vertex {vertex} not found");
                
                return vertexData.Properties.TryGetValue(key, out var value) ? value : null;
            }
        }
        
        /// <summary>
        /// Clear all vertices and edges
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _vertices.Clear();
                _edges.Clear();
            }
        }
        
        /// <summary>
        /// Create a subgraph containing only the specified vertices
        /// </summary>
        public Graph<TVertex, TEdge> Subgraph(IEnumerable<TVertex> vertices)
        {
            if (vertices == null)
                throw new ArgumentNullException(nameof(vertices));
                
            var subgraph = new Graph<TVertex, TEdge>(_direction, _allowParallelEdges);
            
            lock (_lock)
            {
                // Add vertices
                foreach (var vertex in vertices)
                {
                    if (_vertices.ContainsKey(vertex))
                    {
                        subgraph.AddVertex(vertex);
                    }
                }
                
                // Add edges between vertices in the subgraph
                foreach (var edge in _edges)
                {
                    if (subgraph.ContainsVertex(edge.Source) && subgraph.ContainsVertex(edge.Target))
                    {
                        subgraph.AddEdge(edge);
                    }
                }
            }
            
            return subgraph;
        }
        
        private bool RemoveEdgeInternal(TEdge edge)
        {
            if (!_edges.Remove(edge))
                return false;
            
            // Update vertex data
            _vertices[edge.Source].OutEdges.Remove(edge);
            _vertices[edge.Target].InEdges.Remove(edge);
            
            // For undirected graphs, remove reverse connections
            if (_direction == GraphDirection.Undirected)
            {
                _vertices[edge.Target].OutEdges.Remove(edge);
                _vertices[edge.Source].InEdges.Remove(edge);
            }
            
            return true;
        }
        
        private class VertexData
        {
            public List<TEdge> OutEdges { get; } = new List<TEdge>();
            public List<TEdge> InEdges { get; } = new List<TEdge>();
            public Dictionary<string, object> Properties { get; } = new Dictionary<string, object>();
        }
    }
}