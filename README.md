# AroAro DataCore (Unity Package)

[![GitHub](https://img.shields.io/badge/GitHub-Repository-blue)](https://github.com/Stlouislee/DataCore-for-Unity)

Tabular + graph datasets with CRUD/query, persistence, and extensible algorithm components.

## Installation

### Method 1: Git URL (Recommended)

In Unity: **Window → Package Manager → + → Add package from git URL...** and enter:

```
https://github.com/Stlouislee/DataCore-for-Unity.git
```

### Method 2: Manifest.json

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.aroaro.datacore": "https://github.com/Stlouislee/DataCore-for-Unity.git"
  }
}
```

### Method 3: Package Manager UI

1. Open **Window → Package Manager**
2. Click the **+** button
3. Select **Add package from git URL**
4. Enter: `https://github.com/Stlouislee/DataCore-for-Unity.git`
5. Click **Add**

## Dependencies

This package includes precompiled DLLs:

- **Apache.Arrow.dll** - Tabular data serialization
- **LiteDB.dll** - Embedded document database
- **Microsoft.Data.Analysis.dll** - DataFrame support

These dependencies are included in the package and will be automatically available after installation.

## Quick start

### Option 1: Shared Instance Pattern (Recommended)

Add a `DataCoreEditorComponent` to a GameObject in your scene. Other scripts can then access it as a shared singleton:

```csharp
using AroAro.DataCore;

// From any script, access the shared DataCore instance
var store = DataCoreEditorComponent.Instance.GetStore();

// Create and work with datasets
var playerData = store.CreateTabular("player-stats");
playerData.AddNumericColumn("score", new double[] { 100, 200, 300 });
playerData.AddStringColumn("name", new[] { "Alice", "Bob", "Charlie" });

// Query the data
var highScorers = playerData.Query()
    .Where("score", QueryOp.Gt, 150)
    .ToRowIndices();

// Data persists automatically via LiteDB.
// You can force a write to disk if needed:
store.Checkpoint();
```

**Setup:**
1. Create a GameObject in your scene (e.g., "DataCore Manager")
2. Add the `DataCoreEditorComponent` component
3. Configure the persistence path (default: "DataCore/")
4. Enable auto-save on exit (enabled by default)
5. Access from any script using `DataCoreEditorComponent.Instance`

### Option 2: Direct Store Usage

```csharp
using AroAro.DataCore;

// Initialize a store at a specific path
var store = new DataCoreStore("Data/my_database.db");
var t = store.GetOrCreateTabular("my-table");

t.AddNumericColumn("x", new double[] { 1, 2, 3 });
var indices = t.Query().Where("x", QueryOp.Gt, 1.0).ToRowIndices();

// Ensure data is written to disk
store.Checkpoint();

var g = store.GetOrCreateGraph("my-graph");
g.AddNode("a");
g.AddNode("b");
g.AddEdge("a", "b");

store.Checkpoint();
```

### Option 3: Workspace (Recommended for Data Analysis)

The Workspace is a unified in-memory working area — think of it as your "desktop" for data operations. It's always available on `DataCoreStore`, no session management needed.

```csharp
using AroAro.DataCore;
using AroAro.DataCore.Workspace;

var store = new DataCoreStore("Data/my_database.db");

// Load from persistent store into workspace
store.Workspace.Get("player-stats"); // auto-loads from store

// Register computed results directly
var filtered = new List<Dictionary<string, object>>
{
    new() { ["name"] = "Alice", ["score"] = 100.0 },
    new() { ["name"] = "Bob", ["score"] = 200.0 }
};
store.Workspace.Register("top-players", filtered, DataSource.Derived);

// "What do I have right now?" — one call answers everything
var all = store.Workspace.DescribeAll();
// → [{ name: "player-stats", source: Store, rows: 1000, ... },
//    { name: "top-players", source: Derived, rows: 2, ... }]

// One-line summary
store.Workspace.Summary();
// → "Workspace: 2 datasets (1 store, 1 derived)"

// Lifecycle management
store.Workspace.Rename("top-players", "elite-players");
store.Workspace.Clone("elite-players", "elite-backup");
store.Workspace.Remove("elite-backup");
store.Workspace.Clear(); // clear workspace, store data unaffected
```

