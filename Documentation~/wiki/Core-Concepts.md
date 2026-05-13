# Core Concepts

## Architectural Overview

DataCore uses a layered architecture to provide a flexible and robust data management solution for Unity:

```
┌─────────────────────────────────────┐
│          Application Layer          │
│  (Your Game/Application Code)       │
└─────────────────────────────────────┘
                │
┌─────────────────────────────────────┐
│        Agent Tools Layer            │
│  (46 tools via DataCoreTools)       │
└─────────────────────────────────────┘
                │
┌─────────────────────────────────────┐
│         Workspace Layer             │
│  (Unified in-memory "desktop")      │
└─────────────────────────────────────┘
                │
┌─────────────────────────────────────┐
│          DataCoreStore API          │
│  (Simplified High-level API)        │
└─────────────────────────────────────┘
                │
┌─────────────────────────────────────┐
│          DataStore Abstraction      │
│  (IDataStore Interface)             │
└─────────────────────────────────────┘
                │
┌─────────────────────────────────────┐
│          LiteDB Implementation      │
│  (Persistence Layer)                │
└─────────────────────────────────────┘
```

## Key Components

### 1. DataCoreStore
The primary entry point for the system. It provides a high-level API for managing datasets and persistence.

**Main Responsibilities**:
- Creating and managing Tabular/Graph datasets.
- Executing queries and manipulating data.
- Managing database-wide persistence and transactions.
- Handling logical sessions.

```csharp
// Initialize store
var store = new DataCoreStore();

// Create datasets
var tabular = store.CreateTabular("player-stats");
var graph = store.CreateGraph("world-map");
```

### 2. Workspace (v0.5+)
The unified in-memory working area that replaces Session as the default "desktop" for data operations.

**Key Concepts**:
- `store.Workspace` is always available — no manual creation needed.
- Datasets from the store are loaded with **copy semantics** (Agent won't accidentally modify store data).
- Source tracking: `Store` / `Derived` / `Imported`.
- Memory policy: `Strong` / `Weak` / `Auto` (auto uses weak for ≥100K rows).

```csharp
// Access workspace
var ws = store.Workspace;

// Load from store (copy semantics)
ws.Register("Users", store.Get<ITabularDataset>("Users"), DataSource.Store);

// Create derived dataset
var filtered = ws.Get("Users").Query().Where("age", QueryOp.Gt, 18).ToTabular();
ws.Register("Adults", filtered, DataSource.Derived);

// Introspect
ws.DescribeAll();  // metadata for all datasets
ws.Summary();      // "Workspace: 3 datasets (1 store, 2 derived)"
```

### 3. Agent Tools (v0.5+)
46 static tools exposed via `DataCoreTools.Execute(toolName, args)` for AI agent integration.

**Tool Categories**:
- **Management** (3): create/destroy/list workspaces
- **Loading** (3): open from store (copy/ref), import CSV
- **Inspection** (4): describe, sample, schema, statistics
- **Transform** (9): filter, select, rename, sort, distinct, add column, drop columns, limit, random sample
- **Combine** (3): join (inner/left/right/full), union, cross
- **Aggregate** (3): group by, summarize, count
- **Persist** (3): save to store, export CSV/JSON
- **Modify** (4): update rows, delete rows, append, clear
- **Meta** (5): clone, rename, remove, search, diff
- **DataFrame** (5): create, convert, list, remove, to dataset
- **Graph** (5): open graph, add nodes/edges, neighbors, describe

```csharp
DataCoreTools.Initialize(store);
string json = DataCoreTools.Execute("workspace_filter", new Dictionary<string, object>
{
    ["workspace"] = "default",
    ["source"] = "Users",
    ["filter"] = "age > 18 AND city == Shanghai",
    ["resultName"] = "adults"
});
// Returns: {"success":true,"action":"workspace_filter","result":{...}}

// Get JSON Schema for all tools (Agent framework registration)
string schemas = DataCoreTools.GetToolSchemas();
```

### 3. Dataset Types (formerly #2)

#### Tabular Data
Structured, row-oriented data similar to a database table or Excel spreadsheet.

**Features**:
- Support for Numeric (Double/Float/Int) and String columns.
- High-speed columnar access via NumSharp (`NDArray`).
- Fluent query API for filtering, sorting, and projection.

```csharp
var data = store.GetOrCreateTabular("players");
data.AddNumericColumn("score", new double[] { 100, 200, 300 });
data.AddStringColumn("name", new[] { "Alice", "Bob", "Charlie" });
```

#### Graph Data
Network-structured data consisting of nodes and directed edges.

**Features**:
- Property graph support (attributes on both nodes and edges).
- Efficient graph traversal and neighbor retrieval.
- Pathfinding and relation-based queries.

```csharp
var graph = store.GetOrCreateGraph("social-network");
graph.AddNode("user1", new Dictionary<string, object> { { "name", "Alice" } });
graph.AddNode("user2", new Dictionary<string, object> { { "name", "Bob" } });
graph.AddEdge("user1", "user2", new Dictionary<string, object> { { "type", "friend" } });
```

### 6. Session Management (Deprecated)
> **⚠ Deprecated**: Use Workspace instead. Session is retained for backward compatibility but will be removed in a future version.
Supports isolated data workspaces, ideal for "what-if" analysis, temporary processing, or multi-user scenarios.

**Session Features**:
- Data isolation: Changes in a session don't affect the global store until persisted.
- DataFrame support: First-class integration with `Microsoft.Data.Analysis`.
- Fast memory-based copies of global datasets.

```csharp
var sessionManager = store.SessionManager;
var session1 = sessionManager.CreateSession("experiment-A");

// This copy is private to session1
var privateData = session1.OpenDataset("players");
```

### 7. Event System
DataCore provides a complete set of lifecycle events for decoupled data-driven architectures.

**Main Events**:
- `OnDatasetCreated`: Triggered when a new dataset is first initialized.
- `OnDatasetModified`: Triggered whenever rows/nodes are added, updated, or deleted.
- `OnDatasetSaved` / `OnDatasetLoaded`: Triggered during disk operations.
- `OnDatasetDeleted`: Triggered when a dataset is removed from the store.

### 8. Persistence Layer
Transactions and storage are handled by **LiteDB**, a serverless NoSQL database for .NET.

**Persistence Features**:
- **Automatic**: Data is synced to disk during the Unity lifestyle (e.g., exiting Play Mode).
- **Fast**: Binary serialization for tabular data (using Apache Arrow concepts).
- **Safe**: ACID-compliant transactions ensure no data corruption on crashes.

## Data Workflow

### Creation Workflow
```
Initialize Store → Create/Get Dataset → Add Data (AddNumericColumn / AddNode) → Checkpoint (Commit to disk)
```

### Analysis Workflow
```
Open Session → Load Global Dataset as DataFrame → Filter/Transform → Save Result to Session → Persist back to Global Store
```
