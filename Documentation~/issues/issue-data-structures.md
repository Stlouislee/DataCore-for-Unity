# Data Structures Issue Tracker

## GraphData Issues

---
title: "GraphData.WithName copies adjacency entries for nodes that may not exist in source"
severity: high
---

### Description

In `GraphData.WithName()`, the copy loop iterates `_nodes` and simultaneously accesses `_outAdjacency[kvp.Key]` and `_inAdjacency[kvp.Key]`. This assumes every key in `_nodes` also exists in both adjacency dictionaries. While `AddNode` maintains this invariant, any internal corruption or future refactor that breaks it will cause a `KeyNotFoundException` during copy.

More critically, the copy does **not** handle edges whose endpoints exist but are stored only in `_edgeProperties`. If an edge's key references nodes that were added, the adjacency sets are correct — but the `_edgeProperties` copy is independent of the node loop, meaning orphaned edge properties could be copied without their adjacency entries, or vice versa.

### Recommendation

Add defensive checks or restructure to copy edges from adjacency sets rather than independently copying `_edgeProperties`. Consider copying edges by iterating `_outAdjacency` to reconstruct `_edgeProperties` keys, ensuring consistency.

---

title: "Edge key separator \0 can appear in node IDs"
severity: high
---

### Description

`GetEdgeKey` uses `"\0"` (null byte) as separator: `$"{fromId}\0{toId}"`. `ParseEdgeKey` splits on `'\0'` and returns `parts[0]` and `parts[1]`. If a node ID contains a null byte character, the split will produce more than 2 parts, and `ParseEdgeKey` will silently return incorrect results (only the first segment of `fromId` and the first segment of `toId`).

While null bytes in string IDs are unusual, nothing in `AddNode` prevents them.

### Recommendation

Either:
1. Validate node IDs in `AddNode` to reject null bytes, or
2. Use a separator that is guaranteed not to appear in valid IDs, or
3. Use a tuple-based dictionary (`(string, string)`) as the edge key instead of string concatenation

---

title: "GetEdges returns IEnumerable but parsing is deferred — concurrent modification causes incorrect results"
severity: medium
---

### Description

`GetEdges()` returns `_edgeProperties.Keys.Select(ParseEdgeKey)`, which is lazily evaluated. If the graph is modified between iterations of the returned enumerable, the edge keys may no longer be valid, or `ParseEdgeKey` may encounter malformed keys if the dictionary was modified during enumeration.

### Recommendation

Return a materialized list: `return _edgeProperties.Keys.Select(ParseEdgeKey).ToList();`

---

title: "BFS yields start node even if it fails node filters"
severity: medium
---

### Description

In `InMemoryGraphQuery.BFS()`, the start node is always added to the visited set and enqueued. When dequeued, filters are applied and the node is yielded only if it passes. However, the start node is **always visited** — meaning if it fails filters, its neighbors are still explored. This is semantically debatable: should traversal stop if the root doesn't match?

