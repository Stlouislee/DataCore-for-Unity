---
title: "Session class has no internal thread safety"
severity: high
---

The `Session` class uses plain `Dictionary<string, IDataSet>`, `Dictionary<string, DataFrame>`, and `Dictionary<string, WeakReference<DataFrame>>` for its internal state, with no synchronization primitives. While `SessionManager` protects its own `_sessions` dictionary with a `lock`, once a session reference is obtained, all operations on it are completely unsynchronized.

**Impact**: If two threads call `OpenDataset`, `RemoveDataset`, `CreateDataFrame`, `ExecuteDataFrameQuery`, or any DataFrame method concurrently on the same session, dictionary corruption, `InvalidOperationException` (collection modified during enumeration), or lost writes can occur.

**Code References**:
- `Session.cs`: `_datasets`, `_dataFrameCache`, `_weakDataFrames` — all unsynchronized plain dictionaries
- `SessionManager.cs`: `GetSession()` returns the `ISession` reference under lock, but subsequent usage is unguarded

**Recommendation**: Either add a `ReaderWriterLockSlim` to `Session` and synchronize all public methods, document that `Session` is single-threaded and must only be accessed from one thread (e.g., Unity main thread per user), or use `ConcurrentDictionary` for the internal dictionaries.

---
title: "GroupBy overwrites result on each aggregate — only last aggregate survives"
severity: critical
---

In `SessionDataFrameQueryBuilder.GroupBy()`, the loop over `aggregates` calls `groupBy.Sum/Average/Min/Max/Count` on each iteration, assigning the result to the same `resultDf` variable. Each call returns a new DataFrame with only that aggregate, overwriting the previous result. Only the last aggregate's result is returned.

**Impact**: Silent data loss. Users calling `GroupBy("col", ("a", Sum), ("b", Average))` will only get the `Average` result; the `Sum` is discarded.

**Code References** (`SessionDataFrameQueryBuilder.cs`, `GroupBy` method):
```csharp
foreach (var (aggColumn, function) in aggregates)
{
    switch (function)
    {
        case AggregateFunction.Sum:
            resultDf = groupBy.Sum(aggColumn);  // overwritten next iteration
            break;
        // ...
    }
}
```

**Recommendation**: Merge aggregate results into a single DataFrame. Build the group key once, then join each aggregate column into a combined result. Or throw `NotSupportedException` for multi-aggregate calls until properly implemented.

---
title: "Offset implementation is semantically wrong"
severity: high
---

`SessionDataFrameQueryBuilder.Offset(count)` is implemented as:
```csharp
_operations.Add(df => df.Tail(Math.Max(0, (int)(df.Rows.Count - count))));
```

`Tail(n)` returns the **last n rows**, not "all rows except the first n". For a 100-row DataFrame with `Offset(10)`, this returns the last 90 rows — which happens to be correct only if the DataFrame is in natural order. After an `OrderBy` or `Where` filter, `Tail` still takes from the end, not from position `count`.

**Impact**: Incorrect pagination results. `Limit(10).Offset(20)` should return rows 21–30, but will return the last 10 rows of the post-offset DataFrame.

**Recommendation**: Implement offset by slicing from position `count` to end using proper row indexing, not `Tail`.

---
title: "PersistDataset silently succeeds when dataset type cast fails"
severity: high
---

In `Session.PersistDataset()`, the `switch` on `dataset.Kind` checks `if (dataset is ITabularDataset)` and `if (dataset is IGraphDataset)`. If the cast fails (e.g., a `DataFrameAdapter` with `Kind == Tabular` but not implementing `ITabularDataset`), neither branch executes, yet the method returns `true`.

**Impact**: Silent data loss. User believes data was persisted but nothing was actually written to the global store. `DataFrameAdapter` always returns `DataSetKind.Tabular` but does not implement `ITabularDataset`, so `PersistDataset` on a DataFrame-backed dataset will always silently fail.

