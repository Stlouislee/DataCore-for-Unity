using System;
using System.Collections.Generic;
using AroAro.DataCore.Events;

namespace AroAro.DataCore.Examples
{
    /// <summary>
    /// 事件监听示例
    /// </summary>
    public class EventListenerExample
    {
        public static void RunExample()
        {
            Console.WriteLine("=== 事件监听示例 ===");

            // 订阅事件
            DataCoreEventManager.DatasetCreated += OnDatasetCreated;
            DataCoreEventManager.DatasetModified += OnDatasetModified;
            DataCoreEventManager.SessionDatasetAdded += OnSessionDatasetAdded;
            DataCoreEventManager.SessionDatasetCreated += OnSessionDatasetCreated;
            DataCoreEventManager.SessionQueryResultSaved += OnSessionQueryResultSaved;

            try
            {
                // 创建数据存储和会话
                var store = new DataCoreStore();
                var session = store.SessionManager.CreateSession("EventTestSession");

                // 触发数据集创建事件
                Console.WriteLine("创建数据集...");
                var dataset = store.CreateTabular("TestDataset");

                // 触发会话数据集创建事件
                Console.WriteLine("在会话中创建数据集...");
                var sessionDataset = session.CreateDataset("SessionDataset", DataSetKind.Tabular);

                // 触发会话数据集添加事件
                Console.WriteLine("在会话中打开数据集副本...");
                var copy = session.OpenDataset("TestDataset", "TestCopy");

                // 触发数据集修改事件
                Console.WriteLine("修改数据集...");
                // 这里可以添加实际的数据修改操作

                // 触发会话查询结果保存事件
                Console.WriteLine("保存查询结果...");
                var queryResult = session.SaveQueryResult("TestCopy", ds => ds.WithName("QueryResult"), "QueryResult");

                Console.WriteLine("事件监听完成");
            }
            finally
            {
                // 清理事件订阅
                DataCoreEventManager.ClearAllSubscriptions();
            }
        }

        private static void OnDatasetCreated(object sender, DatasetCreatedEventArgs e)
        {
            Console.WriteLine($"[事件] 数据集创建: {e.Dataset.Name} ({e.Dataset.Kind})");
        }

        private static void OnDatasetModified(object sender, DatasetModifiedEventArgs e)
        {
            Console.WriteLine($"[事件] 数据集修改: {e.DatasetName} - {e.Operation}");
        }

        private static void OnSessionDatasetAdded(object sender, SessionDatasetAddedEventArgs e)
        {
            Console.WriteLine($"[事件] 会话数据集添加: 会话={e.Session.Name}, 数据集={e.Dataset.Name}");
        }

        private static void OnSessionDatasetCreated(object sender, SessionDatasetCreatedEventArgs e)
        {
            Console.WriteLine($"[事件] 会话数据集创建: 会话={e.Session.Name}, 数据集={e.Dataset.Name}");
        }

        private static void OnSessionQueryResultSaved(object sender, SessionQueryResultSavedEventArgs e)
        {
            Console.WriteLine($"[事件] 会话查询结果保存: 会话={e.Session.Name}, 源={e.Source.Name}, 结果={e.Result.Name}");
        }
    }
}