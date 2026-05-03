# Events & Import Module — Design Issues

---

## Issue 1: No Thread Safety on Static Event Manager

```yaml
title: "DataCoreEventManager has no thread safety — race conditions on subscribe/invoke"
severity: high
```

`DataCoreEventManager` is a static class with mutable static event fields. There are no locks, no `volatile` keywords, and no thread-affinity enforcement.

**Problematic patterns:**

```csharp
// DataCoreEventManager.cs — event fields
public static event EventHandler<DatasetCreatedEventArgs> DatasetCreated;

// Raise method — ?.Invoke is NOT atomic with subscription changes
public static void RaiseDatasetCreated(IDataSet dataset)
{
    DatasetCreated?.Invoke(null, new DatasetCreatedEventArgs(dataset));
}
```

**Scenario:** Thread A calls `RaiseDatasetCreated()` while Thread B calls `ClearAllSubscriptions()`. The `?.Invoke` null-check can pass, then the delegate becomes null before invocation completes → `NullReferenceException`.

**`ClearAllSubscriptions()` is particularly dangerous** — it sets 16 fields to null with no synchronization, while any number of Raise methods could be mid-invocation.

**Recommendation:** Either:
- Add `[ThreadSafe]` enforcement and use `lock` or `Interlocked.CompareExchange` for delegate swapping.
- Or document and enforce main-thread-only usage (Unity-friendly approach), potentially with `UnitySynchronizationContext` checks.

---

## Issue 2: DatasetCreated Fires Before Data Population

```yaml
title: "Importers fire DatasetCreated on empty dataset — subscribers see incomplete data"
severity: high
```

In both `CsvImporter.ImportFromFile(DataCoreStore, ...)` and `GraphMLImporter.ImportFromFile(DataCoreStore, ...)`:

```csharp
// CsvImporter.cs — ImportFromFile(DataCoreStore, ...)
return store.UnderlyingStore.ExecuteInTransaction(() =>
{
    var tabular = store.CreateTabular(datasetName);  // ← fires DatasetCreated HERE
    tabular.ImportFromCsv(csvText, hasHeader, delimiter);  // ← data added AFTER
    return tabular;
});
```

Subscribers to `DatasetCreated` receive a reference to an empty dataset. If they immediately read it (e.g., to display a preview, run validation, or index it), they get incorrect results.

**Recommendation:** Either:
- Fire a separate `DatasetImportCompleted` event after population.
- Or defer `DatasetCreated` until after the transaction completes.
- Or add an `IsImporting` flag to `IDataSet` that subscribers can check.

---

## Issue 3: CSV Parser Doesn't Handle Quoted Fields with Delimiters

```yaml
title: "CSV parser breaks on quoted fields containing delimiters"
severity: high
```

```csharp
// CsvImporter.cs — ImportToTabular
var values = line.Split(delimiter);
```

A naive `Split(delimiter)` does not respect CSV quoting rules. Consider:

```csv
Name,Bio
Alice,"Software engineer, data scientist"
```

This produces `["Alice", "\"Software engineer", " data scientist\""]` — three fields instead of two.

**Recommendation:** Implement a proper CSV state machine or use a library that handles RFC 4180 quoting (escaped quotes `""`, quoted delimiters, quoted newlines).

---

## Issue 4: CSV Header Stripping is Destructive

```yaml
title: "Header parsing strips ALL quotes, breaking headers that contain quotes"
severity: medium
```

```csharp
// CsvImporter.cs
headers = lines[0].Trim().Replace("\"", "").Split(delimiter);
```

`Replace("\"", "")` removes all quote characters from headers. If a header is `"My"Column"`, it becomes `MyColumn`. The data rows use `Trim('"')` which only strips leading/trailing quotes — inconsistent behavior between header and data parsing.

**Recommendation:** Use the same quote-handling logic for headers as for data cells.

---

## Issue 5: GraphML Imports Without Transaction Guarantee

```yaml
title: "GraphML ImportFromFile doesn't wrap creation + population in a transaction"
severity: high
```

