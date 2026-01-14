using System;
using System.Collections.Generic;
using System.IO;
using NumSharp;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// DataCore 表格数据测试 - 使用抽象化 API
    /// </summary>
    public static class LiteDbTabularTest
    {
        /// <summary>
        /// 运行所有测试
        /// </summary>
        public static void RunAllTests()
        {
            var results = new List<(string Name, bool Passed, string Error)>();

            results.Add(RunTest("CreateAndBasicOperations", TestCreateAndBasicOperations));
            results.Add(RunTest("ColumnOperations", TestColumnOperations));
            results.Add(RunTest("RowOperations", TestRowOperations));
            results.Add(RunTest("QueryOperations", TestQueryOperations));
            results.Add(RunTest("CsvImportExport", TestCsvImportExport));
            results.Add(RunTest("TransactionSupport", TestTransactionSupport));
            results.Add(RunTest("LargeDataset", TestLargeDataset));
            results.Add(RunTest("MemoryBackend", TestMemoryBackend));

            // 输出结果
            Console.WriteLine("\n=== DataCore Tabular Test Results ===\n");
            int passed = 0, failed = 0;
            foreach (var (name, success, error) in results)
            {
                if (success)
                {
                    Console.WriteLine($"✓ {name}");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ {name}: {error}");
                    failed++;
                }
            }
            Console.WriteLine($"\nTotal: {passed} passed, {failed} failed");
        }

        private static (string, bool, string) RunTest(string name, Action test)
        {
            try
            {
                test();
                return (name, true, null);
            }
            catch (Exception ex)
            {
                return (name, false, ex.Message);
            }
        }

        /// <summary>
        /// 测试基本创建和操作
        /// </summary>
        public static void TestCreateAndBasicOperations()
        {
            var dbPath = GetTestDbPath("basic_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);
            
            Assert(store.StorageBackend == StorageBackend.LiteDb, "Backend should be LiteDb");
            
            // 创建表格
            var table = store.CreateTabular("test-table");
            Assert(table != null, "Table should be created");
            Assert(table.Name == "test-table", "Table name should match");
            Assert(table.Kind == DataSetKind.Tabular, "Kind should be Tabular");
            Assert(table.RowCount == 0, "Initial row count should be 0");
            Assert(table.ColumnCount == 0, "Initial column count should be 0");

            // 添加列
            table.AddNumericColumn("score", new double[] { 100, 200, 300 });
            table.AddStringColumn("name", new[] { "Alice", "Bob", "Charlie" });

            Assert(table.RowCount == 3, "Row count should be 3");
            Assert(table.ColumnCount == 2, "Column count should be 2");
            Assert(table.HasColumn("score"), "Should have 'score' column");
            Assert(table.HasColumn("name"), "Should have 'name' column");

            // 获取数据
            var scores = table.GetNumericColumn("score");
            Assert(scores.size == 3, "Score array size should be 3");
            Assert(Math.Abs(scores.GetDouble(0) - 100) < 0.001, "First score should be 100");

            var names = table.GetStringColumn("name");
            Assert(names.Length == 3, "Name array length should be 3");
            Assert(names[0] == "Alice", "First name should be Alice");

            // 验证数据集持久化
            store.Dispose();

            using var store2 = DataStoreFactory.CreateLiteDb(dbPath);
            var table2 = store2.GetTabular("test-table");
            Assert(table2.RowCount == 3, "Reloaded table should have 3 rows");
            Assert(table2.ColumnCount == 2, "Reloaded table should have 2 columns");

            CleanupDb(dbPath);
        }

        /// <summary>
        /// 测试列操作
        /// </summary>
        public static void TestColumnOperations()
        {
            var dbPath = GetTestDbPath("column_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);
            var table = store.CreateTabular("column-test");

            // 使用 NDArray 添加列
            var arr = np.array(new double[] { 1.5, 2.5, 3.5, 4.5 });
            table.AddNumericColumn("values", arr);

            Assert(table.RowCount == 4, "Should have 4 rows");

            // 获取列类型
            Assert(table.GetColumnType("values") == ColumnType.Numeric, "Should be numeric type");

            // 统计函数
            Assert(Math.Abs(table.Sum("values") - 12.0) < 0.001, "Sum should be 12");
            Assert(Math.Abs(table.Mean("values") - 3.0) < 0.001, "Mean should be 3");
            Assert(Math.Abs(table.Min("values") - 1.5) < 0.001, "Min should be 1.5");
            Assert(Math.Abs(table.Max("values") - 4.5) < 0.001, "Max should be 4.5");

            // 移除列
            Assert(table.RemoveColumn("values"), "Should remove column");
            Assert(!table.HasColumn("values"), "Column should be removed");

            CleanupDb(dbPath);
        }

        /// <summary>
        /// 测试行操作
        /// </summary>
        public static void TestRowOperations()
        {
            var dbPath = GetTestDbPath("row_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);
            var table = store.CreateTabular("row-test");

            // 添加列定义
            table.AddNumericColumn("id", new double[] { 1, 2, 3 });
            table.AddStringColumn("name", new[] { "A", "B", "C" });

            Assert(table.RowCount == 3, "Should have 3 rows");

            // 添加单行
            table.AddRow(new Dictionary<string, object>
            {
                { "id", 4.0 },
                { "name", "D" }
            });
            Assert(table.RowCount == 4, "Should have 4 rows after insert");

            // 获取行
            var row = table.GetRow(3);
            Assert(row != null, "Row should exist");
            Assert(Convert.ToDouble(row["id"]) == 4.0, "ID should be 4");
            Assert(row["name"].ToString() == "D", "Name should be D");

            // 更新行
            table.UpdateRow(3, new Dictionary<string, object> { { "name", "D-Updated" } });
            row = table.GetRow(3);
            Assert(row["name"].ToString() == "D-Updated", "Name should be updated");

            // 删除行
            Assert(table.DeleteRow(3), "Should delete row");
            Assert(table.RowCount == 3, "Should have 3 rows after delete");

            // 批量添加
            var newRows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object> { { "id", 5.0 }, { "name", "E" } },
                new Dictionary<string, object> { { "id", 6.0 }, { "name", "F" } }
            };
            var count = table.AddRows(newRows);
            Assert(count == 2, "Should insert 2 rows");
            Assert(table.RowCount == 5, "Should have 5 rows");

            CleanupDb(dbPath);
        }

        /// <summary>
        /// 测试查询操作
        /// </summary>
        public static void TestQueryOperations()
        {
            var dbPath = GetTestDbPath("query_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);
            var table = store.CreateTabular("query-test");

            // 准备测试数据
            table.AddNumericColumn("price", new double[] { 10, 25, 15, 30, 5 });
            table.AddStringColumn("product", new[] { "Apple", "Banana", "Cherry", "Date", "Elderberry" });
            table.AddNumericColumn("quantity", new double[] { 100, 50, 75, 25, 200 });

            // 基本查询
            var indices = table.Where("price", QueryOp.GreaterThan, 15.0);
            Assert(indices.Length == 2, "Should find 2 rows with price > 15");

            // 流式查询
            var results = table.Query()
                .WhereGreaterThan("price", 10)
                .OrderBy("price")
                .ToResults();

            var resultList = new List<Dictionary<string, object>>(results);
            Assert(resultList.Count == 3, "Should have 3 results");
            Assert(Convert.ToDouble(resultList[0]["price"]) == 15, "First should be price 15");

            // 聚合查询
            var avgPrice = table.Query()
                .WhereGreaterThan("quantity", 50)
                .Mean("price");
            Assert(avgPrice > 0, "Average should be positive");

            // Count
            var count = table.Query()
                .WhereLessThan("price", 20)
                .Count();
            Assert(count == 3, "Should count 3 rows");

            CleanupDb(dbPath);
        }

        /// <summary>
        /// 测试 CSV 导入导出
        /// </summary>
        public static void TestCsvImportExport()
        {
            var dbPath = GetTestDbPath("csv_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);
            var table = store.CreateTabular("csv-test");

            // 导入 CSV
            var csvContent = @"Name,Age,Score
Alice,30,95.5
Bob,25,88.0
Carol,35,92.3
Dave,28,85.0";

            table.ImportFromCsv(csvContent);

            Assert(table.RowCount == 4, "Should have 4 rows");
            Assert(table.ColumnCount == 3, "Should have 3 columns");

            // 验证数据
            var ages = table.GetNumericColumn("Age");
            Assert(Math.Abs(ages.GetDouble(0) - 30) < 0.001, "First age should be 30");

            // 导出 CSV
            var exported = table.ExportToCsv();
            Assert(exported.Contains("Alice"), "Export should contain Alice");
            Assert(exported.Contains("95.5"), "Export should contain 95.5");

            CleanupDb(dbPath);
        }

        /// <summary>
        /// 测试事务支持
        /// </summary>
        public static void TestTransactionSupport()
        {
            var dbPath = GetTestDbPath("transaction_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);

            // 成功事务
            store.ExecuteInTransaction(() =>
            {
                var table = store.CreateTabular("tx-test");
                table.AddNumericColumn("value", new double[] { 1, 2, 3 });
            });

            Assert(store.TabularExists("tx-test"), "Table should exist after successful transaction");

            // 验证数据持久化
            var loaded = store.GetTabular("tx-test");
            Assert(loaded.RowCount == 3, "Should have 3 rows");

            CleanupDb(dbPath);
        }

        /// <summary>
        /// 测试大数据集
        /// </summary>
        public static void TestLargeDataset()
        {
            var dbPath = GetTestDbPath("large_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);
            var table = store.CreateTabular("large-test");

            // 创建大数据集
            const int size = 10000;
            var values = new double[size];
            var names = new string[size];
            for (int i = 0; i < size; i++)
            {
                values[i] = i * 0.1;
                names[i] = $"Item_{i}";
            }

            table.AddNumericColumn("value", values);
            table.AddStringColumn("name", names);

            Assert(table.RowCount == size, $"Should have {size} rows");

            // 查询性能测试
            var highValues = table.Query()
                .WhereGreaterThan("value", 500)
                .Limit(100)
                .Count();

            Assert(highValues <= 100, "Should respect limit");

            CleanupDb(dbPath);
        }

        /// <summary>
        /// 测试内存后端
        /// </summary>
        public static void TestMemoryBackend()
        {
            using var store = DataStoreFactory.CreateMemory();
            
            Assert(store.StorageBackend == StorageBackend.Memory, "Backend should be Memory");
            
            // 创建表格
            var table = store.CreateTabular("memory-test");
            table.AddNumericColumn("x", new double[] { 1, 2, 3, 4, 5 });
            table.AddNumericColumn("y", new double[] { 2, 4, 6, 8, 10 });
            
            Assert(table.RowCount == 5, "Should have 5 rows");
            Assert(table.Mean("x") == 3, "Mean of x should be 3");
            Assert(table.Sum("y") == 30, "Sum of y should be 30");
            
            // 查询
            var results = table.Query()
                .WhereGreaterThan("x", 2)
                .ToResults();
            
            int count = 0;
            foreach (var _ in results) count++;
            Assert(count == 3, "Should find 3 rows where x > 2");
            
            // 创建图
            var graph = store.CreateGraph("memory-graph");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B");
            graph.AddEdge("B", "C");
            
            Assert(graph.NodeCount == 3, "Should have 3 nodes");
            Assert(graph.EdgeCount == 2, "Should have 2 edges");
        }

        #region Helpers

        private static string GetTestDbPath(string filename)
        {
            var dir = Path.Combine(Path.GetTempPath(), "DataCoreTests");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, filename);
        }

        private static void CleanupDb(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(path + "-log"))
                    File.Delete(path + "-log");
            }
            catch { }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception($"Assertion failed: {message}");
        }

        #endregion
    }
}