**Code References** (`Session.cs`, `PersistDataset` method):
```csharp
case DataSetKind.Tabular:
    if (dataset is ITabularDataset sourceTabular)  // false for DataFrameAdapter
    {
        var newTabular = _store.CreateTabular(target);
        CopyTabularData(sourceTabular, newTabular);
    }
    break;  // returns true without doing anything
```

**Recommendation**: Throw if the cast fails, or handle `DataFrameAdapter` explicitly by converting via `ToTabularData()` first. Never return `true` when no work was done.

---
title: "Duplicate EstimateMemoryUsage and OptimizeMemory logic across classes"
severity: medium
---

The `EstimateMemoryUsage(DataFrame)` method is implemented identically in both `DataFrameConverter` (private static) and `DataFrameMemoryManager` (private instance). The `OptimizeMemory(DataFrame)` method is also duplicated between these two classes.

**Impact**: Code duplication increases maintenance burden and risk of divergence. A bug fix in one copy may be missed in the other.

**Code References**:
- `DataFrameConverter.cs`: `EstimateMemoryUsage` and `OptimizeMemory`
- `DataFrameMemoryManager.cs`: `EstimateMemoryUsage` and `OptimizeMemory`

**Recommendation**: Extract shared logic into a static utility class (e.g., `DataFrameUtils`) and reference it from both classes.

---
title: "Session.Clear() does not dispose individual datasets"
severity: medium
---

`Session.Clear()` calls `_datasets.Clear()` which removes all references but does not call `Dispose()` on any `IDataSet` that implements `IDisposable`. Similarly, `_dataFrameCache.Clear()` discards DataFrames without cleanup.

**Impact**: Resources held by datasets (file handles, native memory from DataFrame internals) may not be released promptly, relying on finalizers instead.

**Code References** (`Session.cs`):
```csharp
public void Clear()
{
    _datasets.Clear();
    Touch();
}
```

**Recommendation**: Iterate and dispose before clearing. Same applies to `Dispose()`.

---
title: "Session.Dispose() does not dispose individual datasets"
severity: medium
---

Same issue as `Clear()`. `Dispose()` clears all dictionaries but never calls `Dispose()` on the contained datasets or DataFrames.

**Code References**: `Session.cs`, `Dispose()` method.

**Recommendation**: Dispose all `IDataSet` instances and clear all caches before setting `_disposed = true`. Consider implementing the full Dispose pattern with `GC.SuppressFinalize`.

---
title: "Using DateTime.Now instead of DateTime.UtcNow"
severity: low
---

All timestamp operations (`CreatedAt`, `LastActivityAt`, idle timeout comparisons) use `DateTime.Now` (local time). This can cause issues if the server's timezone changes, daylight saving transitions occur, or if sessions are compared across machines.

**Code References**:
- `Session.cs`: `CreatedAt = DateTime.Now`, `LastActivityAt = DateTime.Now`
- `SessionManager.cs`: `var now = DateTime.Now` in `CleanupIdleSessions`
- `DataFrameMemoryManager.cs`: `DateTime.Now` in `RegisterDataFrame`, `RegisterLazyDataFrame`

**Recommendation**: Use `DateTime.UtcNow` for all internal timestamps. Convert to local time only for display purposes.

---
title: "Session.Name has a public setter"
severity: low
---

`Session.Name` has a `public set` accessor, while `ISession.Name` only declares a getter. This allows external code to mutate the name, which could cause confusion if names are ever used as lookup keys or displayed in UI that doesn't expect changes.

**Code References**: `Session.cs`: `public string Name { get; set; }`

**Recommendation**: Make the setter `private` or `internal`. If rename is needed, provide an explicit `Rename(string)` method with validation.

---
title: "Query builder breaks interface abstraction via cast to Session"
severity: medium
---

`SessionDataFrameQueryBuilder.Execute()` and `ExecuteAsDataFrame()` cast `ISession` to the concrete `Session` class:
```csharp
var concreteSession = _session as Session;
if (concreteSession == null)
    throw new InvalidOperationException("Session must be concrete implementation");
```

This breaks the `ISession` abstraction and prevents any alternative implementations (e.g., mock sessions for testing, remote sessions).

