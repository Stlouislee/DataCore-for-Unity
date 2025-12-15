# AroAro DataCore (Unity Package)

Tabular + graph datasets with CRUD/query and persistence.

## Install via Git URL

In Unity: **Window → Package Manager → + → Add package from git URL...** and enter:

```
https://github.com/Stlouislee/DataCore-for-Unity.git
```

Or add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.aroaro.datacore": "https://github.com/Stlouislee/DataCore-for-Unity.git"
  }
}
```

## Dependencies

This package includes precompiled DLLs:

- **NumSharp.Core.dll** - Numerical computing backend
- **Apache.Arrow.dll** - Tabular data serialization

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
    .Where("score", Tabular.TabularOp.Gt, 150)
    .ToRowIndices();

// Data will auto-save when scene ends (if autoSaveOnExit is enabled)
// Or manually save: DataCoreEditorComponent.Instance.SaveDataset("player-stats");
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
using AroAro.DataCore.Tabular;
using NumSharp;

var store = new DataCoreStore();
var t = store.CreateTabular("my-table");

t.AddNumericColumn("x", np.array(new float[] { 1, 2, 3 }));
var indices = t.Query().Where("x", TabularOp.Gt, 1f).ToRowIndices();

store.Save("my-table", "my-table.arrow");
var loaded = store.Load("my-table.arrow");

var g = store.CreateGraph("my-graph");
g.AddNode("a");
g.AddNode("b");
g.AddEdge("a", "b");
store.Save("my-graph", "my-graph.dcgraph");
```

## Self-Test

### Method 1: GameObject (Play Mode)
- Create empty GameObject
- Add `DataCoreSelfTest` component
- Enter Play Mode → tests run automatically

### Method 2: Editor Menu
- **Tools → DataCore → Run Self-Test** (runs in Editor)
- **Tools → DataCore → Create Test GameObject** (creates test GameObject)

## Editor Integration

### Creating Data Core
- **GameObject → Data Core → Create Data Core** - Creates a Data Core GameObject with full editor support
- **Tools → DataCore → Create Data Core GameObject** - Same as above

### Inspector Features
When you select a Data Core GameObject, the Inspector shows:
- **Persistence Configuration**: Set storage path and auto-save options
- **Datasets Panel**: View all datasets with type, size, and delete options
- **Create Buttons**: Create new Tabular or Graph datasets
- **Actions**: Save All, Load All, Run Tests

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
    Debug.Log($"Dataset created: {args.DatasetName} ({args.DatasetKind})");
};

DataCoreEventManager.DatasetModified += (sender, args) => 
{
    Debug.Log($"Dataset modified: {args.DatasetName} - {args.Operation}");
};

DataCoreEventManager.DatasetSaved += (sender, args) => 
{
    Debug.Log($"Dataset saved: {args.DatasetName} to {args.FilePath}");
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

**Usage Methods:**

**Method 1: Static Access (Recommended)**
```csharp
// Load dataset directly
CaliforniaHousingDataset.LoadIntoDataCore();

// Or create dataset manually
var dataset = CaliforniaHousingDataset.CreateDataset();

// Get sample queries
var queries = CaliforniaHousingDataset.GetSampleQueries();

// Get statistics
var stats = CaliforniaHousingDataset.GetStatistics();
```

**Method 2: Component-based**
1. Add `CaliforniaHousingLoader` component to a GameObject
2. Dataset will be loaded on start (configurable)

**Features:**
- **Built-in sample data** - No external files required
- **10 properties** with 9 attributes each (sample subset)
- **Sample queries** included
- **Statistics** generation
- **Static access** - No GameObject required

**Usage:**
```csharp
// Access the loaded dataset
var store = DataCoreEditorComponent.Instance.GetStore();
var housingData = store.Get<Tabular.TabularData>("california-housing");

// Run queries
var expensiveHouses = housingData.Query()
    .Where("median_house_value", Tabular.TabularOp.Gt, 500000)
    .ToRowIndices();

// Persist dataset across play mode sessions
DataCoreEditorComponent.Instance.PersistDataset("california-housing");
```

**Play Mode Persistence:**
- **Auto-save on exit**: Datasets are automatically saved when exiting play mode
- **Manual persistence**: Use `PersistDataset()` to explicitly save datasets
- **Reload persisted data**: Use `LoadPersistedDataset()` to reload after play mode

**Columns:**
- `longitude`, `latitude` - Geographic coordinates
- `housing_median_age` - Median age of houses
- `total_rooms`, `total_bedrooms` - Room counts
- `population`, `households` - Demographic data
- `median_income` - Median income (in tens of thousands)
- `median_house_value` - Median house value (target variable)

## Performance Features

### Lazy Loading
DataCore supports lazy loading for better performance with large datasets:

```csharp
// Register metadata without loading data
store.RegisterMetadata("large-dataset", DataSetKind.Tabular, "path/to/large-dataset.arrow");

// Data is loaded on first access
var dataset = store.Get<Tabular.TabularData>("large-dataset");
```

**Benefits:**
- Faster startup times
- Reduced memory usage
- Automatic loading when needed

### CSV Import Performance
Optimized CSV import with:
- **Batch Processing**: Process data in batches for better performance
- **Streaming Support**: Handle large files efficiently
- **Type Detection**: Automatic numeric/string column detection

### Editor Interface
1. Select a Data Core GameObject
2. In Inspector, find the **CSV Import** section
3. Browse and select a CSV file
4. Configure options:
   - Dataset Name: Name for the new dataset
   - Has Header: Check if first row contains column names
   - Delimiter: CSV delimiter (default: comma)
5. Click **Import CSV**

### Menu Option
- **Tools → DataCore → Import CSV** - Quick CSV import from menu

### Code Usage
```csharp
var dataCore = DataCoreEditorComponent.Instance;
dataCore.ImportCsvToTabular("path/to/file.csv", "MyDataset", true, ',');
```

### CSV Format Support
- Comma-separated values
- Quoted fields with embedded commas
- Header row detection
- Automatic type detection (numeric vs string)

## New Features Summary

### Enhanced Preview System
- **Inline Inspector Preview**: Quick view of dataset contents directly in the Inspector
- **Full Preview Window**: Dedicated window for comprehensive data browsing
- **Tabular Data Preview**: View column headers and first 100 rows
- **Graph Data Preview**: Browse nodes and edges with properties

### Event System
- **Dataset Lifecycle Events**: Created, deleted, loaded, saved
- **Modification Events**: Track changes to rows, columns, nodes, edges
- **Query Events**: Monitor query execution
- **Easy Integration**: Simple event subscription model

### Performance Optimizations
- **Lazy Loading**: Load data only when needed
- **Batch Processing**: Efficient CSV import
- **Memory Management**: Better handling of large datasets

### Cross-Platform Support
- **Unity Package Manager**: Easy installation
- **NumSharp Backend**: Numerical computing support
- **Apache Arrow**: Efficient tabular data serialization
- **Custom Graph Format**: Optimized for Unity

## Getting Started with New Features

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
    Debug.Log($"New dataset: {args.Dataset.Name}");
}

void OnDatasetModified(object sender, DatasetModifiedEventArgs args)
{
    Debug.Log($"Dataset {args.DatasetName} modified: {args.Operation}");
}
```

### Using Lazy Loading
```csharp
// Register metadata for delayed loading
store.RegisterMetadata("big-data", DataSetKind.Tabular, "path/to/big-data.arrow");

// Data loads automatically on first access
var data = store.Get<Tabular.TabularData>("big-data");
```
