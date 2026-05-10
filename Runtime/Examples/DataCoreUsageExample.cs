using UnityEngine;
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
            
            Debug.Log($"Storage Backend: {store.Backend}"); // 输出: LiteDb
            
            // 创建表格数据集
            var tabular = store.CreateTabular("sales_data");
            
            // 添加列
            tabular.AddStringColumn("Product", new[] { "Apple", "Banana", "Orange", "Grape", "Mango" });
            tabular.AddNumericColumn("Price", new double[] { 1.5, 0.8, 1.2, 2.5, 3.0 });
            tabular.AddNumericColumn("Quantity", new double[] { 100, 150, 80, 60, 40 });
            
            // 基本统计
            Debug.Log($"Row Count: {tabular.RowCount}");
            Debug.Log($"Average Price: {tabular.Query().Average("Price"):F2}");
            Debug.Log($"Total Quantity: {tabular.Sum("Quantity")}");
            
            // 查询
            var expensiveProducts = tabular.Query()
                .WhereGreaterThan("Price", 1.0)
                .OrderByDescending("Price")
                .ToDictionaries();
            
            Debug.Log("Expensive products:");
            foreach (var row in expensiveProducts)
            {
                Debug.Log($"  {row["Product"]}: ${row["Price"]}");
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
            
            Debug.Log($"Node Count: {graph.NodeCount}");
            Debug.Log($"Edge Count: {graph.EdgeCount}");
            Debug.Log($"Alice's neighbors: {string.Join(", ", graph.GetNeighbors("Alice"))}");
            
            // 图遍历
            var reachable = graph.Query()
                .From("Alice")
                .MaxDepth(2)
                .ToNodeIds();
            
            Debug.Log($"Nodes reachable from Alice: {string.Join(", ", reachable)}");
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
            
            Debug.Log($"Rows where X > 1: {results.Count}");
            
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
            
            Debug.Log("All operations persisted to LiteDB");
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
            
            Debug.Log($"Imported {tabular.RowCount} rows");
            
            // 导出 CSV
            var exported = tabular.ExportToCsv();
            Debug.Log("Exported CSV:");
            Debug.Log(exported);
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
            Debug.Log($"Session created: {session.Name}");
            
            // 在会话中创建临时数据集
            var tempData = session.CreateDataset("TempData", DataSetKind.Tabular);
            Debug.Log($"Temp dataset created: {tempData.Name}");
            
            // 会话结束时数据自动清理
        }
    }
}
