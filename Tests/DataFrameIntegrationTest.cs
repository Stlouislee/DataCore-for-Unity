using System;
using System.Linq;
using UnityEngine;
using Microsoft.Data.Analysis;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// DataFrame集成测试
    /// </summary>
    public class DataFrameIntegrationTest : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool logToConsole = true;

        private void Start()
        {
            if (runOnStart)
                RunTests();
        }

        public void RunTests()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== DataFrame Integration Test ===");

            try
            {
                TestDataFrameCreation(sb);
                TestDataFrameQuery(sb);
                TestDataFrameAdapter(sb);
                TestDataFrameConversion(sb);
                sb.AppendLine("✅ All DataFrame tests passed!");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Test failed: {ex.Message}");
                sb.AppendLine($"Stack trace: {ex.StackTrace}");
            }

            var result = sb.ToString();
            if (logToConsole)
                Debug.Log(result);
        }

        private static void TestDataFrameCreation(System.Text.StringBuilder sb)
        {
            sb.AppendLine("Testing DataFrame Creation...");
            
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;
            var session = sessionManager.CreateSession("DataFrameTest");

            // 创建DataFrame
            var df = session.CreateDataFrame("TestDataFrame");
            
            // 添加列
            df.Columns.Add(new DoubleDataFrameColumn("x", new double[] { 1, 2, 3, 4, 5 }));
            df.Columns.Add(new StringDataFrameColumn("s", new string[] { "a", "b", "c", "d", "e" }));
            df.Columns.Add(new BooleanDataFrameColumn("b", new bool[] { true, false, true, false, true }));

            // 验证DataFrame
            if (df.Rows.Count != 5) throw new Exception($"Expected 5 rows, got {df.Rows.Count}");
            if (df.Columns.Count != 3) throw new Exception($"Expected 3 columns, got {df.Columns.Count}");

            sb.AppendLine("✅ DataFrame creation OK");
        }

        private static void TestDataFrameQuery(System.Text.StringBuilder sb)
        {
            sb.AppendLine("Testing DataFrame Query...");
            
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;
            var session = sessionManager.CreateSession("QueryTest");

            // 创建测试DataFrame
            var df = session.CreateDataFrame("QuerySource");
            df.Columns.Add(new DoubleDataFrameColumn("value", new double[] { 10, 20, 30, 40, 50 }));
            df.Columns.Add(new StringDataFrameColumn("category", new string[] { "A", "B", "A", "B", "A" }));

            // 执行查询
            var result = session.QueryDataFrame("QuerySource")
                .Where("value", SessionDataFrameQueryBuilder.ComparisonOp.Gt, 25)
                .Execute("QueryResult");

            // 验证结果
            if (result.Name != "QueryResult") throw new Exception("Query result name incorrect");
            
            var resultDf = session.GetDataFrame("QueryResult");
            if (resultDf.Rows.Count != 3) throw new Exception($"Expected 3 rows after filtering, got {resultDf.Rows.Count}");

            sb.AppendLine("✅ DataFrame query OK");
        }

        private static void TestDataFrameAdapter(System.Text.StringBuilder sb)
        {
            sb.AppendLine("Testing DataFrame Adapter...");
            
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;
            var session = sessionManager.CreateSession("AdapterTest");

            // 创建DataFrame
            var df = session.CreateDataFrame("AdapterSource");
            df.Columns.Add(new DoubleDataFrameColumn("numeric", new double[] { 1, 2, 3 }));
            df.Columns.Add(new StringDataFrameColumn("text", new string[] { "x", "y", "z" }));

            // 执行查询并获取适配器
            var result = session.ExecuteDataFrameQuery("AdapterSource", 
                sourceDf => sourceDf.Filter(sourceDf["numeric"] > 1), 
                "AdapterResult");

            // 验证适配器
            if (!(result is DataFrameAdapter adapter)) throw new Exception("Result is not DataFrameAdapter");
            if (adapter.Name != "AdapterResult") throw new Exception("Adapter name incorrect");
            if (adapter.RowCount != 2) throw new Exception($"Expected 2 rows, got {adapter.RowCount}");

            // 转换为TabularData
            var tabular = adapter.ToTabularData();
            if (tabular.RowCount != 2) throw new Exception($"Tabular conversion failed: expected 2 rows, got {tabular.RowCount}");

            sb.AppendLine("✅ DataFrame adapter OK");
        }

        private static void TestDataFrameConversion(System.Text.StringBuilder sb)
        {
            sb.AppendLine("Testing DataFrame Conversion...");
            
            var store = new DataCoreStore();
            var sessionManager = store.SessionManager;
            var session = sessionManager.CreateSession("ConversionTest");

            // 创建TabularData
            var tabular = session.CreateDataset("TabularSource", DataSetKind.Tabular) as Tabular.TabularData;
            tabular.AddNumericColumn("x", NumSharp.np.array(new double[] { 1, 2, 3 }));
            tabular.AddStringColumn("s", new string[] { "a", "b", "c" });

            // 转换为DataFrame
            var df = session.ConvertToDataFrame("TabularSource");
            
            // 验证转换
            if (df.Rows.Count != 3) throw new Exception($"Expected 3 rows, got {df.Rows.Count}");
            if (df.Columns.Count != 2) throw new Exception($"Expected 2 columns, got {df.Columns.Count}");

            // 转换回TabularData
            var convertedTabular = DataFrameConverter.DataFrameToTabular(df, "ConvertedTabular");
            if (convertedTabular.RowCount != 3) throw new Exception($"Back conversion failed: expected 3 rows, got {convertedTabular.RowCount}");

            sb.AppendLine("✅ DataFrame conversion OK");
        }

        [ContextMenu("Run DataFrame Tests")]
        private void RunTestsMenu()
        {
            RunTests();
        }
    }
}