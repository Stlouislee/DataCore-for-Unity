using UnityEngine;
using DataCore;
using DataCore.Graph;
using DataCore.UnityIntegration;

namespace DataCore.Samples
{
    /// <summary>
    /// Example script demonstrating graph operations with DataCore
    /// </summary>
    public class GraphExampleScript : MonoBehaviour
    {
        [SerializeField] private DataManagerBehaviour dataManager;
        
        private void Start()
        {
            if (dataManager == null)
            {
                dataManager = FindObjectOfType<DataManagerBehaviour>();
            }
            
            if (dataManager != null && dataManager.DataManager != null)
            {
                RunGraphExample();
            }
            else
            {
                Debug.LogError("DataManager not found!");
            }
        }
        
        private async void RunGraphExample()
        {
            var graphManager = dataManager.DataManager.GraphManager;
            
            // Example 1: Create a graph
            Debug.Log("=== Graph Operations Example ===");
            
            var graph = graphManager.CreateGraph<string>("example_graph", true); // Directed graph
            
            // Add vertices
            graph.AddVertex("A");
            graph.AddVertex("B");
            graph.AddVertex("C");
            graph.AddVertex("D");
            graph.AddVertex("E");
            
            // Add edges with weights
            graph.AddEdge("A", "B", 4.0f);
            graph.AddEdge("A", "C", 2.0f);
            graph.AddEdge("B", "C", 1.0f);
            graph.AddEdge("B", "D", 5.0f);
            graph.AddEdge("C", "D", 8.0f);
            graph.AddEdge("C", "E", 10.0f);
            graph.AddEdge("D", "E", 2.0f);
            
            Debug.Log("Graph Structure:");
            Debug.Log($"Vertices: {graph.VertexCount}, Edges: {graph.EdgeCount}");
            
            // Example 2: Pathfinding
            var shortestPath = graphManager.FindShortestPath(graph, "A", "E");
            Debug.Log("Shortest Path from A to E:");
            if (shortestPath != null)
            {
                Debug.Log(string.Join(" -> ", shortestPath));
            }
            
            // Example 3: Graph algorithms
            var neighbors = graphManager.GetNeighbors(graph, "B");
            Debug.Log("Neighbors of B:");
            Debug.Log(string.Join(", ", neighbors));
            
            var degree = graphManager.GetDegree(graph, "C");
            Debug.Log($"Degree of C: {degree}");
            
            // Example 4: Save and load graph
            await graphManager.SaveGraphAsync(graph, "graphs/example.json");
            Debug.Log("Graph saved to: graphs/example.json");
            
            var loadedGraph = await graphManager.LoadGraphAsync<string>("graphs/example.json");
            Debug.Log("Loaded Graph:");
            Debug.Log($"Vertices: {loadedGraph.VertexCount}, Edges: {loadedGraph.EdgeCount}");
            
            // Example 5: Graph analysis
            var isConnected = graphManager.IsConnected(graph);
            Debug.Log($"Graph is connected: {isConnected}");
            
            var hasCycle = graphManager.HasCycle(graph);
            Debug.Log($"Graph has cycle: {hasCycle}");
            
            // Example 6: PageRank algorithm
            var pageRank = graphManager.CalculatePageRank(graph);
            Debug.Log("PageRank scores:");
            foreach (var kvp in pageRank)
            {
                Debug.Log($"{kvp.Key}: {kvp.Value:F4}");
            }
            
            Debug.Log("Graph example completed!");
        }
    }
}