# Workspace API Design — Agent-Facing Tool Surface

**日期** 2026-05-12  
**分支** `feature/workspace`  
**PR** #165  
**状态** 设计稿 → 实现中

---

## 一、核心理念

### Workspace 是 Agent 的操作台

Agent 不关心数据存在 LiteDB 还是内存里。它的工作场景就是：

```
open Users on workspace
filter workspace.Users where age > 18 as adults
join adults with Orders on workspace using key user_id
describe workspace
save adults to store
```

**Store 是后台仓库，Workspace 是前台桌面。**

### 设计原则

| 原则 | 说明 |
|------|------|
| **Flat > 嵌套** | 每个 tool 是一个平的函数调用，参数即意图 |
| **命令式 > 链式** | Agent 每次调用是独立的 function call，不依赖中间对象 |
| **自描述返回** | 每个操作返回结构化 JSON，Agent 直接能理解 |
| **幂等安全** | 重复调用同一操作结果一致，Agent 可以安全重试 |
| **错误可恢复** | 错误带 suggestion，Agent 可以自动纠正 |

### Get() 语义决策

**采用复制语义**：`Open()` 从 store 加载到 workspace 时做副本，隔离安全。需要持久化时显式 `Save()`。

理由：
- Workspace 是"桌面"不是"沙箱引用"
- 避免 Agent 无意中修改 store 原始数据
- `Clone()` 已提供显式复制能力
- 性能敏感场景可通过 `OpenRef()` 显式 opt-in 零拷贝

---

## 二、架构

```
┌─────────────────────────────────────────────┐
│  AI Agent                                    │
│  function_call: workspace_filter(...)        │
└──────────────┬──────────────────────────────┘
               │
┌──────────────▼──────────────────────────────┐
│  DataCoreTools (Dispatch Layer)              │
│  - 静态方法，参数全基本类型                    │
│  - 路由到 IWorkspace 实例方法                  │
│  - 统一返回 JSON 字符串                       │
│  - GetToolSchemas() 暴露给 Agent 框架         │
└──────────────┬──────────────────────────────┘
               │
┌──────────────▼──────────────────────────────┐
│  IWorkspace (Workspace 实现)                 │
│  - 数据操作的内部逻辑                          │
│  - 返回结构化结果对象                          │
└──────────────┬──────────────────────────────┘
               │
┌──────────────▼──────────────────────────────┐
│  DataCoreStore (持久层)                       │
│  - LiteDB 后端                               │
│  - 不暴露给 Agent                             │
└─────────────────────────────────────────────┘
```

### 多 Workspace 支持

```csharp
// Store 管理 workspace 集合
store.CreateWorkspace("analysis")       // 创建
store.DestroyWorkspace("analysis")      // 销毁
store.ListWorkspaces()                  // 列举
store.Workspace["analysis"]            // 索引

// 默认 workspace（向后兼容）
store.Workspace                        // 等价于 store.Workspace["default"]
```

---

## 三、Tool Surface（完整清单）

### 3.1 Workspace 管理（3 个）

#### `workspace_create`

创建一个新的 workspace。

```json
{
  "name": "workspace_create",
  "parameters": {
    "name": { "type": "string", "description": "Workspace name" }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_create",
  "result": {
    "name": "analysis",
    "created": true
  }
}
```

#### `workspace_destroy`

销毁一个 workspace 及其所有 derived 数据。

```json
{
  "name": "workspace_destroy",
  "parameters": {
    "name": { "type": "string", "description": "Workspace name" }
  }
}
```

#### `workspace_list`

列出所有 workspace。

```json
{
  "name": "workspace_list",
  "parameters": {}
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_list",
  "result": {
    "workspaces": [
      { "name": "default", "datasetCount": 3, "totalRows": 5000 },
      { "name": "analysis", "datasetCount": 1, "totalRows": 420 }
    ]
  }
}
```

---

### 3.2 数据加载（3 个）

#### `workspace_open`

从 store 加载数据集到 workspace（复制语义）。

