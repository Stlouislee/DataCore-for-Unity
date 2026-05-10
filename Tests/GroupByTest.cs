using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Microsoft.Data.Analysis;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// GroupBy API测试
    /// NOTE: This file overlaps with DataFrameGroupByTest.cs — both test GroupBy/Sum/Mean/Count
    /// on the same data shape. DataFrameGroupByTest additionally covers Min/Max and multi-column.
    /// See also: issue #73.
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

            // --- Sum: A = 10+30+50 = 90, B = 20+40 = 60 ---
            var sumResult = groupBy.Sum(valueColumn);
            Assert.AreEqual(2, (int)sumResult.Rows.Count, "Sum should produce 2 groups");
            VerifyGroupByValues(sumResult, "value", 90.0, 60.0, "Sum");

            // --- Mean: A = (10+30+50)/3 = 30, B = (20+40)/2 = 30 ---
            var meanResult = groupBy.Mean(valueColumn);
            Assert.AreEqual(2, (int)meanResult.Rows.Count, "Mean should produce 2 groups");
            VerifyGroupByValues(meanResult, "value", 30.0, 30.0, "Mean");

            // --- Count: A = 3, B = 2 ---
            var countResult = groupBy.Count(valueColumn);
            Assert.AreEqual(2, (int)countResult.Rows.Count, "Count should produce 2 groups");
            VerifyGroupByValues(countResult, "value", 3.0, 2.0, "Count");

            Debug.Log("✅ GroupBy test passed!");
        }

        /// <summary>
        /// Verifies that a GroupBy result contains the expected values for groups A and B.
        /// Order-independent: finds the row for each group key, then checks the aggregated value.
        /// </summary>
        private static void VerifyGroupByValues(DataFrame result, string valueColName,
            double expectedA, double expectedB, string operation)
        {
            var keys = result.Columns[0]; // first column is the group key
            var values = result.Columns[valueColName];

            double actualA = double.NaN, actualB = double.NaN;
            for (int i = 0; i < result.Rows.Count; i++)
            {
                string key = keys[i]?.ToString();
                if (key == "A") actualA = Convert.ToDouble(values[i]);
                else if (key == "B") actualB = Convert.ToDouble(values[i]);
            }

            Assert.IsFalse(double.IsNaN(actualA), $"{operation}: group A not found");
            Assert.IsFalse(double.IsNaN(actualB), $"{operation}: group B not found");
            Assert.AreEqual(expectedA, actualA, 0.001, $"{operation}: expected A={expectedA}, got {actualA}");
            Assert.AreEqual(expectedB, actualB, 0.001, $"{operation}: expected B={expectedB}, got {actualB}");
        }

        [ContextMenu("Run GroupBy Test")]
        private void RunGroupByTestMenu()
        {
            TestGroupByAPI();
        }
    }
}
