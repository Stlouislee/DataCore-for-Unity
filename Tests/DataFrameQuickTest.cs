using System;
using System.Linq;
using UnityEngine;
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
            try
            {
                Debug.Log("=== DataFrame Quick Test ===");
                
                // 基本DataFrame创建
                var store = new DataCoreStore();
                var sessionManager = store.SessionManager;
                var session = sessionManager.CreateSession("QuickTest");

                // 创建DataFrame
                var df = session.CreateDataFrame("TestDF");
                df.Columns.Add(new DoubleDataFrameColumn("values", new double[] { 1, 2, 3, 4, 5 }));
                df.Columns.Add(new StringDataFrameColumn("categories", new string[] { "A", "B", "A", "B", "A" }));

                Debug.Log($"Created DataFrame with {df.Rows.Count} rows and {df.Columns.Count} columns");

                // 测试查询构建器
                var result = session.QueryDataFrame("TestDF")
                    .Where("values", SessionDataFrameQueryBuilder.ComparisonOp.Gt, 2)
                    .Execute("FilteredResult");

                Debug.Log($"Query result: {result.Name}");

                // 测试适配器
                if (result is DataFrameAdapter adapter)
                {
                    Debug.Log($"Adapter row count: {adapter.RowCount}");
                    Debug.Log($"Adapter column count: {adapter.ColumnCount}");

                    // 转换为TabularData
                    var tabular = adapter.ToTabularData();
                    Debug.Log($"Tabular conversion: {tabular.RowCount} rows");
                }

                Debug.Log("✅ DataFrame quick test passed!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ DataFrame test failed: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        [ContextMenu("Run Quick Test")]
        private void RunQuickTestMenu()
        {
            RunQuickTest();
        }
    }
}