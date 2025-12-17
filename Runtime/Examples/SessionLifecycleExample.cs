using System;
using System.Collections.Generic;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Examples
{
    /// <summary>
    /// 会话生命周期管理示例
    /// </summary>
    public class SessionLifecycleExample
    {
        public static void RunExample()
        {
            Console.WriteLine("=== 会话生命周期管理示例 ===");

            // 创建数据存储
            var store = new DataCoreStore();

            // 1. 创建会话
            var session1 = store.SessionManager.CreateSession("Session1");
            var session2 = store.SessionManager.CreateSession("Session2");
            Console.WriteLine($"创建会话: {session1.Name}, {session2.Name}");

            // 2. 模拟会话活动
            SimulateSessionActivity(session1, "用户A");
            SimulateSessionActivity(session2, "用户B");

            // 3. 检查会话状态
            Console.WriteLine($"会话统计: {store.SessionManager.GetStatistics().TotalSessions} 个会话");

            // 4. 关闭特定会话
            Console.WriteLine($"关闭会话: {session1.Name}");
            store.SessionManager.CloseSession(session1.Id);

            // 5. 检查剩余会话
            Console.WriteLine($"剩余会话数: {store.SessionManager.GetStatistics().TotalSessions}");

            // 6. 清理空闲会话（模拟）
            var idleTimeout = TimeSpan.FromMinutes(1);
            Console.WriteLine($"清理空闲超过 {idleTimeout.TotalMinutes} 分钟的会话");
            var cleanedCount = store.SessionManager.CleanupIdleSessions(idleTimeout);
            Console.WriteLine($"清理了 {cleanedCount} 个空闲会话");

            // 7. 关闭所有会话
            Console.WriteLine("关闭所有会话");
            store.SessionManager.CloseAllSessions();

            // 8. 最终检查
            Console.WriteLine($"最终会话数: {store.SessionManager.GetStatistics().TotalSessions}");

            // 9. 清理资源
            store.Dispose();
            Console.WriteLine("完成会话生命周期管理示例");
        }

        private static void SimulateSessionActivity(ISession session, string user)
        {
            Console.WriteLine($"用户 {user} 在会话 {session.Name} 中工作...");

            // 创建数据集
            var dataset = session.CreateDataset($"{user}_Data", DataSetKind.Tabular);
            
            // 模拟一些活动
            session.Touch();
            
            // 模拟时间流逝
            System.Threading.Thread.Sleep(100); // 短暂延迟
            
            Console.WriteLine($"用户 {user} 完成会话 {session.Name} 中的工作");
        }
    }
}