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

## 六、Phase 8：命名空间重构 + Analysis API

### 6.1 命名空间体系

将 53 个 `workspace_*` tool 重构为分层命名空间，`.` 分隔：

| 命名空间 | 职责 | 当前 tool 迁移 |
|----------|------|---------------|
| `workspace.*` | 工作区管理 | create / destroy / list / open / open_ref / import_csv / save |
| `data.*` | 表格数据操作 | filter / select / rename / sort / distinct / add_column / drop / limit / random / cast / fill_null / update / delete_rows / append / clear / clone / remove / search / diff / describe / sample / schema / statistics / value_counts / count / aggregate / summarize / join / union / cross / export_csv / export_json |
| `graph.*` | 图数据操作 | open_graph / add_nodes / add_edges / neighbors / describe_graph / path / stats |
| `df.*` | DataFrame | create / convert / list / remove / to_dataset |
| `analysis.*` | 统计分析 | **新增**（见 6.2） |
| `algorithm.*` | 算法执行 | **新增**（见 6.3） |

**向后兼容**：路由层保留旧 `workspace_*` 名称为别名，自动映射到新名称。

```csharp
// 路由别名映射
private static string NormalizeToolName(string name) => name switch
{
    "workspace_filter" => "data.filter",
    "workspace_sort" => "data.sort",
    "workspace_join" => "data.join",
    "workspace_graph_path" => "graph.path",
    // ... 全部 53 个
    _ => name  // 新名称直接透传
};
```

### 6.2 Analysis API（新增）

#### 统计分析

| Tool | 说明 | 复杂度 |
|------|------|--------|
| `analysis.describe` | 增强版数据概况（中位数/分位数/空值率/偏度/峰度） | 低 |
| `analysis.correlation` | 相关系数矩阵（Pearson/Spearman） | 低 |
| `analysis.outliers` | 异常值检测（IQR / Z-score） | 低 |
| `analysis.regression` | 线性回归（最小二乘，返回系数/R²/残差） | 中 |
| `analysis.clustering` | K-Means 聚类 | 中 |
| `analysis.hypothesis_test` | 假设检验（t-test / chi-square） | 中 |
| `analysis.distribution` | 分布拟合 + 正态性检验 | 中 |

#### 网络分析

| Tool | 说明 | 来源 |
|------|------|------|
| `analysis.centrality` | 度/接近/介数中心性 | 新实现 |
| `analysis.communities` | 社区检测 | 桥接 ConnectedComponents |
| `analysis.influence` | 影响力排名 | 桥接 PageRank |
| `analysis.ego_network` | 自我中心子图 | BFS 实现 |
| `analysis.shortest_path` | 最短路径 | 桥接 graph.path |

### 6.3 Algorithm 桥接（新增）

| Tool | 说明 |
|------|------|
| `algorithm.list` | 列出所有已注册算法 |
| `algorithm.run` | 执行单个算法（输入从 workspace 取，输出注册回 workspace） |
| `algorithm.pipeline` | 多步流水线 |
| `algorithm.register` | 注册自定义算法 |

### 6.4 实现顺序

```
Phase 8a: 命名空间重构（路由别映射，不改实现）
Phase 8b: analysis.describe + analysis.correlation + analysis.outliers
Phase 8c: algorithm.list + algorithm.run（桥接已有算法）
Phase 8d: analysis.regression + analysis.clustering + analysis.hypothesis_test
Phase 8e: analysis.centrality + analysis.communities + analysis.ego_network
```

---

## 七、Algorithm 迁移方案

### 现状

Algorithm 框架已实现但与 Workspace/Agent Tools **完全隔离**：

| 组件 | 文件 | 状态 |
|------|------|------|
| `IAlgorithm` / `ITabularAlgorithm` / `IGraphAlgorithm` | `Runtime/Algorithms/IAlgorithm.cs` | ✅ 接口完整 |
| `AlgorithmBase` / `GraphAlgorithmBase` / `TabularAlgorithmBase` | `Runtime/Algorithms/AlgorithmBase.cs` | ✅ 模板方法 |
| `AlgorithmContext` | `Runtime/Algorithms/AlgorithmContext.cs` | ✅ 参数+CancellationToken+进度 |
| `AlgorithmResult` | `Runtime/Algorithms/AlgorithmResult.cs` | ✅ 输出数据集+指标+元数据 |
| `AlgorithmRegistry` | `Runtime/Algorithms/AlgorithmRegistry.cs` | ✅ 单例注册表 |
| `AlgorithmPipeline` | `Runtime/Algorithms/AlgorithmPipeline.cs` | ✅ 多步流水线 |
| `PageRankAlgorithm` | `Runtime/Algorithms/Graph/PageRankAlgorithm.cs` | ✅ |
| `ConnectedComponentsAlgorithm` | `Runtime/Algorithms/Graph/ConnectedComponentsAlgorithm.cs` | ✅ |
| `MinMaxNormalizeAlgorithm` | `Runtime/Algorithms/Tabular/MinMaxNormalizeAlgorithm.cs` | ✅ |

