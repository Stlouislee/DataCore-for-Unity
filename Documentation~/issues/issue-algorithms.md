# Algorithm Pipeline — Design Issues

---

### 1. AlgorithmBase.Execute silently swallows all exceptions

---
title: "AlgorithmBase.Execute catches all exceptions, masking bugs"
severity: high
---

**File**: `AlgorithmBase.cs` — `Execute()` method

The `catch (Exception ex)` block in `Execute()` catches **every** exception type, including `NullReferenceException`, `StackOverflowException`, `OutOfMemoryException`, and `IndexOutOfRangeException`. These critical runtime errors are silently converted into a failed `AlgorithmResult` with only `ex.Message` preserved.

```csharp
catch (Exception ex)
{
    sw.Stop();
    DataCoreEventManager.RaiseAlgorithmCompleted(Name, input, null, false, sw.Elapsed, ex.Message);
    return AlgorithmResult.Failed(Name, ex.Message, sw.Elapsed);
}
```

**Impact**: Bugs in algorithm implementations are hidden behind a `Success = false` result. Callers that only check `result.Success` will never know a crash occurred. Stack traces are lost — `AlgorithmResult` has no `Exception` property.

**Recommendation**:
- At minimum, log the full exception (including stack trace) via `UnityEngine.Debug.LogException` or equivalent.
- Consider re-throwing fatal exceptions (`OutOfMemoryException`, `StackOverflowException`) rather than catching them.
- Add an `Exception` property to `AlgorithmResult` for callers that need the full error.

---

### 2. AlgorithmRegistry.Default is not thread-safe for lazy initialization

---
title: "Race condition in AlgorithmRegistry.Default singleton"
severity: high
---

**File**: `AlgorithmRegistry.cs` — `Default` property getter

```csharp
public static AlgorithmRegistry Default
{
    get
    {
        if (_default == null)
        {
            _default = new AlgorithmRegistry();
            _default.RegisterBuiltIns();
        }
        return _default;
    }
}
```

This is a classic **check-then-act race condition**. If two threads access `Default` simultaneously on first access, two instances may be created, and `RegisterBuiltIns()` may run twice or interleave with registration calls.

**Impact**: Duplicate registrations, lost registrations, or two different "Default" instances used by different parts of the application.

**Recommendation**: Use `Lazy<AlgorithmRegistry>` or a lock:
```csharp
private static readonly Lazy<AlgorithmRegistry> _default = new(() =>
{
    var r = new AlgorithmRegistry();
    r.RegisterBuiltIns();
    return r;
});
public static AlgorithmRegistry Default => _default.Value;
```

---

### 3. AlgorithmContext.Parameters exposes internal mutable dictionary

---
title: "AlgorithmContext.Parameters allows mutation through cast"
severity: medium
---

**File**: `AlgorithmContext.cs`

The `Parameters` property returns `IReadOnlyDictionary<string, object>`, but the backing field `_parameters` is a `Dictionary<string, object>`. Any caller that casts the return value to `IDictionary<string, object>` can mutate the context after construction:

```csharp
((IDictionary<string, object>)context.Parameters).Add("evil", true);
```

**Impact**: Violates the immutability contract of the context. Algorithms could interfere with each other in a pipeline if they hold references to the same context.

**Recommendation**: Return a read-only wrapper:
```csharp
public IReadOnlyDictionary<string, object> Parameters =>
    new ReadOnlyDictionary<string, object>(_parameters);
```
Or use `FrozenDictionary` (.NET 8+) at build time.

---

### 4. AlgorithmContext.Empty allocates a new instance on every access

---
title: "AlgorithmContext.Empty creates a new instance each call"
severity: low
---

**File**: `AlgorithmContext.cs`

```csharp
public static AlgorithmContext Empty => Create().Build();
```

Every access to `Empty` allocates a new `Builder` + `AlgorithmContext` + internal `Dictionary`. This is called frequently (default parameter in `Execute`, `Pipeline.Execute`).

**Impact**: Unnecessary GC pressure, especially in hot paths or loops.

**Recommendation**: Cache the instance:
```csharp
private static readonly AlgorithmContext _empty = Create().Build();
public static AlgorithmContext Empty => _empty;
```

