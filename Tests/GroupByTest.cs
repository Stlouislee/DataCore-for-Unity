using System;
using System.Linq;
using UnityEngine;
using Microsoft.Data.Analysis;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// GroupBy API测试
    /// </summary>
    public class GroupByTest : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
                TestGroupByAPI();
        }

        public void TestGroupByAPI()
        {
            try
            {
                Debug.Log("=== GroupBy API Test ===");
                
                // 创建测试DataFrame
                var df = new DataFrame();
                df.Columns.Add(new StringDataFrameColumn("category", new string[] { "A", "B", "A", "B", "A" }));
                df.Columns.Add(new DoubleDataFrameColumn("value", new double[] { 10, 20, 30, 40, 50 }));
                
                Debug.Log($"Original DataFrame: {df.Rows.Count} rows");

                // 测试GroupBy方法是否存在
                var groupByMethods = typeof(DataFrame).GetMethods()
                    .Where(m => m.Name == "GroupBy")
                    .ToList();
                
                Debug.Log($"Found {groupByMethods.Count} GroupBy methods");
                
                foreach (var method in groupByMethods)
                {
                    Debug.Log($"GroupBy method: {method.Name}, Parameters: {string.Join(", ", method.GetParameters().Select(p => p.Name))}");
                }

                // 尝试调用GroupBy
                try
                {
                    var categoryColumn = df["category"];
                    var groupBy = df.GroupBy(categoryColumn);
                    Debug.Log("✅ GroupBy method exists and works!");
                    
                    // 测试聚合方法
                    var valueColumn = df["value"];
                    var sumResult = groupBy.Sum(valueColumn);
                    Debug.Log($"Sum result: {sumResult.Rows.Count} groups");
                    
                    var meanResult = groupBy.Mean(valueColumn);
                    Debug.Log($"Mean result: {meanResult.Rows.Count} groups");
                    
                    var countResult = groupBy.Count(valueColumn);
                    Debug.Log($"Count result: {countResult.Rows.Count} groups");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ GroupBy failed: {ex.Message}");
                    Debug.LogError($"Stack trace: {ex.StackTrace}");
                }

                Debug.Log("GroupBy test completed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Test failed: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        [ContextMenu("Run GroupBy Test")]
        private void RunGroupByTestMenu()
        {
            TestGroupByAPI();
        }
    }
}