### 问题

1. **无 Agent Tool 入口** — Agent 无法通过 `DataCoreTools.Execute()` 调用算法
2. **无 Workspace 集成** — 算法输出是 `IDataSet`，需要手动注册到 Workspace
3. **无 Schema 暴露** — `GetToolSchemas()` 不包含算法参数信息
4. **Pipeline 未暴露** — 多步算法组合对 Agent 不可用

### 迁移方案

#### 方案 A：桥接层（推荐）

在 `DataCoreTools` 中新增 3 个 tool，桥接 Algorithm → Workspace：

```
workspace_algorithm_list     → 列出所有已注册算法（名称+描述+参数+kind）
workspace_algorithm_run      → 执行单个算法（输入从 workspace 取，输出注册回 workspace）
workspace_algorithm_pipeline → 执行多步流水线
```

**实现要点**：
- `AlgorithmRegistry.Default` 已有全部算法，无需重新注册
- `AlgorithmContext.Builder` 已支持参数字典，直接从 tool args 构建
- 输出 `IDataSet` 自动注册到 Workspace（`DataSource.Derived`）
- `AlgorithmResult.Metrics` 返回到 tool result JSON
- 参数 Schema 从 `IAlgorithm.Parameters`（`AlgorithmParameterDescriptor`）自动生成

**调用示例**：
```json
{
  "tool": "workspace_algorithm_run",
  "args": {
    "algorithm": "PageRank",
    "dataset": "SocialGraph",
    "resultName": "RankedGraph",
    "params": {
      "dampingFactor": 0.85,
      "maxIterations": 200
    }
  }
}
```

**代码位置**：新增 `Runtime/Tools/AlgorithmTools.cs` 或直接加到 `DataCoreTools.cs`。

#### 方案 B：独立 Tool 文件（更干净）

将算法 tool 抽到 `Runtime/Tools/AlgorithmTools.cs`：
- 避免 `DataCoreTools.cs` 继续膨胀（当前 ~2200 行）
- 独立命名空间 `AroAro.DataCore.Tools.Algorithms`
- `DataCoreTools.Execute()` 路由到 `AlgorithmTools.Execute()`

### 迁移步骤

```
1. 新增 workspace_algorithm_list（30 行）
   - 遍历 AlgorithmRegistry.Default.GetAll()
   - 返回 name/description/kind/parameters

2. 新增 workspace_algorithm_run（60 行）
   - Registry.Get(name) → IAlgorithm
   - ws.Get(dataset) → IDataSet
   - AlgorithmContext.Builder.WithParameters(params).Build()
   - algo.Execute(input, context) → AlgorithmResult
   - ws.Register(resultName, result.OutputDataset)
   - 返回 {success, outputDataset, metrics, duration}

3. 新增 workspace_algorithm_pipeline（80 行）
   - 解析 steps 数组
   - 逐步执行，前一步输出作为后一步输入
   - 每步结果注册到 Workspace

4. GetToolSchemas() 追加 3 个 schema

5. GetToolNames() 追加 3 个名称

6. 测试（~10 个）
```

### 后续扩展

迁移完成后可自然扩展：
- 自定义算法注册（`workspace_algorithm_register`）
- 算法可视化（执行历史、指标趋势）
- 更多内置算法（社区检测、最短路径全源、特征工程）

---

## 八、代码约定

- 命名空间：`AroAro.DataCore.Tools` / `AroAro.DataCore.Workspace`
- 测试：xUnit，每测试独立 `DataCoreStore`，`Dispose` 清理
- 编译：`cd DataCore.Tests~ && dotnet build`
- 测试：`dotnet test`（全量）/ `dotnet test --filter "FullyQualifiedName~XxxTests"`
- .meta：`Runtime/` 下每个新文件需要 `.meta`

---

## 九、快速上手

```bash
git clone https://github.com/Stlouislee/DataCore-for-Unity.git
cd DataCore-for-Unity && git checkout feature/workspace
cd DataCore.Tests~ && dotnet build && dotnet test
```

---

**53 个 tool 已就绪，832 测试全绿。下一步见 Phase 8（命名空间重构 + Analysis API + Algorithm 桥接）。**