```csharp
// GraphMLImporter.cs — ImportFromFile(DataCoreStore, ...)
var graph = store.CreateGraph(datasetName);  // creates dataset, fires event
ImportToGraph(graphmlText, graph);           // populates — could fail partway
return graph;
```

Unlike `CsvImporter` which wraps in `ExecuteInTransaction()`, `GraphMLImporter.ImportFromFile` does NOT wrap the creation + import in a transaction. If `ImportToGraph` throws mid-way (e.g., LiteDB corruption, XML parsing error after partial node insertion), the dataset exists in a partially-populated state with no rollback.

**Contrast with CsvImporter:**

```csharp
// CsvImporter.cs — properly wrapped
return store.UnderlyingStore.ExecuteInTransaction(() =>
{
    var tabular = store.CreateTabular(datasetName);
    tabular.ImportFromCsv(csvText, hasHeader, delimiter);
    return tabular;
});
```

**Recommendation:** Wrap `GraphMLImporter.ImportFromFile(DataCoreStore, ...)` in `ExecuteInTransaction()` like the CSV importer does.

---

## Issue 6: Hard Dependency on LiteDB in GraphML Importer

```yaml
title: "GraphMLImporter has direct LiteDB dependency, violating abstraction"
severity: medium
```

```csharp
// GraphMLImporter.cs — ImportToGraph
if (graph is LiteDb.LiteDbGraphDataset liteDbGraph)
{
    liteDbGraph.FlushMetadata();
}
```

The importer performs a runtime type check against a concrete LiteDB implementation. This violates the `IGraphDataset` abstraction — if the backend changes (e.g., SQLite, in-memory), this code path silently skips `FlushMetadata()`.