**Workspace features:**
- **Always available**: `store.Workspace` is ready on store creation
- **Auto-fallback**: `Get()` checks workspace first, then loads from store
- **Source tracking**: Every dataset tagged as Store, Derived, or Imported
- **AI-friendly**: `DescribeAll()` returns schema, row counts, and sample data
- **Lazy caching**: Metadata is computed on-demand and cached until invalidated
- **DataFrame support**: `CreateDataFrame`, `ConvertToDataFrame`, full lifecycle
- **Multi-workspace**: `store.CreateWorkspace("analysis")` + indexer `store["analysis"]`

### Option 4: Agent Tools (For AI Integration)

55 static tools for AI agent integration via `DataCoreTools.Execute()`:

```csharp
using AroAro.DataCore.Tools;

DataCoreTools.Initialize(store);

// Filter with human-readable expressions
var result = DataCoreTools.Execute("workspace_filter", new Dictionary<string, object>
{
    ["source"] = "player-stats",
    ["filter"] = "score > 300 AND class == Warrior",
    ["resultName"] = "top-warriors"
});
// → {"success":true,"action":"workspace_filter","result":{"name":"top-warriors","rows":42,...}}

// Join two datasets
DataCoreTools.Execute("workspace_join", new Dictionary<string, object>
{
    ["left"] = "Players", ["right"] = "Scores",
    ["leftKey"] = "id", ["rightKey"] = "player_id",
    ["resultName"] = "PlayerScores"
});

// Graph operations
DataCoreTools.Execute("workspace_open_graph", new Dictionary<string, object>
{
    ["dataset"] = "social-network"
});

// Get all tool schemas for Agent framework registration
string schemas = DataCoreTools.GetToolSchemas();
```

## Editor Integration

### Creating Data Core
- **GameObject → Data Core → Create Data Core** - Creates a Data Core GameObject with full editor support
- **Tools → DataCore → Create Data Core GameObject** - Same as above

### Inspector Features
When you select a Data Core GameObject, the Inspector shows:
- **Persistence Configuration**: Set storage path and auto-save options
- **Datasets Panel**: View all datasets with type, size, and delete options
- **Create Buttons**: Create new Tabular or Graph datasets
- **Actions**: Save All, Load All

### Dataset Preview
For each dataset, you can see:
- **Tabular**: Row count, column names
- **Graph**: Node count, edge count

### Enhanced Preview Features
- **Inline Preview**: View first 5 rows of tabular data or sample nodes/edges in the Inspector
- **Full Preview Window**: Click "Open Full Preview" to open a dedicated window showing:
  - **Tabular Data**: First 100 rows with column headers and formatted values
  - **Graph Data**: First 50 nodes and edges with detailed properties
  - **Scrollable Interface**: Handle large datasets with ease

### Event System
DataCore now includes a comprehensive event system for monitoring dataset changes:

```csharp
using AroAro.DataCore.Events;

// Subscribe to dataset events
DataCoreEventManager.DatasetCreated += (sender, args) => 
{
    // Handle dataset creation
    Console.WriteLine($"Dataset created: {args.DatasetName}");
};

DataCoreEventManager.DatasetModified += (sender, args) => 
{
    // Handle dataset modifications
    Console.WriteLine($"Dataset modified: {args.DatasetName}");
};

// Available events:
// - DatasetCreated: When a new dataset is created
// - DatasetDeleted: When a dataset is deleted
// - DatasetLoaded: When a dataset is loaded from file
// - DatasetSaved: When a dataset is saved to file
// - DatasetModified: When data is modified (add/remove rows, columns, nodes, edges)
// - DatasetQueried: When a query is executed
```

## Sample Datasets

### California Housing Dataset
A built-in sample dataset containing housing data for California districts.

**Usage:**
```csharp
// Load the sample dataset
CaliforniaHousingDataset.LoadIntoDataCore();

// Access and query the dataset
var store = DataCoreEditorComponent.Instance.GetStore();
var housingData = store.Get<Tabular.TabularData>("california-housing");

var expensiveHouses = housingData.Query()
    .Where("median_house_value", Tabular.TabularOp.Gt, 500000)
    .ToRowIndices();
```

**Features:**
- Built-in sample data with housing statistics
- Ready-to-use queries and examples
- Automatic persistence across play mode sessions

## Performance Features

### Lazy Loading
DataCore supports lazy loading for better performance with large datasets:

