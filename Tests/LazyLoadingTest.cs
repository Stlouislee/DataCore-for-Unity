using NUnit.Framework;
using UnityEngine;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// LiteDB 后端测试 - 测试数据集的创建、访问和删除
    /// Migrated from MonoBehaviour to NUnit for CI/CD compatibility.
    /// Uses DataCoreStore directly instead of scene-dependent DataCoreEditorComponent.
    /// </summary>
    [TestFixture]
    public class LazyLoadingTest
    {
        [Test]
        public void TestLiteDbBackend()
        {
            using var store = new DataCoreStore();

            // 测试1：检查Names属性是否正常工作
            Assert.IsNotNull(store.Names, "Store.Names should not be null");
            Debug.Log($"Total datasets: {store.Names.Count}");

            // 测试2：创建和访问数据集
            var testName = "lazy-load-test";
            if (!store.HasDataset(testName))
            {
                var tabular = store.CreateTabular(testName);
                tabular.AddNumericColumn("x", new double[] { 1, 2, 3 });
                tabular.AddStringColumn("name", new[] { "a", "b", "c" });
                Assert.AreEqual(3, tabular.RowCount, "Created dataset should have 3 rows");
                Assert.AreEqual(2, tabular.ColumnCount, "Created dataset should have 2 columns");
                Debug.Log($"Created test dataset: {testName}");
            }

            // 测试3：访问数据集
            Assert.IsTrue(store.HasDataset(testName), "Store should contain the test dataset");
            Assert.IsTrue(store.TryGet(testName, out var testDataset), "TryGet should succeed");
            Assert.AreEqual(DataSetKind.Tabular, testDataset.Kind);

            if (testDataset is ITabularDataset tabularData)
            {
                Assert.AreEqual(3, tabularData.RowCount);
                Assert.AreEqual(2, tabularData.ColumnCount);
            }

            // 测试4：检查点
            store.Checkpoint();
            Debug.Log("Checkpoint completed successfully");

            // 测试5：删除测试数据集
            Assert.IsTrue(store.Delete(testName), "Delete should succeed");
            Assert.IsFalse(store.HasDataset(testName), "Dataset should be deleted");

            Debug.Log("LiteDB backend test completed");
        }
    }
}
