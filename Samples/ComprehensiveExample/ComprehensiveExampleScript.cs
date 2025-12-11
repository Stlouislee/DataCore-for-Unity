using UnityEngine;
using DataCore;
using DataCore.UnityIntegration;

namespace DataCore.Samples
{
    /// <summary>
    /// Comprehensive example demonstrating all DataCore features working together
    /// </summary>
    public class ComprehensiveExampleScript : MonoBehaviour
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
                RunComprehensiveExample();
            }
            else
            {
                Debug.LogError("DataManager not found!");
            }
        }
        
        private async void RunComprehensiveExample()
        {
            Debug.Log("=== Comprehensive DataCore Example ===");
            
            // Example 1: Unified data management
            Debug.Log("1. Unified Data Management");
            Debug.Log($"Dataset Count: {dataManager.DataManager.DatasetCount}");
            Debug.Log($"Memory Usage: {dataManager.DataManager.GetTotalMemoryUsage() / (1024.0 * 1024.0):F2} MB");
            
            // Example 2: Tensor operations
            Debug.Log("\n2. Tensor Operations");
            var tensor = dataManager.DataManager.TensorManager.CreateTensor("comprehensive_tensor", new int[] { 2, 3 });
            tensor.Data[0, 0] = 1.0f; tensor.Data[0, 1] = 2.0f; tensor.Data[0, 2] = 3.0f;
            tensor.Data[1, 0] = 4.0f; tensor.Data[1, 1] = 5.0f; tensor.Data[1, 2] = 6.0f;
            Debug.Log("Created tensor:");
            Debug.Log(tensor.ToString());
            
            // Example 3: DataFrame operations
            Debug.Log("\n3. DataFrame Operations");
            var df = dataManager.DataManager.DataFrameManager.CreateDataFrame("comprehensive_df");
            df.AddColumn("ID", new int[] { 1, 2, 3, 4 });
            df.AddColumn("Value", new float[] { 10.5f, 20.3f, 15.7f, 25.1f });
            df.AddColumn("Category", new string[] { "A", "B", "A", "C" });
            Debug.Log("Created DataFrame:");
            Debug.Log(df.ToString());
            
            // Example 4: Graph operations
            Debug.Log("\n4. Graph Operations");
            var graph = dataManager.DataManager.GraphManager.CreateGraph<int>("comprehensive_graph", false); // Undirected
            graph.AddVertex(1); graph.AddVertex(2); graph.AddVertex(3); graph.AddVertex(4);
            graph.AddEdge(1, 2, 1.0f); graph.AddEdge(2, 3, 2.0f); graph.AddEdge(3, 4, 1.5f);
            Debug.Log($"Created graph with {graph.VertexCount} vertices and {graph.EdgeCount} edges");
            
            // Example 5: Data pipeline
            Debug.Log("\n5. Data Pipeline");
            var pipeline = dataManager.DataManager.CreatePipeline("comprehensive_pipeline");
            pipeline.AddStep("load_tensor", async (data) => {
                var tensorData = await dataManager.DataManager.TensorManager.LoadTensorAsync("tensors/example.npy");
                return tensorData;
            });
            pipeline.AddStep("process_data", (data) => {
                Debug.Log("Processing data in pipeline...");
                return data;
            });
            
            // Example 6: Memory management
            Debug.Log("\n6. Memory Management");
            var memoryPool = dataManager.DataManager.MemoryPoolManager;
            var pooledTensor = memoryPool.GetTensorPool("float32").Get(new int[] { 10, 10 });
            Debug.Log("Acquired tensor from memory pool");
            memoryPool.GetTensorPool("float32").Return(pooledTensor);
            Debug.Log("Returned tensor to memory pool");
            
            // Example 7: Performance monitoring
            Debug.Log("\n7. Performance Monitoring");
            var monitor = dataManager.DataManager.PerformanceMonitor;
            monitor.StartOperation("comprehensive_example");
            
            // Simulate some work
            await System.Threading.Tasks.Task.Delay(100);
            
            monitor.EndOperation("comprehensive_example");
            var stats = monitor.GetOperationStats("comprehensive_example");
            Debug.Log($"Operation completed in {stats.AverageExecutionTime:F2}ms");
            
            // Example 8: Cross-platform file operations
            Debug.Log("\n8. Cross-platform File Operations");
            var fileSystem = dataManager.DataManager.FileSystem;
            var exists = await fileSystem.FileExistsAsync("data/examples/test.txt");
            Debug.Log($"File exists: {exists}");
            
            // Example 9: Data binding
            Debug.Log("\n9. Data Binding");
            var bindingComponent = GetComponent<DataBindingComponent>();
            if (bindingComponent != null)
            {
                Debug.Log("Data binding component found");
            }
            
            Debug.Log("\n=== Comprehensive Example Completed ===");
            Debug.Log("All DataCore features demonstrated successfully!");
        }
    }
}