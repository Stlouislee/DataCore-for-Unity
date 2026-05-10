# DataCore-for-Unity — severity:medium Fix Plan

Branch: `fix/medium-severity-batch`
Total: 30 issues

## Batch 1 (dispatched)
- [ ] #124 — MinMaxNormalize throws exceptions in ExecuteTabular instead of returning validation errors
- [ ] #122 — Pipeline does not propagate OutputName from base context to step contexts
- [ ] #120 — AlgorithmContext.Parameters allows mutation via unsafe cast
- [ ] #86 — FindFirstObjectByType requires Unity 2023+ — add version guard
- [ ] #81 — Destroy(this) leaves orphaned GameObject

## Batch 2 (pending)
- [ ] #116 — ClearAllSubscriptions — add scoped subscription support
- [ ] #115 — File importers use system default encoding — no BOM/encoding parameter
- [ ] #95 — No teardown/cleanup in MonoBehaviour tests
- [ ] #91 — GraphMLImportTest creates .db files in working directory
- [ ] #90 — Inconsistent store disposal in Examples

## Batch 3 (pending)
- [ ] #87 — Mobile Direct mode has no concurrency protection
- [ ] #83 — Constructor uses fragile GC.Collect + Thread.Sleep
- [ ] #84 — Example demonstrates unimplemented PersistDataset
- [ ] #78 — SelfTest.TestSessions uses fragile Thread.Sleep(10)
- [ ] #77 — Metadata batch updates can lose up to 99 changes on crash

## Batch 4 (pending)
- [ ] #76 — Make SampleDatasetManager extensible via registry pattern
- [ ] #75 — GraphMLImportTest uses fragile relative path
- [ ] #74 — Cache parsed CSV data in CaliforniaHousingDataset
- [ ] #73 — Consolidate GroupBy tests into single file
- [ ] #70 — Event subscriptions leak on test failure

## Batch 5 (pending)
- [ ] #68 — DeleteRow triggers O(N) individual updates
- [ ] #64 — GraphDatasetTest uses unseeded Random
- [ ] #53 — AddRow numeric type detection misses decimal, short, byte
- [ ] #51 — ToDictionaries ordering uses untyped comparison
- [ ] #47 — GetStringColumn exposes mutable internal array reference

## Batch 6 (pending)
- [ ] #41 — ImportFromCsv does not handle quoted fields
- [ ] #39 — SessionManager lazy initialization is not thread-safe
- [ ] #36 — Add async variants for I/O-heavy operations
- [ ] #34 — CompareNumeric throws FormatException on non-numeric values