```json
{
  "name": "workspace_open",
  "parameters": {
    "workspace": { "type": "string", "description": "Workspace name" },
    "dataset": { "type": "string", "description": "Dataset name in store" }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_open",
  "result": {
    "name": "Users",
    "rows": 1500,
    "columns": 8,
    "columnNames": ["id", "name", "age", "email", "city", "score", "created", "status"],
    "source": "Store",
    "sample": [
      { "id": 1, "name": "Alice", "age": 25, "email": "alice@example.com", "city": "Shanghai", "score": 88.5, "created": "2024-01-15", "status": "active" },
      { "id": 2, "name": "Bob", "age": 30, "email": "bob@example.com", "city": "Beijing", "score": 92.0, "created": "2024-02-20", "status": "active" }
    ]
  }
}
```

#### `workspace_open_ref`

从 store 加载数据集到 workspace（零拷贝引用语义，性能优先）。

```json
{
  "name": "workspace_open_ref",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" }
  }
}
```

#### `workspace_import_csv`

从 CSV 内容导入数据到 workspace。

```json
{
  "name": "workspace_import_csv",
  "parameters": {
    "workspace": { "type": "string" },
    "csv": { "type": "string", "description": "CSV content" },
    "name": { "type": "string", "description": "Name for the imported dataset" },
    "hasHeader": { "type": "boolean", "default": true },
    "delimiter": { "type": "string", "default": "," }
  }
}
```

---

### 3.3 数据检视（4 个）

#### `workspace_describe`

描述 workspace 中的数据集（单个或全部）。

```json
{
  "name": "workspace_describe",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string", "description": "Optional. Omit to describe all." }
  }
}
```

**返回（单个）**：
```json
{
  "success": true,
  "action": "workspace_describe",
  "result": {
    "name": "adults",
    "kind": "Tabular",
    "source": "Derived",
    "rows": 420,
    "columns": 8,
    "columnNames": ["id", "name", "age", "email", "city", "score", "created", "status"],
    "schema": [
      { "name": "id", "type": "Numeric", "nullable": false },
      { "name": "name", "type": "String", "nullable": false },
      { "name": "age", "type": "Numeric", "nullable": false }
    ],
    "sample": [...],
    "derivedFrom": "Users",
    "filter": "age > 18"
  }
}
```

**返回（全部）**：
```json
{
  "success": true,
  "action": "workspace_describe",
  "result": {
    "workspace": "analysis",
    "summary": "3 datasets: 1 store, 2 derived",
    "datasets": [
      { "name": "Users", "rows": 1500, "columns": 8, "source": "Store" },
      { "name": "adults", "rows": 420, "columns": 8, "source": "Derived" },
      { "name": "Orders", "rows": 8200, "columns": 5, "source": "Store" }
    ]
  }
}
```

#### `workspace_sample`

查看数据集的样例行。

```json
{
  "name": "workspace_sample",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" },
    "rows": { "type": "integer", "default": 5, "description": "Number of rows to sample" },
    "offset": { "type": "integer", "default": 0, "description": "Starting row index" }
  }
}
```

#### `workspace_schema`

获取数据集的列 schema 详情。

```json
{
  "name": "workspace_schema",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_schema",
  "result": {
    "dataset": "Users",
    "columns": [
      { "name": "id", "type": "Numeric", "nullable": false, "unique": true, "sample": [1, 2, 3] },
      { "name": "name", "type": "String", "nullable": false, "unique": false, "sample": ["Alice", "Bob", "Charlie"] },
      { "name": "age", "type": "Numeric", "nullable": false, "unique": false, "sample": [25, 30, 35] }
    ],
    "totalColumns": 8,
    "totalRows": 1500
  }
}
```

#### `workspace_statistics`

获取数据集的统计摘要。

```json
{
  "name": "workspace_statistics",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" },
    "columns": { "type": "array", "items": "string", "description": "Optional. Specific columns to analyze." }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_statistics",
  "result": {
    "dataset": "Users",
    "rows": 1500,
    "columns": {
      "age": { "count": 1500, "nulls": 0, "min": 18, "max": 65, "mean": 35.2, "std": 12.1, "distinct": 48 },
      "score": { "count": 1480, "nulls": 20, "min": 0, "max": 100, "mean": 75.8, "std": 15.3, "distinct": 200 },
      "city": { "count": 1500, "nulls": 0, "distinct": 12, "top": ["Shanghai", "Beijing", "Guangzhou"] }
    }
  }
}
```