```csharp
// Register metadata without loading data
store.RegisterMetadata("large-dataset", DataSetKind.Tabular, "path/to/large-dataset.arrow");

// Data is loaded on first access
var dataset = store.Get<Tabular.TabularData>("large-dataset");
```

### CSV Import
Import CSV files with optimized performance:

```csharp
var dataCore = DataCoreEditorComponent.Instance;
dataCore.ImportCsvToTabular("path/to/file.csv", "MyDataset", true, ',');
```

## Key Features

### Data Management
- **Tabular Data**: Store and query structured data with numeric and string columns
- **Graph Data**: Create and manipulate graph datasets with nodes and edges
- **Persistence**: Automatic saving and loading of datasets
- **Workspace**: Unified in-memory working area for data analysis workflows — register results, track sources, introspect state
- **Agent Tools**: 55 tools via `DataCoreTools.Execute()` with JSON Schema exposure for AI frameworks
- **Filter Expressions**: Human-readable syntax (`age > 18 AND city == Shanghai`) with predicate cache
- **DataFrame**: First-class DataFrame support within workspace

### Algorithm Framework
- **Built-in Algorithms**: PageRank, Connected Components, Min-Max Normalization
- **Composable Pipelines**: Chain algorithms sequentially with automatic data flow
- **Algorithm Registry**: Discover, register, and look up algorithms at runtime
- **Extensible**: Create custom algorithms by extending base classes

### Analysis API
- **Statistical Analysis**: `describe`, `correlation`, `outliers`, `clustering`, `distribution` via `workspace_analysis`
- **Graph Analysis**: `centrality`, `communities`, `shortest_path` via `workspace_analysis`
- **Algorithm Bridge**: Execute registered algorithms and list available ones via `workspace_algorithm`

### Editor Integration
- **Inspector Preview**: View dataset contents directly in Unity Inspector
- **CSV Import**: Import CSV files with automatic type detection
- **Event System**: Monitor dataset changes and algorithm execution with comprehensive events

### Performance
- **Lazy Loading**: Load large datasets only when needed
- **Optimized Processing**: Efficient batch processing for CSV import
- **Memory Efficient**: Better handling of large datasets

## Getting Started with Features

### Using the Preview System
```csharp
// Access preview functionality through DataCoreEditorComponent
var component = DataCoreEditorComponent.Instance;

// Preview is automatically available in the Inspector
// Or open full preview window programmatically
DataCorePreviewWindow.ShowWindow(component, "my-dataset");
```

### Using the Event System
```csharp
// Subscribe to events
DataCoreEventManager.DatasetCreated += OnDatasetCreated;
DataCoreEventManager.DatasetModified += OnDatasetModified;

void OnDatasetCreated(object sender, DatasetCreatedEventArgs args)
{
    // Handle new dataset creation
    Console.WriteLine($"New dataset: {args.Dataset.Name}");
}

void OnDatasetModified(object sender, DatasetModifiedEventArgs args)
{
    // Handle dataset modifications
    Console.WriteLine($"Dataset modified: {args.DatasetName}");
}
```

### Using Lazy Loading
```csharp
// Register metadata for delayed loading
store.RegisterMetadata("big-data", DataSetKind.Tabular, "path/to/big-data.arrow");

// Data loads automatically on first access
var data = store.Get<Tabular.TabularData>("big-data");
```

### Using the Algorithm Framework

```csharp
using AroAro.DataCore.Algorithms;
using AroAro.DataCore.Algorithms.Graph;
using AroAro.DataCore.Algorithms.Tabular;
```

**Run a single algorithm on a graph:**
```csharp
// Look up a registered algorithm
var algo = AlgorithmRegistry.Default.Get("PageRank");

// Configure parameters and execute
var context = AlgorithmContext.Create()
    .WithParameter("dampingFactor", 0.85)
    .WithParameter("maxIterations", 100)
    .Build();

var result = algo.Execute(myGraph, context);
if (result.Success)
{
    var rankedGraph = result.OutputDataset as IGraphDataset;
    double score = (double)rankedGraph.GetNodeProperties("nodeA")["pagerank"];
    int iterations = (int)result.Metrics["iterations"];
    bool converged = (bool)result.Metrics["converged"];
}
```

**Normalize tabular data:**
```csharp
var result = new MinMaxNormalizeAlgorithm().Execute(myTable,
    AlgorithmContext.Create()
        .WithParameter("rangeMin", -1.0)
        .WithParameter("rangeMax", 1.0)
        .WithParameter("columns", new[] { "score", "age" })
        .Build());

var normalizedTable = result.OutputDataset as ITabularDataset;
```

