using UnityEngine;
using DataCore;
using DataCore.Tensor;
using DataCore.UnityIntegration;

namespace DataCore.Samples
{
    /// <summary>
    /// Example script demonstrating tensor operations with DataCore
    /// </summary>
    public class TensorExampleScript : MonoBehaviour
    {
        [SerializeField] private DataManagerBehaviour dataManager;
        
        private void Start()
        {
            if (dataManager == null)
            {
                dataManager = FindObjectOfType<DataManagerBehaviour>();
            }
            
            if (dataManager != null && dataManager.DataManager != null)
            {
                RunTensorExample();
            }
            else
            {
                Debug.LogError("DataManager not found!");
            }
        }
        
        private async void RunTensorExample()
        {
            var tensorManager = dataManager.DataManager.TensorManager;
            
            // Example 1: Create and manipulate tensors
            Debug.Log("=== Tensor Operations Example ===");
            
            // Create a 3x3 tensor
            var tensor = tensorManager.CreateTensor("example_tensor", new int[] { 3, 3 });
            
            // Fill with sample data
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    tensor.Data[i, j] = i * 3 + j + 1;
                }
            }
            
            Debug.Log("Original Tensor:");
            Debug.Log(tensor.ToString());
            
            // Perform operations
            var transposed = tensorManager.Transpose(tensor);
            Debug.Log("Transposed Tensor:");
            Debug.Log(transposed.ToString());
            
            var scaled = tensorManager.Scale(tensor, 2.0f);
            Debug.Log("Scaled Tensor (x2):");
            Debug.Log(scaled.ToString());
            
            // Example 2: Save and load tensor
            await tensorManager.SaveTensorAsync("example_tensor", "tensors/example.npy");
            Debug.Log("Tensor saved to: tensors/example.npy");
            
            var loadedTensor = await tensorManager.LoadTensorAsync("tensors/example.npy");
            Debug.Log("Loaded Tensor:");
            Debug.Log(loadedTensor.ToString());
            
            // Example 3: Tensor operations with pooling
            var pooledTensor = tensorManager.GetFromPool("pooled_tensor", new int[] { 2, 2 });
            pooledTensor.Data[0, 0] = 1.0f;
            pooledTensor.Data[0, 1] = 2.0f;
            pooledTensor.Data[1, 0] = 3.0f;
            pooledTensor.Data[1, 1] = 4.0f;
            
            Debug.Log("Pooled Tensor:");
            Debug.Log(pooledTensor.ToString());
            
            // Return to pool
            tensorManager.ReturnToPool(pooledTensor);
            
            Debug.Log("Tensor example completed!");
        }
    }
}