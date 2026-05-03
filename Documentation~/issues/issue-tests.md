# Tests Module — Design Issues

---

```
---
title: "11 of 12 test files use MonoBehaviour instead of NUnit"
severity: high
---
```

The vast majority of tests inherit from `MonoBehaviour` and use a custom `Assert()` helper that throws generic `Exception`. Only `Runtime/DataCoreSmokeTests.cs` uses NUnit.

**Impact**: Tests cannot run in CI/CD pipelines, cannot be discovered by test runners, and require a Unity Editor with scene setup. This makes regression detection manual and unreliable.

**Files affected**: All files except `Runtime/DataCoreSmokeTests.cs`

**Recommendation**: Migrate all tests to NUnit `[Test]` methods. Replace custom `Assert(bool, string)` with `Assert.IsTrue`/`Assert.AreEqual`. Use `[UnityTest]` for tests requiring coroutine/frame timing.

---

```
---
title: "Tests with no assertions cannot detect failures"
severity: high
---
```

Several test methods contain zero assertions — they only call `Debug.Log()`. If the underlying API changes behavior or returns wrong results, these tests will silently pass.

**Affected methods**:
- `DataFrameQuickTest.RunQuickTest()` — no assertions at all
- `GraphDatasetTest.RunGraphTest()` — logs stats but never asserts
- `LazyLoadingTest.TestLiteDbBackend()` — logs but doesn't assert values
- `GraphMLImportTest.RunGraphMLImportTest()` — logs property values but doesn't assert specific content
- `GroupByTest.TestGroupByAPI()` — logs aggregation results without checking values
- `DataFrameGroupByTest.TestGroupByFunctionality()` — logs group counts without asserting expected values

**Recommendation**: Add explicit assertions for all expected outcomes. At minimum, assert row counts, property values, and aggregation results.

---

```
---
title: "Non-reproducible tests due to unseeded Random"
severity: medium
---
```

`GraphDatasetTest` uses `UnityEngine.Random.Range` extensively (50 user nodes, random edges, random properties, random DNA sequences) without setting a seed. This means:
- Test failures are non-reproducible
- Flaky test behavior cannot be debugged
- Graph topology varies between runs

**File**: `Tests/GraphDatasetTest.cs` — all `Create*` methods

**Recommendation**: Set `Random.InitState(seed)` at the start of each test method with a fixed seed, or inject an `IRandom` interface for deterministic testing.

---

```
---
title: "MockSession has inconsistent data storage"
severity: medium
---
```

`DataFrameGroupByTest.MockSession` stores DataFrames in `_dataFrames` dictionary but `GetDataset()` reads from `_datasets` dictionary. The `AddDataset()` method only populates `_dataFrames`, so `GetDataset()` will always throw `KeyNotFoundException` for datasets added via `AddDataset()`.

**File**: `Tests/DataFrameGroupByTest.cs`, lines ~100-140

```csharp
public void AddDataset(string name, DataFrame df)
{
    _dataFrames[name] = df;  // writes to _dataFrames
}

public IDataSet GetDataset(string name) => 
    _datasets.TryGetValue(name, out var ds) ? ds : throw new KeyNotFoundException();
    // reads from _datasets — always fails for AddDataset entries
```

**Recommendation**: Unify storage or implement `GetDataset` to check both dictionaries. This mock may be hiding bugs in the QueryBuilder tests.

---

```
---
title: "Event subscriptions not always cleaned up"
severity: medium
---
```

`AlgorithmTest.TestEventsFireCorrectly()` properly calls `DataCoreEventManager.ClearAllSubscriptions()` at the end. However, if the test throws before reaching cleanup, subscriptions leak.

**File**: `Tests/AlgorithmTest.cs`, `TestEventsFireCorrectly()`

Similarly, `SessionTests.TestSessionEvents()` calls `ClearAllSubscriptions()` but only after the assertion — if the test fails mid-way, subscriptions persist.

**Recommendation**: Use try/finally blocks for event cleanup, or add `[TearDown]` that always clears subscriptions.

---

```
---
title: "GroupBy functionality tested in 3 separate files with overlap"
severity: medium
---
```

GroupBy is tested in:
1. `GroupByTest.cs` — API existence check via reflection
2. `DataFrameGroupByTest.cs` — Direct GroupBy + SessionDataFrameQueryBuilder
3. `DataFrameIntegrationTest.cs` — DataFrame query with Where (not GroupBy, but related)

`GroupByTest` and `DataFrameGroupByTest` have significant overlap — both test `df.GroupBy(column).Sum/Mean/Count`. Neither asserts aggregated values.

**Recommendation**: Consolidate into a single, comprehensive GroupBy test file with proper assertions on computed values.

---

```
---
title: "DataFrameQuickTest is fully redundant"
severity: low
---
```

`DataFrameQuickTest.RunQuickTest()` tests the exact same flow as `DataFrameIntegrationTest` (create DataFrame, query with Where, cast to adapter, convert to TabularData) but with no assertions. It adds zero coverage.

**File**: `Tests/DataFrameQuickTest.cs`

**Recommendation**: Remove this file or convert it to a performance benchmark with timing assertions.

---

```
---
title: "Hardcoded relative file path in GraphMLImportTest"
severity: medium
---
```

