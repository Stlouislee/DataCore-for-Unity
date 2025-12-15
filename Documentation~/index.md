# DataCore

## Datasets

- **Tabular**: column-oriented, numeric columns backed by NumSharp `NDArray`.
- **Graph**: node/edge graph with simple queries.

## Persistence

- Tabular: Apache Arrow IPC (`.arrow`)
- Graph: DataCore graph text (`.dcgraph`, line-based)

The default file backend writes under `Application.persistentDataPath`.
