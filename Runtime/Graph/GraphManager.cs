using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataCore.Monitoring;
using DataCore.Platform;
using DataCore.Serialization;
using NumSharp;

namespace DataCore.Graph
{
    /// <summary>
    /// Manages graph datasets with versioning, persistence, and memory optimization
    /// </summary>
    public class GraphManager : IDisposable
    {
        private readonly Dictionary<string, GraphDataset> _datasets;
        private readonly Dictionary<string, List<GraphVersion>> _versionHistory;
        private readonly IFileSystem _fileSystem;
        private readonly ISerializer<GraphData> _serializer;
        private readonly MemoryUsageTracker _memoryTracker;
        private readonly object _lock = new object();
        
        private bool _disposed;
        
        /// <summary>
        /// Maximum memory usage in bytes (default: 1GB)
        /// </summary>
        public long MaxMemoryUsage { get; set; } = 1024 * 1024 * 1024; // 1GB
        
        /// <summary>
        /// Memory usage warning threshold (default: 80% of MaxMemoryUsage)
        /// </summary>
        public long MemoryWarningThreshold => (long)(MaxMemoryUsage * 0.8);
        
        /// <summary>
        /// Current memory usage in bytes
        /// </summary>
        public long CurrentMemoryUsage => _memoryTracker.TotalMemoryUsage;
        
        /// <summary>
        /// Number of datasets currently loaded
        /// </summary>
        public int DatasetCount => _datasets.Count;
        
        /// <summary>
        /// Event fired when memory usage exceeds warning threshold
        /// </summary>
        public event Action<long> OnMemoryWarning;
        
        /// <summary>
        /// Event fired when a dataset is loaded
        /// </summary>
        public event Action<string, GraphMetadata> OnDatasetLoaded;
        
        /// <summary>
        /// Event fired when a dataset is saved
        /// </summary>
        public event Action<string, GraphMetadata> OnDatasetSaved;
        
        public GraphManager(IFileSystem fileSystem = null, ISerializer<GraphData> serializer = null)
        {
            _datasets = new Dictionary<string, GraphDataset>();
            _versionHistory = new Dictionary<string, List<GraphVersion>>();
            _fileSystem = fileSystem ?? FileSystemFactory.GetFileSystem();
            _serializer = serializer ?? new GraphJsonSerializer();
            _memoryTracker = new MemoryUsageTracker();
        }
        
        /// <summary>
        /// Get a graph dataset by name
        /// </summary>
        public Graph<TVertex, TEdge> Get<TVertex, TEdge>(string name) where TEdge : IEdge<TVertex>
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            lock (_lock)
            {
                if (!_datasets.TryGetValue(name, out var dataset))
                {
                    throw new KeyNotFoundException($"Graph '{name}' not found");
                }
                
                dataset.LastAccessTime = DateTime.UtcNow;
                dataset.AccessCount++;
                return dataset.Graph as Graph<TVertex, TEdge>;
            }
        }
        
        /// <summary>
        /// Set a graph dataset
        /// </summary>
        public void Set<TVertex, TEdge>(string name, Graph<TVertex, TEdge> graph, Dictionary<string, object> properties = null) where TEdge : IEdge<TVertex>
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
                
