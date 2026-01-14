using UnityEngine;
using AroAro.DataCore.SampleDatasets;

namespace AroAro.DataCore
{
    /// <summary>
    /// Manages the initialization of sample datasets on startup.
    /// </summary>
    public class SampleDatasetManager : MonoBehaviour
    {
        [SerializeField] private bool loadCaliforniaHousing = true;

        private void Start()
        {
            if (DataCoreEditorComponent.Instance == null)
            {
                Debug.LogWarning("DataCoreEditorComponent not found. Sample datasets cannot be loaded.");
                return;
            }

            if (loadCaliforniaHousing)
            {
                CheckAndLoadCaliforniaHousing();
            }
        }

        private void CheckAndLoadCaliforniaHousing()
        {
            var store = DataCoreEditorComponent.Instance.GetStore();
            string datasetName = "california-housing";

            if (!store.TryGet(datasetName, out _))
            {
                Debug.Log($"Sample dataset '{datasetName}' not found. Creating...");
                bool success = CaliforniaHousingDataset.LoadIntoDataCore(datasetName);
                if (success)
                {
                    // LiteDB auto-persists, no manual save needed
                    Debug.Log($"Sample dataset '{datasetName}' created and auto-persisted.");
                }
            }
            else
            {
                Debug.Log($"Sample dataset '{datasetName}' already exists.");
            }
        }
    }
}
