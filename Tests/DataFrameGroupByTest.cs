using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Microsoft.Data.Analysis;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// DataFrame GroupBy功能测试
    /// Migrated from MonoBehaviour to NUnit for CI/CD compatibility.
    /// NOTE: This file overlaps with GroupByTest.cs — both test GroupBy/Sum/Mean/Count
    /// on similar data. This file additionally covers Min, Max, and multi-column aggregation.
    /// See also: issue #73.
    /// </summary>
    [TestFixture]
    public class DataFrameGroupByTest
    {
        [Test]
        public void TestGroupByFunctionality()
        {
            Debug.Log("=== DataFrame GroupBy Functionality Test ===");

            var df = new DataFrame();
            df.Columns.Add(new StringDataFrameColumn("category", new string[] { "A", "B", "A", "B", "A" }));
            df.Columns.Add(new DoubleDataFrameColumn("value", new double[] { 10, 20, 30, 40, 50 }));
            df.Columns.Add(new DoubleDataFrameColumn("score", new double[] { 1, 2, 3, 4, 5 }));

            Assert.That(df.Rows.Count, Is.EqualTo(5));

            var categoryColumn = df["category"];
            var groupBy = df.GroupBy(categoryColumn);

            var valueColumn = df["value"];

            // --- Sum: A = 10+30+50 = 90, B = 20+40 = 60 ---
            var sumResult = groupBy.Sum(valueColumn);
            Assert.That(sumResult.Rows.Count, Is.EqualTo(2), "Sum should have 2 groups");
            VerifyGroupByValues(sumResult, "value", 90.0, 60.0, "Sum");

            // --- Mean: A = (10+30+50)/3 = 30, B = (20+40)/2 = 30 ---
            var meanResult = groupBy.Mean(valueColumn);
            Assert.That(meanResult.Rows.Count, Is.EqualTo(2), "Mean should have 2 groups");
            VerifyGroupByValues(meanResult, "value", 30.0, 30.0, "Mean");

            // --- Count: A = 3, B = 2 ---
            var countResult = groupBy.Count(valueColumn);
            Assert.That(countResult.Rows.Count, Is.EqualTo(2), "Count should have 2 groups");
            VerifyGroupByValues(countResult, "value", 3.0, 2.0, "Count");

            // --- Min: A = 10, B = 20 ---
            var minResult = groupBy.Min(valueColumn);
            Assert.That(minResult.Rows.Count, Is.EqualTo(2), "Min should have 2 groups");
            VerifyGroupByValues(minResult, "value", 10.0, 20.0, "Min");

            // --- Max: A = 50, B = 40 ---
            var maxResult = groupBy.Max(valueColumn);
            Assert.That(maxResult.Rows.Count, Is.EqualTo(2), "Max should have 2 groups");
            VerifyGroupByValues(maxResult, "value", 50.0, 40.0, "Max");

            // --- Multi-column sum: value and score ---
            var scoreColumn = df["score"];
            var multiResult = groupBy.Sum(valueColumn, scoreColumn);
            Assert.That(multiResult.Rows.Count, Is.EqualTo(2), "Multi-column sum should have 2 groups");
            Assert.That(multiResult.Columns.Count, Is.GreaterThanOrEqualTo(3), "Multi-column result should have at least 3 columns");
            VerifyGroupByValues(multiResult, "value", 90.0, 60.0, "Multi-Sum(value)");
            VerifyGroupByValues(multiResult, "score", 9.0, 6.0, "Multi-Sum(score)");

            Debug.Log("✅ GroupBy functionality test passed!");
        }

        private static void VerifyGroupByValues(DataFrame result, string valueColName,
            double expectedA, double expectedB, string operation)
        {
            var keys = result.Columns[0];
            var values = result.Columns[valueColName];

            double actualA = double.NaN, actualB = double.NaN;
            for (int i = 0; i < result.Rows.Count; i++)
            {
                string key = keys[i]?.ToString();
                if (key == "A") actualA = Convert.ToDouble(values[i]);
                else if (key == "B") actualB = Convert.ToDouble(values[i]);
            }

            Assert.That(double.IsNaN(actualA), Is.False, $"{operation}: group A not found");
            Assert.That(double.IsNaN(actualB), Is.False, $"{operation}: group B not found");
            Assert.That(actualA, Is.EqualTo(expectedA).Within(0.001), $"{operation}: expected A={expectedA}, got {actualA}");
            Assert.That(actualB, Is.EqualTo(expectedB).Within(0.001), $"{operation}: expected B={expectedB}, got {actualB}");
        }
    }
}
