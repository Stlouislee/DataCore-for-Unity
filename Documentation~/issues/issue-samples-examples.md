---
title: "Silent data corruption in CSV parser — unparseable values replaced with 0.0"
severity: critical
---

**File:** `Runtime/SampleDatasets/CaliforniaHousingDataset.cs`
**Method:** `ParseCsv()`

```csharp
if (double.TryParse(values[j], System.Globalization.NumberStyles.Any,
    System.Globalization.CultureInfo.InvariantCulture, out var val))
{
    data[headers[j]].Add(val);
}
else
{
    data[headers[j]].Add(0.0);  // ← Silent corruption
}
```

**Problem:** When a CSV cell contains non-numeric data (e.g., "N/A", "null", empty string, or corrupted bytes), the parser silently substitutes `0.0` without any warning. This affects:

1. **Statistical accuracy** — `GetStatistics()` will report misleading min/max/mean values
2. **Query results** — `Where("median_house_value", QueryOp.Gt, 500000)` will incorrectly exclude rows that had missing data
3. **No diagnostic information** — Users have no way to know how many values were corrupted

**Impact:** If the CSV file has even a few corrupted cells, all downstream analysis is silently wrong. The 10-row fallback data masks this because it's hardcoded and always valid.

**Recommendation:**
- Log a warning for each unparseable value with row/column info
- Track a corruption count and surface it in `GetStatistics()`
- Consider returning `double.NaN` instead of `0.0` for missing values
- At minimum, add a `parseErrors` count to the return value

---

---
title: "No thread safety on DataCoreEditorComponent singleton initialization"
severity: critical
---

**File:** `Runtime/DataCoreEditorComponent.cs`
**Method:** `Awake()`

```csharp
private void Awake()
{
    if (Instance != null && Instance != this)
    {
        Destroy(this);
        return;
    }
    Instance = this;
    InitializeStore();
    // ...
}
```

**Problem:** The singleton check-then-set is not atomic. If two `DataCoreEditorComponent` instances exist in the scene (e.g., from prefab duplication), both could pass the `Instance != null` check before either sets `Instance = this`. While Unity's main thread model reduces this risk, it's not guaranteed — especially with:

- Additive scene loading (multiple scenes each with their own instance)
- `Instantiate()` calls during `Awake()`
- Future multi-threaded Unity scenarios (Jobs system)

**Impact:** Two `DataCoreStore` instances could be created, each pointing to the same LiteDB file. LiteDB uses file-level locking, so concurrent writes would throw `LiteException`.

**Recommendation:** Use a lock or compare-exchange pattern, or enforce single-instance via `[DisallowMultipleComponent]` attribute.

---

---
title: "Duplicate dataset loading logic across three classes"
severity: high
---

**Files:**
- `Runtime/SampleDatasets/CaliforniaHousingDataset.cs` — `LoadIntoDataCore()`
- `Runtime/SampleDatasets/CaliforniaHousingLoader.cs` — `LoadDataset()`
- `Runtime/SampleDatasets/SampleDatasetManager.cs` — `CheckAndLoadCaliforniaHousing()`

**Problem:** Three separate code paths can load the California Housing dataset:

1. `CaliforniaHousingDataset.LoadIntoDataCore()` — static method, creates tabular directly
2. `CaliforniaHousingLoader.LoadDataset()` — MonoBehaviour, calls `CaliforniaHousingDataset.GetSampleData()` then adds columns
3. `SampleDatasetManager.Start()` — calls `CaliforniaHousingDataset.LoadIntoDataCore()`

Each path does slightly different things:
- Path 1 creates the dataset via `store.CreateTabular()` then adds columns
- Path 2 creates via `store.CreateTabular()` then calls `AddCaliforniaHousingData()`
- Path 3 delegates to path 1

**Impact:** If a user adds both `CaliforniaHousingLoader` and `SampleDatasetManager` to the scene, the dataset may be loaded twice (or the second load will delete and recreate it). The `loadOnStart` flag on `CaliforniaHousingLoader` and `loadCaliforniaHousing` on `SampleDatasetManager` are independent toggles with no coordination.

