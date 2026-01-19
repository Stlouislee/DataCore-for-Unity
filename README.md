# AroAro DataCore (Unity Package)

[![GitHub](https://img.shields.io/badge/GitHub-Repository-blue)](https://github.com/Stlouislee/DataCore-for-Unity)

Tabular + graph datasets with CRUD/query and persistence.

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

- **NumSharp.Core.dll** - Numerical computing backend
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
playerData.AddNumericColumn("score", NumSharp.np.array(new double[] { 100, 200, 300 }));
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
using NumSharp;

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

### Editor Integration
- **Inspector Preview**: View dataset contents directly in Unity Inspector
- **CSV Import**: Import CSV files with automatic type detection
- **Event System**: Monitor dataset changes with comprehensive events

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
