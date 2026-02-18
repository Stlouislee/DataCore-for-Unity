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
