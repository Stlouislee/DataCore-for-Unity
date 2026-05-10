using System.Collections.Generic;
using UnityEngine;
using AroAro.DataCore.SampleDatasets;

namespace AroAro.DataCore
{
    /// <summary>
    /// Manages the initialization of sample datasets on startup.
    /// Supports both a ScriptableObject-based registry for extensibility
    /// and the legacy loadCaliforniaHousing toggle for backward compatibility.
    /// </summary>
    public class SampleDatasetManager : MonoBehaviour
    {
        /// <summary>
        /// Optional registry of sample datasets. When set, datasets listed here
        /// with loadOnStart=true will be loaded automatically.
        /// </summary>
        [SerializeField] private SampleDatasetRegistry registry;

        /// <summary>
        /// Legacy toggle for backward compatibility.
        /// When true (and not overridden by registry), the California Housing
        /// dataset will be loaded on startup.
        /// </summary>
        [SerializeField] private bool loadCaliforniaHousing = true;

        private void Start()
        {
            if (DataCoreEditorComponent.Instance == null)
            {
                Debug.LogWarning("DataCoreEditorComponent not found. Sample datasets cannot be loaded.");
                return;
            }

            if (registry != null)
            {
                LoadFromRegistry();
            }
            else if (loadCaliforniaHousing)
            {
                // Backward compatibility: load California Housing directly
                CheckAndLoadCaliforniaHousing();
            }
        }

        /// <summary>
        /// Iterates over all definitions in the registry and loads those
        /// marked with loadOnStart = true.
        /// </summary>
        private void LoadFromRegistry()
        {
            foreach (var def in registry.Datasets)
            {
                if (def.loadOnStart)
                {
                    LoadDatasetByName(def.datasetName);
                }
            }
        }

        /// <summary>
        /// Loads a dataset by name using the built-in dataset loaders.
        /// Currently supports "california-housing". Extend here for additional datasets.
        /// </summary>
        private void LoadDatasetByName(string datasetName)
        {
            switch (datasetName)
            {
                case "california-housing":
                    CheckAndLoadCaliforniaHousing();
                    break;
                default:
                    Debug.LogWarning($"Unknown sample dataset: '{datasetName}'. No loader registered for this name.");
                    break;
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
