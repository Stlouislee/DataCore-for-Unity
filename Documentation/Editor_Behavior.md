# DataCore 编辑器行为说明

## 非Play模式下显示数据集

### 问题描述
在Unity编辑器的非Play模式下，DataCore组件默认不会显示已保存的数据集。这是因为：

1. **MonoBehaviour生命周期**：`Awake()` 和 `Start()` 方法只在Play模式下被调用
2. **数据初始化**：`DataCoreStore` 在非Play模式下不会被自动创建
3. **持久化数据加载**：需要手动触发数据加载

### 解决方案

现在DataCore已经优化，支持在非Play模式下显示数据集：

#### 1. 自动数据加载
- `GetStore()` 方法现在会在非Play模式下自动创建store并加载持久化数据
- 无需手动操作，编辑器会自动显示已保存的数据集

#### 2. 持久化路径
- 数据保存在 `Application.persistentDataPath` 下的指定目录
- 默认路径：`DataCore/`
- 可以在Inspector中修改 `persistencePath` 字段

#### 3. 文件格式
- **表格数据**：保存为 `.arrow` 文件（Apache Arrow格式）
- **图数据**：保存为 `.dcgraph` 文件（自定义格式）

### 使用场景

#### Play模式
- 数据在 `Awake()` 时自动加载
- 运行时操作会实时保存到文件
- 退出Play模式时自动保存（如果启用 `autoSaveOnExit`）

#### 非Play模式（编辑器模式）
- 打开包含DataCore组件的场景时自动加载数据
- 可以在Inspector中查看、删除、管理数据集
- CSV导入功能正常工作

### 配置选项

#### `clearOnEditMode`（默认：false）
- **false**：退出Play模式后保留数据（推荐）
- **true**：退出Play模式后清空数据

#### `autoSaveOnExit`（默认：true）
- **true**：退出Play模式时自动保存
- **false**：需要手动调用 `SaveAll()` 方法

### 常见问题

#### Q: 为什么非Play模式下看不到数据集？
A: 确保：
1. 数据已经通过Play模式保存过
2. `persistencePath` 配置正确
3. 文件确实存在于持久化目录中

#### Q: 如何手动刷新数据？
A: 在Inspector中：
1. 点击 "Load All" 按钮
2. 或重新选择GameObject

#### Q: CSV导入在非Play模式下工作吗？
A: 是的，CSV导入功能在Play模式和非Play模式下都能正常工作。

### 最佳实践

1. **数据备份**：定期备份持久化目录中的重要数据
2. **路径管理**：使用相对路径便于项目迁移
3. **版本控制**：将重要的数据集文件纳入版本控制
4. **性能考虑**：大型数据集在非Play模式下加载可能需要一些时间

### 技术细节

#### 数据加载流程
1. 检查 `persistencePath` 目录是否存在
2. 扫描 `.arrow` 和 `.dcgraph` 文件
3. 使用对应的序列化器加载数据
4. 注册到 `DataCoreStore` 中

#### 错误处理
- 文件损坏时会跳过并记录错误
- 格式不兼容时会尝试兼容性处理
- 权限问题会显示明确的错误信息