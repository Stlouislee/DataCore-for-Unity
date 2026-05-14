# API Reference

This document provides a comprehensive overview of the public API for AroAro DataCore. This is the primary reference for developers looking to integrate with or extend the DataCore system.

## Table of Contents

- [AroAro.DataCore](#aroarodatacore)
  - [DataCoreStore](#datacorestore)
  - [IDataSet](#idataset)
  - [ITabularDataset](#itabulardataset)
  - [IGraphDataset](#igraphdataset)
  - [QueryOp (Enum)](#queryop-enum)
  - [ITabularQuery](#itabulardatery)
  - [IGraphQuery](#igraphquery)
- [AroAro.DataCore.Workspace](#aroarodatacoreworkspace)
  - [IWorkspace](#iworkspace)
  - [Workspace](#workspace)
- [AroAro.DataCore.Tools](#aroarodatacoretools)
  - [DataCoreTools](#datacoretools)
  - [FilterExpressionParser](#filterexpressionparser)
  - [ToolResult](#toolresult)
- [AroAro.DataCore (Unity Integration)](#unity-integration)
  - [DataCoreEditorComponent](#datacoreeditorcomponent)
- [AroAro.DataCore.Events](#aroarodatacoreevents)
  - [DataCoreEventManager](#datacoreeventmanager)
- [AroAro.DataCore.Session](#aroarodatacoresession) *(Deprecated)*
  - [SessionManager](#sessionmanager)
  - [ISession](#isession)

---

## AroAro.DataCore

### DataCoreStore
The main entry point for the DataCore system. It manages the lifecycle of datasets, persistence via LiteDB, and transaction handling.

#### Constructors
- `DataCoreStore()`: Initializes the store using the default database path.
- `DataCoreStore(string dbPath)`: Initializes the store at the specified database file path.

#### Properties
- `Names`: A collection of all dataset names in the store.
- `TabularNames`: Names of all tabular datasets.
- `GraphNames`: Names of all graph datasets.
- `SessionManager`: Accesses the `SessionManager` instance for this store.
- `DatabasePath`: The absolute path to the active database file.
- `UnderlyingStore`: Accesses the internal `IDataStore` (advanced use).

#### Methods (Management)
- `ITabularDataset CreateTabular(string name)`: Creates a new tabular dataset.
- `IGraphDataset CreateGraph(string name)`: Creates a new graph dataset.
- `T Get<T>(string name) where T : class, IDataSet`: Retrieves a dataset by name and casts it to the requested type.
- `bool TryGet(string name, out IDataSet dataSet)`: Attempts to find a dataset without throwing an exception.
- `bool HasDataset(string name)`: Checks if a dataset exists by name.
- `bool Delete(string name)`: Deletes a dataset from the store.
- `void ClearAll()`: Wipes all data from the database.
- `void Checkpoint()`: Forces a write of all pending changes to the disk.

#### Methods (Transactions)
- `bool BeginTransaction()`: Starts a manual transaction.
- `bool Commit()`: Commits current transaction.
- `bool Rollback()`: Rolls back current transaction.
- `void ExecuteInTransaction(Action action)`: Executes an action within a transaction block.

---

### IDataSet
The base interface for all datasets in the system.

#### Properties
- `Name`: The unique name of the dataset.
- `Kind`: The type of dataset (`DataSetKind.Tabular` or `DataSetKind.Graph`).
- `Id`: Internal unique identifier.

---

### ITabularDataset
Interface for table-like datasets providing row and column operations.

#### Properties
- `RowCount`: Total number of rows.
- `ColumnCount`: Total number of columns.
- `ColumnNames`: Collection of all column names.

#### Column Operations
- `void AddNumericColumn(string name, double[] data)`: Adds a column of numeric values.
- `void AddStringColumn(string name, string[] data)`: Adds a column of string values.
- `bool HasColumn(string name)`: Checks if a column exists.
- `NDArray GetNumericColumn(string name)`: Returns column data as a NumSharp NDArray.
- `string[] GetStringColumn(string name)`: Returns column data as a string array.

#### Row Operations
- `void AddRow(IDictionary<string, object> values)`: Adds a single row.
- `int AddRows(IEnumerable<IDictionary<string, object>> rows)`: Batch adds multiple rows (significantly faster).
- `bool UpdateRow(int rowIndex, IDictionary<string, object> values)`: Updates data at a specific index.
- `bool DeleteRow(int rowIndex)`: Removes a row by index.

#### Querying
- `ITabularQuery Query()`: Returns a fluent query builder.
- `RawResult ExecuteRaw(string sql, params object[] args)`: Executes native LiteDB SQL-like queries. Returns either tabular data (for SELECT) or scalar values (for COUNT/UPDATE/DELETE). Only supported on LiteDB-backed datasets.

---

### IGraphDataset
Interface for graph-based datasets (Nodes and Edges).

#### Properties
- `NodeCount`: Total number of nodes.
- `EdgeCount`: Total number of edges.

#### Node Operations
- `void AddNode(string id, IDictionary<string, object> properties = null)`: Adds a node.
- `bool RemoveNode(string id)`: Removes a node and its associated edges.
- `bool HasNode(string id)`: Checks node existence.
- `IDictionary<string, object> GetNodeProperties(string id)`: Retrieves node attributes.
- `void UpdateNodeProperties(string id, IDictionary<string, object> properties)`: Updates node attributes.

#### Edge Operations
- `void AddEdge(string source, string target, IDictionary<string, object> properties = null)`: Adds a directed edge.
- `bool RemoveEdge(string source, string target)`: Removes an edge.
- `IDictionary<string, object> GetEdgeProperties(string source, string target)`: Retrieves edge attributes.

#### Querying
- `IGraphQuery Query()`: Returns a graph query builder.

---

### QueryOp (Enum)
Used for defining comparison operations in both Tabular and Graph queries.

- `Eq`: Equals (==)
- `Ne`: Not Equals (!=)
- `Gt`: Greater Than (>)
- `Ge`: Greater Than or Equal To (>=)
- `Lt`: Less Than (<)
- `Le`: Less Than or Equal To (<=)
- `Contains`: String contains substring.
- `StartsWith`: String starts with prefix.
- `EndsWith`: String ends with suffix.

---

### ITabularQuery
Fluent API for filtering and sorting tabular data.

#### Filtering
- `Where(string column, QueryOp op, object value)`: General filter.
- `WhereEquals(column, value)` / `WhereNotEquals(column, value)`: Comparison shortcuts.
- `WhereBetween(string column, object min, object max)`: Range filter.
- `WhereIn(string column, IEnumerable<object> values)`: Set membership.

#### Sorting & Projection
- `OrderBy(string column)` / `OrderByDescending(string column)`: Sorting.
- `Select(params string[] columns)`: Limit the columns in the result.

#### Execution
- `IEnumerable<IDictionary<string, object>> Execute()`: Returns matching rows as dictionaries.
- `int[] ToRowIndices()`: Returns the indices of matching rows.
- `int Count()`: Returns number of matches.
- `bool Any()`: Checks if any row matches.

---

### IGraphQuery
Fluent API for traversing and filtering graph data.

#### Filtering
- `WhereNodeProperty(string property, QueryOp op, object value)`: Filter nodes by property.
- `WhereEdgeProperty(property, QueryOp op, object value)`: Filter edges by property.

#### Traversal
- `From(string nodeId)`: Set the starting node for traversal.
- `TraverseOut(string edgeType = null)`: Follow outgoing edges.
- `TraverseIn(string edgeType = null)`: Follow incoming edges.

#### Execution
- `IEnumerable<string> ToNodeIds()`: Returns IDs of matching nodes.
- `IEnumerable<(string, string)> ToEdges()`: Returns (SourceID, TargetID) pairs.

---

## AroAro.DataCore.Workspace

### IWorkspace
Unified in-memory working area — the "desktop" for data operations.

#### Properties
- `DatasetNames`: Names of datasets in the workspace.
- `DatasetCount`: Number of datasets.
- `AllNames`: Unified view (store ∪ workspace).
- `DataFrameNames`: Names of DataFrames in the workspace.
- `DataFrameCount`: Number of DataFrames.

#### Registration
- `void Register(name, dataset, source, retention)`: Register an IDataSet.
- `void Register(name, data, source, retention)`: Register from dictionary data.
- `void RegisterAuto(baseName, dataset, source, retention)`: Auto-naming on conflict.

#### Retrieval
- `IDataSet Get(name)`: Get dataset (workspace first, fallback to store).
- `bool Has(name)`: Check existence in workspace only.
- `bool TryPeek(name, out entry)`: Metadata-only lookup.

#### Introspection
- `WorkspaceEntry Describe(name)`: Single dataset details.
- `IReadOnlyList<WorkspaceEntry> DescribeAll()`: All dataset details (cached).
- `string Summary()`: One-line workspace status.

#### Lifecycle
- `bool Remove(name)`: Remove from workspace.
- `bool Rename(oldName, newName)`: Rename dataset.
- `IDataSet Clone(name, newName)`: Deep copy.
- `void Clear()`: Clear workspace (not store).

#### DataFrame
- `DataFrame CreateDataFrame(name)`: Create empty DataFrame.
- `DataFrame GetDataFrame(name)`: Get DataFrame.
- `bool HasDataFrame(name)`: Check existence.
- `bool RemoveDataFrame(name)`: Remove DataFrame.
- `DataFrame ConvertToDataFrame(datasetName)`: Convert tabular to DataFrame.

---

### Workspace
Concrete implementation of `IWorkspace`. Created automatically by `DataCoreStore`.

---

## AroAro.DataCore.Tools

### DataCoreTools
Static dispatch layer for 55 Agent tools.

#### Static Methods
- `void Initialize(DataCoreStore store)`: Bind to store instance.
- `string Execute(string toolName, Dictionary<string, object> args)`: Route and execute a tool, returns JSON.
- `string GetToolSchemas()`: Returns JSON Schema array for all 55 tools.
- `IReadOnlyCollection<string> GetToolNames()`: Returns all tool names.

#### Tool Categories (55 total)
| Category | Count | Tools |
|----------|-------|-------|
| Management | 3 | `workspace_create`, `workspace_destroy`, `workspace_list` |
| Loading | 3 | `workspace_open`, `workspace_open_ref`, `workspace_import_csv` |
| Inspection | 4 | `workspace_describe`, `workspace_sample`, `workspace_schema`, `workspace_statistics` |
| Transform | 9 | `workspace_filter`, `workspace_select`, `workspace_rename_columns`, `workspace_sort`, `workspace_distinct`, `workspace_add_column`, `workspace_drop_columns`, `workspace_limit`, `workspace_random_sample` |
| Combine | 3 | `workspace_join`, `workspace_union`, `workspace_cross` |
| Aggregate | 3 | `workspace_aggregate`, `workspace_summarize`, `workspace_count` |
| Persist | 3 | `workspace_save`, `workspace_export_csv`, `workspace_export_json` |
| Modify | 4 | `workspace_update`, `workspace_delete_rows`, `workspace_append`, `workspace_clear` |
| Meta | 5 | `workspace_clone`, `workspace_rename_dataset`, `workspace_remove`, `workspace_search`, `workspace_diff` |
| DataFrame | 5 | `workspace_dataframe_create`, `workspace_dataframe_convert`, `workspace_dataframe_list`, `workspace_dataframe_remove`, `workspace_dataframe_to_dataset` |
| Graph | 5 | `workspace_open_graph`, `workspace_add_nodes`, `workspace_add_edges`, `workspace_graph_neighbors`, `workspace_describe_graph` |
| Analysis | 1 | `workspace_analysis` |
| Algorithm | 1 | `workspace_algorithm` |

---

### FilterExpressionParser
Parses human-readable filter expressions into predicates (with LRU cache).

#### Static Methods
- `Func<Dictionary<string, object>, bool> Parse(string expression)`: Parse and cache a filter predicate.
- `ITabularQuery ApplyTo(ITabularQuery query, string expression)`: Apply filter to a query.
- `void ClearCache()`: Clear the predicate cache.
- `int CacheSize`: Number of cached predicates.

#### Supported Syntax
```
Comparison: age > 18, score >= 90, name == Alice, status != inactive
Logic:      AND, OR, NOT
String:     city contains Shang, name starts with A, name ends with ng
Null:       email is null, email is not null
Range:      age between 18 35
Set:        city in Shanghai Beijing Guangzhou
Parens:     (age > 18 AND city == Shanghai) OR admin == true
```

---

### ToolResult
Unified JSON return structure for all tools.

#### Properties
- `bool Success`: Whether the tool succeeded.
- `string Action`: Tool name.
- `object Result`: Result data (on success).
- `string Error`: Error message (on failure).
- `string Suggestion`: Correction suggestion (on failure).

---

### workspace_analysis
Dispatches statistical and graph analyses based on the `analysis` parameter.

#### Parameters
- `string workspace` (optional): Workspace name (default: "default").
- `string analysis` (required): Analysis type to execute.
- `string dataset` / `string graph`: Target dataset or graph name.
- Additional parameters vary by analysis type.

#### Tabular Analyses

| `analysis` | Description | Extra Parameters | Output |
|------------|-------------|-----------------|--------|
| `describe` | Per-column profile | — | type, non-null count, unique count, min, max, mean, median, std, 25th/75th percentile, skewness, kurtosis, null rate |
| `correlation` | Pearson correlation matrix | `columns` (optional) | Column × column correlation matrix |
| `outliers` | IQR-based outlier detection | `column` | Bounds, outlier rows, outlier count |
| `clustering` | K-Means clustering | `k`, `features` | Centroid output, inertia, cluster label column registered as new dataset |
| `distribution` | Distribution analysis | `column` | Histogram bins with stats (numeric); frequency counts (string) |

#### Graph Analyses

| `analysis` | Description | Extra Parameters | Output |
|------------|-------------|-----------------|--------|
| `centrality` | Centrality scores | `method` (degree/betweenness/closeness) | Normalized scores, top-N nodes |
| `communities` | Community detection | — | Label Propagation Algorithm results, community labels |
| `shortest_path` | BFS shortest path | `from`, `to` | Path, length, edge properties |

#### Usage
```csharp
var result = DataCoreTools.Execute("workspace_analysis", new Dictionary<string, object>
{
    ["workspace"] = "default",
    ["analysis"] = "correlation",
    ["dataset"] = "player-stats",
    ["columns"] = new[] { "score", "playtime" }
});
```

---

### workspace_algorithm
Bridges to `AlgorithmRegistry.Default` for algorithm execution and discovery.

#### Parameters
- `string workspace` (optional): Workspace name (default: "default").
- `string algorithm` (required): Algorithm name, or `"list"` to discover available algorithms.
- `string dataset` (for execution): Target dataset name.
- `string resultName` (optional): Name for the output dataset in workspace.
- `Dictionary<string, object> params` (optional): Algorithm-specific parameters.

#### List Mode
When `algorithm = "list"`, returns all registered algorithms with metadata:
- PageRank — Graph centrality (dampingFactor, maxIterations, tolerance)
- ConnectedComponents — Weakly/strongly connected components (method)
- MinMaxNormalize — Numeric column normalization (columns, min, max)

#### Execute Mode
Looks up the algorithm, builds an `AlgorithmContext` from params, executes on the target dataset, registers the output dataset in workspace, and returns metrics/metadata.

#### Usage
```csharp
// Execute PageRank
var result = DataCoreTools.Execute("workspace_algorithm", new Dictionary<string, object>
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

// List available algorithms
var list = DataCoreTools.Execute("workspace_algorithm", new Dictionary<string, object>
{
    ["algorithm"] = "list"
});
```

---

## Unity Integration

### DataCoreEditorComponent
A `MonoBehaviour` singleton for managing DataCore within the Unity lifecycle.

#### Primary Methods
- `static DataCoreEditorComponent Instance`: Access the singleton instance.
- `DataCoreStore GetStore()`: Retrieves the managed `DataCoreStore`.
- `string GetDatabasePath()`: Returns the current DB path.

---

## AroAro.DataCore.Events

### DataCoreEventManager
Static class used to subscribe to system-wide events.

#### Core Events
- `DatasetCreated`: Triggered when a new dataset is initialized.
- `DatasetDeleted`: Triggered when a dataset is removed.
- `DatasetModified`: Triggered on changes to data within a dataset.
- `DatasetSaved` / `DatasetLoaded`: Triggered during I/O operations.
- `DatasetQueried`: Triggered when a query is executed.

---

## AroAro.DataCore.Session

### SessionManager
Manages logical sessions for multi-user or multi-task scenarios.

#### Methods
- `ISession CreateSession(string name)`: Starts a new session.
- `ISession GetSession(string name)`: Retrieves an existing session.
- `bool CloseSession(string name)`: Closes and disposes a session.

### ISession
A temporary workspace that can hold data copies, results of analyses, and transaction-like states.

#### Key Methods
- `IDataSet OpenDataset(string name, string copyName = null)`: Loads a copy of a dataset into the session.
- `IDataSet CreateDataset(string name, DataSetKind kind)`: Creates a new dataset within the session.
- `bool PersistDataset(string name, string targetName = null)`: Saves a dataset from the session back to the permanent global store.
- `IDataSet SaveQueryResult(string sourceName, Func<IDataSet, IDataSet> query, string newName)`: Executes a query function and saves the result as a new session dataset.
- `void Clear()`: Wipes all temporary datasets in the session.

---

### RawResult
Result wrapper for native LiteDB queries.

#### Properties
- `Data`: Tabular data result (for SELECT queries).
- `ScalarValue`: Scalar result (for COUNT/UPDATE/DELETE queries).
- `HasData`: Whether the result contains tabular data.
- `AsInt32`, `AsInt64`, `AsDouble`, `AsString`, `AsBoolean`: Convenience properties for scalar results.

#### Usage Example
```csharp
// SELECT query
var result = dataset.ExecuteRaw("SELECT * FROM employees WHERE Age > @0", 30);
if (result.HasData)
{
    var data = result.Data; // ITabularDataset
    var rows = data.Query().OrderBy("Salary").ToDictionaries();
}

// COUNT query
var countResult = dataset.ExecuteRaw("SELECT COUNT(*) FROM employees");
int count = countResult.AsInt32;

// UPDATE query
var updateResult = dataset.ExecuteRaw("UPDATE employees SET Salary = Salary * 1.1 WHERE Department = @0", "Engineering");
Console.WriteLine($"Updated {updateResult.AsInt32} rows");
```

---

*Note: For detailed parameter information and XML documentation, please refer directly to the source code interfaces in the `Runtime/Abstractions` folder.*