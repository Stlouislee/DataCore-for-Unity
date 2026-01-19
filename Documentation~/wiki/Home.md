# AroAro DataCore for Unity

AroAro DataCore is a powerful data management suite for Unity, providing storage, query, and persistence functions for both tabular and graph data.

## Key Features

### 📊 Data Management
- **Tabular Data**: Structured storage with complex query support using NumSharp and Microsoft.Data.Analysis.
- **Graph Data**: Property graph support for nodes and edges.
- **Persistence**: Automated persistence via LiteDB embedded database.
- **I/O Support**: Native importers for CSV and GraphML formats.

### 🎮 Unity Integration
- **Editor Interface**: Manage your data directly in the Inspector.
- **Real-time Preview**: Fast inline previews and dedicated data visualization windows.
- **Component-based**: Seamless integration via simple MonoBehaviour components.
- **Event System**: Full lifecycle events for dataset operations.

### ⚡ Performance
- **Lazy Loading**: On-demand loading for massive datasets.
- **Batch Processing**: High-performance bulk data operations.
- **Memory Optimized**: Smart memory management and session isolation.

### 🔧 Advanced Capabilities
- **DataFrame Support**: Full integration with the .NET DataFrame API.
- **Session Management**: Isolated workspaces for multi-task data processing.
- **Sample Datasets**: Built-in examples like California Housing for quick prototyping.
- **Extensible API**: A clean, interface-driven programming model.

## Quick Start

### Installation
```bash
# Add as a package via Unity Package Manager
# URL: https://github.com/Stlouislee/DataCore-for-Unity.git
```

### Basic Code Example
```csharp
using AroAro.DataCore;

// Initialize store
var store = new DataCoreStore();

// Create a table
var playerData = store.CreateTabular("player-stats");
playerData.AddNumericColumn("score", new double[] { 100, 200, 300 });
playerData.AddStringColumn("name", new[] { "Alice", "Bob", "Charlie" });

// Query data
var highScorers = playerData.Query()
    .Where("score", QueryOp.Gt, 150)
    .ToRowIndices();
```

### Using the Editor
1. Create a GameObject in your scene.
2. Add the `DataCoreEditorComponent`.
3. Manage and preview your data entirely within the Inspector.

## Documentation Navigation

- [Installation Guide](Installation)
- [Core Concepts](Core-Concepts)
- [Usage Guide](Usage-Guides)
- [API Reference](API-Reference)
- [Examples & Tutorials](Examples-Tutorials)
- [Performance Optimization](Performance-Optimization)
- [Troubleshooting](Troubleshooting)

## Support

- GitHub Repo: [DataCore-for-Unity](https://github.com/Stlouislee/DataCore-for-Unity)
- Issues: [GitHub Issues](https://github.com/Stlouislee/DataCore-for-Unity/issues)
- Contributions: Pull requests are welcome for both code and documentation!
