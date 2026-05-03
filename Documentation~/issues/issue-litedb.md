# LiteDB Module — Design Issues

---

title: "ExecuteRaw exposes arbitrary SQL execution"
severity: critical

---

`LiteDbTabularDataset.ExecuteRaw(string sql, params object[] args)` passes raw SQL directly to `LiteDatabase.Execute()`. While LiteDB is a local embedded database (not a network service), this still allows:

- **Data destruction:** `DELETE FROM tabular_...`, `DROP COLLECTION`, etc.
- **Bypass of application-level validation:** Direct SQL can modify metadata collections (`tabular_meta`, `graph_meta`) and corrupt the store's bookkeeping.
- **Information disclosure:** Reading any collection in the database, including unrelated datasets.

The method is public and accepts any string. There is no allowlist, read-only guard, or collection-scoping restriction.

**Recommendation:** Either remove `ExecuteRaw` entirely, restrict it to read-only operations (e.g., only SELECT), or scope it to the dataset's own collection with parameterized queries only.

**File:** `LiteDbTabularDataset.cs`, lines ~246-298

---

title: "Thread safety: read methods skip locking"
severity: high

---

Multiple read methods access LiteDB collections without acquiring `_lock`:

**LiteDbGraphDataset:** `HasNode`, `GetNodeProperties`, `GetNodeIds`, `HasEdge`, `GetEdgeProperties`, `GetOutNeighbors`, `GetInNeighbors`, `GetNeighbors`, `GetOutDegree`, `GetInDegree`

**LiteDbTabularDataset:** `GetRow`, `GetRows`, `GetStringColumn`, `GetNumericColumn`, `HasColumn`, `RowCount`, `ColumnCount`, `ColumnNames`

On desktop (`Shared` mode), LiteDB's internal locking may suffice for concurrent readers, but there's no protection against a reader seeing partially-written data from a concurrent writer (no snapshot isolation guarantee at the application level).

On mobile (`Direct` mode), there is no inter-thread protection at all. Concurrent Unity coroutines or background threads calling read methods while another thread writes could encounter inconsistent state or LiteDB exceptions.

**Recommendation:** Either acquire `_lock` in all public methods (consistent locking), or document that this module is not thread-safe and callers must synchronize externally. Consider using `ReaderWriterLockSlim` for better read concurrency.

**Files:** `LiteDbGraphDataset.cs`, `LiteDbTabularDataset.cs`

---

title: "BFS traversal has O(V × N) complexity"
severity: high

---

In `LiteDbGraphQuery.TraverseFromNode()`, for every node visited during BFS:

```csharp
var node = _dataset.GetAllNodesInternal().FirstOrDefault(n => n.NodeId == current);
```

This loads **all nodes from LiteDB** on every BFS step, then does a linear scan. For a graph with V nodes and E edges, this is O(V²) in the best case, and the full BFS becomes O(V² + E) with a very large constant due to repeated DB reads.

**Recommendation:** Either:
1. Load all nodes into a `Dictionary<string, GraphNode>` once before BFS starts.
2. Use `_nodes.FindOne(n => n.NodeId == current)` which can use the existing `NodeId` index.

**File:** `LiteDbGraphQuery.cs`, `TraverseFromNode()` method

---

title: "ClearAll is not atomic"
severity: high

---

`LiteDbDataStore.ClearAll()` acquires `_lock`, then iterates datasets calling `DeleteTabular(name)` and `DeleteGraph(name)`. Each of these methods also acquires `_lock` — but since C# `lock` is reentrant for the same thread, this works. However:

1. The `TabularNames` property (called inside the loop) also acquires `_lock` and queries the DB.
2. Between iterations, another thread could create a new dataset that wouldn't be caught by the initial name listing.
3. If a `DeleteTabular` call fails partway, the store is left in a partially-cleared state with no rollback.

**Recommendation:** Implement `ClearAll` as a single atomic operation: collect all metadata IDs first, then drop all collections and clear metadata in one transaction.

**File:** `LiteDbDataStore.cs`, `ClearAll()` method

---

title: "DeleteRow causes O(N) re-indexing of all subsequent rows"
severity: medium

---

`LiteDbTabularDataset.DeleteRow(int rowIndex)` deletes the target row, then loads **all rows with higher index** and decrements each one individually:

```csharp
var subsequentRows = _rows.Find(r => r.RowIndex > rowIndex).ToList();
foreach (var r in subsequentRows)
{
    r.RowIndex--;
    _rows.Update(r);
}
```

