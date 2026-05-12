# Workspace Handover — 给下一个 Agent 的交接文档

**日期** 2026-05-12  
**当前版本** 0.5.0  
**分支** `feature/workspace`  
**PR** https://github.com/Stlouislee/DataCore-for-Unity/pull/165  

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
| `Runtime/DataCoreStore.cs` | 已添加 `Workspace` 属性，`SessionManager` 标记 `[Obsolete]` |
| `Runtime/Events/DataCoreEventManager.cs` | 已添加 `WorkspaceDatasetRegistered` 事件 |
| `DataCore.Tests~/Workspace/WorkspaceTests.cs` | 52 个测试，全部通过 |
| `Runtime/Examples/WorkspaceExample.cs` | 使用示例 |

### 测试状态

```
Passed:  750
Failed:  0
Skipped: 19 (已有 Session 测试，非 Workspace 引入)
```

---

## 二、需要你实现的 5 个问题

### P0-1：查询结果自动落地

**问题**：查询管道和 Workspace 是断开的。用户执行 query 后拿到原始数据（`List<Dictionary>`、`int[]`），需要手动 `Register` 到 Workspace。

**期望行为**：

```csharp
// 方案 A：给 ITabularQuery 加 ExecuteToWorkspace
store.GetTabular("Users").Query()
    .Where("age", QueryOp.Gt, 18)
    .ExecuteToWorkspace("adult_users");  // 自动注册到 store.Workspace

// 方案 B：让查询构建器持有 Workspace 引用（链式）
store.Workspace.Query("Users")           // 从 store 加载到 workspace
    .Where("age", QueryOp.Gt, 18)
    .Execute();                          // 结果自动注册为 "Users_filtered"
```

**实现要点**：

- 需要修改 `Runtime/Abstractions/ITabularQuery.cs` 和 `TabularQueryExtensions.cs`
- 或者在 Workspace 上加一层查询入口（`Workspace.Query(name)` 返回一个绑定 workspace 的查询构建器）
- 自动命名逻辑已有：`Workspace.AutoName(baseName)` 处理冲突
- 考虑是否要让 `ToDictionaries()` / `ToRowIndices()` 等现有方法也自动注册（可能太侵入，建议用新方法）

**参考文件**：
- `Runtime/Abstractions/ITabularQuery.cs` — 查询接口
- `Runtime/Abstractions/TabularQueryExtensions.cs` — 扩展方法
- `Runtime/LiteDb/LiteDbTabularQuery.cs` — LiteDB 查询实现
- `Runtime/Session/SessionDataFrameQueryBuilder.cs` — Session 的 DataFrame 查询（参考模式）

---

### P0-2：Workspace 数据持久化

**问题**：Workspace 全在内存里。应用重启后 derived 数据全部丢失。

**期望行为**：

```csharp
store.Workspace.Register("analysis", result, DataSource.Derived);
store.Workspace.Persist("analysis");              // 写回 store（LiteDB）
store.Workspace.Persist("analysis", "analysis_v2"); // 写回并重命名

// 下次启动
store.Workspace.Get("analysis");  // 从 store 自动加载
```

**实现要点**：

- `Session.PersistDataset()` 已有类似逻辑但标记 `NotImplementedException`
- `Session.CopyTabularData()` 通过 CSV 导出/导入复制数据，可以复用
- `Session.CopyGraphData()` 通过节点/边遍历复制图数据，可以复用
- Persist 后数据集的 source 应该从 `Derived` 变为 `Store`（或者保持 `Derived` 但标记 `persisted = true`）
- 需要决定：Persist 后 workspace 里的引用是指向 store 的对象还是保持独立副本？

**参考文件**：
- `Runtime/Session/Session.cs` — `PersistDataset()`、`CopyTabularData()`、`CopyGraphData()`

---

### P1：Get() 引用语义明确化

**问题**：`Workspace.Get("name")` 从 store 加载时直接引用 store 的对象（零拷贝）。修改一方影响另一方，这在 API 文档里没有说明，用户可能踩坑。

**当前行为**：

```csharp
var wsUsers = store.Workspace.Get("Users");
var storeUsers = store.GetTabular("Users");
// ReferenceEquals(wsUsers, storeUsers) == true
// 修改 wsUsers 会影响 storeUsers
```

**需要做的事**（选一个方向）：

**方向 A：保持引用，文档明确**（推荐，性能优先）
- 在 `IWorkspace.Get()` 的 XML 注释里明确说明是引用语义
- 在 README 和示例里说明 "Get 返回 store 数据集的直接引用，修改会影响持久层"
- 如果用户需要隔离副本，提供 `Clone()` 方法

**方向 B：改为复制，隔离安全**（Session 的做法）
- `Get()` 从 store 加载时做一次 CSV 导出/导入复制
- 性能开销：大数据集会慢，但语义更安全
- 需要清理 store 里不应该存在的临时副本

**建议选方向 A**，因为：
- Workspace 的定位是"桌面"，不是"沙箱"
- 零拷贝性能好
- `Clone()` 已经提供了显式复制的能力
- 只需要在文档里说清楚

---

### P2：DataFrame 支持迁移

**问题**：Session 有完整的 DataFrame 缓存和查询能力，Workspace 没有。Session 废弃后这些能力断层。

**Session 的 DataFrame API**：

```csharp
// Session 现有的
session.CreateDataFrame("name");
session.GetDataFrame("name");
session.HasDataFrame("name");
session.RemoveDataFrame("name");
session.ExecuteDataFrameQuery("source", df => df.Filter(...), "result");
session.ConvertToDataFrame("datasetName");
session.GetDataFrameStatistics("name");
```

**需要决定**：