**Chain algorithms in a pipeline:**
```csharp
var pipeline = new AlgorithmPipeline("GraphAnalysis")
    .Add(new PageRankAlgorithm(), b => b.WithParameter("dampingFactor", 0.9))
    .Add(new ConnectedComponentsAlgorithm());

var pipelineResult = pipeline.Execute(myGraph);
// pipelineResult.FinalOutput has both "pagerank" and "componentId" on every node
// pipelineResult.StepResults gives per-step metrics
// pipelineResult.GetAllMetrics() aggregates all metrics
```

**Create a custom algorithm:**
```csharp
public class ShortestPathAlgorithm : GraphAlgorithmBase
{
    public override string Name => "ShortestPath";
    public override string Description => "Dijkstra's shortest path from a source node.";

    public override IReadOnlyList<AlgorithmParameterDescriptor> Parameters { get; } =
        new List<AlgorithmParameterDescriptor>
        {
            new("sourceNode", "Starting node ID", typeof(string), required: true),
        };

    protected override AlgorithmResult ExecuteGraph(
        IGraphDataset input, AlgorithmContext context)
    {
        string source = context.GetRequired<string>("sourceNode");
        // ... your algorithm logic ...
        return AlgorithmResult.Succeeded(Name, outputGraph, metrics);
    }
}

// Register it
AlgorithmRegistry.Default.Register(new ShortestPathAlgorithm());
```

**Monitor algorithm execution via events:**
```csharp
DataCoreEventManager.AlgorithmStarted += (sender, args) =>
    Debug.Log($"Algorithm started: {args.AlgorithmName}");

DataCoreEventManager.AlgorithmCompleted += (sender, args) =>
    Debug.Log($"Algorithm completed: {args.AlgorithmName} in {args.Duration.TotalMilliseconds}ms");

DataCoreEventManager.PipelineCompleted += (sender, args) =>
    Debug.Log($"Pipeline '{args.PipelineName}': {args.StepCount} steps in {args.Duration.TotalMilliseconds}ms");
```

### Using the Analysis API

```csharp
using AroAro.DataCore.Tools;

DataCoreTools.Initialize(store);

// Statistical analysis — describe all columns
var describeResult = DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
{
    ["workspace"] = "default",
    ["analysis"] = "describe",
    ["dataset"] = "player-stats"
});
// → per-column profile: type, non-null count, unique count, min, max, mean, median, std, skewness, kurtosis, null rate

// Correlation matrix
var corrResult = DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
{
    ["workspace"] = "default",
    ["analysis"] = "correlation",
    ["dataset"] = "player-stats",
    ["columns"] = new[] { "score", "playtime", "level" }
});

// Outlier detection
var outlierResult = DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
{
    ["workspace"] = "default",
    ["analysis"] = "outliers",
    ["dataset"] = "player-stats",
    ["column"] = "score"
});

// K-Means clustering
var clusterResult = DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
{
    ["workspace"] = "default",
    ["analysis"] = "clustering",
    ["dataset"] = "player-stats",
    ["k"] = 3,
    ["features"] = new[] { "score", "playtime" }
});

// Graph analysis — centrality
var centralityResult = DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
{
    ["workspace"] = "default",
    ["analysis"] = "centrality",
    ["graph"] = "social-network",
    ["method"] = "betweenness"
});

// Community detection
var communityResult = DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
{
    ["workspace"] = "default",
    ["analysis"] = "communities",
    ["graph"] = "social-network"
});

// Execute a registered algorithm via Agent tool
var algoResult = DataCoreTools.Execute("workspace_algorithm", new Dictionary<string, object>
{
    ["workspace"] = "default",
    ["algorithm"] = "PageRank",
    ["dataset"] = "social-graph",
    ["resultName"] = "ranked_graph",
    ["params"] = new Dictionary<string, object>
    {
        ["dampingFactor"] = 0.85,
        ["maxIterations"] = 200
    }
});

// List all available algorithms
var listResult = DataCoreTools.Execute("workspace_algorithm", new Dictionary<string, object>
{
    ["algorithm"] = "list"
});
// → [{ name: "PageRank", ... }, { name: "ConnectedComponents", ... }, { name: "MinMaxNormalize", ... }]
```
