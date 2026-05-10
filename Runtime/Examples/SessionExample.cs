using UnityEngine;
using System;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Examples
{
    /// <summary>
    /// Session使用示例
    /// </summary>
    public class SessionExample
    {
        public static void RunExample()
        {
            // 创建数据核心存储
            using var store = new DataCoreStore();

            // 创建会话
            var session = store.SessionManager.CreateSession("MyAnalysisSession");
            Debug.Log($"Created session: {session.Name} (ID: {session.Id})");

            // 在会话中创建新的数据集
            var tabular = session.CreateDataset("SalesData", DataSetKind.Tabular);
            Debug.Log($"Created dataset in session: {tabular.Name}");

            // 从全局存储打开数据集到会话（创建副本）
            // 首先在全局存储中创建一个数据集
            var globalData = store.CreateTabular("GlobalData");
            var copy = session.OpenDataset("GlobalData", "LocalCopy");
            Debug.Log($"Opened dataset copy in session: {copy.Name}");

            // 检查会话中的数据集
            Debug.Log($"Session has {session.DatasetCount} datasets:");
            foreach (var name in session.DatasetNames)
            {
                Debug.Log($"  - {name}");
            }

            // 执行查询并保存结果
            var queryResult = session.SaveQueryResult("LocalCopy", dataset =>
            {
                // 这里应该执行实际查询，现在只是示例
                return dataset.WithName("QueryResult");
            }, "FilteredData");
            Debug.Log($"Saved query result: {queryResult.Name}");

            // 持久化数据集到全局存储
            try
            {
                var persisted = session.PersistDataset("SalesData", "PersistedSalesData");
                Debug.Log($"Persisted dataset: {persisted}");
            }
            catch (NotImplementedException)
            {
                Debug.Log("Persisting datasets requires implementation");
            }

            // 从会话中移除数据集
            session.RemoveDataset("LocalCopy");
            Debug.Log($"Removed dataset from session");

            // 检查会话统计
            var stats = store.SessionManager.GetStatistics();
            Debug.Log($"Session statistics: {stats.TotalSessions} sessions, {stats.TotalDatasets} total datasets");

            Debug.Log("Cleaned up resources");
        }
    }
}