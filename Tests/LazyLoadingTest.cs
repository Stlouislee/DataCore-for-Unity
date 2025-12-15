using UnityEngine;

namespace AroAro.DataCore.Tests
{
    public class LazyLoadingTest : MonoBehaviour
    {
        private DataCoreEditorComponent dataCore;
        
        private void Start()
        {
            dataCore = FindObjectOfType<DataCoreEditorComponent>();
            if (dataCore == null)
            {
                Debug.LogError("DataCoreEditorComponent not found in scene");
                return;
            }
            
            TestLazyLoading();
        }
        
        private void TestLazyLoading()
        {
            var store = dataCore.GetStore();
            
            // 测试1：检查Names属性是否正常工作
            Debug.Log($"Total datasets: {store.Names.Count}");
            
            // 测试2：尝试访问数据集（应该触发延迟加载）
            foreach (var name in store.Names)
            {
                var metadata = store.GetMetadata(name);
                Debug.Log($"Dataset: {name}, Loaded: {metadata?.IsLoaded ?? false}");
                
                // 尝试访问数据集
                if (store.TryGet(name, out var dataset))
                {
                    Debug.Log($"Successfully loaded: {name}, Type: {dataset.Kind}");
                    
                    // 检查元数据是否更新
                    var updatedMetadata = store.GetMetadata(name);
                    Debug.Log($"After access - Loaded: {updatedMetadata?.IsLoaded ?? false}");
                }
                else
                {
                    Debug.LogError($"Failed to load dataset: {name}");
                }
            }
            
            // 测试3：保存所有数据集（应该不会抛出异常）
            try
            {
                dataCore.SaveAll();
                Debug.Log("SaveAll completed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"SaveAll failed: {ex.Message}");
            }
            
            // 测试4：删除数据集
            if (store.Names.Count > 0)
            {
                var firstName = new System.Collections.Generic.List<string>(store.Names)[0];
                if (store.Delete(firstName))
                {
                    Debug.Log($"Successfully deleted dataset: {firstName}");
                }
                else
                {
                    Debug.LogError($"Failed to delete dataset: {firstName}");
                }
            }
            
            Debug.Log("Lazy loading test completed");
        }
        
        private void OnDestroy()
        {
            // 测试自动保存功能
            if (dataCore != null)
            {
                try
                {
                    dataCore.SaveAll();
                    Debug.Log("Auto-save on destroy completed");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Auto-save on destroy failed: {ex.Message}");
                }
            }
        }
    }
}