---

### 3.4 数据变换（9 个）

#### `workspace_filter`

过滤数据集，结果留在 workspace。

```json
{
  "name": "workspace_filter",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string", "description": "Source dataset name" },
    "filter": { "type": "string", "description": "Filter expression, e.g. 'age > 18 AND city == Shanghai'" },
    "resultName": { "type": "string", "description": "Name for the result dataset" }
  }
}
```

**Filter 语法**：
- 比较：`age > 18`, `score >= 90`, `name == Alice`, `status != inactive`
- 逻辑：`AND`, `OR`, `NOT`
- 字符串：`city contains Shang`, `name starts with A`
- 空值：`email is null`, `email is not null`
- 范围：`age between 18 35`
- 集合：`city in Shanghai Beijing Guangzhou`

**返回**：
```json
{
  "success": true,
  "action": "workspace_filter",
  "result": {
    "name": "adults",
    "rows": 420,
    "columns": 8,
    "columnNames": ["id", "name", "age", ...],
    "source": "Users",
    "filter": "age > 18",
    "sample": [...]
  }
}
```

#### `workspace_select`

选择特定列。

```json
{
  "name": "workspace_select",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "columns": { "type": "array", "items": "string" },
    "resultName": { "type": "string" }
  }
}
```

#### `workspace_rename_columns`

重命名列。

```json
{
  "name": "workspace_rename_columns",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "mapping": { "type": "object", "description": "Old name → New name mapping, e.g. {\"id\": \"user_id\"}" },
    "resultName": { "type": "string" }
  }
}
```

#### `workspace_sort`

排序。

```json
{
  "name": "workspace_sort",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "by": { "type": "string", "description": "Column to sort by" },
    "order": { "type": "string", "enum": ["asc", "desc"], "default": "asc" },
    "resultName": { "type": "string" }
  }
}
```

#### `workspace_distinct`

去重。

```json
{
  "name": "workspace_distinct",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "columns": { "type": "array", "items": "string", "description": "Optional. Columns to consider for dedup. Omit for all columns." },
    "resultName": { "type": "string" }
  }
}
```

#### `workspace_add_column`

新增计算列。

```json
{
  "name": "workspace_add_column",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "columnName": { "type": "string" },
    "expression": { "type": "string", "description": "Expression, e.g. 'age >= 18 ? adult : minor' or 'score * 1.1'" },
    "resultName": { "type": "string" }
  }
}
```

#### `workspace_drop_columns`

删除列。

```json
{
  "name": "workspace_drop_columns",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "columns": { "type": "array", "items": "string" },
    "resultName": { "type": "string" }
  }
}
```

#### `workspace_limit`

限制行数。

```json
{
  "name": "workspace_limit",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "count": { "type": "integer" },
    "offset": { "type": "integer", "default": 0 },
    "resultName": { "type": "string" }
  }
}
```

#### `workspace_random_sample`

随机采样。

```json
{
  "name": "workspace_random_sample",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "count": { "type": "integer" },
    "seed": { "type": "integer", "description": "Optional. Random seed for reproducibility." },
    "resultName": { "type": "string" }
  }
}
```

---

### 3.5 数据组合（3 个）

#### `workspace_join`

Join 两个数据集。

```json
{
  "name": "workspace_join",
  "parameters": {
    "workspace": { "type": "string" },
    "left": { "type": "string" },
    "right": { "type": "string" },
    "key": { "type": "string", "description": "Join key column name (must exist in both)" },
    "joinType": { "type": "string", "enum": ["inner", "left", "right", "full"], "default": "inner" },
    "resultName": { "type": "string" }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_join",
  "result": {
    "name": "user_orders",
    "rows": 7800,
    "columns": 12,
    "leftRows": 1500,
    "rightRows": 8200,
    "matchedRows": 7800,
    "unmatchedLeft": 50,
    "unmatchedRight": 400,
    "joinKey": "user_id",
    "joinType": "inner",
    "columnNames": ["user_id", "name", "age", ..., "order_id", "amount", "date"],
    "sample": [...]
  }
}
```

#### `workspace_union`

上下拼接（列名对齐）。