**Recommendation:** Consolidate into a single loading path. `SampleDatasetManager` should be the only orchestrator, and `CaliforniaHousingLoader` should be a UI/demo component that doesn't auto-load.

---

---
title: "Self-test creates temporary database files that are never cleaned up"
severity: high
---

**File:** `Runtime/DataCoreSelfTest.cs`
**Method:** `CreateTestStore()`

```csharp
private static DataCoreStore CreateTestStore()
{
    var tempPath = System.IO.Path.Combine(
        Application.temporaryCachePath, "DataCore",
        $"selftest_{System.Diagnostics.Process.GetCurrentProcess().Id}.db");
    return new DataCoreStore(tempPath);
}
```

**Problem:** Each test method creates a new `DataCoreStore` with `using var store = ...`, which calls `Dispose()` on the store. However, `Dispose()` only closes the LiteDB connection — it does **not** delete the database file. Over multiple Play mode sessions, these files accumulate:

- `selftest_12345.db`
- `selftest_12345.db-log`
- etc.

**Impact:** Disk space leak. On mobile platforms with limited storage, this could become significant over time. The PID suffix means files from crashed processes are never cleaned up.

**Recommendation:** Add file cleanup after each test:
```csharp
using (var store = CreateTestStore())
{
    // ... tests ...
}
// Delete the test database files
File.Delete(tempPath);
File.Delete(tempPath + "-log");
```

---

---
title: "Implicit execution order dependency between components"
severity: high
---

**Files:**
- `Runtime/DataCoreEditorComponent.cs` — `Awake()` initializes store
- `Runtime/SampleDatasets/SampleDatasetManager.cs` — `Start()` accesses store
- `Runtime/SampleDatasets/CaliforniaHousingLoader.cs` — `Start()` accesses store

**Problem:** Both `SampleDatasetManager` and `CaliforniaHousingLoader` access `DataCoreEditorComponent.Instance.GetStore()` in their `Start()` methods. This assumes `DataCoreEditorComponent.Awake()` has already completed. Unity's execution order is:

1. All `Awake()` methods run (order undefined across GameObjects)
2. All `Start()` methods run (order undefined across GameObjects)

If `SampleDatasetManager.Start()` runs before `DataCoreEditorComponent.Awake()`, `Instance` will be null and the component logs a warning and does nothing.

**Impact:** On some frames or project configurations, sample datasets may silently fail to load. The user sees a warning but no data.

**Recommendation:** Either:
- Use `[DefaultExecutionOrder(-100)]` on `DataCoreEditorComponent` to ensure it initializes first
- Or have `SampleDatasetManager` and `CaliforniaHousingLoader` wait for the store to be ready (e.g., via a coroutine or event)

---

---
title: "Examples use Console.WriteLine instead of Debug.Log — invisible in Unity"
severity: high
---

**Files:** All files in `Runtime/Examples/`

```csharp
// DataCoreUsageExample.cs, EventListenerExample.cs, etc.
Console.WriteLine("=== 数据分析工作流示例 ===");
Console.WriteLine($"Created dataset with {myData.RowCount} rows");
```

**Problem:** `Console.WriteLine` in Unity does not output to the Unity Console window. It goes to:
- On Windows: `stdout` (visible in external terminal if launched from there)
- On macOS/Linux: `Player.log` file
- In Editor: Usually lost unless "Collapse" is disabled and the log file is checked

**Impact:** Users running these examples in the Unity Editor will see no output, thinking the code doesn't work or the examples are broken.

**Recommendation:** Replace all `Console.WriteLine` with `Debug.Log` for Unity compatibility. If the examples are also intended for standalone .NET, use a conditional:
```csharp
#if UNITY_ENGINE
    Debug.Log(message);
#else
    Console.WriteLine(message);
#endif
```

---

