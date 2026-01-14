using System;
using System.Collections.Generic;
using NumSharp;

namespace AroAro.DataCore.Examples
{
    /// <summary>
    /// DataCore 使用示例 - 展示抽象化的存储后端 API
    /// 
    /// 用户代码只需要使用 AroAro.DataCore 命名空间，
    /// 不需要关心底层是 LiteDB 还是内存存储。
    /// </summary>
    public static class DataCoreUsageExample
    {
        /// <summary>
        /// 演示如何使用 LiteDB 后端
        /// </summary>
        public static void LiteDbExample()
        {
            // 方式1: 使用工厂方法创建 LiteDB 存储
            using var store = DataStoreFactory.CreateLiteDb("data/mydata.db");
            
            // 检查存储后端类型
            Console.WriteLine($"Storage Backend: {store.Backend}"); // 输出: LiteDb
            
            // 创建表格数据集
            var tabular = store.CreateTabular("sales_data");
            
            // 添加列
            tabular.AddStringColumn("Product", new[] { "Apple", "Banana", "Orange", "Grape", "Mango" });
            tabular.AddNumericColumn("Price", new double[] { 1.5, 0.8, 1.2, 2.5, 3.0 });
            tabular.AddNumericColumn("Quantity", new double[] { 100, 150, 80, 60, 40 });
            
            // 基本统计
            Console.WriteLine($"Row Count: {tabular.RowCount}");
            Console.WriteLine($"Average Price: {tabular.Mean("Price"):F2}");
            Console.WriteLine($"Total Quantity: {tabular.Sum("Quantity")}");
            
            // 查询
            var expensiveProducts = tabular.Query()
                .WhereGreaterThan("Price", 1.0)
                .OrderByDescending("Price")
                .ToDictionaries();
            
            foreach (var row in expensiveProducts)
            {
                Console.WriteLine($"  {row["Product"]}: ${row["Price"]}");
            }
            
            // 创建图数据集
            var graph = store.CreateGraph("social_network");
            graph.AddNode("Alice", new Dictionary<string, object> { { "age", 30 }, { "city", "NYC" } });
            graph.AddNode("Bob", new Dictionary<string, object> { { "age", 25 }, { "city", "LA" } });
            graph.AddNode("Carol", new Dictionary<string, object> { { "age", 35 }, { "city", "NYC" } });
            
            graph.AddEdge("Alice", "Bob");
            graph.AddEdge("Bob", "Carol");
            graph.AddEdge("Alice", "Carol");
            
            Console.WriteLine($"Node Count: {graph.NodeCount}");
            Console.WriteLine($"Edge Count: {graph.EdgeCount}");
            Console.WriteLine($"Alice's neighbors: {string.Join(", ", graph.GetNeighbors("Alice"))}");
        }
        
        /// <summary>
        /// 演示如何使用内存后端 (适合测试)
        /// </summary>
        public static void MemoryExample()
        {
            // 使用内存存储 (不需要文件路径)
            using var store = DataStoreFactory.CreateMemory();
            
            Console.WriteLine($"Storage Backend: {store.Backend}"); // 输出: Memory
            
            var tabular = store.CreateTabular("test_data");
            tabular.AddNumericColumn("Values", np.array(new double[] { 1, 2, 3, 4, 5 }));
            
            Console.WriteLine($"Mean: {tabular.Mean("Values")}"); // 输出: 3.0
            
            // 内存存储适合单元测试
        }
        
        /// <summary>
        /// 演示如何通过枚举动态选择后端
        /// </summary>
        public static void DynamicBackendSelection(StorageBackend backend, string path = null)
        {
            // 根据配置动态选择后端
            using var store = DataStoreFactory.Create(backend, path);
            
            Console.WriteLine($"Using backend: {store.Backend}");
            
            // 以下代码对所有后端都通用
            var tabular = store.GetOrCreateTabular("unified_data");
            tabular.AddNumericColumn("X", new double[] { 1, 2, 3 });
            tabular.AddNumericColumn("Y", new double[] { 4, 5, 6 });
            
            var sum = tabular.Query()
                .WhereGreaterThan("X", 1)
                .Sum("Y");
            
            Console.WriteLine($"Sum of Y where X > 1: {sum}");
        }
        
        /// <summary>
        /// 演示 CSV 导入导出
        /// </summary>
        public static void CsvExample()
        {
            using var store = DataStoreFactory.CreateMemory();
            var tabular = store.CreateTabular("csv_data");
            
            // 导入 CSV
            var csvContent = @"Name,Age,Score
Alice,30,95.5
Bob,25,88.0
Carol,35,92.3";
            
            tabular.ImportFromCsv(csvContent);
            
            Console.WriteLine($"Imported {tabular.RowCount} rows");
            Console.WriteLine($"Columns: {string.Join(", ", tabular.ColumnNames)}");
            
            // 导出 CSV
            var exported = tabular.ExportToCsv();
            Console.WriteLine("Exported CSV:");
            Console.WriteLine(exported);
        }
        
        /// <summary>
        /// 演示图查询
        /// </summary>
        public static void GraphAlgorithmExample()
        {
            using var store = DataStoreFactory.CreateMemory();
            var graph = store.CreateGraph("traversal_demo");
            
            // 构建图
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddNode("D");
            graph.AddNode("E");
            
            graph.AddEdge("A", "B");
            graph.AddEdge("A", "C");
            graph.AddEdge("B", "D");
            graph.AddEdge("C", "D");
            graph.AddEdge("D", "E");
            
            // 图查询 - 从 A 节点遍历
            Console.WriteLine("Traversal from A:");
            var reachable = graph.Query()
                .From("A")
                .MaxDepth(3)
                .ToNodeIds();
            
            foreach (var node in reachable)
            {
                Console.Write($"{node} -> ");
            }
            Console.WriteLine();
            
            // 查询度数大于 1 的节点
            var hubNodes = graph.Query()
                .WhereNodeHasProperty("id")
                .ToNodeIds();
            Console.WriteLine($"Hub nodes count: {hubNodes.Count()}");
        }
        
        /// <summary>
        /// 演示事务支持 (仅 LiteDB)
        /// </summary>
        public static void TransactionExample()
        {
            using var store = DataStoreFactory.CreateLiteDb("data/transaction_test.db");
            
            // 使用事务确保原子性
            store.ExecuteInTransaction(() =>
            {
                var tabular = store.GetOrCreateTabular("accounts");
                
                // 批量操作
                tabular.AddRow(new Dictionary<string, object>
                {
                    { "AccountId", "A001" },
                    { "Balance", 1000.0 }
                });
                
                tabular.AddRow(new Dictionary<string, object>
                {
                    { "AccountId", "A002" },
                    { "Balance", 2000.0 }
                });
                
                // 如果中间发生异常，整个事务会回滚
            });
            
            Console.WriteLine("Transaction committed successfully");
        }
    }
}