More importantly, the start node is checked against `_nodeFilters` but **not** against `_edgeFilters` (since there's no incoming edge). If edge filters are the only constraints, the start node will always pass.

### Recommendation

Document the behavior clearly. Consider adding a `TraverseOnly(bool)` option that skips yielding the start node but still traverses from it.

---

title: "Query is not reusable — state accumulates across calls"
severity: medium
---

### Description

`GraphData.Query()` creates a new `InMemoryGraphQuery` each time, which is correct. However, calling terminal methods like `ToNodeIds()` or `CountNodes()` multiple times on the same query instance will re-execute the BFS each time, which is expected for lazy queries but may surprise users. The `BFS()` method uses `yield return`, so it's lazy — but `CountNodes()` calls `.Count()` which forces full materialization.

### Recommendation

This is acceptable LINQ-style behavior. Consider documenting that queries are single-execution or provide a `.Cache()` method for repeated access.

---

title: "AddNodes/AddEdges batch operations fail partially on error"
severity: medium
---

### Description

`AddNodes` iterates and calls `AddNode` for each item. If a duplicate node ID is encountered midway, an `ArgumentException` is thrown, leaving the graph in a partially-modified state (some nodes added, the rest not). The same applies to `AddEdges`.

### Recommendation

Either:
1. Validate all inputs before applying any changes (two-pass approach), or
2. Document that batch operations are not atomic, or
3. Wrap in a transaction-like mechanism that rolls back on failure

---

title: "GetNodeProperties and GetEdgeProperties return shallow copies"
severity: low
---

### Description

Both methods create `new Dictionary<string, object>(props)`, which copies the key-value pairs but not the values themselves. If a property value is a mutable reference type (e.g., a List or custom object), modifying it through the returned dictionary will affect the internal state.

### Recommendation

Document that property values are shallow-copied. For deep safety, consider cloning values or returning a read-only wrapper (though this has performance implications).

---

title: "CompareNumeric throws FormatException on non-numeric values"
severity: medium
---

### Description

`InMemoryGraphQuery.CompareNumeric` calls `Convert.ToDouble(left)` without try-catch. If a property value is a non-numeric string (e.g., "abc"), this will throw a `FormatException` during query execution, crashing the query rather than gracefully handling the mismatch.

### Recommendation

Wrap in try-catch and return a sentinel value (e.g., `double.NaN`) or skip non-comparable values:

```csharp
private static int CompareNumeric(object left, object right)
{
    if (!TryConvertToDouble(left, out var leftVal) || !TryConvertToDouble(right, out var rightVal))
        return 0; // or throw a descriptive exception
    return leftVal.CompareTo(rightVal);
}
```

---

title: "WhereEdgeProperty in BFS applies to wrong edge direction"
severity: high
---

### Description

In `BFS()`, edge filters are applied as `_edgeFilters.All(f => f(current, neighbor))`. This checks the edge from `current` to `neighbor`. However, when `_traverseIn` is true, the actual graph edge is from `neighbor` to `current` (we're traversing incoming edges). The edge filter is applied with the wrong argument order, meaning `WhereEdgeProperty` filters will behave incorrectly for inbound traversal.

### Recommendation

When traversing in-edges, apply filters as `f(neighbor, current)` to match the actual edge direction:

```csharp
bool edgePassesFilter = _traverseIn
    ? _edgeFilters.All(f => f(neighbor, current))
    : _edgeFilters.All(f => f(current, neighbor));
```

## TabularData Issues

---
title: "AddRow rebuilds entire NDArray for every row — O(R²) insertion"
severity: high
---

### Description

`AddRow` for numeric columns extracts the entire existing array via `ToArray<double>()`, creates a new array one element larger, copies everything, and reconstructs the NDArray. For R rows, this is:

- Row 1: copy 0 elements
- Row 2: copy 1 element
- Row R: copy R-1 elements
- Total: O(R²) copies

For 10,000 rows with 10 numeric columns, this performs ~500 million element copies. `AddRows` calls `AddRow` in a loop, inheriting this quadratic behavior.

### Recommendation

Use a resizable backing store (e.g., `List<double>`) during construction, then convert to NDArray on finalization. Or batch-allocate NDArrays in chunks and merge.

---

title: "UpdateRow and DeleteRow reconstruct NDArray per operation"
severity: high
---

### Description

`UpdateRow` calls `ToArray<double>()` on the entire column, modifies one element, then `np.array(data)` to reconstruct. `DeleteRow` does the same but also converts to List and back. Single-element operations on large arrays are O(n) due to full reconstruction.

For a table with 100K rows and 5 numeric columns, updating one row copies ~500K doubles unnecessarily.

### Recommendation

Expose NDArray mutation via indexers if NumSharp supports it, or maintain a mutable backing array and only reconstruct NDArray when needed for column-level access.

---

title: "ImportFromCsv type detection only checks first data row"
severity: medium
---

### Description

```csharp
var firstDataRow = lines[dataStartIndex].Split(delimiter);
for (int i = 0; i < headers.Length; i++)
    isNumeric[i] = double.TryParse(firstDataRow[i], out _);
```

If the first data row has `"0"` in a column that is actually string-typed (e.g., a zip code), the entire column will be treated as numeric. Subsequent rows with non-numeric values in that column will silently parse as `0.0` via `double.TryParse` failure.

### Recommendation

Sample multiple rows (e.g., first 100) and use majority voting for type detection. Or provide an explicit schema parameter.

---

title: "ImportFromCsv has no quoted-field or escape handling"
severity: medium
---

### Description

CSV parsing uses `lines[row].Split(delimiter)` with no support for:
- Quoted fields (`"hello, world"` containing a delimiter)
- Escaped quotes (`""` inside quoted fields)
- Fields containing newlines

This will produce incorrect results for any real-world CSV that contains these patterns.

### Recommendation

Use a proper CSV parser library (e.g., CsvHelper) or implement RFC 4180 compliant parsing.

---

title: "ExportToCsv does not escape delimiters in values"
severity: medium
---

### Description

```csharp
values.Add(_stringData[colName][i]);
// ...
sb.AppendLine(string.Join(delimiter.ToString(), values));
```

If a string value contains the delimiter character (e.g., a comma in a name), the exported CSV will have extra columns, corrupting the data.

### Recommendation

Wrap values containing the delimiter, quotes, or newlines in double quotes per RFC 4180.

---

title: "HasColumn uses List.Contains instead of Dictionary lookup"
severity: low
---

### Description

```csharp
public bool HasColumn(string name) => _columnNames.Contains(name);
```

`_columnNames` is a `List<string>`, so `Contains` is O(C) where C is column count. The column type dictionary `_columnTypes` already has all column names as keys, enabling O(1) lookup.

### Recommendation

```csharp
public bool HasColumn(string name) => _columnTypes.ContainsKey(name);
```

---

title: "GetNumericColumn returns internal NDArray reference — callers can corrupt state"
severity: high
---

### Description

```csharp
public NDArray GetNumericColumn(string name)
{
    if (!_numericData.TryGetValue(name, out var data))
        throw new KeyNotFoundException(...);
    return data;
}
```

Returns the **actual internal NDArray**, not a copy. External code modifying this array will silently corrupt TabularData's internal state, bypassing all validation and breaking row count invariants.

### Recommendation

Return a read-only view or a clone. For performance-sensitive reads, document the shared-reference contract clearly.

---

title: "GetStringColumn returns internal array reference"
severity: medium
---

### Description

Same as the NDArray issue: `GetStringColumn` returns the internal `string[]` directly. Callers can modify elements or resize (though arrays are fixed-length, element mutation is possible).

### Recommendation

Return a copy or `IReadOnlyList<string>` wrapper.

---

title: "Where method does not handle QueryOp.EndsWith"
severity: low
---

### Description

```csharp
return op switch
{
    QueryOp.Eq => WhereEquals(column, value),
    // ...
    QueryOp.StartsWith => WhereStartsWith(column, value?.ToString() ?? ""),
    _ => this  // EndsWith silently ignored
};
```

`QueryOp.EndsWith` is defined in the enum but not handled in the `Where` method — it falls through to the default case and silently does nothing.

### Recommendation

Add `QueryOp.EndsWith => WhereEndsWith(...)` and implement the corresponding filter method.

---

title: "Ordering in ToDictionaries uses default object comparison"
severity: medium
---

### Description

```csharp
result = _orderDescending
    ? result.OrderByDescending(x => x.row[_orderByColumn])
    : result.OrderBy(x => x.row[_orderByColumn]);
```

`Dictionary<string, object>` values are typed as `object`. LINQ's `OrderBy` uses `Comparer<object>.Default`, which for numeric columns stored as `double` boxes the value and uses `IComparable`. This works for doubles but will throw if the column contains non-comparable types, and has boxing overhead.

### Recommendation

Check the column type and use typed comparison:

```csharp
var colType = _source.GetColumnType(_orderByColumn);
if (colType == ColumnType.Numeric)
    result = result.OrderBy(x => Convert.ToDouble(x.row[_orderByColumn]));
```

---

title: "TabularData.Clear does not remove columns — only empties data"
severity: low
---

### Description

`Clear()` replaces all arrays with empty ones and resets `_rowCount = 0`, but leaves `_columnNames`, `_columnTypes`, and the empty dictionary entries intact. This is inconsistent with the method name — a "clear" operation might be expected to reset to a completely empty state.

The interface documentation says "清空所有行数据" (clear all row data), so the behavior matches the intent. However, the method returns the cleared row count, which is useful for reporting.

### Recommendation

This is acceptable if documented. Consider adding a `Reset()` method that also removes columns for full cleanup.

---

title: "Std uses population standard deviation (N) not sample (N-1)"
severity: low
---

### Description

```csharp
return Math.Sqrt(sumSquares / data.Length);
```

This computes the population standard deviation (divides by N). For sample data, the convention is to divide by N-1 (Bessel's correction). The method name `Std` is ambiguous.

### Recommendation

Either:
1. Use N-1 and rename to `SampleStd` or document the convention, or
2. Provide both `StdPop` and `StdSample` methods

---

title: "Numeric type detection in AddRow is incomplete"
severity: medium
---

### Description

```csharp
if (kvp.Value is double or int or float or long)
{
    _columnTypes[kvp.Key] = ColumnType.Numeric;
}
```

This misses `decimal`, `short`, `byte`, `uint`, `ulong`, `ushort` types. A `decimal` value will be stored as a string via `ToString()`, losing numeric semantics.

### Recommendation

Use a more comprehensive check:

```csharp
if (kvp.Value is IConvertible && kvp.Value is not bool and not char and not string)
```

Or check `Convert.GetTypeCode` for numeric type codes.

---

title: "ExecuteRaw always throws — interface leak from LiteDB implementation"
severity: low
---

### Description

```csharp
public RawResult ExecuteRaw(string sql, params object[] args)
{
    throw new NotSupportedException("ExecuteRaw is only supported on LiteDB-backed datasets");
}
```

This method exists on the `ITabularDataset` interface but is meaningless for in-memory implementations. It forces every implementer to provide a throwing stub.

### Recommendation

Consider splitting the interface: `ITabularDataset` for core operations, `IRawQueryable` for SQL-like access. This follows Interface Segregation Principle.