---
title: "CaliforniaHousingExample uses fragile Invoke-based timing"
severity: high
---

**File:** `Runtime/SampleDatasets/CaliforniaHousingExample.cs`

```csharp
private void Start()
{
    // ...
    Invoke(nameof(RunExampleQueries), 1f);
}

[ContextMenu("Reload Dataset")]
private void ReloadDataset()
{
    if (loader != null)
    {
        loader.LoadDataset();
        Invoke(nameof(RunExampleQueries), 0.5f);
    }
}
```

**Problem:** The example uses hardcoded delays (1s, 0.5s) to wait for dataset loading. This is fragile because:
- On slow devices or large datasets, 0.5s may not be enough
- On fast devices, it wastes time
- It teaches users a bad pattern

**Impact:** Users copying this pattern will experience intermittent failures in their own code.

**Recommendation:** Use an event-driven approach:
```csharp
// In CaliforniaHousingLoader:
public event Action OnDatasetLoaded;

// In CaliforniaHousingExample:
loader.OnDatasetLoaded += RunExampleQueries;
```

---

---
title: "CaliforniaHousingDataset.GetStatistics() re-parses CSV on every call"
severity: medium
---

**File:** `Runtime/SampleDatasets/CaliforniaHousingDataset.cs`
**Method:** `GetStatistics()`

```csharp
public static Dictionary<string, string> GetStatistics()
{
    var data = GetSampleData();  // ← Re-parses CSV every time
    // ...
}
```

**Problem:** `GetSampleData()` calls `Resources.Load()` and `ParseCsv()` on every invocation. The CSV parsing involves string splitting, double parsing, and list allocation. For the full California Housing dataset (~20K rows), this is non-trivial.

**Impact:** Performance waste if called multiple times (e.g., in a UI that refreshes statistics). Not a correctness issue.

**Recommendation:** Cache the parsed data in a static field:
```csharp
private static Dictionary<string, double[]> _cachedData;
public static Dictionary<string, double[]> GetSampleData()
{
    if (_cachedData != null) return _cachedData;
    // ... parse and cache ...
}
```

---

---
title: "SampleDatasetManager is not extensible — hardcoded dataset list"
severity: medium
---

**File:** `Runtime/SampleDatasets/SampleDatasetManager.cs`

```csharp
[SerializeField] private bool loadCaliforniaHousing = true;

private void Start()
{
    if (loadCaliforniaHousing)
    {
        CheckAndLoadCaliforniaHousing();
    }
}
```

**Problem:** Adding a new sample dataset requires:
1. Adding a new `[SerializeField] bool` field
2. Adding a new `CheckAndLoad*()` method
3. Calling it from `Start()`

This violates the Open/Closed Principle.

**Impact:** Every new sample dataset requires modifying this class. Third-party extensions can't register their own sample datasets.

**Recommendation:** Use a registration pattern:
```csharp
[SerializeField] private SampleDatasetDefinition[] datasets;

[Serializable]
public class SampleDatasetDefinition
{
    public string datasetName;
    public bool loadOnStart;
    // ... loader delegate or ScriptableObject reference
}
```

---

---
title: "DataCoreSelfTest.TestSessions uses Thread.Sleep for timing — fragile"
severity: medium
---

**File:** `Runtime/DataCoreSelfTest.cs`
**Method:** `TestSessions()`

```csharp
System.Threading.Thread.Sleep(10); // 确保有一点时间间隔
var cleanupCount = sessionManager.CleanupIdleSessions(TimeSpan.FromMilliseconds(1));
```

**Problem:** The test relies on `Thread.Sleep(10)` to create enough time difference for `CleanupIdleSessions` to detect idle sessions. This is fragile because:
- On heavily loaded systems, 10ms may not elapse as expected
- `Thread.Sleep` blocks the Unity main thread
- The 1ms timeout is very tight

**Impact:** Test may intermittently fail on slow or heavily loaded systems.

