# DataCore-for-Unity — Agent Onboarding Prompt

## 项目概述

**DataCore-for-Unity** 是一个 Unity Package，为 Unity 提供数据管理能力，主要运行在 **Meta Quest (Android/ARM64)** 和 **Android** 平台上。

- **仓库**：https://github.com/Stlouislee/DataCore-for-Unity
- **包格式**：Unity Package (`package.json`)
- **数据层**：LiteDB（嵌入式 NoSQL）
- **测试框架**：xUnit（`.NET 8.0`），461 passed / 22 skipped / 0 failed（截至 2026-05-04）

## 项目架构

```
DataCore-for-Unity/
├── Runtime/                          # 核心运行时代码
│   ├── DataCoreEditorComponent.cs    # 主组件（多实例模式，注册表模式）
│   ├── DataCoreStore.cs              # 数据存储层（LiteDB 封装）
│   ├── Algorithms/                   # 算法模块
│   │   ├── AlgorithmBase.cs          # 算法基类（模板方法模式，分层异常处理）
│   │   ├── AlgorithmRegistry.cs      # 算法注册表（单例模式）
│   │   ├── AlgorithmResult.cs        # 算法结果（含 Exception 属性）
│   │   ├── AlgorithmPipeline.cs      # 算法流水线
│   │   ├── Graph/                    # 图算法（PageRank, ConnectedComponents）
│   │   └── Tabular/                  # 表格算法（MinMaxNormalize）
│   ├── Session/                      # 会话管理
│   │   ├── SessionManager.cs
│   │   └── SessionDataFrameQueryBuilder.cs  # DataFrame 查询构建器
│   ├── Events/                       # 事件系统
│   ├── Import/                       # 导入器（CSV, GraphML）
│   ├── LiteDb/                       # LiteDB 数据集实现
│   ├── SampleDatasets/               # 示例数据集
│   └── Plugins/                      # 第三方 DLL（NumSharp, Microsoft.Data.Analysis 等）
├── Editor/                           # Unity 编辑器扩展
│   ├── DataCoreEditor.cs             # 自定义 Inspector
│   ├── SessionWindow.cs              # 会话窗口
│   └── DataCorePreviewWindow.cs      # 预览窗口
├── DataCore.Tests/                   # xUnit 测试项目（.NET 8.0）
│   ├── DataCore.Tests.csproj
│   └── DataCoreEditorComponentStub.cs # 测试用 stub
├── Tests/                            # Unity Test Runner 测试（MonoBehaviour）
├── .github/workflows/test.yml        # GitHub Actions CI
└── package.json
```

## 环境准备

### 必需工具

1. **GitHub CLI (`gh`)**：用于 issue 管理、PR、代码查看
   ```bash
   apt install gh
   gh auth login --with-token <<< "YOUR_TOKEN"
   ```
   - Token 需要 `repo`、`workflow`、`issues:write` 权限

2. **.NET SDK 8.0**：用于编译和运行测试
   ```bash
   # 使用 dotnet-install.sh 安装
   wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
   chmod +x /tmp/dotnet-install.sh
   /tmp/dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet
   ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
   ```

3. **Git**：克隆和提交代码

### 克隆仓库

```bash
cd /root/.openclaw/workspace
git clone https://github.com/Stlouislee/DataCore-for-Unity.git
```

## 常用操作

### 运行测试

```bash
cd DataCore-for-Unity/DataCore.Tests
dotnet restore
dotnet build
dotnet test --no-build --verbosity normal
```

- 测试结果：461 passed, 22 skipped, 0 failed
- 22 个 skipped 是代码中标记的已知问题（`[Fact(Skip = "Known issue: ...")]`），不是 CI 配置问题

### GitHub Actions CI

- 配置文件：`.github/workflows/test.yml`
- 触发条件：push 到 `main`/`master`/`develop`，或 PR 目标为这些分支
- 流程：checkout → setup .NET 8.0 → restore → build → test
- 使用 `actions/checkout@v6` 和 `actions/setup-dotnet@v5`（避免 Node.js 20 废弃警告）
- 结果查看：https://github.com/Stlouislee/DataCore-for-Unity/actions

### Issue 管理

```bash
gh issue list --state all --limit 50
gh issue view <number>
gh issue comment <number> --body '...'
gh issue close <number> --comment '...'
gh issue edit <number> --remove-label "severity:high" --add-label "severity:low"
```