`GraphMLImportTest` uses `graphmlFilePath = "TestGraphML.graphml"` as a serialized field default. This path is relative to the working directory, which varies depending on how Unity is launched. The test will fail silently if the file isn't found.

**File**: `Tests/GraphMLImportTest.cs`, line ~15

**Recommendation**: Use `Application.dataPath` or `Path.Combine(Application.dataPath, "Tests", "TestGraphML.graphml")` for a reliable path. Alternatively, embed the test file as a TextAsset.

---

```
---
title: "No test isolation — tests depend on scene objects"
severity: high
---
```

`GraphDatasetTest` and `LazyLoadingTest` call `FindFirstObjectByType<DataCoreEditorComponent>()` in `Start()`. If no `DataCoreEditorComponent` exists in the scene, the test logs an error and returns — but doesn't fail the test runner.

**Files**: `Tests/GraphDatasetTest.cs`, `Tests/LazyLoadingTest.cs`

**Recommendation**: Tests should create their own dependencies (e.g., `new DataCoreStore()`) rather than depending on scene objects. This makes tests portable and CI-compatible.

---

```
---
title: "Custom Assert helper throws generic Exception instead of AssertionException"
severity: low
---
```

Both `AlgorithmTest` and `LiteDbTabularTest` define their own `Assert(bool, string)` that throws `new Exception(...)`. This means:
- Test runners can't distinguish assertion failures from unexpected exceptions
- Stack traces are less informative
- No support for collection assertions, approximate equality, etc.

**Files**: `Tests/AlgorithmTest.cs` (line ~280), `Tests/LiteDbTabularTest.cs` (line ~260)

**Recommendation**: Use NUnit's `Assert` class or, if staying MonoBehaviour-based, throw a custom `AssertionException` type.

---

```
---
title: "LiteDbTabularTest uses Console.WriteLine instead of Debug.Log"
severity: low
---
```

`LiteDbTabularTest` is a static class that writes results to `Console.WriteLine`. In Unity, `Console.WriteLine` doesn't appear in the Unity Console — only `Debug.Log` does. Test results are invisible in the Editor.

**File**: `Tests/LiteDbTabularTest.cs`, `RunAllTests()` method

**Recommendation**: Use `Debug.Log` or convert to NUnit tests that output to the Test Runner window.

---

```
---
title: "SessionTests.TestSessionLifecycle uses fragile Thread.Sleep for timing"
severity: low
---
```

The idle cleanup test sleeps for 10ms then checks if `TimeSpan.FromMilliseconds(1)` catches the idle session. On slow CI machines or under load, this could be flaky.

**File**: `Tests/SessionTests.cs`, `TestSessionLifecycle()` method

**Recommendation**: Use a larger, more forgiving timeout (e.g., 500ms idle threshold with 100ms sleep), or mock the time source.

---

```
---
title: "No test for concurrent/parallel access patterns"
severity: high
---
```

The entire test suite has zero concurrency tests. `DataCoreStore`, sessions, and datasets may be accessed from multiple threads in production (Unity main thread + background workers). No tests verify:
- Thread-safe read/write on datasets
- Concurrent session creation/destruction
- Parallel algorithm execution
- Lock contention under load

**Recommendation**: Add stress tests using `Task.WhenAll` or `Parallel.For` that exercise shared state from multiple threads.

---

```
---
title: "No test for null, NaN, or empty data handling"
severity: high
---
```

No test in the suite exercises:
- Null values in string columns
- NaN or Infinity in numeric columns
- Empty DataFrames (0 rows)
- Empty graphs (0 nodes, 0 edges with properties)
- Whitespace-only strings
- Very long strings

These are common edge cases that can cause crashes or silent data corruption.

**Recommendation**: Add a dedicated edge-case test class covering null/NaN/empty/boundary inputs for all dataset types.

---

```
---
title: "GraphDatasetTest method name typo: CreateCaffeineaMolecule"
severity: low
---
```

The method is named `CreateCaffeineaMolecule` — should be `CreateCaffeineMolecule` (extra 'a').

**File**: `Tests/GraphDatasetTest.cs`, line ~340

---

```
---
title: "Test database files may leak on disk"
severity: medium
---
```

`GraphMLImportTest` creates `graphml_test.db` and `graphml_text_test.db` in the current working directory. While it cleans up at the start of each run, if the test crashes mid-execution, orphaned `.db` files remain. `LiteDbTabularTest` uses temp directory which is better.

**Files**: `Tests/GraphMLImportTest.cs`

**Recommendation**: Use `Path.GetTempPath()` for all test database files, or use in-memory stores for tests that don't need persistence verification.

---

```
---
title: "No teardown/cleanup in MonoBehaviour test classes"
severity: medium
---
```

Most MonoBehaviour tests create `DataCoreStore` instances, sessions, and datasets but never dispose or clean them up. While Unity's garbage collector will eventually collect them, this can:
- Leave LiteDB files locked
- Cause test interference if multiple tests run in sequence
- Leak event subscriptions

**Files**: `DataFrameGroupByTest.cs`, `DataFrameIntegrationTest.cs`, `DataFrameQuickTest.cs`, `SessionSmokeTest.cs`, `SessionTests.cs`

**Recommendation**: Add `using` blocks or explicit `Dispose()` calls for all store/session instances.