```json
{
  "name": "workspace_union",
  "parameters": {
    "workspace": { "type": "string" },
    "sources": { "type": "array", "items": "string", "description": "Dataset names to union" },
    "resultName": { "type": "string" }
  }
}
```

#### `workspace_cross`

Cross join（笛卡尔积）。

```json
{
  "name": "workspace_cross",
  "parameters": {
    "workspace": { "type": "string" },
    "left": { "type": "string" },
    "right": { "type": "string" },
    "resultName": { "type": "string" }
  }
}
```

---

### 3.6 聚合（3 个）

#### `workspace_aggregate`

Group by + 聚合。

```json
{
  "name": "workspace_aggregate",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "groupBy": { "type": "string", "description": "Column to group by" },
    "aggregations": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "column": { "type": "string" },
          "op": { "type": "string", "enum": ["count", "sum", "mean", "min", "max", "std"] },
          "alias": { "type": "string" }
        }
      }
    },
    "resultName": { "type": "string" }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_aggregate",
  "result": {
    "name": "city_stats",
    "rows": 12,
    "columns": 3,
    "groupBy": "city",
    "aggregations": ["avg_age", "user_count"],
    "columnNames": ["city", "avg_age", "user_count"],
    "sample": [
      { "city": "Shanghai", "avg_age": 32.5, "user_count": 450 },
      { "city": "Beijing", "avg_age": 35.1, "user_count": 380 }
    ]
  }
}
```

#### `workspace_summarize`

全局聚合（不 group by）。

```json
{
  "name": "workspace_summarize",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "aggregations": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "column": { "type": "string" },
          "op": { "type": "string", "enum": ["count", "sum", "mean", "min", "max", "std"] },
          "alias": { "type": "string" }
        }
      }
    },
    "resultName": { "type": "string" }
  }
}
```

#### `workspace_count`

快速计数（可带 filter）。

```json
{
  "name": "workspace_count",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "filter": { "type": "string", "description": "Optional filter expression" }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_count",
  "result": {
    "dataset": "Users",
    "filter": "age > 18",
    "count": 420
  }
}
```

---

### 3.7 持久化（3 个）

#### `workspace_save`

将 workspace 数据集推回 store。

```json
{
  "name": "workspace_save",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" },
    "newName": { "type": "string", "description": "Optional. Save with a different name in store." }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_save",
  "result": {
    "name": "adults",
    "savedTo": "adults",
    "rows": 420,
    "columns": 8,
    "sourceChanged": "Derived → Store"
  }
}
```

#### `workspace_export_csv`

导出为 CSV 字符串。

```json
{
  "name": "workspace_export_csv",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" },
    "delimiter": { "type": "string", "default": "," },
    "includeHeader": { "type": "boolean", "default": true }
  }
}
```

#### `workspace_export_json`

导出为 JSON 字符串。

```json
{
  "name": "workspace_export_json",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" },
    "format": { "type": "string", "enum": ["records", "columns", "values"], "default": "records" }
  }
}
```

---

### 3.8 数据修改（4 个）

#### `workspace_update`

更新满足条件的行。

```json
{
  "name": "workspace_update",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" },
    "filter": { "type": "string", "description": "Filter for rows to update" },
    "set": { "type": "object", "description": "Column → Value mapping, e.g. {\"status\": \"archived\"}" }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_update",
  "result": {
    "dataset": "Users",
    "filter": "status == inactive",
    "updatedRows": 35,
    "columns": ["status"],
    "newValues": { "status": "archived" }
  }
}
```

#### `workspace_delete_rows`

删除满足条件的行。

```json
{
  "name": "workspace_delete_rows",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" },
    "filter": { "type": "string" }
  }
}
```

#### `workspace_append`

追加行到数据集。

```json
{
  "name": "workspace_append",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" },
    "rows": { "type": "array", "items": "object" }
  }
}
```

#### `workspace_clear`

清空数据集（保留结构）。

```json
{
  "name": "workspace_clear",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" }
  }
}
```

---

### 3.9 图数据（5 个）

#### `workspace_open_graph`

从 store 加载图数据集到 workspace。