For a dataset with 10,000 rows, deleting row 0 triggers 9,999 individual `Update` calls. This is extremely slow for large datasets.

**Recommendation:** Either:
1. Use a "tombstone" approach (mark rows deleted, compact later).
2. Don't re-index — use stable row IDs instead of sequential indices.
3. Batch the updates using a LiteDB SQL `UPDATE` statement if supported.

**File:** `LiteDbTabularDataset.cs`, `DeleteRow()` method

---

title: "Duplicated BsonValue conversion code across 4 files"
severity: medium

---

The following methods are copy-pasted identically in `LiteDbGraphDataset`, `LiteDbGraphQuery`, `LiteDbTabularDataset`, and `LiteDbTabularQuery`:

- `ConvertToBsonValue(object)` (or used inline)
- `ConvertFromBsonValue(BsonValue)`
- `BsonValueEquals(BsonValue, object)`
- `EvaluateCondition(BsonValue, QueryOp, object)`
- `ConvertToBsonDocument(IDictionary<string, object>)`

This violates DRY. Any bug fix or improvement (e.g., adding support for `byte[]`, `Guid`, or `decimal`) must be applied in 4 places.

**Recommendation:** Extract into a shared static utility class (e.g., `BsonValueHelper` or `LiteDbConvert`).

**Files:** All five source files

---

title: "Metadata batch updates can lose up to 99 changes on crash"
severity: medium

---

Both `LiteDbGraphDataset` and `LiteDbTabularDataset` batch metadata writes. The `UpdateMetadata()` method only persists to DB every `MetadataUpdateBatchSize` (100) operations. If the process crashes or is killed between flushes:

- `RowCount`, `NodeCount`, `EdgeCount`, `ModifiedAt`, and `Columns` can be stale.
- The next startup will use the old metadata, potentially reporting wrong counts or missing column definitions.

**Recommendation:** Either:
1. Flush metadata on every mutation (accept the I/O cost).
2. Use a write-ahead log or checkpoint-on-idle strategy.
3. At minimum, flush on `Application.pause` / `Application.focusChanged` in Unity.

**Files:** `LiteDbGraphDataset.cs`, `LiteDbTabularDataset.cs`

---

title: "CSV ImportFromCsv silently overwrites all existing data"
severity: medium

---

`ImportFromCsv` calls `_rows.DeleteAll()` and `_metadata.Columns.Clear()` without any confirmation or option to merge. If a user calls this on an existing dataset expecting to append or merge, all data is permanently lost.

**Recommendation:** Add an `ImportMode` parameter: `Overwrite` (current behavior), `Append`, or `Merge`. At minimum, throw if the dataset is non-empty unless explicitly opted in.

**File:** `LiteDbTabularDataset.cs`, `ImportFromCsv()` method

---

title: "Constructor uses GC.Collect + Thread.Sleep for corruption recovery"
severity: medium

---

The `LiteDbDataStore` constructor handles corruption by deleting the DB file, calling `GC.Collect()` + `GC.WaitForPendingFinalizers()`, then sleeping 100ms before retry:

```csharp
GC.Collect();
GC.WaitForPendingFinalizers();
System.Threading.Thread.Sleep(100);
```

This is fragile because:
- `GC.Collect()` doesn't guarantee finalizers run immediately.
- 100ms is arbitrary and may not be enough on slow I/O.
- `Thread.Sleep` blocks the calling thread (likely the Unity main thread).
- On IL2CPP (mobile), finalizer behavior differs from Mono.

**Recommendation:** Instead of relying on GC to release file handles, ensure `LiteDatabase` is properly disposed before retry. Use `using` patterns or explicit disposal. If a handle leak is the root cause, fix the leak rather than relying on GC.

**File:** `LiteDbDataStore.cs`, constructor retry loop

---

title: "Platform connection type may cause data corruption on mobile"
severity: medium

---

On Android/iOS/tvOS/WebGL, the connection type is `Direct`:

```csharp
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_WEBGL
    return ConnectionType.Direct;
```

`Direct` mode opens the file without any locking. If two threads (e.g., a background data loading coroutine and the main thread) access the store concurrently, LiteDB can corrupt the database file or throw `LiteException`.

Unity's main thread and async operations (coroutines, `Task.Run`, `UniTask`) can easily create concurrent access patterns.

