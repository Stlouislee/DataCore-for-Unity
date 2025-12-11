# DataCore for Unity

A comprehensive data management framework for Unity that provides efficient tensor operations, DataFrame processing, and graph algorithms with cross-platform support.

## Features

- **Tensor Operations**: Multi-dimensional array operations powered by NumSharp
- **DataFrame Processing**: Tabular data manipulation with Microsoft.Data.Analysis
- **Graph Algorithms**: Graph data structures with pathfinding and analysis algorithms
- **Cross-Platform Support**: Unified file system abstraction for Windows, macOS, Linux, iOS, Android, and WebGL
- **Memory Optimization**: Multi-level caching and object pooling for performance
- **Serialization**: Multiple format support (NPY, CSV, JSON)
- **Unity Integration**: MonoBehaviour components and Editor tools

## Installation

### Unity Package Manager

Add the following to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.stlouislee.datacore": "https://github.com/Stlouislee/DataCore-for-Unity.git"
  }
}
```

### Meta Files for Unity Package

All necessary .meta files are included with the package. If you need to regenerate meta files for any reason, Unity will automatically create them when importing the package.

### Manual Installation

1. Clone this repository
2. Copy the `DataCore-for-Unity` folder to your Unity project's `Packages` directory
3. Ensure NumSharp.Core.dll and Microsoft.Data.Analysis.dll are available

## Quick Start

### Basic Usage

```csharp
using DataCore;
using DataCore.UnityIntegration;

public class DataExample : MonoBehaviour
{
    private DataManager dataManager;
    
    void Start()
    {
        // Get the data manager
        var dataManagerBehaviour = FindObjectOfType<DataManagerBehaviour>();
        dataManager = dataManagerBehaviour.DataManager;
        
        // Tensor operations
        var tensor = dataManager.TensorManager.CreateTensor("my_tensor", new int[] { 3, 3 });
        
        // DataFrame operations
        var df = dataManager.DataFrameManager.CreateDataFrame("my_df");
        df.AddColumn("Values", new float[] { 1.0f, 2.0f, 3.0f });
        
        // Graph operations
        var graph = dataManager.GraphManager.CreateGraph<string>("my_graph", true);
        graph.AddVertex("A");
        graph.AddEdge("A", "B", 1.0f);
    }
}
```

## Modules

### Tensor Module

Efficient multi-dimensional array operations:

```csharp
var tensorManager = dataManager.TensorManager;
var tensor = tensorManager.CreateTensor("data", new int[] { 10, 10 });
var transposed = tensorManager.Transpose(tensor);
var scaled = tensorManager.Scale(tensor, 2.0f);
await tensorManager.SaveTensorAsync(tensor, "data.npy");
```

### DataFrame Module

Tabular data processing with filtering, grouping, and joins:

```csharp
var dfManager = dataManager.DataFrameManager;
var df = dfManager.CreateDataFrame("employees");
df.AddColumn("Name", new string[] { "Alice", "Bob", "Charlie" });
df.AddColumn("Salary", new float[] { 50000, 60000, 55000 });
var filtered = dfManager.Filter(df, "Salary > 55000");
await dfManager.SaveDataFrameAsync(df, "employees.csv");
```

### Graph Module

Graph algorithms and analysis:

```csharp
var graphManager = dataManager.GraphManager;
var graph = graphManager.CreateGraph<string>("network", true);
graph.AddVertex("A"); graph.AddVertex("B"); graph.AddVertex("C");
graph.AddEdge("A", "B", 1.0f); graph.AddEdge("B", "C", 2.0f);
var path = graphManager.FindShortestPath(graph, "A", "C");
var pageRank = graphManager.CalculatePageRank(graph);
```

## Editor Tools

### Data Manager Window

Access via `Window > DataCore > Data Manager` to manage datasets, monitor performance, and configure settings.

### Graph Visualizer

Access via `Window > DataCore > Graph Visualizer` to visualize graph structures and algorithms.

### Custom Inspectors

Enhanced inspectors for DataCore components with quick access to data management features.

## Samples

### Included Examples

1. **Tensor Example**: Basic tensor operations and serialization
2. **DataFrame Example**: DataFrame manipulation and analysis
3. **Graph Example**: Graph algorithms and pathfinding
4. **Comprehensive Example**: All features working together

### Running Samples

1. Open the sample scenes in `Samples/` directory
2. Attach the corresponding example script to a GameObject
3. Add a `DataManagerBehaviour` component to the scene
4. Run the scene

## Platform Support

- **Windows**: Full support
- **macOS**: Full support
- **Linux**: Full support
- **iOS**: Limited file system access
- **Android**: Limited file system access
- **WebGL**: Limited file system access

## Performance Features

### Memory Pooling

```csharp
var pooledTensor = dataManager.MemoryPoolManager.GetTensorPool("float32").Get(new int[] { 100, 100 });
// Use the tensor...
dataManager.MemoryPoolManager.GetTensorPool("float32").Return(pooledTensor);
```

### Multi-Level Caching

- **L1 Cache**: In-memory (fastest)
- **L2 Cache**: Compressed memory
- **L3 Cache**: Disk storage

### Performance Monitoring

```csharp
var monitor = dataManager.PerformanceMonitor;
monitor.StartOperation("data_processing");
// Process data...
monitor.EndOperation("data_processing");
var stats = monitor.GetOperationStats("data_processing");
```

## API Reference

### Core Classes

- `DataManager`: Main entry point for data operations
- `TensorDataManager`: Tensor operations and management
- `DataFrameManager`: DataFrame operations and management
- `GraphManager`: Graph operations and management
- `MemoryPoolManager`: Memory optimization and pooling
- `PerformanceMonitor`: Performance tracking and analysis

### Unity Integration

- `DataManagerBehaviour`: MonoBehaviour wrapper for DataManager
- `DataBindingComponent`: Data binding for Unity components
- `DataManagerWindow`: Editor window for data management
- `GraphVisualizer`: Graph visualization tool

## Dependencies

- **NumSharp.Core**: Tensor operations
- **Microsoft.Data.Analysis**: DataFrame processing
- **Unity.Collections**: Native collections
- **Unity.Burst**: High-performance compilation

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

MIT License - see LICENSE file for details.

## Support

For issues and questions, please open an issue on GitHub.