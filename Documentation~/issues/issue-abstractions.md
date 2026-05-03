# Design Issues — Abstractions & Core Store

---

```yaml
---
title: "Leaking LiteDB types through abstraction boundary"
severity: high
---
```

**File:** `Runtime/Abstractions/RawResult.cs`, line: `public BsonValue ScalarValue { get; }`

**Problem:** `RawResult` exposes `LiteDB.BsonValue` directly as a public property. This breaks the abstraction layer — consumers of `ITabularDataset.ExecuteRaw()` are forced to take a dependency on LiteDB to read scalar results. The `AsInt32`, `AsDouble`, etc. shortcuts mitigate this partially, but `ScalarValue` is still public and `BsonValue` leaks into the API surface.

**Impact:** Any future storage backend swap would require changing `RawResult` or wrapping `BsonValue` in a backend-agnostic type. Consumers who use `ScalarValue` directly would break.

**Recommendation:** Either make `ScalarValue` internal/private and expose only the typed accessors, or introduce a backend-agnostic `DataValue` wrapper type.

---

```yaml
---
title: "ExecuteRaw exposes raw SQL injection surface"
severity: critical
---
```

**File:** `Runtime/Abstractions/ITabularDataset.cs`, method: `ExecuteRaw(string sql, params object[] args)`

**Problem:** The `ExecuteRaw` method accepts arbitrary SQL-like strings. While parameterized queries via `@0, @1...` are supported, there is no enforcement that callers actually use parameters. If user-supplied input is concatenated into the `sql` string, it creates a classic SQL injection vector. The `DataCoreRawQueryException` even logs the expression and parameters in its message, which could leak data in logs.

**Impact:** If any consumer builds SQL strings from untrusted input, data corruption or unauthorized data access is possible.

**Recommendation:**
1. Add XML doc warnings about injection risk.
2. Consider making `ExecuteRaw` internal or restricting it behind an explicit opt-in flag on `DataStoreOptions`.
3. Ensure `DataCoreRawQueryException` does not log sensitive parameter values in production builds.

---

```yaml
---
title: "DataCoreRawQueryException exposes query parameters in exception message"
severity: medium
---
```

**File:** `Runtime/Abstractions/DataCoreRawQueryException.cs`

**Problem:** The exception's `Message` property includes the full formatted parameter list via `FormatParameters()`. If parameters contain user data, PII, or sensitive values, these will appear in stack traces, log aggregators, and crash reports.

**Recommendation:** Redact or truncate parameter values in the message. Store full details only in the `Parameters` property for programmatic access, not in the string message.

---

```yaml
---
title: "IDataStore.CreateTabular/CreateGraph lack null/empty name validation"
severity: medium
---
```

**File:** `Runtime/Abstractions/IDataStore.cs`

**Problem:** The `IDataStore` interface defines `CreateTabular(string name)` and `CreateGraph(string name)` but the interface itself cannot enforce that implementations validate the `name` parameter. Only `DataCoreStore` performs `string.IsNullOrWhiteSpace` checks. Direct `IDataStore` users could pass null or empty names.

**Impact:** LiteDB behavior with null/empty collection names is undefined and could throw unhandled exceptions.

**Recommendation:** Add `[NotNull]` annotations or document the validation contract. Consider a shared validation helper used by all implementations.

---

```yaml
---
title: "DataCoreStore.DatabasePath returns null for non-LiteDB stores"
severity: medium
---
```

**File:** `Runtime/DataCoreStore.cs`, property: `DatabasePath`

```csharp
public string DatabasePath => (_store as LiteDb.LiteDbDataStore)?.DatabasePath;
```

**Problem:** This property uses `as` cast which returns `null` if the underlying store is not a `LiteDbDataStore`. Since `IDataStore` is the abstraction, consumers shouldn't need to know the concrete type. Returning `null` silently is a hidden contract violation.

**Recommendation:** Either add `DatabasePath` (or a generic `ConnectionString`) to `IDataStore`, or throw `NotSupportedException` when the cast fails instead of returning null.

---

```yaml
---
title: "DataCoreStore.Delete initializes kind variable incorrectly"
severity: low
---
```

**File:** `Runtime/DataCoreStore.cs`, method: `Delete`

```csharp
var kind = DataSetKind.Tabular;  // initialized to Tabular even before checking
```

**Problem:** The local `kind` is initialized to `DataSetKind.Tabular` before the existence check. If the dataset is actually a graph, the variable gets reassigned correctly, but if neither exists, `kind` remains `Tabular` — which is misleading though harmless since `removed` stays `false`. This is a minor code smell.

**Recommendation:** Initialize `kind` to a default or use a nullable `DataSetKind?` to make the intent clear.

---

```yaml
---
title: "IDataSet.WithName lacks documentation on copy semantics"
severity: low
---
```

**File:** `Runtime/IDataSet.cs`, method: `IDataSet WithName(string name)`

**Problem:** The XML doc says "Returns a dataset copy with a different name" but doesn't specify:
- Is it a shallow or deep copy?
- Does the copy share the same underlying data or is it independent?
- Does the copy get registered in the store?

