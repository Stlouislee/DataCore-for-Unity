using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// DataCore 表格数据测试 - 使用抽象化 API
    /// Migrated from static class to NUnit TestFixture for CI/CD compatibility.
    /// </summary>
    [TestFixture]
    public class LiteDbTabularTest
    {
        [Test]
        public void CreateAndBasicOperations()
        {
            var dbPath = GetTestDbPath("basic_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);

            Assert.That(store.StorageBackend, Is.EqualTo(StorageBackend.LiteDb), "Backend should be LiteDb");

            var table = store.CreateTabular("test-table");
            Assert.That(table, Is.Not.Null, "Table should be created");
            Assert.That(table.Name, Is.EqualTo("test-table"), "Table name should match");
            Assert.That(table.Kind, Is.EqualTo(DataSetKind.Tabular), "Kind should be Tabular");
            Assert.That(table.RowCount, Is.EqualTo(0), "Initial row count should be 0");
            Assert.That(table.ColumnCount, Is.EqualTo(0), "Initial column count should be 0");

            table.AddNumericColumn("score", new double[] { 100, 200, 300 });
            table.AddStringColumn("name", new[] { "Alice", "Bob", "Charlie" });

            Assert.That(table.RowCount, Is.EqualTo(3), "Row count should be 3");
            Assert.That(table.ColumnCount, Is.EqualTo(2), "Column count should be 2");
            Assert.That(table.HasColumn("score"), Is.True, "Should have 'score' column");
            Assert.That(table.HasColumn("name"), Is.True, "Should have 'name' column");

            var scores = table.GetNumericColumnRaw("score");
            Assert.That(scores.Length, Is.EqualTo(3), "Score array size should be 3");
            Assert.That(Math.Abs(scores[0] - 100), Is.LessThan(0.001), "First score should be 100");

            var names = table.GetStringColumn("name");
            Assert.That(names.Length, Is.EqualTo(3), "Name array length should be 3");
            Assert.That(names[0], Is.EqualTo("Alice"), "First name should be Alice");

            store.Dispose();

            using var store2 = DataStoreFactory.CreateLiteDb(dbPath);
            var table2 = store2.GetTabular("test-table");
            Assert.That(table2.RowCount, Is.EqualTo(3), "Reloaded table should have 3 rows");
            Assert.That(table2.ColumnCount, Is.EqualTo(2), "Reloaded table should have 2 columns");

            CleanupDb(dbPath);
        }

        [Test]
        public void ColumnOperations()
        {
            var dbPath = GetTestDbPath("column_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);
            var table = store.CreateTabular("column-test");

            table.AddNumericColumn("values", new double[] { 1.5, 2.5, 3.5, 4.5 });

            Assert.That(table.RowCount, Is.EqualTo(4), "Should have 4 rows");
            Assert.That(table.GetColumnType("values"), Is.EqualTo(ColumnType.Numeric), "Should be numeric type");

            Assert.That(Math.Abs(table.Sum("values") - 12.0), Is.LessThan(0.001), "Sum should be 12");
            Assert.That(Math.Abs(table.Mean("values") - 3.0), Is.LessThan(0.001), "Mean should be 3");
            Assert.That(Math.Abs(table.Min("values") - 1.5), Is.LessThan(0.001), "Min should be 1.5");
            Assert.That(Math.Abs(table.Max("values") - 4.5), Is.LessThan(0.001), "Max should be 4.5");

            Assert.That(table.RemoveColumn("values"), Is.True, "Should remove column");
            Assert.That(table.HasColumn("values"), Is.False, "Column should be removed");

            CleanupDb(dbPath);
        }

        [Test]
        public void RowOperations()
        {
            var dbPath = GetTestDbPath("row_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);
            var table = store.CreateTabular("row-test");

            table.AddNumericColumn("id", new double[] { 1, 2, 3 });
            table.AddStringColumn("name", new[] { "A", "B", "C" });

            Assert.That(table.RowCount, Is.EqualTo(3), "Should have 3 rows");

            table.AddRow(new Dictionary<string, object>
            {
                { "id", 4.0 },
                { "name", "D" }
            });
            Assert.That(table.RowCount, Is.EqualTo(4), "Should have 4 rows after insert");

            var row = table.GetRow(3);
            Assert.That(row, Is.Not.Null, "Row should exist");
            Assert.That(Convert.ToDouble(row["id"]), Is.EqualTo(4.0), "ID should be 4");
            Assert.That(row["name"].ToString(), Is.EqualTo("D"), "Name should be D");

            table.UpdateRow(3, new Dictionary<string, object> { { "name", "D-Updated" } });
            row = table.GetRow(3);
            Assert.That(row["name"].ToString(), Is.EqualTo("D-Updated"), "Name should be updated");

            Assert.That(table.DeleteRow(3), Is.True, "Should delete row");
            Assert.That(table.RowCount, Is.EqualTo(3), "Should have 3 rows after delete");

            var newRows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object> { { "id", 5.0 }, { "name", "E" } },
                new Dictionary<string, object> { { "id", 6.0 }, { "name", "F" } }
            };
            var count = table.AddRows(newRows);
            Assert.That(count, Is.EqualTo(2), "Should insert 2 rows");
            Assert.That(table.RowCount, Is.EqualTo(5), "Should have 5 rows");

            CleanupDb(dbPath);
        }

        [Test]
        public void QueryOperations()
        {
            var dbPath = GetTestDbPath("query_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);
            var table = store.CreateTabular("query-test");

            table.AddNumericColumn("price", new double[] { 10, 25, 15, 30, 5 });
            table.AddStringColumn("product", new[] { "Apple", "Banana", "Cherry", "Date", "Elderberry" });
            table.AddNumericColumn("quantity", new double[] { 100, 50, 75, 25, 200 });

            var indices = table.Where("price", QueryOp.Gt, 15.0);
            Assert.That(indices.Length, Is.EqualTo(2), "Should find 2 rows with price > 15");

            var results = table.Query()
                .WhereGreaterThan("price", 10)
                .OrderBy("price")
                .ToDictionaries();

            Assert.That(results.Count, Is.EqualTo(3), "Should have 3 results");
            Assert.That(Convert.ToDouble(results[0]["price"]), Is.EqualTo(15), "First should be price 15");

            var avgPrice = table.Query()
                .WhereGreaterThan("quantity", 50)
                .Average("price");
            Assert.That(avgPrice, Is.GreaterThan(0), "Average should be positive");

            var rowCount = table.Query()
                .WhereLessThan("price", 20)
                .Count();
            Assert.That(rowCount, Is.EqualTo(3), "Should count 3 rows");

            CleanupDb(dbPath);
        }

        [Test]
        public void CsvImportExport()
        {
            var dbPath = GetTestDbPath("csv_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);
            var table = store.CreateTabular("csv-test");

            var csvContent = @"Name,Age,Score
Alice,30,95.5
Bob,25,88.0
Carol,35,92.3
Dave,28,85.0";

            table.ImportFromCsv(csvContent);

            Assert.That(table.RowCount, Is.EqualTo(4), "Should have 4 rows");
            Assert.That(table.ColumnCount, Is.EqualTo(3), "Should have 3 columns");

            var ages = table.GetNumericColumnRaw("Age");
            Assert.That(Math.Abs(ages[0] - 30), Is.LessThan(0.001), "First age should be 30");

            var exported = table.ExportToCsv();
            Assert.That(exported.Contains("Alice"), Is.True, "Export should contain Alice");
            Assert.That(exported.Contains("95.5"), Is.True, "Export should contain 95.5");

            CleanupDb(dbPath);
        }

        [Test]
        public void TransactionSupport()
        {
            var dbPath = GetTestDbPath("transaction_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);

            store.ExecuteInTransaction(() =>
            {
                var table = store.CreateTabular("tx-test");
                table.AddNumericColumn("value", new double[] { 1, 2, 3 });
            });

            Assert.That(store.TabularExists("tx-test"), Is.True, "Table should exist after successful transaction");

            var loaded = store.GetTabular("tx-test");
            Assert.That(loaded.RowCount, Is.EqualTo(3), "Should have 3 rows");

            CleanupDb(dbPath);
        }

        [Test]
        public void LargeDataset()
        {
            var dbPath = GetTestDbPath("large_test.db");
            CleanupDb(dbPath);

            using var store = DataStoreFactory.CreateLiteDb(dbPath);
            var table = store.CreateTabular("large-test");

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

            Assert.That(table.RowCount, Is.EqualTo(size), $"Should have {size} rows");

            var highValues = table.Query()
                .WhereGreaterThan("value", 500)
                .Limit(100)
                .Count();

            Assert.That(highValues, Is.LessThanOrEqualTo(100), "Should respect limit");

            CleanupDb(dbPath);
        }

        [Test]
        public void MemoryBackend()
        {
            using var store = DataStoreFactory.CreateMemory();

            Assert.That(store.StorageBackend, Is.EqualTo(StorageBackend.Memory), "Backend should be Memory");

            var table = store.CreateTabular("memory-test");
            table.AddNumericColumn("x", new double[] { 1, 2, 3, 4, 5 });
            table.AddNumericColumn("y", new double[] { 2, 4, 6, 8, 10 });

            Assert.That(table.RowCount, Is.EqualTo(5), "Should have 5 rows");
            Assert.That(table.Mean("x"), Is.EqualTo(3), "Mean of x should be 3");
            Assert.That(table.Sum("y"), Is.EqualTo(30), "Sum of y should be 30");

            var results = table.Query()
                .WhereGreaterThan("x", 2)
                .ToDictionaries();

            Assert.That(results.Count, Is.EqualTo(3), "Should find 3 rows where x > 2");

            var graph = store.CreateGraph("memory-graph");
            graph.AddNode("A");
            graph.AddNode("B");
            graph.AddNode("C");
            graph.AddEdge("A", "B");
            graph.AddEdge("B", "C");

            Assert.That(graph.NodeCount, Is.EqualTo(3), "Should have 3 nodes");
            Assert.That(graph.EdgeCount, Is.EqualTo(2), "Should have 2 edges");
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

        #endregion
    }
}
