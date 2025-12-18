using System;
using System.Linq;
using UnityEngine;
using Microsoft.Data.Analysis;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// DataFrame GroupBy功能测试
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
            try
            {
                Debug.Log("=== DataFrame GroupBy Functionality Test ===");
                
                // 创建测试DataFrame
                var df = new DataFrame();
                df.Columns.Add(new StringDataFrameColumn("category", new string[] { "A", "B", "A", "B", "A" }));
                df.Columns.Add(new DoubleDataFrameColumn("value", new double[] { 10, 20, 30, 40, 50 }));
                df.Columns.Add(new DoubleDataFrameColumn("score", new double[] { 1, 2, 3, 4, 5 }));
                
                Debug.Log($"Original DataFrame: {df.Rows.Count} rows");
                Debug.Log("Columns: " + string.Join(", ", df.Columns.Select(c => c.Name)));

                // 测试直接调用GroupBy
                try
                {
                    var categoryColumn = df["category"];
                    var groupBy = df.GroupBy(categoryColumn);
                    Debug.Log("✅ GroupBy method exists and works!");
                    
                    // 测试Sum聚合
                    var valueColumn = df["value"];
                    var sumResult = groupBy.Sum(valueColumn);
                    Debug.Log($"✅ Sum aggregation: {sumResult.Rows.Count} groups");
                    Debug.Log("Sum result columns: " + string.Join(", ", sumResult.Columns.Select(c => c.Name)));
                    
                    // 测试Mean聚合
                    var meanResult = groupBy.Mean(valueColumn);
                    Debug.Log($"✅ Mean aggregation: {meanResult.Rows.Count} groups");
                    
                    // 测试Count聚合
                    var countResult = groupBy.Count(valueColumn);
                    Debug.Log($"✅ Count aggregation: {countResult.Rows.Count} groups");
                    
                    // 测试Min聚合
                    var minResult = groupBy.Min(valueColumn);
                    Debug.Log($"✅ Min aggregation: {minResult.Rows.Count} groups");
                    
                    // 测试Max聚合
                    var maxResult = groupBy.Max(valueColumn);
                    Debug.Log($"✅ Max aggregation: {maxResult.Rows.Count} groups");
                    
                    // 测试多个列聚合
                    var scoreColumn = df["score"];
                    var multiResult = groupBy.Sum(valueColumn, scoreColumn);
                    Debug.Log($"✅ Multi-column aggregation: {multiResult.Rows.Count} groups");
                    Debug.Log("Multi-column result columns: " + string.Join(", ", multiResult.Columns.Select(c => c.Name)));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ Direct GroupBy failed: {ex.Message}");
                    Debug.LogError($"Stack trace: {ex.StackTrace}");
                }

                // 测试SessionDataFrameQueryBuilder的GroupBy
                try
                {
                    // 创建模拟Session
                    var mockSession = new MockSession();
                    mockSession.AddDataset("test", df);
                    
                    var queryBuilder = new SessionDataFrameQueryBuilder(mockSession, "test");
                    
                    // 构建GroupBy查询
                    var resultDataSet = queryBuilder
                        .GroupBy("category", 
                            ("value", AggregateFunction.Sum),
                            ("score", AggregateFunction.Average))
                        .Execute("grouped_result");
                    
                    Debug.Log($"✅ SessionDataFrameQueryBuilder GroupBy succeeded!");
                    Debug.Log($"Result dataset name: {resultDataSet.Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ QueryBuilder GroupBy failed: {ex.Message}");
                    Debug.LogError($"Stack trace: {ex.StackTrace}");
                }

                Debug.Log("GroupBy functionality test completed");
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
            TestGroupByFunctionality();
        }

        /// <summary>
        /// 模拟Session用于测试
        /// </summary>
        private class MockSession : ISession
        {
            private readonly Dictionary<string, DataFrame> _dataFrames = new();

            public void AddDataset(string name, DataFrame df)
            {
                _dataFrames[name] = df;
            }

            public DataFrame GetDataFrame(string name)
            {
                return _dataFrames[name];
            }

            // 实现ISession接口的必需方法
            public string Id => "mock-session";
            public string Name => "Mock Session";
            public IReadOnlyCollection<string> DatasetNames => _dataFrames.Keys.ToList();
            public IDataSet GetDataset(string name) => throw new NotImplementedException();
            public IDataSet CreateDataset(string name, IDataSet sourceDataset) => throw new NotImplementedException();
            public IDataSet CreateDataset(string name, TabularData tabularData) => throw new NotImplementedException();
            public IDataSet CreateDataset(string name, GraphData graphData) => throw new NotImplementedException();
            public bool RemoveDataset(string name) => throw new NotImplementedException();
            public bool ContainsDataset(string name) => _dataFrames.ContainsKey(name);
            public IDataSet ExecuteDataFrameQuery(string sourceName, Func<DataFrame, DataFrame> query, string resultName) => throw new NotImplementedException();
            public DataFrame CreateDataFrame(string name, TabularData tabularData) => throw new NotImplementedException();
            public DataFrame ConvertToDataFrame(IDataSet dataset) => throw new NotImplementedException();
            public void PersistDataset(string name) => throw new NotImplementedException();
            public event EventHandler<DataCoreEventArgs> SessionEvent;
        }
    }
}