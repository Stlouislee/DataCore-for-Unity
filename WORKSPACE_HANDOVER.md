# Workspace Handover — 给下一个 Agent 的交接文档

**日期** 2026-05-12  
**当前版本** 0.5.0  
**分支** `feature/workspace`  
**PR** https://github.com/Stlouislee/DataCore-for-Unity/pull/165  
**上次交接** WORKSPACE_HANDOVER.md（原始交接文档，仍可参考架构决策记录）

---

## 一、你拿到的是什么

Workspace 是 DataCore 的统一内存工作区，替代 Session 成为 `DataCoreStore` 的默认"桌面"。

### 已实现

| 文件 | 说明 |
|------|------|
| `Runtime/Workspace/IWorkspace.cs` | 接口定义 |
| `Runtime/Workspace/Workspace.cs` | 完整实现（~500 行） |
| `Runtime/Workspace/DataSource.cs` | `Store / Derived / Imported` 枚举 |
| `Runtime/Workspace/ColumnInfo.cs` | 列信息 |
| `Runtime/Workspace/WorkspaceEntry.cs` | DescribeAll 返回的元数据结构 |
| `Runtime/Workspace/WorkspaceRetentionPolicy.cs` | `Strong / Weak / Auto` 内存策略 |
| `Runtime/DataCoreStore.cs` | `Workspace` 属性 + 多 workspace 支持（索引器、Create/Destroy/Get/List） |
| `Runtime/Events/DataCoreEventManager.cs` | `WorkspaceDatasetRegistered` 事件 |
| `DataCore.Tests~/Workspace/WorkspaceTests.cs` | 52 个测试，全部通过 |
| `Runtime/Examples/WorkspaceExample.cs` | 使用示例 |
| **`Runtime/Tools/DataCoreTools.cs`** | **35 个 Agent tool 的 dispatch 层（Phase 1 新增）** |
| **`Runtime/Tools/FilterExpressionParser.cs`** | **Filter 表达式解析器（Phase 1 新增）** |
| **`Runtime/Tools/ToolResult.cs`** | **统一 JSON 返回结构（Phase 1 新增）** |
| **`DataCore.Tests~/Tools/DataCoreToolsTests.cs`** | **40 个 tool 测试（Phase 1 新增）** |

### 测试状态

```
Total:  835
Passed: 816
Skipped: 19 (已有 Session 测试，非 Workspace 引入)
Failed:  0
```

---

## 二、已解决的设计决策

以下决策在本轮实现中已确定：

| 决策 | 选择 | 理由 |
|------|------|------|
| Get() 语义 | **复制语义**（默认）+ `OpenRef()` 零拷贝 opt-in | Workspace 是"桌面"不是"沙箱"，Agent 不应意外改 store 数据 |
| Agent API 风格 | **Workspace-centric + flat tools** | Agent 的操作台是 Workspace，Store 是实现细节 |
| 查询结果落地 | **不需要"落地"概念** | Agent 在 Workspace 上操作，结果自然在 Workspace 上 |
| Filter 表达式 | **自研 FilterExpressionParser** | 支持 AND/OR/NOT/contains/starts_with/between/in/is_null，独立于查询引擎 |
| Join key | **leftKey + rightKey 分离** | 两边列名可能不同（如 Users.id = Orders.user_id） |
| 多 Workspace | **DataCoreStore 管理集合** | 索引器 `store["analysis"]` + Create/Destroy/Get/List |
| 返回格式 | **统一 JSON：success/action/result/error/suggestion** | Agent 可直接解析，错误带 suggestion 可自动纠正 |

---

## 三、已实现的 46 个 Tool

### 管理（3）
- `workspace_create` — 创建命名 workspace
- `workspace_destroy` — 销毁 workspace
- `workspace_list` — 列出所有 workspace

### 加载（3）
- `workspace_open` — 从 store 加载（**复制语义**，CSV 导出/导入）
- `workspace_open_ref` — 从 store 加载（**零拷贝引用**，性能优先）
- `workspace_import_csv` — 从 CSV 导入

### 检视（4）
- `workspace_describe` — 单个或全部数据集描述
- `workspace_sample` — 查看样例行（支持 offset）
- `workspace_schema` — 列 schema 详情
- `workspace_statistics` — 统计摘要（min/max/mean/std/distinct/top）

### 变换（9）
- `workspace_filter` — 过滤（支持复杂表达式）
- `workspace_select` — 选择列
- `workspace_rename_columns` — 重命名列
- `workspace_sort` — 排序
- `workspace_distinct` — 去重
- `workspace_add_column` — 新增计算列（支持三元表达式）
- `workspace_drop_columns` — 删除列
- `workspace_limit` — 限制行数（支持 offset）
- `workspace_random_sample` — 随机采样（支持 seed）