            lock (_lock)
            {
                var metadata = new GraphMetadata(name)
                {
                    VertexCount = graph.VertexCount,
                    EdgeCount = graph.EdgeCount,
                    Direction = graph.Direction,
                    Properties = properties ?? new Dictionary<string, object>()
                };
                
                var dataset = new GraphDataset
                {
                    Graph = graph,
                    Metadata = metadata,
                    LastAccessTime = DateTime.UtcNow,
                    AccessCount = 0
                };
                
                // Check if dataset already exists
                if (_datasets.ContainsKey(name))
                {
                    // Remove old dataset from memory tracking
                    var oldDataset = _datasets[name];
                    _memoryTracker.ReleaseMemory(oldDataset.Metadata.GetEstimatedSizeInBytes());
                }
                
                _datasets[name] = dataset;
                _memoryTracker.AllocateMemory(metadata.GetEstimatedSizeInBytes());
                
                // Add to version history
                if (!_versionHistory.ContainsKey(name))
                {
                    _versionHistory[name] = new List<GraphVersion>();
                }
                
                var version = new GraphVersion
                {
                    VersionNumber = metadata.Version,
                    Description = "Graph created/updated",
                    VertexCount = graph.VertexCount,
                    EdgeCount = graph.EdgeCount
                };
                
                _versionHistory[name].Add(version);
                
                CheckMemoryUsage();
            }
        }
        
        /// <summary>
        /// Create a new graph
        /// </summary>
        public Graph<TVertex, TEdge> CreateGraph<TVertex, TEdge>(string name, GraphDirection direction = GraphDirection.Directed, 
            bool allowParallelEdges = false, Dictionary<string, object> properties = null) where TEdge : IEdge<TVertex>
        {
            var graph = new Graph<TVertex, TEdge>(direction, allowParallelEdges);
            Set(name, graph, properties);
            return graph;
        }
        
        /// <summary>
        /// Check if a dataset exists
        /// </summary>
        public bool Contains(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
                
            lock (_lock)
            {
                return _datasets.ContainsKey(name);
            }
        }
        
        /// <summary>
        /// Remove a dataset
        /// </summary>
        public bool Remove(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
                
            lock (_lock)
            {
                if (_datasets.TryGetValue(name, out var dataset))
                {
                    _memoryTracker.ReleaseMemory(dataset.Metadata.GetEstimatedSizeInBytes());
                    _datasets.Remove(name);
                    return true;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Load a graph from file asynchronously
        /// </summary>
        public async Task<Graph<TVertex, TEdge>> LoadAsync<TVertex, TEdge>(string name, string filePath = null, 
            CancellationToken cancellationToken = default) where TEdge : IEdge<TVertex>
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            filePath ??= $"{name}.json";
            
            try
            {
                var graphData = await _serializer.DeserializeAsync(filePath, cancellationToken);
                var graph = ConvertToGraph<TVertex, TEdge>(graphData);
                
                Set(name, graph, new Dictionary<string, object>
                {
                    ["file_path"] = filePath,
                    ["loaded_time"] = DateTime.UtcNow
                });
                
                OnDatasetLoaded?.Invoke(name, _datasets[name].Metadata);
                
                return graph;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load graph '{name}' from '{filePath}'", ex);
            }
        }
        
        /// <summary>
        /// Save a graph to file asynchronously
        /// </summary>
        public async Task SaveAsync<TVertex, TEdge>(string name, string filePath = null, 
            CancellationToken cancellationToken = default) where TEdge : IEdge<TVertex>
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            Graph<TVertex, TEdge> graph;
            lock (_lock)
            {
                if (!_datasets.TryGetValue(name, out var dataset))
                {
                    throw new KeyNotFoundException($"Graph '{name}' not found");
                }
                graph = dataset.Graph as Graph<TVertex, TEdge>;
            }
            
            if (graph == null)
                throw new InvalidOperationException($"Graph '{name}' is not of the requested type");
            
            filePath ??= $"{name}.json";
            
            try
            {
                var graphData = ConvertFromGraph(graph);
                await _serializer.SerializeAsync(graphData, filePath, cancellationToken);
                
                lock (_lock)
                {
                    _datasets[name].Metadata.FilePath = filePath;
                    _datasets[name].Metadata.Touch();
                }
                
                OnDatasetSaved?.Invoke(name, _datasets[name].Metadata);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save graph '{name}' to '{filePath}'", ex);
            }
        }
        
        /// <summary>
        /// Save all datasets
        /// </summary>
        public async Task SaveAllAsync(CancellationToken cancellationToken = default)
        {
            string[] datasetNames;
            lock (_lock)
            {
                datasetNames = _datasets.Keys.ToArray();
            }
            
            var tasks = datasetNames.Select(name => SaveAsync<object, IEdge<object>>(name, cancellationToken: cancellationToken));
            await Task.WhenAll(tasks);
        }
        
        /// <summary>
        /// Get dataset metadata
        /// </summary>
        public GraphMetadata GetMetadata(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Dataset name cannot be null or empty", nameof(name));
                
            lock (_lock)
            {
                if (_datasets.TryGetValue(name, out var dataset))
                {
                    return dataset.Metadata;
                }
                
                throw new KeyNotFoundException($"Graph '{name}' not found");
            }
        }
        
        /// <summary>
        /// Get all dataset names
        /// </summary>
        public string[] GetDatasetNames()
        {
            lock (_lock)
            {
                return _datasets.Keys.ToArray();
            }
        }
        
        /// <summary>
        /// Clear all datasets
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                foreach (var dataset in _datasets.Values)
                {
                    _memoryTracker.ReleaseMemory(dataset.Metadata.GetEstimatedSizeInBytes());
                }
                
                _datasets.Clear();
                _versionHistory.Clear();
            }
        }
        
        /// <summary>
        /// Get memory usage statistics
        /// </summary>
        public MemoryUsageReport GetMemoryReport()
        {
            return _memoryTracker.GetReport();
        }
        
        /// <summary>
        /// Convert graph to adjacency matrix
        /// </summary>
        public NDArray ToAdjacencyMatrix<TVertex, TEdge>(string name) where TEdge : IEdge<TVertex>
        {
            var graph = Get<TVertex, TEdge>(name);
            var vertices = graph.Vertices.ToList();
            var n = vertices.Count;
            
            var matrix = np.zeros((n, n), typeof(double));
            
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (graph.ContainsEdge(vertices[i], vertices[j]))
                    {
                        matrix[i, j] = graph.GetEdgeWeight(vertices[i], vertices[j]);
                    }
                }
            }
            
            return matrix;
        }
        
        /// <summary>
        /// Convert graph to edge list matrix
        /// </summary>
        public NDArray ToEdgeList<TVertex, TEdge>(string name, bool includeWeights = true) where TEdge : IEdge<TVertex>
        {
            var graph = Get<TVertex, TEdge>(name);
            var edges = graph.Edges.ToList();
            var n = edges.Count;
            
            var columns = includeWeights ? 3 : 2;
            var matrix = np.zeros((n, columns), typeof(double));
            
            var vertices = graph.Vertices.ToList();
            
            for (int i = 0; i < n; i++)
            {
                var edge = edges[i];
                var sourceIndex = vertices.IndexOf(edge.Source);
                var targetIndex = vertices.IndexOf(edge.Target);
                
                matrix[i, 0] = sourceIndex;
                matrix[i, 1] = targetIndex;
                
                if (includeWeights)
                {
                    matrix[i, 2] = edge.Weight;
                }
            }
            
            return matrix;
        }
        
        private void CheckMemoryUsage()
        {
            if (CurrentMemoryUsage > MemoryWarningThreshold)
            {
                OnMemoryWarning?.Invoke(CurrentMemoryUsage);
            }
        }
        
        private Graph<TVertex, TEdge> ConvertToGraph<TVertex, TEdge>(GraphData data) where TEdge : IEdge<TVertex>
        {
            var graph = new Graph<TVertex, TEdge>(data.Direction, data.AllowParallelEdges);
            
            // Add vertices
            foreach (var vertexData in data.Vertices)
            {
                var vertex = (TVertex)Convert.ChangeType(vertexData.Id, typeof(TVertex));
                graph.AddVertex(vertex);
                
                // Set vertex properties
                foreach (var prop in vertexData.Properties)
                {
                    graph.SetVertexProperty(vertex, prop.Key, prop.Value);
                }
            }
            
            // Add edges
            foreach (var edgeData in data.Edges)
            {
                var source = (TVertex)Convert.ChangeType(edgeData.SourceId, typeof(TVertex));
                var target = (TVertex)Convert.ChangeType(edgeData.TargetId, typeof(TVertex));
                
                TEdge edge;
                if (data.Direction == GraphDirection.Undirected)
                {
                    edge = (TEdge)(object)new UndirectedEdge<TVertex>(source, target, edgeData.Weight);
                }
                else
                {
                    edge = (TEdge)(object)new Edge<TVertex>(source, target, edgeData.Weight);
                }
                
                // Set edge properties
                foreach (var prop in edgeData.Properties)
                {
                    edge.Properties[prop.Key] = prop.Value;
                }
                
                graph.AddEdge(edge);
            }
            
            return graph;
        }
        
        private GraphData ConvertFromGraph<TVertex, TEdge>(Graph<TVertex, TEdge> graph) where TEdge : IEdge<TVertex>
        {
            var data = new GraphData
            {
                Direction = graph.Direction,
                AllowParallelEdges = graph.AllowParallelEdges,
                Vertices = new List<VertexData>(),
                Edges = new List<EdgeData>()
            };
            
            // Convert vertices
            var vertexIds = new Dictionary<TVertex, int>();
            int vertexId = 0;
            
            foreach (var vertex in graph.Vertices)
            {
                vertexIds[vertex] = vertexId++;
                
                var vertexData = new VertexData
                {
                    Id = vertex.ToString(),
                    Properties = new Dictionary<string, object>()
                };
                
                // Get vertex properties (this would need to be implemented in the Graph class)
                data.Vertices.Add(vertexData);
            }
            
            // Convert edges
            foreach (var edge in graph.Edges)
            {
                var edgeData = new EdgeData
                {
                    SourceId = edge.Source.ToString(),
                    TargetId = edge.Target.ToString(),
                    Weight = edge.Weight,
                    Properties = new Dictionary<string, object>(edge.Properties)
                };
                
                data.Edges.Add(edgeData);
            }
            
            return data;
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                Clear();
                _disposed = true;
            }
        }
        
        private class GraphDataset
        {
            public object Graph { get; set; }
            public GraphMetadata Metadata { get; set; }
            public DateTime LastAccessTime { get; set; }
            public int AccessCount { get; set; }
        }
    }
    
    /// <summary>
    /// Metadata for graph datasets
    /// </summary>
    [Serializable]
    public class GraphMetadata
    {
        public string Name { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime ModifiedTime { get; set; }
        public int VertexCount { get; set; }
        public int EdgeCount { get; set; }
        public GraphDirection Direction { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public int Version { get; set; } = 1;
        public string FilePath { get; set; }
        public string Format { get; set; }
        
        public GraphMetadata()
        {
            CreatedTime = DateTime.UtcNow;
            ModifiedTime = DateTime.UtcNow;
        }
        
        public GraphMetadata(string name) : this()
        {
            Name = name;
        }
        
        public void Touch()
        {
            ModifiedTime = DateTime.UtcNow;
            Version++;
        }
        
        public long GetEstimatedSizeInBytes()
        {
            // Rough estimate: 100 bytes per vertex + 50 bytes per edge
            return (VertexCount * 100) + (EdgeCount * 50);
        }
    }
    
    /// <summary>
    /// Version information for graph datasets
    /// </summary>
    [Serializable]
    public class GraphVersion
    {
        public int VersionNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public int VertexCount { get; set; }
        public int EdgeCount { get; set; }
        
        public GraphVersion()
        {
            Timestamp = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Serializable graph data
    /// </summary>
    [Serializable]
    public class GraphData
    {
        public GraphDirection Direction { get; set; }
        public bool AllowParallelEdges { get; set; }
        public List<VertexData> Vertices { get; set; } = new List<VertexData>();
        public List<EdgeData> Edges { get; set; } = new List<EdgeData>();
    }
    
    [Serializable]
    public class VertexData
    {
        public string Id { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }
    
    [Serializable]
    public class EdgeData
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public double Weight { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }
}