using UnityEngine;
using System;
using System.Collections.Generic;
using AroAro.DataCore.Session;

namespace AroAro.DataCore.Examples
{
    /// <summary>
    /// 实际应用场景示例：数据分析工作流
    /// </summary>
    public class DataAnalysisWorkflowExample
    {
        public static void RunExample()
        {
            Debug.Log("=== 数据分析工作流示例 ===");

            // 1. 初始化数据存储
            var store = new DataCoreStore();

            // 2. 创建分析会话
            var session = store.SessionManager.CreateSession("SalesAnalysis");
            Debug.Log($"开始分析会话: {session.Name}");

            // 3. 在全局存储中准备原始数据
            var salesData = store.CreateTabular("SalesData");
            var customerData = store.CreateTabular("CustomerData");
            Debug.Log("在全局存储中创建了原始数据集");

            // 4. 在会话中加载数据副本进行分析
            var salesCopy = session.OpenDataset("SalesData", "SalesAnalysis");
            var customerCopy = session.OpenDataset("CustomerData", "CustomerAnalysis");
            Debug.Log("在会话中加载了数据副本");

            // 5. 执行数据清洗和转换（在会话中进行）
            Debug.Log("执行数据清洗和转换...");
            
            // 示例：创建清洗后的数据集
            var cleanedSales = session.CreateDataset("CleanedSales", DataSetKind.Tabular);
            var enrichedCustomer = session.CreateDataset("EnrichedCustomer", DataSetKind.Tabular);

            // 6. 执行分析查询
            Debug.Log("执行分析查询...");
            
            // 示例：保存查询结果
            var highValueCustomers = session.SaveQueryResult("CustomerAnalysis", dataset =>
            {
                // 这里应该执行实际的查询逻辑
                return dataset.WithName("HighValueCustomers");
            }, "HighValueCustomers");

            var salesByRegion = session.SaveQueryResult("SalesAnalysis", dataset =>
            {
                // 这里应该执行实际的查询逻辑
                return dataset.WithName("SalesByRegion");
            }, "SalesByRegion");

            // 7. 查看会话中的所有数据集
            Debug.Log($"会话中的数据集 ({session.DatasetCount}个):");
            foreach (var name in session.DatasetNames)
            {
                Debug.Log($"  - {name}");
            }

            // 8. 选择需要持久化的结果
            Debug.Log("选择需要持久化的分析结果...");
            
            // 通常只持久化最终的分析结果，而不是中间数据
            // PersistDataset is not yet implemented — see DATACORE_V2_Roadmap.md
            Debug.Log("ℹ️ PersistDataset is planned for a future release. See DATACORE_V2_Roadmap.md.");

            // 9. 清理会话中的临时数据
            session.Clear();
            Debug.Log("已清理会话中的临时数据");

            // 10. 检查全局存储中的持久化数据
            Debug.Log($"全局存储中的数据集: {string.Join(", ", store.Names)}");

            // 11. 清理资源
            store.Dispose();
            Debug.Log("完成分析工作流");
        }
    }
}