**Recommendation:** Add `FlushMetadata()` to the `IGraphDataset` interface (as a no-op for implementations that don't need it), or move the flush responsibility to the caller/store layer.

---

## Issue 7: Encoding Not Configurable in File Importers

```yaml
title: "File.ReadAllText uses system default encoding — no BOM/encoding support"
severity: medium
```

```csharp
// CsvImporter.cs
var csvText = File.ReadAllText(csvPath);

// GraphMLImporter.cs
var graphmlText = File.ReadAllText(graphmlPath);
```

Both importers use `File.ReadAllText(path)` without specifying an `Encoding` parameter. On .NET this defaults to UTF-8 with BOM detection, but:
- Unity's Mono runtime may behave differently.
- Users with GB2312, Shift-JIS, or Latin-1 CSV files will get garbled data.
- No way to specify encoding from the API.

**Recommendation:** Add an optional `Encoding` parameter (defaulting to `null` = auto-detect) to all `ImportFromFile` overloads.

---

## Issue 8: AdditionalData is `object` — Boxing and Type Safety

```yaml
title: "DatasetModifiedEventArgs.AdditionalData uses untyped object — no compile-time safety"
severity: low
```

```csharp
// DataCoreEventArgs.cs
public class DatasetModifiedEventArgs : DataCoreEventArgs
{
    public object AdditionalData { get; }
}
```

Using `object` for `AdditionalData` means:
- Value types are boxed on every event raise.
- Subscribers must cast with no compile-time guarantee.
- No way to know what type to expect.

Similarly, `DatasetQueriedEventArgs.QueryResult` is `object`.

**Recommendation:** Consider generics (`DatasetModifiedEventArgs<T>`) or a `Dictionary<string, object>` for structured metadata.

---

## Issue 9: ClearAllSubscriptions is Dangerous and Unscoped

```yaml
title: "ClearAllSubscriptions() removes ALL listeners globally with no recovery"
severity: medium
```

```csharp
// DataCoreEventManager.cs
public static void ClearAllSubscriptions()
{
    DatasetCreated = null;
    DatasetDeleted = null;
    // ... all 16 events
}
```

This method:
- Removes ALL subscribers from ALL events — including ones from unrelated modules.
- Cannot be undone (no way to restore previous subscriptions).
- Is called during `DataCoreStore.Dispose()` — meaning disposal of one store nukes events for all stores.
- Has no concept of scoped subscriptions (per-session, per-module).

**Recommendation:** Consider subscription scopes or weak-event patterns. At minimum, add a `RemoveSubscriptionsFor(object target)` overload.

---

## Issue 10: CSV Type Inference is All-or-Nothing Per Column

```yaml
title: "One non-numeric value makes entire column string — no mixed-type handling"
severity: low
```

```csharp
// CsvImporter.cs — IsNumeric
if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
{
    doubleValues.Add(d);
}
else
{
    return false;  // entire column becomes string
}
```

A column like `[1, 2, 3, "N/A", 5]` is treated as entirely string. There's no option to:
- Keep the numeric values and use NaN/null for non-numeric.
- Detect and report type conflicts.
- Support integer-only columns (everything becomes `double`).

**Recommendation:** Consider a configurable strategy: `TypeInferenceMode.Strict` (current), `TypeInferenceMode.Coerce` (NaN for failures), `TypeInferenceMode.PerValue` (mixed column type).

---

## Issue 11: GraphML ConvertValue Silently Falls Back to String

```yaml
title: "GraphML type conversion silently returns raw string on parse failure"
severity: low
```

```csharp
// GraphMLImporter.cs — ConvertValue
catch
{
    return value;  // silently returns raw string instead of typed value
}
```

If a GraphML file declares `attr.type="int"` but the actual value is `"abc"`, the importer silently stores `"abc"` as a string. No warning is logged, no error is raised. Downstream code expecting `int` will get `string` at runtime.

**Recommendation:** Log a warning when type conversion fails. Consider returning `null` or a typed default instead of silently changing the type.

---

## Issue 12: No Input Validation on Dataset Names from Importers

```yaml
title: "Importers pass dataset names to store without validation"
severity: low
```

`CsvImporter.ImportFromText` and `GraphMLImporter.ImportFromText` accept a `datasetName` string and pass it directly to `store.CreateTabular(datasetName)`. While `DataCoreStore.CreateTabular` validates for null/whitespace, the importers themselves don't validate before doing expensive file I/O.

This means: read an entire CSV file into memory, then fail on name validation.

**Recommendation:** Validate `datasetName` at the top of importer methods before file I/O.

---

## Issue 13: Inconsistent EventArgs Hierarchy

```yaml
title: "Session/DataFrame/Algorithm events don't use DataCoreEventArgs base class"
severity: low
```

Dataset lifecycle events inherit from `DataCoreEventArgs` (which provides `DatasetName` + `DatasetKind`), but Session, DataFrame, and Algorithm events inherit directly from `EventArgs`. This means:

- No common base for filtering or routing events.
- Session events that *do* involve datasets (`SessionDatasetAddedEventArgs`) don't surface `DatasetName`/`DatasetKind` — you must access `Dataset.Name` / `Dataset.Kind` manually.
- Code that handles "all dataset-related events" must subscribe to each event individually.

**Recommendation:** Either extend `DataCoreEventArgs` for session/dataframe events, or create a parallel `SessionEventArgs` base for the session group.

---

## Issue 14: GraphML Auto-Created Nodes Have No Properties

```yaml
title: "Auto-created missing nodes in GraphML import have null properties"
severity: low
```

```csharp
// GraphMLImporter.cs — ParseEdges
var nodesToAdd = missingNodes.Select(id => (id, (IDictionary<string, object>)null));
```

When edges reference nodes that weren't declared, the importer creates them with `null` properties. This means:
- No default values from `<key>` definitions are applied.
- Downstream code accessing properties on these nodes may get `NullReferenceException`.
- These "phantom" nodes are indistinguishable from intentionally empty nodes.

**Recommendation:** Apply default property values from the `keyMap` when auto-creating nodes (same as `ParseProperties` does for declared nodes).

---

## Summary

| Severity | Count | Issues |
|----------|-------|--------|
| 🔴 Critical | 0 | — |
| 🟠 High | 4 | #1 Thread safety, #2 Event timing, #3 CSV quoting, #5 Transaction gap |
| 🟡 Medium | 4 | #4 Header stripping, #6 LiteDB coupling, #7 Encoding, #9 ClearAll |
| 🟢 Low | 6 | #8 Boxing, #10 Type inference, #11 Silent fallback, #12 Validation, #13 Hierarchy, #14 Null properties |
