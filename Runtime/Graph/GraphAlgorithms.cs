using System;
using System.Collections.Generic;
using System.Linq;
using NumSharp;

namespace DataCore.Graph.Algorithms
{
    /// <summary>
    /// Path finding result
    /// </summary>
    public class PathResult<TVertex>
    {
        public List<TVertex> Path { get; set; }
        public double Distance { get; set; }
        public bool Found { get; set; }
        
        public PathResult()
        {
            Path = new List<TVertex>();
        }
    }
    
    /// <summary>
    /// Graph algorithm implementations
    /// </summary>
    public static class GraphAlgorithms
    {
        /// <summary>
        /// Dijkstra's shortest path algorithm
        /// </summary>
        public static PathResult<TVertex> Dijkstra<TVertex, TEdge>(Graph<TVertex, TEdge> graph, TVertex source, TVertex target) 
            where TEdge : IEdge<TVertex>
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            
            var distances = new Dictionary<TVertex, double>();
            var previous = new Dictionary<TVertex, TVertex>();
            var visited = new HashSet<TVertex>();
            var priorityQueue = new SortedSet<(double distance, TVertex vertex)>(Comparer<(double, TVertex)>.Create((a, b) => 
            {
                var cmp = a.distance.CompareTo(b.distance);
                if (cmp == 0)
                    cmp = a.vertex.GetHashCode().CompareTo(b.vertex.GetHashCode());
                return cmp;
            }));
            
            // Initialize distances
            foreach (var vertex in graph.Vertices)
            {
                distances[vertex] = double.PositiveInfinity;
            }
            distances[source] = 0;
            priorityQueue.Add((0, source));
            
            while (priorityQueue.Count > 0)
            {
                var (currentDist, currentVertex) = priorityQueue.Min;
                priorityQueue.Remove(priorityQueue.Min);
                
                if (visited.Contains(currentVertex))
                    continue;
                    
                visited.Add(currentVertex);
                
                if (currentVertex.Equals(target))
                    break;
                
                foreach (var edge in graph.GetOutEdges(currentVertex))
                {
                    var neighbor = edge.Target;
                    var newDist = currentDist + edge.Weight;
                    
                    if (newDist < distances[neighbor])
                    {
                        distances[neighbor] = newDist;
                        previous[neighbor] = currentVertex;
                        priorityQueue.Add((newDist, neighbor));
                    }
                }
            }
            
            // Build path
            var result = new PathResult<TVertex>();
            if (!visited.Contains(target))
            {
                result.Found = false;
                return result;
            }
            
            result.Found = true;
            result.Distance = distances[target];
            
            var current = target;
            while (!current.Equals(source))
            {
                result.Path.Insert(0, current);
                current = previous[current];
            }
            result.Path.Insert(0, source);
            
            return result;
        }
        
        /// <summary>
        /// A* path finding algorithm
        /// </summary>
        public static PathResult<TVertex> AStar<TVertex, TEdge>(Graph<TVertex, TEdge> graph, TVertex source, TVertex target, 
            Func<TVertex, TVertex, double> heuristic) where TEdge : IEdge<TVertex>
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (heuristic == null)
                throw new ArgumentNullException(nameof(heuristic));
            
            var gScore = new Dictionary<TVertex, double>();
            var fScore = new Dictionary<TVertex, double>();
            var previous = new Dictionary<TVertex, TVertex>();
            var openSet = new HashSet<TVertex> { source };
            var priorityQueue = new SortedSet<(double fScore, TVertex vertex)>(Comparer<(double, TVertex)>.Create((a, b) => 
            {
                var cmp = a.fScore.CompareTo(b.fScore);
                if (cmp == 0)
                    cmp = a.vertex.GetHashCode().CompareTo(b.vertex.GetHashCode());
                return cmp;
            }));
            
            // Initialize scores
            foreach (var vertex in graph.Vertices)
            {
                gScore[vertex] = double.PositiveInfinity;
                fScore[vertex] = double.PositiveInfinity;
            }
            gScore[source] = 0;
            fScore[source] = heuristic(source, target);
            priorityQueue.Add((fScore[source], source));
            
