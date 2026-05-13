# Workspace Handover — 给下一个 Agent

**版本** 0.7.0 | **分支** `feature/workspace` | **PR** #165 | **更新** 2026-05-13

---

## 一、你拿到的是什么

Workspace 是 DataCore 的统一内存工作区，替代 Session 成为 `DataCoreStore` 的默认"桌面"。Phase 1-7 全部完成，53 个 Agent Tool 已就绪，832 测试全绿。

### 核心文件

| 层 | 文件 | 说明 |
|----|------|------|
| Workspace | `Runtime/Workspace/IWorkspace.cs` | 接口 |
| | `Runtime/Workspace/Workspace.cs` | 实现（~600 行） |
| | `Runtime/DataCoreStore.cs` | Workspace 属性 + 多 workspace |
| Tools | `Runtime/Tools/DataCoreTools.cs` | 53 个 tool dispatch（~2200 行） |
| | `Runtime/Tools/FilterExpressionParser.cs` | 表达式解析 + 谓词缓存 |
| | `Runtime/Tools/ToolResult.cs` | 统一 JSON 返回 |
| Editor | `Runtime/DataCoreEditorComponent.cs` | Workspace API |
| | `Editor/DataCoreEditor.cs` | Inspector Workspace 分区 |
| | `Editor/DataCorePreviewWindow.cs` | Workspace 预览 |

### 测试：832 pass / 0 fail / 19 skip

---

## 二、已实现的 53 个 Tool

| 类别 | 数量 | Tool |
|------|------|------|
| 管理 | 3 | create / destroy / list |
| 加载 | 3 | open(复制) / open_ref(零拷贝) / import_csv |
| 检视 | 5 | describe / sample / schema / statistics / value_counts |
| 变换 | 11 | filter / select / rename / sort / distinct / add_column / drop / limit / random / cast_column / fill_null |
| 组合 | 3 | join(inner/left/right/full) / union / cross |
| 聚合 | 3 | aggregate(group by) / summarize / count |
| 持久化 | 3 | save / export_csv / export_json |
| 修改 | 4 | update / delete_rows / append / clear |
| 元操作 | 5 | clone / rename / remove / search / diff |
| DataFrame | 5 | create / convert / list / remove / to_dataset |
| 图数据 | 7 | open_graph / add_nodes / add_edges / neighbors / describe / path(BFS) / stats |
| 批量 | 1 | batch(多步骤) |

**调用方式**：`DataCoreTools.Execute("tool_name", args)` → JSON

**Schema 暴露**：`DataCoreTools.GetToolSchemas()` 返回全部 tool 的 JSON Schema，供 Agent 框架注册。

**参数统一**：所有 tool 接受 `dataset`（向后兼容 `source`），图 tool 接受 `graph`。

**错误处理**：所有错误带 `suggestion`（Levenshtein 模糊匹配 "Did you mean?"）。

**Filter 语法**：`age > 18 AND city contains Shang` / `between` / `in` / `is null` / `starts with` / `ends with` / 括号 / AND/OR/NOT。

---

## 三、架构决策（已定）

| 决策 | 选择 |
|------|------|
| Get() 语义 | 复制语义（默认）+ OpenRef() 零拷贝 |
| Agent API | Workspace-centric，Store 是实现细节 |
| Filter | 自研 FilterExpressionParser，带 ConcurrentDictionary 缓存 |
| Join | Hash Join（字典索引） |
| 多 Workspace | DataCoreStore 管理，索引器 `store["name"]` |
| 返回格式 | `{success, action, result, error, suggestion}` |
| Session | 标记 `[Obsolete]`，保留迁移窗口 |
| 内存策略 | Strong/Weak/Auto（≥100K 行自动弱引用） |

---

## 四、已知风险

1. `ws.Get("name")` 回退到 store 零拷贝引用 — 如果注册失败可能返回 store 原始数据
2. Filter 解析器无转义 — `AND`/`OR` 等关键字在列名/值中可能误判
3. `add_column` 表达式引擎只支持简单算术+三元，不支持函数调用
4. Tool 参数无 Schema 验证 — 类型不匹配时直接抛异常

---

## 五、Tool Coverage Gap Analysis

### 对照 pandas/SQL/dplyr 的缺失

#### P1 — 立即需要

| Tool | 说明 | 对标 |
|------|------|------|
| `workspace_if_column` | 条件列（if/else） | `CASE WHEN`, `pd.where` |
| `workspace_string_ops` | lower/upper/trim/replace/substring/regex | `pd.str.*`, SQL string funcs |
| `workspace_head` | 前 N 行 | `df.head(n)`, `LIMIT n` |
| `workspace_describe_all` | 一键数据概况 | `df.describe(include='all')` |

#### P2 — 结构变换

| Tool | 说明 |
|------|------|
| `workspace_pivot` | 透视表 |
| `workspace_melt` | 宽表→长表 |
| `workspace_window` | 窗口函数（排名/累计/滑动） |
| `workspace_anti_join` | 找不匹配行 |
| `workspace_regex_filter` | 正则过滤 |
| `workspace_date_parse` | 日期解析 |

#### P3 — 数据质量

| Tool | 说明 |
|------|------|
| `workspace_profile` | 数据质量报告 |
| `workspace_bin` | 分箱 |
| `workspace_concat_columns` | 列拼接 |
| `workspace_quantile` | 分位数 |
| `workspace_coalesce` | 多列取首非空 |

### 实现路线

```
Phase 8:  if_column + string_ops + head + describe_all
Phase 9:  pivot + melt + anti_join
Phase 10: window + regex_filter + date_parse
Phase 11: profile + bin + coalesce
```

---

## 六、代码约定

- 命名空间：`AroAro.DataCore.Tools` / `AroAro.DataCore.Workspace`
- 测试：xUnit，每测试独立 `DataCoreStore`，`Dispose` 清理
- 编译：`cd DataCore.Tests~ && dotnet build`
- 测试：`dotnet test`（全量）/ `dotnet test --filter "FullyQualifiedName~XxxTests"`
- .meta：`Runtime/` 下每个新文件需要 `.meta`

---

## 七、快速上手

```bash
git clone https://github.com/Stlouislee/DataCore-for-Unity.git
cd DataCore-for-Unity && git checkout feature/workspace
cd DataCore.Tests~ && dotnet build && dotnet test
```

---

**53 个 tool 已就绪，832 测试全绿。下一步见 Phase 8-11 roadmap。**