## 重要设计模式与约定

### DataCoreEditorComponent — 多实例模式

- **不是单例**：使用注册表模式，支持场景中多个实例
- `AllInstances`（静态）：所有活跃实例
- `Instance`（静态）：向后兼容，返回第一个注册的实例
- `FindByName(string)` / `FindByPath(string)`：按名称或路径查找
- `InstanceName`（序列化字段）：Inspector 中可配置的标识名
- `databasePath`（序列化字段）：每个实例独立的数据库路径
- 同路径冲突：Awake 中警告日志，不销毁

### AlgorithmBase — 模板方法模式

- `Execute()` 是模板方法：验证 → 计时 → 事件 → 调用 `ExecuteCore()` → 包装结果
- 分层异常处理：
  - `OperationCanceledException` → 取消
  - 非致命异常 → catch + `Debug.LogException` + Failed result
  - 致命异常（OOM/StackOverflow）→ 仍然 catch（VR 稳定性），标记 `[FATAL]`
- `AlgorithmResult.Exception` 属性保留原始异常

### AlgorithmRegistry — 单例模式

- `Default` 属性：懒初始化，无线程安全保护（Unity 主线程模型下足够）
- `RegisterBuiltIns()`：自动注册 PageRank、ConnectedComponents、MinMaxNormalize
- `ResetDefault()`：测试用

### DataFrame 查询（SessionDataFrameQueryBuilder）

- 链式调用：`Query().Where().GroupBy().Select().Execute()`
- `GroupBy` 支持多聚合列（合并到结果 DataFrame，不覆盖）

## 平台注意事项（Unity / Android / Meta Quest）

1. **IL2CPP (AOT)**：
   - `Lazy<T>` 在旧版 IL2CPP 有 bug，优先用 `volatile` + `lock`
   - 避免反射和动态代码生成
   - `System.Threading` 基本可用但有限制

2. **VR 稳定性**：
   - 优先 graceful degradation 而非 crash
   - 致命异常也应 catch，避免整个 app 崩溃
   - 用 `Debug.LogException` 记录完整堆栈

3. **Unity 主线程模型**：
   - `Awake`/`Start`/`Update` 都在主线程
   - 大部分竞态条件在实践中不会触发
   - 评估 issue 时考虑实际调用场景，不要过度设计

4. **LiteDB 文件锁**：
   - 同一 `.db` 文件不能被多个实例同时打开
   - 多实例必须使用不同 `databasePath`

## 修复 Issue 的工作流

1. `gh issue view <number>` — 理解问题
2. **深入调查代码** — 理解架构和上下文，不要只看 issue 描述
3. **评估平台影响** — Unity/IL2CPP/Android/VR 的约束
4. **实施修复** — 代码改动
5. **更新测试** — un-skip 已修复的测试，更新断言
6. `dotnet test` — 确保全部通过
7. `git commit && git push` — 提交
8. `gh issue comment` — 写分析和修复说明
9. `gh issue close` — 关闭

## 跳过测试的分类（截至 2026-05-04）

| 类别 | 数量 | 原因 |
|------|------|------|
| LiteDbTabularDataset.WithName | 5 | NotSupportedException |
| RemoveColumn | 2 | LiteDB 引擎 dispose 问题 |
| Transaction rollback | 2 | 回滚不移除已创建的 dataset |
| AlgorithmBase 异常处理 | 3 | 捕获所有异常 / Pipeline 独立问题 |
| CSV 导入/导出 | 2 | 解析和引号问题 |
| DataFrame 转换 | 2 | 列转换和类型强转 |
| Query/Session 其他 | 6 | EndsWith 未实现、RemoveNode 等 |

## 已完成的修复（2026-05-04）

| Issue | 修复 | 状态 |
|-------|------|------|
| #89 Critical | GroupBy 多聚合列合并 | ✅ Closed |
| #58 Critical | DataCoreEditorComponent 多实例模式 | ✅ Closed |
| #56 Critical | CSV parser 用 NaN 替代 0.0 | ✅ Closed |
| #118 High | AlgorithmBase 分层异常处理 | ✅ Closed |
| #119 High | AlgorithmRegistry 单例（降级为理论问题） | ✅ Closed |
| CI | GitHub Actions workflow + 升级 actions | ✅ |
| CI | xUnit analyzer 警告修复 | ✅ |
