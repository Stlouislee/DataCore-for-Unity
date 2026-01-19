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
- [AroAro.DataCore (Unity Integration)](#unity-integration)
  - [DataCoreEditorComponent](#datacoreeditorcomponent)
- [AroAro.DataCore.Events](#aroarodatacoreevents)
  - [DataCoreEventManager](#datacoreeventmanager)
- [AroAro.DataCore.Session](#aroarodatacoresession)
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

*Note: For detailed parameter information and XML documentation, please refer directly to the source code interfaces in the `Runtime/Abstractions` folder.*