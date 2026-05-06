using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
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
            Debug.Log("=== GroupBy API Test ===");

            var df = new DataFrame();
            df.Columns.Add(new StringDataFrameColumn("category", new string[] { "A", "B", "A", "B", "A" }));
            df.Columns.Add(new DoubleDataFrameColumn("value", new double[] { 10, 20, 30, 40, 50 }));

            Assert.AreEqual(5, (int)df.Rows.Count);

            var categoryColumn = df["category"];
            var groupBy = df.GroupBy(categoryColumn);

            var valueColumn = df["value"];
            var sumResult = groupBy.Sum(valueColumn);
            Assert.AreEqual(2, (int)sumResult.Rows.Count, "Sum should produce 2 groups");

            var meanResult = groupBy.Mean(valueColumn);
            Assert.AreEqual(2, (int)meanResult.Rows.Count, "Mean should produce 2 groups");

            var countResult = groupBy.Count(valueColumn);
            Assert.AreEqual(2, (int)countResult.Rows.Count, "Count should produce 2 groups");

            Debug.Log("✅ GroupBy test passed!");
        }

        [ContextMenu("Run GroupBy Test")]
        private void RunGroupByTestMenu()
        {
            TestGroupByAPI();
        }
    }
}