**Recommendation:** Use a longer timeout or inject a time provider for testing:
```csharp
// Use a 100ms timeout instead of 1ms
var cleanupCount = sessionManager.CleanupIdleSessions(TimeSpan.FromMilliseconds(100));
```

---

---
title: "DataCoreEditorComponent uses Destroy(this) instead of Destroy(gameObject)"
severity: medium
---

**File:** `Runtime/DataCoreEditorComponent.cs`
**Method:** `Awake()`

```csharp
if (Instance != null && Instance != this)
{
    Debug.LogWarning($"Multiple DataCoreEditorComponent instances detected. Destroying duplicate on '{gameObject.name}'.");
    Destroy(this);  // ← Only destroys the component, not the GameObject
    return;
}
```

**Problem:** `Destroy(this)` destroys only the `DataCoreEditorComponent` script, leaving the GameObject in the scene with a potentially orphaned configuration. If the user added the component as part of a prefab, the remaining GameObject may cause confusion.

**Impact:** Minor — the duplicate is still neutralized, but the empty GameObject remains.

**Recommendation:** Consider `Destroy(gameObject)` if the GameObject's sole purpose is to host this component. Or use `[DisallowMultipleComponent]` to prevent the situation entirely.

---

---
title: "DataAnalysisWorkflowExample catches NotImplementedException for PersistDataset"
severity: medium
---

**File:** `Runtime/Examples/DataAnalysisWorkflowExample.cs`

```csharp
try
{
    session.PersistDataset("HighValueCustomers", "Final_HighValueCustomers");
    session.PersistDataset("SalesByRegion", "Final_SalesByRegion");
}
catch (NotImplementedException)
{
    Console.WriteLine("持久化功能需要实现");
}
```

**Problem:** The example demonstrates a feature (`PersistDataset`) that throws `NotImplementedException`. Catching it and printing a message doesn't help the user — it just shows that the API is incomplete.

**Impact:** Users will try to use `PersistDataset()` and get confused when it doesn't work. The catch block masks the error.

**Recommendation:** Either implement `PersistDataset()` or remove it from the example. If it's a planned feature, mark it with `[Obsolete]` or document it as "not yet implemented" in the XML comments.

---

---
title: "CaliforniaHousingExample uses FindFirstObjectByType — Unity 2023+ API"
severity: medium
---

**File:** `Runtime/SampleDatasets/CaliforniaHousingExample.cs`

```csharp
loader = FindFirstObjectByType<CaliforniaHousingLoader>();
```

**Problem:** `FindFirstObjectByType<T>()` was introduced in Unity 2023.1. Earlier versions use `FindObjectOfType<T>()`. If the project targets Unity 2021 or 2022 LTS, this will not compile.

**Impact:** Compilation error on Unity versions before 2023.1.

**Recommendation:** Use `FindObjectOfType<CaliforniaHousingLoader>()` for backwards compatibility, or add a version check:
```csharp
#if UNITY_2023_1_OR_NEWER
    loader = FindFirstObjectByType<CaliforniaHousingLoader>();
#else
    loader = FindObjectOfType<CaliforniaHousingLoader>();
#endif
```

---

---
title: "Multiple example classes create stores without cleanup — resource leak"
severity: medium
---

**Files:**
- `Runtime/Examples/DataCoreUsageExample.cs`
- `Runtime/Examples/EventListenerExample.cs`
- `Runtime/Examples/MultiUserSessionExample.cs`
- `Runtime/Examples/SessionExample.cs`
- `Runtime/Examples/SessionLifecycleExample.cs`

**Problem:** Several examples create `DataCoreStore` with `new DataCoreStore()` (default path) and don't always dispose it:

```csharp
// MultiUserSessionExample.cs
var store = new DataCoreStore();
// ... operations ...
store.Dispose();  // ← This one is fine

// EventListenerExample.cs
var store = new DataCoreStore();
// ... operations ...
// No explicit Dispose() — relies on GC
```

**Impact:** LiteDB file handles may not be released promptly. On Windows, this can prevent file deletion or cause sharing violations on the next run.