This ambiguity can lead to bugs where consumers assume independence but get a shared reference (or vice versa).

**Recommendation:** Document the exact copy semantics. Consider whether this method belongs on the interface at all — cloning a dataset is a store-level operation, not a dataset-level one.

---

```yaml
---
title: "ITabularQuery lacks ThenBy/ThenByDescending for multi-column sorting"
severity: low
---
```

**File:** `Runtime/Abstractions/ITabularQuery.cs`

**Problem:** The query builder only supports `OrderBy` and `OrderByDescending` as single-column sorts. There is no `ThenBy`/`ThenByDescending` for secondary sort keys. This limits query expressiveness.

**Recommendation:** Add `ThenBy(string column)` and `ThenByDescending(string column)` methods to the fluent API.

---

```yaml
---
title: "ITabularQuery.WhereIn uses generic IEnumerable<T> but stores as object"
severity: low
---
```

**File:** `Runtime/Abstractions/ITabularQuery.cs`, method: `WhereIn<T>(string column, IEnumerable<T> values)`

**Problem:** The generic `T` is erased at the storage layer since all values end up as `object` in dictionaries. The generic signature provides compile-time safety but could be misleading about type enforcement.

**Impact:** Minor — the generic parameter is useful for caller ergonomics but doesn't prevent boxing or type mismatch at runtime.

---

```yaml
---
title: "ITabularDataset.Clear returns int but has no documentation on what it returns"
severity: low
---
```

**File:** `Runtime/Abstractions/ITabularDataset.cs`, method: `int Clear()`

**Problem:** The `Clear` method returns `int` but there's no XML doc explaining what the return value represents (presumably the number of rows cleared). This is inconsistent with other methods that have full documentation.

**Recommendation:** Add `<returns>` XML doc tag.

---

```yaml
---
title: "IGraphDataset edge operations don't support multi-edges"
severity: medium
---
```

**File:** `Runtime/Abstractions/IGraphDataset.cs`

**Problem:** Edge operations use `(fromId, toId)` as a composite key. This means:
- Only one edge can exist between any two nodes
- No support for multiple edges with different properties between the same pair
- `RemoveEdge(fromId, toId)` removes the only possible edge

This is a significant limitation for real-world graph modeling (e.g., multiple relationships between two entities).

**Recommendation:** Consider adding an optional edge ID or edge-type parameter, or document this as an intentional simplification.

---

```yaml
---
title: "DataStoreOptions.ConnectionString purpose is unclear"
severity: low
---
```

**File:** `Runtime/Abstractions/DataStoreFactory.cs`, property: `ConnectionString`

**Problem:** `DataStoreOptions.ConnectionString` is documented as "用于高级配置" (for advanced configuration) but it's unclear how it interacts with the individual properties (`AutoSave`, `CacheSize`, etc.) and the `path` parameter. If both `ConnectionString` and `path` are provided, which takes precedence?

**Recommendation:** Document the interaction between `ConnectionString` and other options. Consider making them mutually exclusive or merging them.

---

```yaml
---
title: "IDataStore lacks async operations"
severity: medium
---
```

**File:** `Runtime/Abstractions/IDataStore.cs` and all related interfaces

**Problem:** All operations are synchronous. For large datasets, operations like `AddRows`, `ExecuteRaw`, `ExportToCsv`, and graph traversals could block the main thread in Unity, causing frame hitches.

**Impact:** Unity games using DataCore with large datasets will experience frame drops during data operations.

**Recommendation:** Add async variants (or Unity coroutine-compatible patterns) for I/O-heavy operations. At minimum, document that heavy operations should be offloaded to background threads.

---

```yaml
---
title: "DataCoreStore.SessionManager is not thread-safe"
severity: medium
---
```

**File:** `Runtime/DataCoreStore.cs`, property: `SessionManager`

```csharp
public SessionManager SessionManager
{
    get
    {
        _sessionManager ??= new SessionManager(this);
        return _sessionManager;
    }
}
```

**Problem:** The lazy initialization of `SessionManager` is not thread-safe. If two threads access `SessionManager` simultaneously on a fresh `DataCoreStore`, two instances could be created, or one thread could see a partially constructed object.

**Recommendation:** Use `Lazy<SessionManager>` or add a lock.

---

```yaml
---
title: "ITabularDataset.Where convenience method is redundant with ITabularQuery"
severity: low
---
```

**File:** `Runtime/Abstractions/ITabularDataset.cs`, method: `int[] Where(string column, QueryOp op, object value)`

**Problem:** `ITabularDataset` has a direct `Where` method that returns `int[]` (row indices), while also exposing `Query()` which returns a full `ITabularQuery` with much richer filtering. The `Where` shortcut is a subset of what `Query` offers and creates API surface confusion.

**Recommendation:** Either remove `Where` in favor of `Query().Where(...).ToRowIndices()`, or document it as a performance shortcut for simple single-condition lookups.
