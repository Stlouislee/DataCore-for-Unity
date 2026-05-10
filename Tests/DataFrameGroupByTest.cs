using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Microsoft.Data.Analysis;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// DataFrame GroupBy功能测试
    /// NOTE: This file overlaps with GroupByTest.cs — both test GroupBy/Sum/Mean/Count
    /// on similar data. This file additionally covers Min, Max, and multi-column aggregation.
    /// See also: issue #73.
    /// </summary>
    public class DataFrameGroupByTest : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
                TestGroupByFunctionality();
        }

        public void TestGroupByFunctionality()
        {
            Debug.Log("=== DataFrame GroupBy Functionality Test ===");

            var df = new DataFrame();
            df.Columns.Add(new StringDataFrameColumn("category", new string[] { "A", "B", "A", "B", "A" }));
            df.Columns.Add(new DoubleDataFrameColumn("value", new double[] { 10, 20, 30, 40, 50 }));
            df.Columns.Add(new DoubleDataFrameColumn("score", new double[] { 1, 2, 3, 4, 5 }));

            Assert.AreEqual(5, (int)df.Rows.Count);

            var categoryColumn = df["category"];
            var groupBy = df.GroupBy(categoryColumn);

            var valueColumn = df["value"];

            // --- Sum: A = 10+30+50 = 90, B = 20+40 = 60 ---
            var sumResult = groupBy.Sum(valueColumn);
            Assert.AreEqual(2, (int)sumResult.Rows.Count, "Sum should have 2 groups");
            VerifyGroupByValues(sumResult, "value", 90.0, 60.0, "Sum");

            // --- Mean: A = (10+30+50)/3 = 30, B = (20+40)/2 = 30 ---
            var meanResult = groupBy.Mean(valueColumn);
            Assert.AreEqual(2, (int)meanResult.Rows.Count, "Mean should have 2 groups");
            VerifyGroupByValues(meanResult, "value", 30.0, 30.0, "Mean");

            // --- Count: A = 3, B = 2 ---
            var countResult = groupBy.Count(valueColumn);
            Assert.AreEqual(2, (int)countResult.Rows.Count, "Count should have 2 groups");
            VerifyGroupByValues(countResult, "value", 3.0, 2.0, "Count");

            // --- Min: A = 10, B = 20 ---
            var minResult = groupBy.Min(valueColumn);
            Assert.AreEqual(2, (int)minResult.Rows.Count, "Min should have 2 groups");
            VerifyGroupByValues(minResult, "value", 10.0, 20.0, "Min");

            // --- Max: A = 50, B = 40 ---
            var maxResult = groupBy.Max(valueColumn);
            Assert.AreEqual(2, (int)maxResult.Rows.Count, "Max should have 2 groups");
            VerifyGroupByValues(maxResult, "value", 50.0, 40.0, "Max");

            // --- Multi-column sum: value and score ---
            var scoreColumn = df["score"];
            var multiResult = groupBy.Sum(valueColumn, scoreColumn);
            Assert.AreEqual(2, (int)multiResult.Rows.Count, "Multi-column sum should have 2 groups");
            Assert.IsTrue(multiResult.Columns.Count >= 3, "Multi-column result should have at least 3 columns");
            VerifyGroupByValues(multiResult, "value", 90.0, 60.0, "Multi-Sum(value)");
            VerifyGroupByValues(multiResult, "score", 9.0, 6.0, "Multi-Sum(score)");

            Debug.Log("✅ GroupBy functionality test passed!");
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
            TestGroupByFunctionality();
        }

        /// <summary>
        /// 模拟Session用于测试
        /// </summary>
        private class MockSession : ISession
        {
            private readonly Dictionary<string, DataFrame> _dataFrames = new();
            private readonly Dictionary<string, IDataSet> _datasets = new();

            public void AddDataset(string name, DataFrame df)
            {
                _dataFrames[name] = df;
                // Also register as IDataSet so DatasetCount/DatasetNames/HasDataset work
                _datasets[name] = new DataFrameAdapter(name, df);
            }

            public DataFrame GetDataFrame(string name)
            {
                return _dataFrames[name];
            }

            // 实现ISession接口的必需方法
            public string Id => "mock-session";
            public string Name => "Mock Session";
            public DateTime CreatedAt => DateTime.Now;
            public DateTime LastActivityAt => DateTime.Now;
            public int DatasetCount => _datasets.Count;
            public IReadOnlyCollection<string> DatasetNames => _datasets.Keys.ToList();
            
            public IDataSet OpenDataset(string name, string copyName = null) => throw new NotImplementedException();
            public IDataSet CreateDataset(string name, DataSetKind kind) => throw new NotImplementedException();
            public IDataSet GetDataset(string name) => _datasets.TryGetValue(name, out var ds) ? ds : throw new KeyNotFoundException();
            public bool HasDataset(string name) => _datasets.ContainsKey(name);
            public bool RemoveDataset(string name)
            {
                _dataFrames.Remove(name);
                return _datasets.Remove(name);
            }
            public IDataSet SaveQueryResult(string sourceName, Func<IDataSet, IDataSet> query, string newName) => throw new NotImplementedException();
            public bool PersistDataset(string name, string targetName = null) => throw new NotImplementedException();
            public void Clear()
            {
                _dataFrames.Clear();
                _datasets.Clear();
            }
            public void Touch() { }
            public void Dispose() { }
        }
    }
}