### 组合（3）
- `workspace_join` — Join（inner/left/right/full，支持 leftKey/rightKey）
- `workspace_union` — 上下拼接
- `workspace_cross` — 笛卡尔积

### 聚合（3）
- `workspace_aggregate` — Group by + 聚合（count/sum/mean/min/max/std）
- `workspace_summarize` — 全局聚合
- `workspace_count` — 快速计数（支持 filter）

### 持久化（3）
- `workspace_save` — 推回 store
- `workspace_export_csv` — 导出 CSV
- `workspace_export_json` — 导出 JSON（records/columns/values 格式）

### 修改（4）
- `workspace_update` — 更新满足条件的行
- `workspace_delete_rows` — 删除满足条件的行
- `workspace_append` — 追加行
- `workspace_clear` — 清空数据集

### 元操作（5）
- `workspace_clone` — 克隆数据集
- `workspace_rename_dataset` — 重命名
- `workspace_remove` — 从 workspace 移除
- `workspace_search` — 搜索数据集名
- `workspace_diff` — 比较两个数据集

### DataFrame（5）— Phase 2 新增
- `workspace_dataframe_create` — 创建空 DataFrame
- `workspace_dataframe_convert` — 表格数据集转 DataFrame
- `workspace_dataframe_list` — 列出所有 DataFrame
- `workspace_dataframe_remove` — 移除 DataFrame
- `workspace_dataframe_to_dataset` — DataFrame 转回表格数据集

### 图数据（5）— Phase 4 新增
- `workspace_open_graph` — 从 Store 加载图到 Workspace
- `workspace_add_nodes` — 批量添加节点
- `workspace_add_edges` — 批量添加边
- `workspace_graph_neighbors` — 邻居查询（in/out/all 方向）
- `workspace_describe_graph` — 图数据集描述

---

## 四、Tool 调用模式

### 调用方式

```csharp
// 静态入口
DataCoreTools.Initialize(store);
string json = DataCoreTools.Execute("workspace_filter", new Dictionary<string, object>
{
    ["workspace"] = "default",
    ["source"] = "Users",
    ["filter"] = "age > 18 AND city == Shanghai",
    ["resultName"] = "adults"
});
// 返回：{"success":true,"action":"workspace_filter","result":{...}}
```

### 参数类型

所有参数通过 `Dictionary<string, object>` 传入。支持：
- `string` — 直接传
- `int/bool` — 通过 `Convert.ToInt32/Convert.ToBoolean`
- `string[]` — 通过 `JsonElement` 数组或 `IEnumerable<object>`
- `Dictionary<string, string>` — 通过 `JsonElement` object
- `List<Dictionary<string, object>>` — 行数据，通过 `JsonElement` 数组或 `IEnumerable<Dictionary>`

### Filter 表达式语法

```
比较: age > 18, score >= 90, name == Alice, status != inactive
逻辑: AND, OR, NOT
字符串: city contains Shang, name starts with A, name ends with ng
空值: email is null, email is not null
范围: age between 18 35
集合: city in Shanghai Beijing Guangzhou
括号: (age > 18 AND city == Shanghai) OR admin == true
```

### 返回值格式

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
    "sample": [{ "id": 1, "name": "Alice", "age": 25, ... }]
  }
}
```

错误时：
```json
{
  "success": false,
  "action": "workspace_filter",
  "error": "Dataset 'nonexistent' not found on workspace 'analysis'",
  "suggestion": "Available datasets: Users, Orders. Did you mean 'Users'?"
}
```

---

## 五、多 Workspace 支持

```csharp
// Store 级：管理 workspace 集合
store.CreateWorkspace("analysis")       // 创建
store.DestroyWorkspace("analysis")      // 销毁（不能销毁 default）
store.GetWorkspace("analysis")          // 获取（不存在时自动创建）
store.ListWorkspaces()                  // 列出所有

// 索引器
store["analysis"]                       // 等价于 GetWorkspace