**选项 A：Workspace 吸收 DataFrame**
- 在 `IWorkspace` 上加 DataFrame 相关方法
- Workspace 内部维护 `_dataFrameCache`（类似 Session）
- 好处：统一入口
- 坏处：Workspace 变复杂

**选项 B：DataFrame 独立为 DataCoreStore 级能力**
- `store.DataFrame` 或 `store.DataFrameManager`
- 与 Workspace 平级，不耦合
- 好处：职责清晰
- 坏处：用户需要知道两个入口

**选项 C：暂不迁移，保留 Session 的 DataFrame 支持**
- Session 标记 Obsolete 但不删除
- DataFrame 仍在 Session 里用
- 好处：最小改动
- 坏处：两个"桌面"并存，用户困惑

**建议选 A 或 B**，不要选 C。

**参考文件**：
- `Runtime/Session/Session.cs` — `#region DataFrame Support Methods`
- `Runtime/Session/DataFrameAdapter.cs`
- `Runtime/Session/DataFrameConverter.cs`
- `Runtime/Session/SessionDataFrameQueryBuilder.cs`
- `Runtime/Session/DataFrameMemoryManager.cs`

---

### P2：Editor 集成

**问题**：`DataCoreEditorComponent` 和 `DataCorePreviewWindow` 只显示 store 的数据集。Workspace 里的 derived 数据在 Inspector 里看不见。

**需要做的事**：

1. **DataCoreEditorComponent** — Inspector 面板显示 Workspace 内容
   - 分区显示：Store 数据集 / Workspace 数据集
   - 每个数据集显示 source 标记（Store / Derived / Imported）
   - 支持 Remove / Clear 操作

2. **DataCorePreviewWindow** — 预览窗口支持 Workspace 数据集
   - 点击 Workspace 数据集可以预览
   - 显示 schema、行数、样例数据

3. **创建按钮** — 支持从 Editor 创建 Workspace 数据集
   - "Import to Workspace" 按钮
   - 选择 source 类型

**参考文件**：
- `Runtime/DataCoreEditorComponent.cs`
- `Runtime/Editor/DataCorePreviewWindow.cs`（如果存在）

---

## 三、架构决策记录

### 已决定

| 决策 | 理由 |
|------|------|
| Workspace 替代 Session 作为默认桌面 | Session 需要手动创建，不是自然到达的 |
| SessionManager 标记 `[Obsolete]` 但保留 | 给老用户迁移窗口 |
| `Has()` 只查 workspace，不查 store | 避免 Rename 后语义混乱；用 `TryPeek()` / `AllNames` 查全部 |
| `DescribeAll` 懒缓存 + 脏标记 | 避免每次调用都遍历所有数据集 |
| `WorkspaceRetentionPolicy.Auto`：≥100K 行走弱引用 | 防止大 join 结果吃光内存 |
| Workspace 不拥有 store 数据集的生命周期 | Dispose 时只释放 derived/imported，不释放 store 数据集 |

### 待决定（留给下一个 Agent）

| 决策 | 选项 |
|------|------|
| Get() 引用 vs 复制 | A: 引用+文档明确 / B: 复制+隔离 |
| DataFrame 放哪里 | A: Workspace 吸收 / B: 独立为 Store 级 / C: 保留在 Session |
| 查询结果自动落地的方式 | A: ITabularQuery 新方法 / B: Workspace 查询入口 |
| Persist 后 source 标记是否变化 | A: 变为 Store / B: 保持 Derived + persisted 标记 |

---

## 四、代码约定

- **命名空间**：`AroAro.DataCore.Workspace`
- **测试框架**：xUnit（不是 NUnit）
- **测试隔离**：每个测试用独立的 `DataCoreStore`（独立 LiteDB 文件 + 临时目录），`Dispose` 里清理
- **Unity .meta 文件**：`Runtime/` 下每个新文件都需要 `.meta`（GUID 随机生成）
- **编译验证**：`dotnet build` 在 `DataCore.Tests~/` 目录下运行
- **测试验证**：`dotnet test --filter "FullyQualifiedName~WorkspaceTests"` 运行 workspace 测试，`dotnet test` 运行全部

---

## 五、风险提示

1. **LiteDB `WithName` 不可用** — `LiteDbTabularDataset.WithName()` 会抛 `NotSupportedException`。Workspace 的 `Clone()` 和 `Rename()` 已经绕开了它（Clone 用 CSV 导出/导入，Rename 只移动字典键）。如果新功能需要复制 LiteDB 数据集，用 CSV 导出/导入或遍历复制，**不要用 `WithName`**。

2. **测试共享 LiteDB 状态** — 默认 `DataCoreStore()` 用共享的 LiteDB 文件。不同测试之间的数据会互相污染。每个测试必须用独立路径：`new DataCoreStore(Path.Combine(tmpDir, "test.db"))`。

3. **事件系统是静态的** — `DataCoreEventManager` 用静态字段存事件订阅。如果测试之间不清理，会互相影响。测试 `Dispose` 里调用 `DataCoreEventManager.ClearAllSubscriptions()`。

4. **package.json 版本号** — 当前是 `0.5.0`。如果下一个 agent 的改动是 breaking change，考虑 bump 到 `0.6.0`。

---

## 六、快速上手

```bash
# 克隆
git clone https://github.com/Stlouislee/DataCore-for-Unity.git
cd DataCore-for-Unity
git checkout feature/workspace

# 编译
cd DataCore.Tests~ && dotnet build

# 跑 workspace 测试
dotnet test --filter "FullyQualifiedName~WorkspaceTests"

# 跑全部测试
dotnet test
```

---

**祝你好运。代码是干净的，测试是绿的，方向是清楚的。去吧。**
