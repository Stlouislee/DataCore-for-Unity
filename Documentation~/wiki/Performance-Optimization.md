# Performance Optimization Guide

When using DataCore to handle large datasets, following these best practices can significantly improve application responsiveness and reduce memory overhead.

## 1. Enable Lazy Loading

This is the most effective way to handle massive amounts of data. When enabled, DataCore only loads the dataset structure (metadata) at startup, keeping the actual data payload on disk until you explicitly need to access it.

- **Benefit**: Dramatically reduces Game/Editor startup and loading times.
- **How to Enable**: In the `DataCoreEditorComponent` Inspector, check the `Lazy Loading` box.
- **Code Access**: When using `store.GetTabular(name)`, if the data is not yet loaded, DataCore will automatically perform the necessary I/O to bring it into memory.

## 2. Optimized CSV Import

For CSV files containing thousands of rows or more, follow these strategies:

- **Batch Ingestion**: The editor's import tool uses optimized batching to minimize database fragmentation.
- **Resources Loading**: Use the auto-load feature (`Resources/AroAro/DataCore/AutoLoad`) to pre-package data into the efficient LiteDB format, rather than parsing raw CSVs at runtime.
- **Pathing**: Ensure `databasePath` is correctly set (preferably using `Application.persistentDataPath`) to ensure fast local NVMe/SSD access.

## 3. Use Batch Operations (AddRows)

Adding rows one-by-one in a loop causes frequent array reallocations and transaction overhead. This is a common performance killer.

- **Bad Practice**: Calling `AddRow` 10,000 times in a `foreach` loop.
- **Best Practice**: Collect your data into a `List<IDictionary<string, object>>` and call `AddRows()` once.
- **Result**: This can improve insertion performance by several orders of magnitude.

## 4. Memory Management

- **Strategic Access**: Use `Select` projections in queries to retrieve only the columns you need, reducing memory pressure. 
- **Preview Optimization**: The `DataCorePreviewWindow` is designed for debugging. By default, it only displays a slice of large datasets to prevent UI lag. Avoid having many preview windows open simultaneously with very large datasets.
- **NumSharp Buffers**: DataCore uses NumSharp for numeric columns, which utilizes contiguous memory buffers. This is much more memory-efficient than storing `double[]` as object lists.

## 5. Persistence & Transactions

DataCore handles transactions automatically via `Checkpoint()`.
- **Checkpoint Frequency**: Call `Checkpoint()` after logical "blocks" of work rather than after every write. This minimizes disk I/O.
- **LiteDB Under-the-hood**: While transparent to users, you can access the underlying LiteDB engine via the `DataStore` abstraction if you need to perform advanced tasks like database rebuilding or index creation.

---