// 默认 workspace（向后兼容）
store.Workspace                         // 等价于 store["default"]
```

Tool 调用时通过 `workspace` 参数指定：
```
workspace_filter(workspace: "analysis", source: "Users", filter: "age > 18", resultName: "adults")
```

---

## 六、已修复的 Bug

### LiteDB CSV 导出双重引号

**问题**：`LiteDbTabularDataset.ExportToCsv()` 调用 `BsonValue.ToString()` 导出字符串值，但 `BsonValue.ToString()` 对字符串返回 `"Shanghai"`（带引号），导致 CSV 中出现 `""Shanghai""`，导入后值变成 `"Shanghai"`（带引号的字符串）。

**修复**：`Runtime/LiteDb/LiteDbTabularDataset.cs` 的 `ExportToCsv` 方法中，对字符串类型使用 `value.AsString` 而非 `value.ToString()`。

---

## 七、架构决策记录（保留自上次交接）

### 已决定

| 决策 | 理由 |
|------|------|
| Workspace 替代 Session 作为默认桌面 | Session 需要手动创建，不是自然到达的 |
| SessionManager 标记 `[Obsolete]` 但保留 | 给老用户迁移窗口 |
| `Has()` 只查 workspace，不查 store | 避免 Rename 后语义混乱 |
| `DescribeAll` 懒缓存 + 脏标记 | 避免每次调用都遍历所有数据集 |
| `WorkspaceRetentionPolicy.Auto`：≥100K 行走弱引用 | 防止大 join 结果吃光内存 |
| Workspace 不拥有 store 数据集的生命周期 | Dispose 时只释放 derived/imported |

### 待决定（留给下一个 Agent）

| 决策 | 选项 | 建议 |
|------|------|------|
| DataFrame 放哪里 | A: Workspace 吸收 / B: 独立为 Store 级 / C: 保留在 Session | A 或 B，不要 C |
| Persist 后 source 标记 | A: 变为 Store / B: 保持 Derived + persisted | 当前实现为 A |

---

## 八、需要你实现的（Phase 2-6）

### ~~Phase 2：DataFrame 支持（P2）~~ ✅ 已完成

Workspace 现有完整的 DataFrame 支持：
- `IWorkspace.CreateDataFrame/GetDataFrame/HasDataFrame/RemoveDataFrame/ConvertToDataFrame`
- 5 个 Agent tool：`workspace_dataframe_create/convert/list/remove/to_dataset`
- 测试：6 个新测试

### ~~Phase 3：Editor 集成（P2）~~ ✅ 已完成

- `DataCoreEditorComponent` 新增 Workspace API（GetWorkspace、GetAllDatasetViews、LoadToWorkspace）
- `DataCorePreviewWindow` 支持 Workspace 数据预览（ShowWorkspaceWindow）
- Inspector 新增 Workspace Datasets 分区

### ~~Phase 4：图数据 Tool 支持（P2）~~ ✅ 已完成

5 个新 tool：
- `workspace_open_graph` — 从 Store 加载图到 Workspace
- `workspace_add_nodes` — 批量添加节点
- `workspace_add_edges` — 批量添加边
- `workspace_graph_neighbors` — 邻居查询（in/out/all）
- `workspace_describe_graph` — 图数据集描述

### ~~Phase 5：Tool Schema 暴露（P1）~~ ✅ 已完成

- `DataCoreTools.GetToolSchemas()` — 返回全部 46 个 tool 的 JSON Schema
- `DataCoreTools.GetToolNames()` — 返回 tool 名称列表
- 测试：5 个新测试

### ~~Phase 6：性能优化（P3）~~ ✅ 已完成

- `CopyTabular`：直接数据复制替代 CSV 序列化往返
- `FilterExpressionParser`：ConcurrentDictionary 谓词缓存
- `workspace_join`：已确认使用 Hash Join
- 测试：6 个新测试

---

## 九、代码约定

- **命名空间**：`AroAro.DataCore.Tools`（Tool 层）、`AroAro.DataCore.Workspace`（Workspace）
- **测试框架**：xUnit（不是 NUnit）
- **测试隔离**：每个测试用独立的 `DataCoreStore`（独立 LiteDB 文件 + 临时目录），`Dispose` 里清理
- **Unity .meta 文件**：`Runtime/` 下每个新文件都需要 `.meta`（GUID 随机生成）— **Phase 1 新增的 3 个文件还没有 .meta**
- **编译验证**：`dotnet build` 在 `DataCore.Tests~/` 目录下运行
- **测试验证**：`dotnet test --filter "FullyQualifiedName~DataCoreToolsTests"` 运行 tool 测试，`dotnet test` 运行全部

---

## 十、风险提示

1. **Workspace `Get` 回退到 store** — 当前 `ws.Get("name")` 优先查 workspace slots，找不到时回退到 store（零拷贝引用）。这意味着 `workspace_open` 注册了复制，但后续 `Get` 可能返回 store 的原始数据（如果注册失败或被覆盖）。建议后续改为 `Get` 只查 workspace，`Open` 才从 store 加载。

2. **Filter 表达式解析器没有转义支持** — 如果列名或值包含特殊字符（如 `AND`、`OR`），解析器可能误判。当前通过大小写区分关键字（`and` vs `AND`），但不完美。

3. **`workspace_add_column` 的表达式引擎** — 只支持简单的算术和三元表达式，复杂场景（函数调用、嵌套三元）不支持。

4. **Tool 参数没有 Schema 验证** — 当前直接从 `Dictionary<string, object>` 取值，类型不匹配时抛异常。建议后续加 JSON Schema 验证层。

5. **.meta 文件缺失** — ~~`Runtime/Tools/` 下的 3 个新文件没有 Unity .meta 文件~~ ✅ 已在 Phase 7 补全。

---

## 十一、Tool Coverage Gap Analysis（对照 pandas/SQL/dplyr）

### 当前 53 个 Tool 的覆盖情况

| 类别 | 已覆盖 | 缺失 |
|------|--------|------|
| **IO/Manage** | open/import/export/save/create/destroy/list | Parquet/Excel 导出 |
| **Inspect** | describe/sample/schema/statistics/count/value_counts | 数据概况一键报告、列级分位数 |
| **Transform** | filter/select/rename/sort/distinct/add_column/drop/limit/random/cast/fill_null | 条件列、字符串操作、分箱、正则过滤 |
| **Combine** | join(inner/left/right/full)/union/cross | anti-join、semi-join |
| **Aggregate** | aggregate(group by)/summarize/count | 透视表(pivot)、melt/unpivot |
| **Modify** | update/delete_rows/append/clear | — |
| **Meta** | clone/rename/remove/search/diff | — |
| **Graph** | open/add_nodes/add_edges/neighbors/describe/path/stats | 图算法(PageRank 等已有但未暴露为 tool) |
| **DataFrame** | create/convert/list/remove/to_dataset | — |
| **Batch** | batch | — |

### 缺失的高频操作（按优先级）

#### P1 — 立即需要

| Tool | 说明 | 对标 |
|------|------|------|
| `workspace_if_column` | 条件列：根据表达式生成新列（if/else 逻辑） | `pd.Series.where`, SQL `CASE WHEN` |
| `workspace_string_ops` | 字符串操作：lower/upper/trim/replace/substring/contains(regex) | `pd.str.*`, SQL string functions |
| `workspace_head` | 取前 N 行（简化 limit offset=0） | `df.head(n)`, SQL `LIMIT n` |
| `workspace_describe_all` | 一键数据概况：每列类型、非空数、唯一数、min/max/mean、top 值 | `df.describe(include='all')` |

#### P2 — 结构变换与高级分析

| Tool | 说明 | 对标 |
|------|------|------|
| `workspace_pivot` | 透视表：行→列转换 | `pd.pivot_table`, SQL `PIVOT` |
| `workspace_melt` | 反透视：宽表→长表 | `pd.melt`, SQL `UNPIVOT` |
| `workspace_window` | 窗口函数：排名/累计/滑动平均 | `ROW_NUMBER()`, `RANK()`, `SUM() OVER` |
| `workspace_anti_join` | 找不匹配的行 | `LEFT JOIN ... WHERE NULL`, `~isin()` |
| `workspace_regex_filter` | 正则表达式过滤 | `pd.str.contains(regex)`, `REGEXP` |
| `workspace_date_parse` | 日期解析：提取年/月/日/星期/季度 | `pd.to_datetime`, SQL date functions |

#### P3 — 数据质量与高级功能

| Tool | 说明 | 对标 |
|------|------|------|
| `workspace_profile` | 数据质量报告：缺失率、类型分布、异常值检测 | `pandas-profiling` |
| `workspace_bin` | 分箱：连续值→离散区间 | `pd.cut`, SQL `NTILE` |
| `workspace_concat_columns` | 列拼接：合并多列为字符串 | `pd.Series.str.cat` |
| `workspace_quantile` | 分位数计算 | `pd.Series.quantile` |
| `workspace_coalesce` | 多列取第一个非空值 | SQL `COALESCE()` |
| `workspace_cast_column` 增强 | 支持 date/bool/int/float 互转 | — |

### 建议实现顺序

```
Phase 8:  workspace_if_column + workspace_string_ops + workspace_head + workspace_describe_all
Phase 9:  workspace_pivot + workspace_melt + workspace_anti_join
Phase 10: workspace_window + workspace_regex_filter + workspace_date_parse
Phase 11: workspace_profile + workspace_bin + workspace_coalesce
```

---

## 十二、快速上手

```bash
# 克隆
git clone https://github.com/Stlouislee/DataCore-for-Unity.git
cd DataCore-for-Unity
git checkout feature/workspace

# 编译
cd DataCore.Tests~ && dotnet build

# 跑 tool 测试
dotnet test --filter "FullyQualifiedName~DataCoreToolsTests"

# 跑全部测试
dotnet test

# 跑 workspace 测试
dotnet test --filter "FullyQualifiedName~WorkspaceTests"
```

---

**Phase 1-7 全部完成。代码是干净的，测试是绿的，53 个 tool 已就绪。下一阶段见 Phase 8-11 roadmap。**
