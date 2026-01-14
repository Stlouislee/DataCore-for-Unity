using UnityEngine;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// LiteDB 后端测试 - 测试数据集的创建、访问和删除
    /// </summary>
    public class LazyLoadingTest : MonoBehaviour
    {
        private DataCoreEditorComponent dataCore;
        
        private void Start()
        {
            dataCore = FindFirstObjectByType<DataCoreEditorComponent>();
            if (dataCore == null)
            {
                Debug.LogError("DataCoreEditorComponent not found in scene");
                return;
            }
            
            TestLiteDbBackend();
        }
        
        private void TestLiteDbBackend()
        {
            var store = dataCore.GetStore();
            
            // 测试1：检查Names属性是否正常工作
            Debug.Log($"Total datasets: {store.Names.Count}");
            
            // 测试2：创建和访问数据集
            var testName = "lazy-load-test";
            if (!store.HasDataset(testName))
            {
                var tabular = store.CreateTabular(testName);
                tabular.AddNumericColumn("x", new double[] { 1, 2, 3 });
                tabular.AddStringColumn("name", new[] { "a", "b", "c" });
                Debug.Log($"Created test dataset: {testName}");
            }
            
            // 测试3：访问数据集
            foreach (var name in store.Names)
            {
                // 尝试访问数据集
                if (store.TryGet(name, out var dataset))
                {
                    Debug.Log($"Successfully accessed: {name}, Type: {dataset.Kind}");
                    
                    if (dataset is ITabularDataset tabular)
                    {
                        Debug.Log($"  Rows: {tabular.RowCount}, Columns: {tabular.ColumnCount}");
                    }
                    else if (dataset is IGraphDataset graph)
                    {
                        Debug.Log($"  Nodes: {graph.NodeCount}, Edges: {graph.EdgeCount}");
                    }
                }
                else
                {
                    Debug.LogError($"Failed to access dataset: {name}");
                }
            }
            
            // 测试4：检查点（强制写入）
            try
            {
                store.Checkpoint();
                Debug.Log("Checkpoint completed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Checkpoint failed: {ex.Message}");
            }
            
            // 测试5：删除测试数据集
            if (store.HasDataset(testName))
            {
                if (store.Delete(testName))
                {
                    Debug.Log($"Successfully deleted dataset: {testName}");
                }
                else
                {
                    Debug.LogError($"Failed to delete dataset: {testName}");
                }
            }
            
            Debug.Log("LiteDB backend test completed");
        }
        
        private void OnDestroy()
        {
            // LiteDB 自动持久化，无需手动保存
            Debug.Log("LazyLoadingTest destroyed - LiteDB auto-persists data");
        }
    }
}