**Recommendation:** Always use `using var store = new DataCoreStore()` to ensure deterministic disposal.

---

---
title: "CaliforniaHousingLoader.RunSampleQueries uses QueryOp directly instead of fluent API"
severity: low
---

**File:** `Runtime/SampleDatasets/CaliforniaHousingLoader.cs`

```csharp
var highValueIndices = housingData.Where("median_house_value", QueryOp.Gt, 500000);
```

**Problem:** This uses the lower-level `Where()` method with `QueryOp` enum, while `CaliforniaHousingExample` and other examples demonstrate the fluent API:

```csharp
var results = housingData.Query()
    .WhereGreaterThan("median_house_value", 400000)
    .ToDictionaries()
    .ToList();
```

**Impact:** Inconsistent API usage across examples may confuse users about which approach to use.

**Recommendation:** Use the fluent API consistently across all examples, or document when to use each approach.

---

---
title: "Fallback dataset is only 10 rows — insufficient for meaningful analysis"
severity: low
---

**File:** `Runtime/SampleDatasets/CaliforniaHousingDataset.cs`
**Method:** `GetFallbackData()`

```csharp
private static Dictionary<string, double[]> GetFallbackData()
{
    return new Dictionary<string, double[]>
    {
        ["longitude"] = new double[] { -122.23, -122.22, -122.24, -118.30, -118.31, -117.81, -117.82, -119.67, -119.56, -121.43 },
        // ... 10 rows total
    };
}
```

**Problem:** The fallback dataset has only 10 rows. This is too small for meaningful statistical analysis — `Mean`, `Min`, `Max` are not representative. Queries like "find houses with median value > $500,000" return 0 or 1 results.

**Impact:** Users who don't bundle the CSV file get a poor demo experience.

**Recommendation:** Increase to at least 100 rows, or document that the fallback is minimal and recommend bundling the full CSV.

---

---
title: "Unused using directive in CaliforniaHousingExample"
severity: low
---

**File:** `Runtime/SampleDatasets/CaliforniaHousingExample.cs`

```csharp
using System.Linq;
```

**Problem:** `System.Linq` is imported but only used for `.ToList()` which could be avoided by using `Query().ToDictionaries()` directly (which may already return a list).

**Impact:** Minor code cleanliness issue. May cause compiler warnings in strict configurations.

**Recommendation:** Remove if not needed, or add a comment explaining why it's there.

---

---
title: "Namespace inconsistency between SampleDatasetManager and dataset classes"
severity: low
---

**Files:**
- `Runtime/SampleDatasets/SampleDatasetManager.cs` → `namespace AroAro.DataCore`
- `Runtime/SampleDatasets/CaliforniaHousing*.cs` → `namespace AroAro.DataCore.SampleDatasets`

**Problem:** `SampleDatasetManager` lives in `AroAro.DataCore` while the dataset classes it manages are in `AroAro.DataCore.SampleDatasets`. This requires an explicit `using AroAro.DataCore.SampleDatasets;` in the manager.

**Impact:** Minor organizational inconsistency. Users looking at the namespace hierarchy may expect all sample dataset code to be in the same namespace.

**Recommendation:** Move `SampleDatasetManager` to `AroAro.DataCore.SampleDatasets` namespace for consistency.

---

---
title: "DataCoreSelfTest log output is all-or-nothing — no granular control"
severity: low
---

**File:** `Runtime/DataCoreSelfTest.cs`

```csharp
[SerializeField] private bool logToConsole = true;
```

**Problem:** The entire test output (all 4 test sections) is controlled by a single boolean. There's no way to run just the tabular tests or just the algorithm tests.

**Impact:** Minor — users who want to debug a specific test area must read through all output.

**Recommendation:** Add per-test toggles:
```csharp
[SerializeField] private bool testTabular = true;
[SerializeField] private bool testGraph = true;
[SerializeField] private bool testSessions = true;
[SerializeField] private bool testAlgorithms = true;
```