**Code References**: `SessionDataFrameQueryBuilder.cs`: `Execute()` and `ExecuteAsDataFrame()` methods.

**Recommendation**: Add the required methods (`ExecuteDataFrameQuery`, `GetDataFrame`) to the `ISession` interface, or use a separate interface (e.g., `ISessionDataFrameSupport`) that the query builder checks for.

---
title: "No cancellation support for long-running queries"
severity: low
---

None of the query builder operations or DataFrame conversions accept a `CancellationToken`. Long-running operations on large DataFrames (millions of rows) cannot be cancelled, potentially blocking the Unity main thread.

**Code References**:
- `SessionDataFrameQueryBuilder.cs`: All `Where`, `GroupBy`, `OrderBy` operations
- `Session.cs`: `ConvertToDataFrame`, `TabularToDataFrame`

**Recommendation**: Add `CancellationToken` parameters to long-running methods and check `token.ThrowIfCancellationRequested()` in loops.

---
title: "DataFrameAdapter.ToTabularData silently swallows column conversion errors"
severity: medium
---

In `ToTabularData()`, each column conversion is wrapped in a `try/catch` that logs a warning but continues. A partially converted `TabularData` object is returned, which may have missing columns — a subtle data integrity issue.

**Code References** (`DataFrameAdapter.cs`, `ToTabularData()`):
```csharp
catch (Exception ex)
{
    UnityEngine.Debug.LogWarning($"Failed to convert column {column.Name}: {ex.Message}");
}
```

**Recommendation**: Either throw on conversion failure to make the error explicit, add a `bool strict` parameter that controls behavior, or track which columns failed and expose that in the return value.

---
title: "CopyTabularData uses CSV serialization for in-memory copy"
severity: low
---

`Session.CopyTabularData()` copies data by serializing to CSV string and reimporting. This is unnecessarily expensive for an in-memory operation — it involves string allocation, parsing, and potential data loss (type information, special characters in CSV).

**Code References** (`Session.cs`, `CopyTabularData()`):
```csharp
var csvContent = source.ExportToCsv();
target.ImportFromCsv(csvContent);
```

**Recommendation**: Copy data at the column level directly, or use a deep-clone mechanism if available on `ITabularDataset`.

---
title: "Memory optimization only downgrades double→float"
severity: low
---

`OptimizeMemory` in both `DataFrameConverter` and `DataFrameMemoryManager` only attempts to downgrade `double` columns to `float`. It doesn't check if `int` columns could fit in `short`/`byte`, or if `float` columns could fit in smaller types.

**Code References**:
- `DataFrameConverter.cs`, `OptimizeMemory()`
- `DataFrameMemoryManager.cs`, `OptimizeMemory()`

**Recommendation**: Extend the optimization to cover `int → short/byte` downgrades based on value range analysis.

---
title: "Weak reference cleanup not triggered automatically"
severity: low
---

`Session.CleanupWeakReferences()` and `DataFrameMemoryManager.CleanupWeakReferences()` must be called explicitly. There is no automatic periodic cleanup, so dead references accumulate until someone remembers to call cleanup.

**Code References**:
- `Session.cs`, `CleanupWeakReferences()`
- `DataFrameMemoryManager.cs`, `CleanupWeakReferences()`

**Recommendation**: Consider hooking cleanup into `GetDataFrame`/`HasDataFrame` calls with a probability check (e.g., 1% chance per call), or integrate with Unity's `PlayerLoop` for periodic cleanup.

---
title: "GroupBy validation happens inside the deferred lambda"
severity: medium
---

Column existence checks and type validations in `Where`, `Select`, `OrderBy`, and `GroupBy` happen inside the `Func<DataFrame, DataFrame>` lambda — meaning errors are only thrown at execution time (`Execute()`), not when the builder method is called. This violates the fail-fast principle and makes debugging harder.

**Code References**: `SessionDataFrameQueryBuilder.cs`: All builder methods defer validation to the lambda.

**Recommendation**: Perform eager validation in the builder method itself when possible. The source DataFrame could be fetched once during construction for validation purposes, or validation deferral could be documented as intentional.