```json
{
  "name": "workspace_open_graph",
  "parameters": {
    "workspace": { "type": "string" },
    "graph": { "type": "string", "description": "Graph dataset name in store" }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_open_graph",
  "result": {
    "name": "social",
    "nodes": 1500,
    "edges": 4200,
    "nodeTypes": ["player", "guild", "item"],
    "edgeTypes": ["follows", "member_of", "owns"],
    "source": "Store"
  }
}
```

#### `workspace_add_nodes`

向图数据集添加节点。

```json
{
  "name": "workspace_add_nodes",
  "parameters": {
    "workspace": { "type": "string" },
    "graph": { "type": "string" },
    "nodes": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "id": { "type": "string" },
          "properties": { "type": "object" }
        }
      }
    }
  }
}
```

#### `workspace_add_edges`

向图数据集添加边。

```json
{
  "name": "workspace_add_edges",
  "parameters": {
    "workspace": { "type": "string" },
    "graph": { "type": "string" },
    "edges": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "from": { "type": "string" },
          "to": { "type": "string" },
          "properties": { "type": "object" }
        }
      }
    }
  }
}
```

#### `workspace_graph_neighbors`

查询节点的邻居。

```json
{
  "name": "workspace_graph_neighbors",
  "parameters": {
    "workspace": { "type": "string" },
    "graph": { "type": "string" },
    "nodeId": { "type": "string" },
    "direction": { "type": "string", "enum": ["out", "in", "both"], "default": "both" },
    "depth": { "type": "integer", "default": 1, "description": "Traversal depth (1 = direct neighbors)" }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_graph_neighbors",
  "result": {
    "graph": "social",
    "centerNode": "player_42",
    "direction": "both",
    "depth": 2,
    "neighbors": [
      { "id": "player_10", "depth": 1, "properties": { "name": "Alice", "level": 25 } },
      { "id": "player_99", "depth": 1, "properties": { "name": "Bob", "level": 30 } },
      { "id": "guild_1", "depth": 1, "properties": { "name": "Dragon Slayers" } },
      { "id": "player_200", "depth": 2, "properties": { "name": "Charlie", "level": 15 } }
    ],
    "totalNeighbors": 4
  }
}
```

#### `workspace_describe_graph`

描述图数据集。

```json
{
  "name": "workspace_describe_graph",
  "parameters": {
    "workspace": { "type": "string" },
    "graph": { "type": "string" }
  }
}
```

---

### 3.10 元操作（5 个）

#### `workspace_clone`

克隆数据集。

```json
{
  "name": "workspace_clone",
  "parameters": {
    "workspace": { "type": "string" },
    "source": { "type": "string" },
    "newName": { "type": "string" }
  }
}
```

#### `workspace_rename_dataset`

重命名 workspace 中的数据集。

```json
{
  "name": "workspace_rename_dataset",
  "parameters": {
    "workspace": { "type": "string" },
    "oldName": { "type": "string" },
    "newName": { "type": "string" }
  }
}
```

#### `workspace_remove`

从 workspace 移除数据集（不影响 store）。

```json
{
  "name": "workspace_remove",
  "parameters": {
    "workspace": { "type": "string" },
    "dataset": { "type": "string" }
  }
}
```

#### `workspace_search`

搜索数据集名。

```json
{
  "name": "workspace_search",
  "parameters": {
    "workspace": { "type": "string" },
    "query": { "type": "string", "description": "Search term (case-insensitive partial match)" }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_search",
  "result": {
    "query": "user",
    "matches": [
      { "name": "Users", "source": "Store", "rows": 1500 },
      { "name": "user_sessions", "source": "Derived", "rows": 3200 },
      { "name": "admin_users", "source": "Imported", "rows": 15 }
    ]
  }
}
```

#### `workspace_diff`

比较两个数据集的差异。

```json
{
  "name": "workspace_diff",
  "parameters": {
    "workspace": { "type": "string" },
    "left": { "type": "string" },
    "right": { "type": "string" }
  }
}
```

**返回**：
```json
{
  "success": true,
  "action": "workspace_diff",
  "result": {
    "left": { "name": "Users", "rows": 1500, "columns": 8 },
    "right": { "name": "Users_v2", "rows": 1600, "columns": 9 },
    "rowDiff": "+100",
    "columnDiff": {
      "onlyInLeft": ["age"],
      "onlyInRight": ["age_group"],
      "common": ["id", "name", "email", "city", "score", "created", "status"]
    },
    "summary": "right has 100 more rows. Schema differs: left has 'age', right has 'age_group'."
  }
}
```

