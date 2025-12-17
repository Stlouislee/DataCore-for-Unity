using System;
using System.Collections.Generic;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Examples
{
    /// <summary>
    /// 多用户会话管理示例
    /// </summary>
    public class MultiUserSessionExample
    {
        public static void RunExample()
        {
            Console.WriteLine("=== 多用户会话管理示例 ===");

            // 创建数据存储
            var store = new DataCoreStore();

            // 模拟多个用户
            var users = new[] { "Alice", "Bob", "Charlie" };
            var userSessions = new Dictionary<string, ISession>();

            // 为每个用户创建会话
            foreach (var user in users)
            {
                var sessionName = $"{user}'s Analysis Session";
                var session = store.SessionManager.CreateSession(sessionName);
                userSessions[user] = session;
                Console.WriteLine($"用户 {user} 创建会话: {session.Name}");
            }

            // 模拟用户在各自的会话中工作
            foreach (var user in users)
            {
                var session = userSessions[user];
                
                // 每个用户创建自己的数据集
                var datasetName = $"{user}_Data";
                var dataset = session.CreateDataset(datasetName, DataSetKind.Tabular);
                Console.WriteLine($"用户 {user} 在会话中创建数据集: {dataset.Name}");

                // 每个用户打开全局数据集的副本
                var globalData = store.CreateTabular("GlobalData");
                var copyName = $"{user}_Copy";
                var copy = session.OpenDataset("GlobalData", copyName);
                Console.WriteLine($"用户 {user} 打开数据集副本: {copy.Name}");
            }

            // 显示会话统计
            var stats = store.SessionManager.GetStatistics();
            Console.WriteLine($"会话统计: {stats.TotalSessions} 个会话, 总共 {stats.TotalDatasets} 个数据集, 平均每会话 {stats.AverageDatasetsPerSession:F1} 个数据集");

            // 模拟用户完成工作并关闭会话
            foreach (var user in users)
            {
                var session = userSessions[user];
                Console.WriteLine($"用户 {user} 完成工作，关闭会话: {session.Name}");
                store.SessionManager.CloseSession(session.Id);
            }

            // 检查剩余会话
            stats = store.SessionManager.GetStatistics();
            Console.WriteLine($"关闭用户会话后统计: {stats.TotalSessions} 个会话, 总共 {stats.TotalDatasets} 个数据集");

            // 清理
            store.Dispose();
            Console.WriteLine("完成多用户会话管理示例");
        }
    }
}