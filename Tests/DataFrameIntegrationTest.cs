using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Microsoft.Data.Analysis;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// DataFrame集成测试
    /// Migrated from MonoBehaviour to NUnit for CI/CD compatibility.
    /// </summary>
    [TestFixture]
    public class DataFrameIntegrationTest
    {
        [Test]
        public void TestDataFrameCreation()
        {
            Debug.Log("Testing DataFrame Creation...");

            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;
                var session = sessionManager.CreateSession("DataFrameTest");

                var df = session.CreateDataFrame("TestDataFrame");

                df.Columns.Add(new DoubleDataFrameColumn("x", new double[] { 1, 2, 3, 4, 5 }));
                df.Columns.Add(new StringDataFrameColumn("s", new string[] { "a", "b", "c", "d", "e" }));
                df.Columns.Add(new BooleanDataFrameColumn("b", new bool[] { true, false, true, false, true }));

                Assert.That(df.Rows.Count, Is.EqualTo(5), "Expected 5 rows");
                Assert.That(df.Columns.Count, Is.EqualTo(3), "Expected 3 columns");

                Debug.Log("✅ DataFrame creation OK");
            }
            finally
            {
                store.Dispose();
            }
        }

        [Test]
        public void TestDataFrameQuery()
        {
            Debug.Log("Testing DataFrame Query...");

            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;
                var session = sessionManager.CreateSession("QueryTest");

                var df = session.CreateDataFrame("QuerySource");
                df.Columns.Add(new DoubleDataFrameColumn("value", new double[] { 10, 20, 30, 40, 50 }));
                df.Columns.Add(new StringDataFrameColumn("category", new string[] { "A", "B", "A", "B", "A" }));

                var result = session.QueryDataFrame("QuerySource")
                    .Where("value", SessionDataFrameQueryBuilder.ComparisonOp.Gt, 25)
                    .Execute("QueryResult");

                Assert.That(result.Name, Is.EqualTo("QueryResult"), "Query result name incorrect");

                var resultDf = session.GetDataFrame("QueryResult");
                Assert.That(resultDf.Rows.Count, Is.EqualTo(3), "Expected 3 rows after filtering");

                Debug.Log("✅ DataFrame query OK");
            }
            finally
            {
                store.Dispose();
            }
        }

        [Test]
        public void TestDataFrameAdapter()
        {
            Debug.Log("Testing DataFrame Adapter...");

            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;
                var session = sessionManager.CreateSession("AdapterTest");

                var df = session.CreateDataFrame("AdapterSource");
                df.Columns.Add(new DoubleDataFrameColumn("numeric", new double[] { 1, 2, 3 }));
                df.Columns.Add(new StringDataFrameColumn("text", new string[] { "x", "y", "z" }));

                var result = session.ExecuteDataFrameQuery("AdapterSource",
                    sourceDf => sourceDf.Filter(sourceDf["numeric"] > 1),
                    "AdapterResult");

                Assert.That(result, Is.InstanceOf<DataFrameAdapter>(), "Result is not DataFrameAdapter");
                var adapter = (DataFrameAdapter)result;
                Assert.That(adapter.Name, Is.EqualTo("AdapterResult"), "Adapter name incorrect");
                Assert.That(adapter.RowCount, Is.EqualTo(2), "Expected 2 rows");

                var tabular = adapter.ToTabularData();
                Assert.That(tabular.RowCount, Is.EqualTo(2), "Tabular conversion failed: expected 2 rows");

                Debug.Log("✅ DataFrame adapter OK");
            }
            finally
            {
                store.Dispose();
            }
        }

        [Test]
        public void TestDataFrameConversion()
        {
            Debug.Log("Testing DataFrame Conversion...");

            var store = new DataCoreStore();
            try
            {
                var sessionManager = store.SessionManager;
                var session = sessionManager.CreateSession("ConversionTest");

                var tabular = session.CreateDataset("TabularSource", DataSetKind.Tabular) as Tabular.TabularData;
                tabular.AddNumericColumn("x", new double[] { 1, 2, 3 });
                tabular.AddStringColumn("s", new string[] { "a", "b", "c" });

                var df = session.ConvertToDataFrame("TabularSource");

                Assert.That(df.Rows.Count, Is.EqualTo(3), "Expected 3 rows");
                Assert.That(df.Columns.Count, Is.EqualTo(2), "Expected 2 columns");

                var convertedTabular = DataFrameConverter.DataFrameToTabular(df, "ConvertedTabular");
                Assert.That(convertedTabular.RowCount, Is.EqualTo(3), "Back conversion failed: expected 3 rows");

                Debug.Log("✅ DataFrame conversion OK");
            }
            finally
            {
                store.Dispose();
            }
        }
    }
}
