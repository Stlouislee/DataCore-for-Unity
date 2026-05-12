## 0.5.0

### New Features
- **Workspace**: Unified in-memory working area replacing the Session pattern as the default "desktop" for data operations
  - `store.Workspace` — always available on `DataCoreStore`, no manual creation needed
  - `Register(name, dataset, source)` — register datasets from `IDataSet`, dictionary data, or auto-naming
  - `Get(name)` — auto-falls back to store (lazy load into workspace)
  - `Describe(name)` / `DescribeAll()` — full metadata with schema, row counts, and sample data (AI agent friendly)
  - `Summary()` — one-line workspace status
  - `Rename` / `Clone` / `Remove` / `Clear` — complete lifecycle management
  - Source tracking: `DataSource.Store` / `DataSource.Derived` / `DataSource.Imported`
  - `WorkspaceRetentionPolicy`: `Strong` / `Weak` / `Auto` (auto uses weak for datasets ≥100K rows)
  - `DescribeAll` lazy-caches with dirty flag invalidation
  - `TryPeek(name)` — metadata-only lookup without triggering data load
  - `AllNames` — unified view of store ∪ workspace
  - Async API: `DescribeAllAsync`, `RegisterAsync`
  - Event: `WorkspaceDatasetRegistered` added to `DataCoreEventManager`

### Deprecations
- **SessionManager** and **Session** marked `[Obsolete]` — use `Workspace` instead. Will be removed in a future version.

### Tests
- 52 new tests in `WorkspaceTests.cs` covering all Workspace functionality

## 0.4.1 (Phase 3 — Cleanup & Bug Fixes)

### Bug Fixes
- **CopyGraphData**: Fixed edge properties being silently dropped during session graph copy. Edge properties are now properly transferred via `GetEdgeProperties`/`AddEdge` with properties.
- **LambdaFilteredGraphQuery.ToNodeIds**: Fixed lambda predicate not being applied — the query now properly fetches node/edge properties, constructs `QueryRow`, and filters by the predicate. Added `IGraphQuery.Source` property to enable property access from wrapper queries.
- **LambdaFilteredGraphQuery.CountNodes/CountEdges**: Now returns accurate counts after applying lambda filters instead of delegating to the inner query.

### Cleanup
- **DataStoreOptions**: Marked unused properties (`AutoCreateIndexes`, `EnableCache`, `CacheSize`, `AutoSave`, `AutoSaveInterval`, `ConnectionString`) as `[Obsolete]`. Only `ReadOnly` is actively used by the LiteDB backend.
- **README**: Removed NumSharp dependency documentation and updated code examples to use direct `double[]` arrays.

## 0.4.0

### New Features
- **Async API**: Added async versions of dataset mutation and query operations for offloading LiteDB I/O from the Unity main thread
  - `IGraphDataset`: `AddNodeAsync`, `AddNodesAsync`, `GetOutNeighborsAsync`, `AddEdgeAsync`, `AddEdgesAsync`, `RemoveNodeAsync`, `ClearAsync`
  - `ITabularDataset`: `AddRowAsync`, `AddRowsAsync`, `AddNumericColumnAsync`, `AddStringColumnAsync`, `ClearAsync`
  - All async methods accept `CancellationToken` for cooperative cancellation
  - LiteDB implementations use `Task.Run()` for true background I/O and `SemaphoreSlim` for write serialization
  - In-memory implementations (`GraphData`, `TabularData`) use lightweight `Task.CompletedTask` wrapping

### Architecture
- `LiteDbGraphDataset` / `LiteDbTabularDataset`: Added `SemaphoreSlim(1,1)` write lock to prevent race conditions on concurrent async writes
- Async write pattern: `SemaphoreSlim` → `Task.Run()` → sync `lock` → release, following the `LiteDbGateway` pattern from LiteDB integration best practices

## 0.3.1

### Bug Fixes
- **Event Coverage**: Fixed issue where CSV and GraphML imports via the editor bypassed the `DatasetCreated` event. Added `DataCoreStore` overloads to importers to ensure consistent event dispatching.
- **Event Coverage**: Implemented `DatasetModified` event triggers for all dataset mutation methods (add/update/remove for rows, columns, nodes, and edges).
- **Stability**: Ensured events are raised outside of database locks to prevent deadlocks and improve performance during event processing.

## 0.3.0

### New Features
- **Algorithm Framework**: Extensible algorithm component system that runs on datasets
- **Composable Pipelines**: Chain algorithms sequentially — output of step N feeds step N+1
- **Algorithm Registry**: Central service for discovering, registering, and looking up algorithms
- **Algorithm Events**: `AlgorithmStarted`, `AlgorithmCompleted`, `PipelineCompleted` integrated into `DataCoreEventManager`

### Built-in Algorithms
- **PageRank** (Graph): Iterative power-method PageRank centrality with configurable damping factor, max iterations, and convergence tolerance
- **ConnectedComponents** (Graph): Weakly connected (BFS) and strongly connected (Tarjan's SCC) component detection
- **MinMaxNormalize** (Tabular): Min-max normalization of numeric columns to a configurable target range

### Architecture
- `IAlgorithm` / `IGraphAlgorithm` / `ITabularAlgorithm` — core contracts
- `AlgorithmBase` / `GraphAlgorithmBase` / `TabularAlgorithmBase` — template base classes with timing, validation, cancellation, and event firing
- `AlgorithmResult` — immutable result wrapper with output dataset, metrics, metadata, and duration
- `AlgorithmContext` — fluent-builder execution context with typed parameter access, cancellation tokens, and progress callbacks
- `AlgorithmPipeline` / `PipelineResult` — composable multi-step pipeline with per-step progress partitioning and aggregated metrics
- `AlgorithmRegistry` — singleton registry with auto-registered built-ins and runtime registration
- `AlgorithmParameterDescriptor` — self-describing parameters for UI integration

### Tests
- 19 new tests in `AlgorithmTest.cs` covering registry, all three algorithms, pipeline chaining, validation, and event integration

## 0.2.0

### New Features
- **Enhanced Preview System**: Inline Inspector preview and full preview window
- **Event System**: Comprehensive event system for dataset lifecycle monitoring
- **Lazy Loading**: Support for delayed dataset loading
- **Performance Optimizations**: Batch processing and streaming for large datasets

### Preview Features
- Tabular data preview with column headers and row data
- Graph data preview with node and edge details
- Scrollable interface for large datasets
- Full preview window with dedicated UI

### Event System
- DatasetCreated, DatasetDeleted, DatasetLoaded, DatasetSaved events
- DatasetModified events for row/column/node/edge changes
- DatasetQueried events for query execution
- Easy subscription model

### Performance Improvements
- Lazy loading system with DatasetMetadata
- Optimized CSV import with batch processing
- Better memory management for large datasets

## 0.1.0

- Initial package scaffold
- Tabular + graph datasets
- CRUD/query APIs
- Persistence (Arrow + dcgraph)
