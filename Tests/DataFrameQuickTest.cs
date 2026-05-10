using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Microsoft.Data.Analysis;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// DataFrame快速测试
    /// Migrated from MonoBehaviour to NUnit for CI/CD compatibility.
    /// </summary>
    [TestFixture]
    public class DataFrameQuickTest
    {
        [Test]
        public void RunQuickTest()
        {
            Debug.Log("=== DataFrame Quick Test ===");

            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;
                var session = sessionManager.CreateSession("QuickTest");

                var df = session.CreateDataFrame("TestDF");
                df.Columns.Add(new DoubleDataFrameColumn("values", new double[] { 1, 2, 3, 4, 5 }));
                df.Columns.Add(new StringDataFrameColumn("categories", new string[] { "A", "B", "A", "B", "A" }));

                Assert.That(df.Rows.Count, Is.EqualTo(5), "DataFrame should have 5 rows");
                Assert.That(df.Columns.Count, Is.EqualTo(2), "DataFrame should have 2 columns");

                var result = session.QueryDataFrame("TestDF")
                    .Where("values", SessionDataFrameQueryBuilder.ComparisonOp.Gt, 2)
                    .Execute("FilteredResult");

                Assert.That(result, Is.Not.Null, "Query result should not be null");
                Assert.That(result.Name, Is.EqualTo("FilteredResult"));

                if (result is DataFrameAdapter adapter)
                {
                    Assert.That(adapter.RowCount, Is.EqualTo(3), "Filtered result should have 3 rows (values 3,4,5)");
                    Assert.That(adapter.ColumnCount, Is.EqualTo(2), "Filtered result should have 2 columns");

                    var tabular = adapter.ToTabularData();
                    Assert.That(tabular.RowCount, Is.EqualTo(3), "Tabular conversion should preserve row count");
                }
                else
                {
                    Assert.Fail("Query result should be a DataFrameAdapter");
                }

                Debug.Log("✅ DataFrame quick test passed!");
            }
            finally
            {
                store.Dispose();
            }
        }
    }
}
