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
