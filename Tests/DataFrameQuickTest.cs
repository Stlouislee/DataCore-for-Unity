using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Microsoft.Data.Analysis;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// DataFrame快速测试
    /// </summary>
    public class DataFrameQuickTest : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
                RunQuickTest();
        }

        public void RunQuickTest()
        {
            Debug.Log("=== DataFrame Quick Test ===");

            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;
            var session = sessionManager.CreateSession("QuickTest");

            var df = session.CreateDataFrame("TestDF");
            df.Columns.Add(new DoubleDataFrameColumn("values", new double[] { 1, 2, 3, 4, 5 }));
            df.Columns.Add(new StringDataFrameColumn("categories", new string[] { "A", "B", "A", "B", "A" }));

            Assert.AreEqual(5, (int)df.Rows.Count, "DataFrame should have 5 rows");
            Assert.AreEqual(2, (int)df.Columns.Count, "DataFrame should have 2 columns");

            var result = session.QueryDataFrame("TestDF")
                .Where("values", SessionDataFrameQueryBuilder.ComparisonOp.Gt, 2)
                .Execute("FilteredResult");

            Assert.IsNotNull(result, "Query result should not be null");
            Assert.AreEqual("FilteredResult", result.Name);

            if (result is DataFrameAdapter adapter)
            {
                Assert.AreEqual(3, adapter.RowCount, "Filtered result should have 3 rows (values 3,4,5)");
                Assert.AreEqual(2, adapter.ColumnCount, "Filtered result should have 2 columns");

                var tabular = adapter.ToTabularData();
                Assert.AreEqual(3, tabular.RowCount, "Tabular conversion should preserve row count");
            }
            else
            {
                Assert.Fail("Query result should be a DataFrameAdapter");
            }

            store.Dispose();
            Debug.Log("✅ DataFrame quick test passed!");
        }

        [ContextMenu("Run Quick Test")]
        private void RunQuickTestMenu()
        {
            RunQuickTest();
        }
    }
}