**Recommendation:** Enforce single-threaded access on mobile by dispatching all LiteDB operations to a single dedicated thread or using a `ConcurrentQueue` with a consumer loop. Alternatively, use a semaphore to serialize access.

**File:** `LiteDbDataStore.cs`, `ResolveConnectionType()` method

---

title: "ExportToCsv loads entire dataset into a single string"
severity: low

---

`ExportToCsv` builds the entire CSV in a `StringBuilder`, then returns it as one string. For large datasets (millions of rows), this causes high memory allocation and potential `OutOfMemoryException`.

**Recommendation:** Return a `Stream` or `IEnumerable<string>` (line-by-line) for streaming writes, or accept a `TextWriter` parameter.

**File:** `LiteDbTabularDataset.cs`, `ExportToCsv()` method

---

title: "GetDatabaseSize ignores LiteDB log file"
severity: low

---

`GetDatabaseSize()` only checks the main `.db` file:

```csharp
if (File.Exists(DatabasePath))
    return new FileInfo(DatabasePath).Length;
```

LiteDB in journal mode also creates a `-log` file that can be significant in size (up to the database size during transactions). The constructor even deletes this file during corruption recovery, proving it's aware of its existence.

**Recommendation:** Include `DatabasePath + "-log"` in the size calculation, or use `Directory.GetFiles` to sum all related files.

**File:** `LiteDbDataStore.cs`, `GetDatabaseSize()` method

---

title: "Hardcoded floating-point comparison tolerance"
severity: low

---

`BsonValueEquals` in both query classes uses a hardcoded epsilon:

```csharp
return Math.Abs(bsonValue.AsDouble - Convert.ToDouble(value)) < 0.0001;
```

This tolerance is arbitrary and may be too coarse for scientific data or too fine for financial data. It's also not applied symmetrically — `EvaluateCondition` for `Gt`/`Lt`/`Ge`/`Le` uses exact comparison, creating an inconsistency where `Eq` uses fuzzy matching but `Ge`/`Le` don't.

**Recommendation:** Make the epsilon configurable via `DataStoreOptions`, or use exact equality by default and let callers opt into fuzzy matching.

**Files:** `LiteDbGraphQuery.cs`, `LiteDbTabularQuery.cs`

---

title: "AddNumericColumn/AddStringColumn with existing data loads all rows into memory"
severity: low

---

When adding a column to an existing dataset (where `RowCount > 0`):

```csharp
var existingRows = _rows.FindAll().OrderBy(r => r.RowIndex).ToList();
```

This loads every row into a `List`, then updates each one individually. For a dataset with 100K rows and 50 columns, each row's `BsonDocument` is fully deserialized just to add one field.

**Recommendation:** Use LiteDB's `Update` with an expression or a SQL `UPDATE` to add the field server-side, avoiding full materialization.

**File:** `LiteDbTabularDataset.cs`, `AddNumericColumn` and `AddStringColumn` methods

---

title: "Missing dataset name validation"
severity: low

---

Dataset names are used directly in LiteDB collection names (`tabular_{id}`, `graph_{id}_nodes`). While the ID is a GUID (safe), the `Name` field is user-controlled and stored in metadata. There's no sanitization for:

- Extremely long names (could cause filesystem issues in the metadata collection)
- Names that are valid but confusing (e.g., empty after trim, whitespace-only)

The `string.IsNullOrWhiteSpace` check exists but only for creation. `GetTabular`/`GetGraph` don't validate input.

**Recommendation:** Add consistent name validation across all entry points. Consider a max length constraint.

**File:** `LiteDbDataStore.cs`

---

title: "Event manager calls outside lock scope"
severity: low

---

In both dataset classes, `DataCoreEventManager.RaiseDatasetModified(...)` is called **after** releasing `_lock`:

```csharp
lock (_lock)
{
    // ... mutations ...
}
DataCoreEventManager.RaiseDatasetModified(this, "AddNode", id); // outside lock
```

If event handlers query the dataset, they may see inconsistent state if another thread is mid-mutation. Additionally, if the event handler throws, the mutation has already been committed but the error propagates to the caller, potentially leaving the application in an inconsistent state.

**Recommendation:** Either document that event handlers must not query the dataset, or wrap the event raise inside the lock (accepting potential deadlock risk from reentrant handlers).

**Files:** `LiteDbGraphDataset.cs`, `LiteDbTabularDataset.cs`