---

## 四、工具注册表

所有 tool 的 schema 通过 `DataCoreTools.GetToolSchemas()` 暴露，供 Agent 框架自动注册。

```csharp
public static class DataCoreTools
{
    private static DataCoreStore _store;

    public static void Initialize(DataCoreStore store) => _store = store;

    public static ToolSchema[] GetToolSchemas() => new ToolSchema[]
    {
        // 3.1 管理
        new("workspace_create", "Create a new workspace", ...),
        new("workspace_destroy", "Destroy a workspace", ...),
        new("workspace_list", "List all workspaces", ...),

        // 3.2 加载
        new("workspace_open", "Open a dataset on workspace (copy semantics)", ...),
        new("workspace_open_ref", "Open a dataset on workspace (zero-copy reference)", ...),
        new("workspace_import_csv", "Import CSV data to workspace", ...),

        // ... 共 35 个
    };

    // 路由：按 function name 分发
    public static string Execute(string toolName, Dictionary<string, object> args)
    {
        return toolName switch
        {
            "workspace_create" => WorkspaceCreate(args),
            "workspace_destroy" => WorkspaceDestroy(args),
            "workspace_list" => WorkspaceList(args),
            "workspace_open" => WorkspaceOpen(args),
            // ...
        };
    }
}
```

---

## 五、实现优先级

| 阶段 | Tools | 说明 |
|------|-------|------|
| **Phase 1** | open, describe, sample, schema, filter, select, sort, join, aggregate, save | 最小可用集，覆盖核心数据操作 |
| **Phase 2** | count, summarize, limit, distinct, rename_columns, drop_columns, add_column | 补齐变换能力 |
| **Phase 3** | union, cross, random_sample, update, delete_rows, append, clear | 完整数据操作 |
| **Phase 4** | open_graph, add_nodes, add_edges, graph_neighbors, describe_graph | 图数据支持 |
| **Phase 5** | import_csv, export_csv, export_json, clone, rename_dataset, remove, search, diff | 元操作 |
| **Phase 6** | 多 workspace 管理（create, destroy, list） | 多 workspace 支持 |

---

## 六、测试策略

每个 tool 对应一组 xUnit 测试：

```csharp
// 测试命名约定
[Fact] public void WorkspaceFilter_BasicFilter()
[Fact] public void WorkspaceFilter_InvalidDataset_ReturnsError()
[Fact] public void WorkspaceFilter_ComplexExpression()
[Fact] public void WorkspaceJoin_InnerJoin()
[Fact] public void WorkspaceJoin_LeftJoin_WithUnmatchedRows()
```

测试覆盖：
1. **Happy path** — 正常操作，验证返回值
2. **Error path** — 不存在的数据集、无效的 filter 表达式、重复名称
3. **Edge cases** — 空数据集、大数据集、特殊字符
4. **Integration** — 多个 tool 组合使用（open → filter → join → save）

```bash
# 运行全部测试
cd DataCore.Tests~ && dotnet test

# 只跑 workspace 测试
dotnet test --filter "FullyQualifiedName~WorkspaceTests"

# 只跑工具层测试
dotnet test --filter "FullyQualifiedName~DataCoreToolsTests"
```

---

## 七、风险提示

1. **Filter 表达式解析** — 需要实现一个简单的表达式解析器，支持 `AND/OR/NOT/比较/字符串操作`。复杂度中等，建议独立为 `FilterExpressionParser` 类。

2. **Join 性能** — 内存 Join 对大数据集可能很慢。Phase 1 先实现简单的嵌套循环 Join，后续可优化为 Hash Join。

3. **计算列表达式** — `add_column` 的表达式引擎可以先支持简单的算术和三元表达式，复杂场景留给后续迭代。

4. **向后兼容** — 现有的 `IWorkspace.Get()`, `Register()`, `DescribeAll()` 等方法保持不变，新方法是增量添加。

---

**下一步：开始 Phase 1 实现。**
