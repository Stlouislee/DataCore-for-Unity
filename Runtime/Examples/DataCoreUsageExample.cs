using System;
using System.Collections.Generic;
using System.Linq;

namespace AroAro.DataCore.Examples
{
    /// <summary>
    /// DataCore 使用示例 - 展示 LiteDB 后端 API
    /// 
    /// 所有数据自动持久化到 LiteDB 数据库
    /// </summary>
    public static class DataCoreUsageExample
    {
        /// <summary>
        /// 基础使用示例
        /// </summary>
        public static void BasicExample()
        {
            // 创建数据存储（自动持久化到 LiteDB）
            using var store = DataStoreFactory.CreateLiteDb("data/mydata.db");
            
            Console.WriteLine($"Storage Backend: {store.Backend}"); // 输出: LiteDb
            
            // 创建表格数据集
            var tabular = store.CreateTabular("sales_data");
            
            // 添加列
            tabular.AddStringColumn("Product", new[] { "Apple", "Banana", "Orange", "Grape", "Mango" });
            tabular.AddNumericColumn("Price", new double[] { 1.5, 0.8, 1.2, 2.5, 3.0 });
            tabular.AddNumericColumn("Quantity", new double[] { 100, 150, 80, 60, 40 });
            
            // 基本统计
            Console.WriteLine($"Row Count: {tabular.RowCount}");
            Console.WriteLine($"Average Price: {tabular.Query().Average("Price"):F2}");
            Console.WriteLine($"Total Quantity: {tabular.Sum("Quantity")}");
            
            // 查询
            var expensiveProducts = tabular.Query()
                .WhereGreaterThan("Price", 1.0)
                .OrderByDescending("Price")
                .ToDictionaries();
            
            Console.WriteLine("Expensive products:");
            foreach (var row in expensiveProducts)
            {
                Console.WriteLine($"  {row["Product"]}: ${row["Price"]}");
            }
        }
        
        /// <summary>
        /// 图数据示例
        /// </summary>
        public static void GraphExample()
        {
            using var store = DataStoreFactory.CreateLiteDb("data/social.db");
            
            // 创建图数据集
            var graph = store.CreateGraph("social_network");
            
            // 添加节点
            graph.AddNode("Alice", new Dictionary<string, object> { { "age", 30 }, { "city", "NYC" } });
            graph.AddNode("Bob", new Dictionary<string, object> { { "age", 25 }, { "city", "LA" } });
            graph.AddNode("Carol", new Dictionary<string, object> { { "age", 35 }, { "city", "NYC" } });
            
            // 添加边
            graph.AddEdge("Alice", "Bob");
            graph.AddEdge("Bob", "Carol");
            graph.AddEdge("Alice", "Carol");
            
            Console.WriteLine($"Node Count: {graph.NodeCount}");
            Console.WriteLine($"Edge Count: {graph.EdgeCount}");
            Console.WriteLine($"Alice's neighbors: {string.Join(", ", graph.GetNeighbors("Alice"))}");
            
            // 图遍历
            var reachable = graph.Query()
                .From("Alice")
                .MaxDepth(2)
                .ToNodeIds();
            
            Console.WriteLine($"Nodes reachable from Alice: {string.Join(", ", reachable)}");
        }
        
        /// <summary>
        /// DataCoreStore 简化 API 示例
        /// </summary>
        public static void DataCoreStoreExample()
        {
            // 使用 DataCoreStore 简化 API
            using var store = new DataCoreStore("data/unified.db");
            
            // 创建表格
            var tabular = store.CreateTabular("my_table");
            tabular.AddNumericColumn("X", new double[] { 1, 2, 3 });
            tabular.AddNumericColumn("Y", new double[] { 4, 5, 6 });
            
            // 查询
            var results = tabular.Query()
                .WhereGreaterThan("X", 1)
                .ToDictionaries()
                .ToList();
            
            Console.WriteLine($"Rows where X > 1: {results.Count}");
            
            // 创建图
            var graph = store.CreateGraph("my_graph");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddEdge("A", "B");
            
            // 事务支持
            store.ExecuteInTransaction(() =>
            {
                var t = store.GetOrCreateTabular("accounts");
                t.AddRow(new Dictionary<string, object>
                {
                    { "AccountId", "A001" },
                    { "Balance", 1000.0 }
                });
            });
            
            Console.WriteLine("All operations persisted to LiteDB");
        }
        
        /// <summary>
        /// CSV 导入导出示例
        /// </summary>
        public static void CsvExample()
        {
            using var store = new DataCoreStore("data/csv_test.db");
            var tabular = store.CreateTabular("csv_data");
            
            // 导入 CSV 内容
            var csvContent = @"Name,Age,Score
Alice,30,95.5
Bob,25,88.0
Carol,35,92.3";
            
            tabular.ImportFromCsv(csvContent);
            
            Console.WriteLine($"Imported {tabular.RowCount} rows");
            
            // 导出 CSV
            var exported = tabular.ExportToCsv();
            Console.WriteLine("Exported CSV:");
            Console.WriteLine(exported);
        }
        
        /// <summary>
        /// 会话管理示例
        /// </summary>
        public static void SessionExample()
        {
            using var store = new DataCoreStore("data/session_test.db");
            var sessionManager = store.SessionManager;
            
            // 创建会话
            using var session = sessionManager.CreateSession("MySession");
            Console.WriteLine($"Session created: {session.Name}");
            
            // 在会话中创建临时数据集
            var tempData = session.CreateDataset("TempData", DataSetKind.Tabular);
            Console.WriteLine($"Temp dataset created: {tempData.Name}");
            
            // 会话结束时数据自动清理
        }
    }
}