            while (openSet.Count > 0)
            {
                var (currentFScore, currentVertex) = priorityQueue.Min;
                priorityQueue.Remove(priorityQueue.Min);
                
                if (currentVertex.Equals(target))
                {
                    return ReconstructPath(previous, source, target, gScore[target]);
                }
                
                openSet.Remove(currentVertex);
                
                foreach (var edge in graph.GetOutEdges(currentVertex))
                {
                    var neighbor = edge.Target;
                    var tentativeGScore = gScore[currentVertex] + edge.Weight;
                    
                    if (tentativeGScore < gScore[neighbor])
                    {
                        previous[neighbor] = currentVertex;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = gScore[neighbor] + heuristic(neighbor, target);
                        
                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                            priorityQueue.Add((fScore[neighbor], neighbor));
                        }
                    }
                }
            }
            
            return new PathResult<TVertex> { Found = false };
        }
        
        /// <summary>
        /// Breadth-First Search (BFS)
        /// </summary>
        public static List<TVertex> BFS<TVertex, TEdge>(Graph<TVertex, TEdge> graph, TVertex start) 
            where TEdge : IEdge<TVertex>
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (start == null)
                throw new ArgumentNullException(nameof(start));
            
            var visited = new HashSet<TVertex>();
            var queue = new Queue<TVertex>();
            var result = new List<TVertex>();
            
            queue.Enqueue(start);
            visited.Add(start);
            
            while (queue.Count > 0)
            {
                var vertex = queue.Dequeue();
                result.Add(vertex);
                
                foreach (var edge in graph.GetOutEdges(vertex))
                {
                    if (!visited.Contains(edge.Target))
                    {
                        visited.Add(edge.Target);
                        queue.Enqueue(edge.Target);
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Depth-First Search (DFS)
        /// </summary>
        public static List<TVertex> DFS<TVertex, TEdge>(Graph<TVertex, TEdge> graph, TVertex start) 
            where TEdge : IEdge<TVertex>
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (start == null)
                throw new ArgumentNullException(nameof(start));
            
            var visited = new HashSet<TVertex>();
            var stack = new Stack<TVertex>();
            var result = new List<TVertex>();
            
            stack.Push(start);
            
            while (stack.Count > 0)
            {
                var vertex = stack.Pop();
                
                if (!visited.Contains(vertex))
                {
                    visited.Add(vertex);
                    result.Add(vertex);
                    
                    foreach (var edge in graph.GetOutEdges(vertex))
                    {
                        if (!visited.Contains(edge.Target))
                        {
                            stack.Push(edge.Target);
                        }
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Topological sort (for directed acyclic graphs)
        /// </summary>
        public static List<TVertex> TopologicalSort<TVertex, TEdge>(Graph<TVertex, TEdge> graph) 
            where TEdge : IEdge<TVertex>
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            
            if (graph.Direction != GraphDirection.Directed)
                throw new InvalidOperationException("Topological sort only works on directed graphs");
            
            var inDegree = new Dictionary<TVertex, int>();
            var queue = new Queue<TVertex>();
            var result = new List<TVertex>();
            
            // Calculate in-degrees
            foreach (var vertex in graph.Vertices)
            {
                inDegree[vertex] = graph.GetInDegree(vertex);
                if (inDegree[vertex] == 0)
                {
                    queue.Enqueue(vertex);
                }
            }
            
            // Process vertices with 0 in-degree
            while (queue.Count > 0)
            {
                var vertex = queue.Dequeue();
                result.Add(vertex);
                
                foreach (var edge in graph.GetOutEdges(vertex))
                {
                    inDegree[edge.Target]--;
                    if (inDegree[edge.Target] == 0)
                    {
                        queue.Enqueue(edge.Target);
                    }
                }
            }
            
            // Check for cycles
            if (result.Count != graph.VertexCount)
            {
                throw new InvalidOperationException("Graph contains cycles and cannot be topologically sorted");
            }
            
            return result;
        }
        
        /// <summary>
        /// Find connected components (for undirected graphs)
        /// </summary>
        public static List<List<TVertex>> ConnectedComponents<TVertex, TEdge>(Graph<TVertex, TEdge> graph) 
            where TEdge : IEdge<TVertex>
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            
            if (graph.Direction != GraphDirection.Undirected)
                throw new InvalidOperationException("Connected components only works on undirected graphs");
            
            var visited = new HashSet<TVertex>();
            var components = new List<List<TVertex>>();
            
            foreach (var vertex in graph.Vertices)
            {
                if (!visited.Contains(vertex))
                {
                    var component = new List<TVertex>();
                    ExploreComponent(graph, vertex, visited, component);
                    components.Add(component);
                }
            }
            
            return components;
        }
        
        private static void ExploreComponent<TVertex, TEdge>(Graph<TVertex, TEdge> graph, TVertex start, 
            HashSet<TVertex> visited, List<TVertex> component) where TEdge : IEdge<TVertex>
        {
            var stack = new Stack<TVertex>();
            stack.Push(start);
            
            while (stack.Count > 0)
            {
                var vertex = stack.Pop();
                
                if (!visited.Contains(vertex))
                {
                    visited.Add(vertex);
                    component.Add(vertex);
                    
                    foreach (var edge in graph.GetOutEdges(vertex))
                    {
                        if (!visited.Contains(edge.Target))
                        {
                            stack.Push(edge.Target);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Calculate PageRank for all vertices
        /// </summary>
        public static Dictionary<TVertex, double> PageRank<TVertex, TEdge>(Graph<TVertex, TEdge> graph, 
            double dampingFactor = 0.85, int maxIterations = 100, double tolerance = 1e-6) 
            where TEdge : IEdge<TVertex>
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            
            var vertices = graph.Vertices.ToList();
            var n = vertices.Count;
            
            if (n == 0)
                return new Dictionary<TVertex, double>();
            
            // Initialize PageRank scores
            var pageRank = vertices.ToDictionary(v => v, v => 1.0 / n);
            var newPageRank = new Dictionary<TVertex, double>();
            
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                var maxDiff = 0.0;
                
                foreach (var vertex in vertices)
                {
                    var rank = (1.0 - dampingFactor) / n;
                    
                    foreach (var edge in graph.GetInEdges(vertex))
                    {
                        var neighbor = edge.Source;
                        var outDegree = graph.GetOutDegree(neighbor);
                        if (outDegree > 0)
                        {
                            rank += dampingFactor * pageRank[neighbor] / outDegree;
                        }
                    }
                    
                    newPageRank[vertex] = rank;
                    maxDiff = Math.Max(maxDiff, Math.Abs(rank - pageRank[vertex]));
                }
                
                // Swap dictionaries
                var temp = pageRank;
                pageRank = newPageRank;
                newPageRank = temp;
                newPageRank.Clear();
                
                // Check convergence
                if (maxDiff < tolerance)
                    break;
            }
            
            return pageRank;
        }
        
        /// <summary>
        /// Calculate degree centrality for all vertices
        /// </summary>
        public static Dictionary<TVertex, double> DegreeCentrality<TVertex, TEdge>(Graph<TVertex, TEdge> graph) 
            where TEdge : IEdge<TVertex>
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            
            var n = graph.VertexCount;
            if (n <= 1)
                return graph.Vertices.ToDictionary(v => v, v => 0.0);
            
            return graph.Vertices.ToDictionary(
                v => v, 
                v => (double)graph.GetDegree(v) / (n - 1)
            );
        }
        
        private static PathResult<TVertex> ReconstructPath<TVertex>(Dictionary<TVertex, TVertex> previous, 
            TVertex source, TVertex target, double distance)
        {
            var path = new List<TVertex>();
            var current = target;
            
            while (!current.Equals(source))
            {
                path.Insert(0, current);
                current = previous[current];
            }
            path.Insert(0, source);
            
            return new PathResult<TVertex>
            {
                Found = true,
                Path = path,
                Distance = distance
            };
        }
    }
    
    /// <summary>
    /// Heuristic functions for A* algorithm
    /// </summary>
    public static class Heuristics
    {
        /// <summary>
        /// Euclidean distance heuristic (for 2D/3D coordinates)
        /// </summary>
        public static double EuclideanDistance(UnityEngine.Vector3 a, UnityEngine.Vector3 b)
        {
            return UnityEngine.Vector3.Distance(a, b);
        }
        
        /// <summary>
        /// Manhattan distance heuristic (for grid-based graphs)
        /// </summary>
        public static double ManhattanDistance(UnityEngine.Vector3 a, UnityEngine.Vector3 b)
        {
            return UnityEngine.Mathf.Abs(a.x - b.x) + UnityEngine.Mathf.Abs(a.y - b.y) + UnityEngine.Mathf.Abs(a.z - b.z);
        }
        
        /// <summary>
        /// Zero heuristic (for Dijkstra's algorithm behavior)
        /// </summary>
        public static double Zero<TVertex>(TVertex a, TVertex b)
        {
            return 0.0;
        }
    }
}