---

### 5. Pipeline does not propagate OutputName to step contexts

---
title: "Pipeline ignores baseContext.OutputName for individual steps"
severity: medium
---

**File**: `AlgorithmPipeline.cs` — `Execute()` method

When building per-step contexts, the pipeline creates a new builder and copies `CancellationToken` and `Store` from the base context, but does **not** copy `OutputName`:

```csharp
var stepBuilder = AlgorithmContext.Create()
    .WithCancellation(baseContext.CancellationToken)
    .WithStore(baseContext.Store);
```

**Impact**: If a caller sets `OutputName` on the base context expecting it to propagate to pipeline steps, it will be silently ignored. Each step will generate its own output name.

**Recommendation**: Either propagate `OutputName` from base context, or document this behavior explicitly. Per-step `OutputName` overrides should still be possible via `step.Configure`.

---

### 6. ConnectedComponents: Tarjan's SCC parent low-link update logic is fragile

---
title: "Tarjan's iterative implementation has subtle parent update issue"
severity: medium
---

**File**: `ConnectedComponentsAlgorithm.cs` — `TarjanSCC()`

The iterative Tarjan's implementation uses a custom call stack with `(Node, Neighbors, Initialized)` tuples. When all neighbors of a node are processed (`!pushedChild`), it updates the parent's low-link:

```csharp
if (callStack.Count > 0)
{
    var parent = callStack.Peek();
    low[parent.Node] = Math.Min(low[parent.Node], low[node]);
}
```

However, `callStack.Peek()` may not be the actual DFS parent — it's the next frame on the explicit stack, which could be a sibling or ancestor depending on the call stack structure. The `Initialized` flag is declared but never used in the tuple, suggesting incomplete implementation.

**Impact**: May produce incorrect strongly-connected component assignments for certain graph topologies.

**Recommendation**: Add a `Parent` field to the call stack tuple to explicitly track the DFS parent, and verify correctness with test cases covering complex SCC graphs (e.g., multiple cross-edges, nested SCCs).

---

### 7. MinMaxNormalize throws exceptions instead of returning validation errors

---
title: "MinMaxNormalize throws in ExecuteTabular instead of using ValidateParameters"
severity: medium
---

**File**: `MinMaxNormalizeAlgorithm.cs`

When a requested column doesn't exist or isn't numeric, the algorithm throws `ArgumentException` directly inside `ExecuteTabular`:

```csharp
if (!input.HasColumn(col))
    throw new ArgumentException($"Column '{col}' does not exist...");
if (input.GetColumnType(col) != ColumnType.Numeric)
    throw new ArgumentException($"Column '{col}' is not numeric...");
```

And when no numeric columns are found:
```csharp
throw new InvalidOperationException("No numeric columns found to normalize.");
```

While the base class's `Execute` catches these and converts to `AlgorithmResult.Failed`, the pattern is inconsistent with the framework's `ValidateParameters` approach. These validations should happen before execution.

**Impact**: Violates the validation-then-execute contract. Callers that pre-validate via `ValidateParameters` won't catch these errors, leading to unexpected failures during execution.

**Recommendation**: Move column existence and type checks into an overridden `ValidateParameters` method. Keep only truly runtime-dependent checks (e.g., data corruption during iteration) in the execute path.

---

### 8. AlgorithmBase.Execute uses unsafe cast on result.Metadata

---
title: "Unsafe IDictionary cast on result metadata"
severity: low
---

**File**: `AlgorithmBase.cs` — `Execute()` method

```csharp
var metadata = new Dictionary<string, object>(
    result.Metadata as IDictionary<string, object> ?? new Dictionary<string, object>());
```

This cast (`as IDictionary<string, object>`) relies on the concrete `Dictionary<string, object>` returned by `AlgorithmResult.Succeeded` also implementing `IDictionary<TKey,TValue>`. If a future `AlgorithmResult` implementation returns a different `IReadOnlyDictionary` implementation (e.g., `FrozenDictionary`, `ImmutableDictionary`), the cast fails silently and all original metadata is lost.

