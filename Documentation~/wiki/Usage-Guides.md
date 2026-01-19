# Usage Guide

## Table of Contents

1. [Basic Usage](#basic-usage)
2. [Editor Integration](#editor-integration)
3. [Advanced Features](#advanced-features)
4. [Performance Optimization](#performance-optimization)

## Basic Usage

### 1. Initializing DataCore

#### Option 1: Using DataCoreStore directly
```csharp
using AroAro.DataCore;

// Create a data store with default path
var store = new DataCoreStore();

// Or specify a custom path
var store = new DataCoreStore("Custom/Data/my_database.db");
```

#### Option 2: Using the Editor Component (Recommended for Unity)
```csharp
using AroAro.DataCore;

// Access via Singleton
var store = DataCoreEditorComponent.Instance.GetStore();

// Or via a GameObject reference
var component = GetComponent<DataCoreEditorComponent>();
var store = component.GetStore();
```

### 2. Managing Tabular Data

#### Creating a Tabular Dataset
```csharp
// Create a new tabular dataset
var playerData = store.CreateTabular("player-stats");

// Add numeric columns (supports double, float, int, etc.)
playerData.AddNumericColumn("score", new double[] { 100, 200, 300, 400, 500 });
playerData.AddNumericColumn("level", new float[] { 1, 2, 3, 4, 5 });

// Add string columns
playerData.AddStringColumn("name", new[] { "Alice", "Bob", "Charlie", "David", "Eve" });
playerData.AddStringColumn("class", new[] { "Warrior", "Mage", "Rogue", "Warrior", "Mage" });
```

#### Querying Data
```csharp
using AroAro.DataCore;

// Simple filter
var highScores = playerData.Query()
    .Where("score", QueryOp.Gt, 300)
    .ToRowIndices();

// Multiple conditions (chained)
var warriors = playerData.Query()
    .Where("score", QueryOp.Gt, 200)
    .Where("class", QueryOp.Eq, "Warrior")
    .ToRowIndices();

// Sorting
var sortedPlayers = playerData.Query()
    .OrderByDescending("score")
    .ToRowIndices();
```

#### Updating Data
```csharp
// Retrieve existing dataset
var data = store.Get<ITabularDataset>("player-stats");

// Update a specific row
data.UpdateRow(0, new Dictionary<string, object>
{
    { "score", 150 },
    { "name", "Alice Updated" }
});

// Add a new row
data.AddRow(new Dictionary<string, object>
{
    { "score", 600 },
    { "level", 6 },
    { "name", "Frank" },
    { "class", "Rogue" }
});

// Delete a row
data.DeleteRow(2);
```

### 3. Managing Graph Data

#### Creating a Graph Dataset
```csharp
// Create a new graph
var socialGraph = store.CreateGraph("social-network");

// Add nodes with properties
socialGraph.AddNode("user1", new Dictionary<string, object>
{
    { "name", "Alice" },
    { "age", 25 }
});

socialGraph.AddNode("user2", new Dictionary<string, object>
{
    { "name", "Bob" },
    { "age", 30 }
});

// Add a directed edge
socialGraph.AddEdge("user1", "user2", new Dictionary<string, object>
{
    { "relationship", "friends" },
    { "since", "2023-01-01" }
});
```

#### Graph Queries
```csharp
// Filter nodes
var youngUsers = socialGraph.Query()
    .WhereNodeProperty("age", QueryOp.Lt, 30)
    .ToNodeIds();

// Traverse the graph
var outgoingFromAlice = socialGraph.Query()
    .From("user1")
    .TraverseOut()
    .ToNodeIds();
```

### 4. Persistence

DataCore uses **LiteDB** for automatic persistence. All changes made to datasets are stored in the database file.

- **Automatic Sync**: Managed by `DataCoreEditorComponent` during the Unity lifecycle (e.g., when exiting Play Mode).
- **Manual Checkpoint**: Force write all pending changes to disk.
```csharp
store.Checkpoint();
```

---

## Editor Integration

### 1. DataCoreEditorComponent

#### Setup
1. Create an empty GameObject in your scene.
2. Add the `DataCoreEditorComponent`.
3. Configure the **Database Path** in the Inspector.

#### Inspector Features
- **Datasets**: List all tabular and graph datasets in the current database.
- **Preview**: Inline view of the first few rows/nodes of any dataset.
- **Import/Export**: Tools for CSV and GraphML files.

### 2. CSV Import

#### Via Inspector
1. Select the DataCore GameObject.
2. Locate the **Import CSV** section.
3. Click **Browse** to select your file.
4. Set the dataset name and delimiter.
5. Click **Import CSV**.

#### Via Code
```csharp
var component = DataCoreEditorComponent.Instance;
component.ImportCsvToTabular("Assets/Data/players.csv", "imported-players");
```

### 3. Preview Window
You can open a dedicated preview window for large datasets:
- **Button**: Click "Open Full Preview" in the Inspector dataset list.
- **Code**: `DataCorePreviewWindow.ShowWindow(component, "dataset-name");`

---

## Advanced Features

### 1. Session Management
Sessions provide isolated workspaces for temporary data or complex analysis without affecting the main database.

```csharp
var sessionManager = store.SessionManager;

// Create an isolated session
var session = sessionManager.CreateSession("analysis-01");

// Load a copy of a global dataset into the session
var sessionCopy = session.OpenDataset("player-stats");

// Perform calculations...
// ...

// Persist session results back to the global store if needed
session.PersistDataset("temp-results", "permanent-results");
```

### 2. Microsoft.Data.Analysis (DataFrame) Support
DataCore integrates with the `Microsoft.Data.Analysis` library for advanced data science operations.

#### Working with DataFrames
```csharp
using AroAro.DataCore.Session;
using Microsoft.Data.Analysis;

var session = store.SessionManager.CreateSession("df-session");

// Create or convert to DataFrame
var df = session.ConvertToDataFrame("player-stats");

// Use standard DataFrame API
var filteredDf = df.Filter(df["score"].Description().Count > 0);

// Use DataCore's Fluent Query for DataFrames
var result = session.QueryDataFrame("player-stats")
    .Where("score", ComparisonOp.Gt, 100)
    .Execute("high-score-results");
```

### 3. Events
Subscribe to system events for decoupled architecture.

```csharp
using AroAro.DataCore.Events;

void Start() {
    DataCoreEventManager.DatasetCreated += OnDatasetCreated;
}

void OnDatasetCreated(object sender, DatasetCreatedEventArgs e) {
    Debug.Log($"New dataset created: {e.DatasetName}");
}
```

---

## Performance Optimization

1. **Batch Updates**: Use `AddRows` instead of calling `AddRow` multiple times.
2. **Columnar Access**: Use `GetNumericColumn` to get a `NDArray` for high-speed numeric processing (NumSharp).
3. **Transactions**: Wrap multiple operations in a transaction for both speed and safety.
```csharp
store.ExecuteInTransaction(() => {
    for(int i=0; i<1000; i++) {
        data.AddRow(...);
    }
});
```
4. **Sessions**: Use sessions for large-scale "what-if" analysis to avoid excessive disk I/O on the main database.

## Next Steps

After mastering basic usage, you can:
1. Check the [API Reference](API-Reference) for detailed interfaces.
2. Try the [Examples & Tutorials](Examples-Tutorials) for practical scenarios.
3. Learn about [Performance Optimization](Performance-Optimization) to boost your application's speed.