**Impact**: Metadata from the algorithm's own result would be silently discarded and replaced with only the base-class-added metadata.

**Recommendation**: Use `new Dictionary<string, object>(result.Metadata)` directly — the `IReadOnlyDictionary<TKey,TValue>` constructor overload exists in .NET.

---

### 9. AlgorithmResult.Failed sets Metrics to null instead of empty dictionary

---
title: "Inconsistent null vs empty for Metrics on failed results"
severity: low
---

**File**: `AlgorithmResult.cs` — `Failed()` factory

```csharp
public static AlgorithmResult Failed(...)
{
    return new AlgorithmResult(
        ...
        metrics: null,    // ← null
        ...
    );
}
```

But the constructor defaults `Metrics` to an empty dictionary when null:
```csharp
Metrics = metrics ?? new Dictionary<string, object>();
```

While this prevents `NullReferenceException`, the `PipelineResult.GetAllMetrics()` iterates `StepResults[i].Metrics` — if a failed step's metrics are the empty default, it works. But the `Succeeded` factory explicitly accepts `null` metrics and also defaults to empty. The inconsistency between the factory methods makes the API confusing.

**Recommendation**: Always pass an explicit empty dictionary in `Failed()` instead of relying on the constructor's null-coalescing, for clarity.

---

### 10. AlgorithmPipeline.GetAllMetrics() calls AlgorithmName on result without null check

---
title: "PipelineResult.GetAllMetrics may NPE on malformed results"
severity: low
---

**File**: `AlgorithmPipeline.cs` — `GetAllMetrics()`

```csharp
all[$"{i}.{StepResults[i].AlgorithmName}.{kvp.Key}"] = kvp.Value;
```

If `StepResults[i]` is somehow null, or `AlgorithmName` is null, this will throw `NullReferenceException`. While the current code path always sets `AlgorithmName`, defensive programming suggests a null check.

**Recommendation**: Use null-conditional: `StepResults[i]?.AlgorithmName ?? "unknown"`.

---

### 11. PageRank does not validate dampingFactor range

---
title: "PageRank accepts invalid dampingFactor values"
severity: low
---

**File**: `PageRankAlgorithm.cs`

The `dampingFactor` parameter is documented as "0-1" but no validation enforces this. A damping factor of 0 would give all nodes equal rank (trivial). A value > 1 would cause scores to diverge (non-convergent). Negative values would produce nonsensical results.

**Recommendation**: Override `ValidateParameters`:
```csharp
if (damping < 0 || damping > 1)
    errors.Add("dampingFactor must be between 0 and 1.");
```

---

### 12. ConnectedComponents copies entire graph topology without optimization

---
title: "ConnectedComponents deep-copies all node/edge properties"
severity: low
---

**File**: `ConnectedComponentsAlgorithm.cs`

Both `PageRank` and `ConnectedComponents` copy every node and edge property into the output graph:

```csharp
var newProps = existingProps != null
    ? new Dictionary<string, object>(existingProps)
    : new Dictionary<string, object>();
newProps["componentId"] = componentIds[i];
output.AddNode(nodeIds[i], newProps);
```

For large graphs with many properties, this is expensive in both time and memory. The algorithm only needs to **add** `componentId`/`pagerank` to existing data — a shallow property merge or property-overlay pattern would be more efficient.

**Recommendation**: Consider an overlay/mutation API on `GraphData` that adds properties to existing nodes rather than requiring full reconstruction. Or at minimum, document this as a known memory consideration.

---

### 13. No cancellation check inside PageRank's inner loop

---
title: "PageRank only checks cancellation between iterations, not within"
severity: low
---

**File**: `PageRankAlgorithm.cs`

```csharp
for (int iter = 0; iter < maxIter; iter++)
{
    context.CancellationToken.ThrowIfCancellationRequested();
    // ... inner loop over all nodes ...
}
```

For very large graphs (millions of nodes), a single iteration could take seconds. The cancellation token is only checked once per iteration.

**Impact**: Cancellation responsiveness degrades with graph size.

**Recommendation**: Add periodic cancellation checks inside the inner score-computation loop (e.g., every 10,000 nodes).
