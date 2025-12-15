using System.Collections.Generic;
using UnityEngine;
using NumSharp;

namespace AroAro.DataCore.SampleDatasets
{
    /// <summary>
    /// Built-in California housing dataset for DataCore
    /// Contains 3000+ California properties with housing data
    /// </summary>
    public class CaliforniaHousingLoader : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string datasetName = "california-housing";
        [SerializeField] private bool loadOnStart = true;

        private void Start()
        {
            if (loadOnStart)
            {
                LoadDataset();
            }
        }

        /// <summary>
        /// Load the built-in California housing dataset into DataCore
        /// </summary>
        public void LoadDataset()
        {
            if (DataCoreEditorComponent.Instance == null)
            {
                Debug.LogError("DataCoreEditorComponent not found in scene! Please add it to a GameObject.");
                return;
            }

            var store = DataCoreEditorComponent.Instance.GetStore();
            
            // Remove existing dataset if it exists
            if (store.TryGet(datasetName, out _))
            {
                store.Delete(datasetName);
            }

            try
            {
                var housingData = store.CreateTabular(datasetName);
                
                // Add built-in California housing data
                AddCaliforniaHousingData(housingData);

                Debug.Log($"✅ Loaded California housing dataset with {housingData.RowCount} rows and {housingData.ColumnNames.Count} columns");
                Debug.Log($"Dataset name: {datasetName}");
                Debug.Log($"Columns: {string.Join(", ", housingData.ColumnNames)}");

                // Save the dataset
                DataCoreEditorComponent.Instance.SaveDataset(datasetName);
                Debug.Log($"✅ Dataset saved to persistence storage");

            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load California housing dataset: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Add built-in California housing data to the dataset
        /// </summary>
        private void AddCaliforniaHousingData(Tabular.TabularData housingData)
        {
            var sampleData = CaliforniaHousingDataset.GetSampleData();

            // Add each column to the dataset
            foreach (var column in sampleData)
            {
                housingData.AddNumericColumn(column.Key, np.array(column.Value));
            }
        }



        /// <summary>
        /// Get sample queries for the California housing dataset
        /// </summary>
        public void RunSampleQueries()
        {
            if (DataCoreEditorComponent.Instance == null || !DataCoreEditorComponent.Instance.GetStore().TryGet(datasetName, out var dataset))
            {
                Debug.LogError("Dataset not found. Please load it first.");
                return;
            }

            var housingData = dataset as Tabular.TabularData;
            if (housingData == null)
            {
                Debug.LogError("Dataset is not tabular data");
                return;
            }

            Debug.Log($"=== Sample Queries for California Housing Dataset ===");

            // Query 1: High-value houses
            var highValueHouses = housingData.Query()
                .Where("median_house_value", Tabular.TabularOp.Gt, 500000)
                .ToRowIndices();
            Debug.Log($"High-value houses (>$500k): {highValueHouses.Length} properties");

            // Query 2: High-income areas
            var highIncomeAreas = housingData.Query()
                .Where("median_income", Tabular.TabularOp.Gt, 8.0)
                .ToRowIndices();
            Debug.Log($"High-income areas (>$80k): {highIncomeAreas.Length} properties");

            // Query 3: Large households
            var largeHouseholds = housingData.Query()
                .Where("households", Tabular.TabularOp.Gt, 1000)
                .ToRowIndices();
            Debug.Log($"Large households (>1000 people): {largeHouseholds.Length} properties");

            // Query 4: Old houses
            var oldHouses = housingData.Query()
                .Where("housing_median_age", Tabular.TabularOp.Gt, 50)
                .ToRowIndices();
            Debug.Log($"Old houses (>50 years): {oldHouses.Length} properties");
        }

        /// <summary>
        /// Get basic statistics for the dataset
        /// </summary>
        public void ShowStatistics()
        {
            if (DataCoreEditorComponent.Instance == null || !DataCoreEditorComponent.Instance.GetStore().TryGet(datasetName, out var dataset))
            {
                Debug.LogError("Dataset not found. Please load it first.");
                return;
            }

            var housingData = dataset as Tabular.TabularData;
            if (housingData == null)
            {
                Debug.LogError("Dataset is not tabular data");
                return;
            }

            Debug.Log($"=== California Housing Dataset Statistics ===");
            Debug.Log($"Total properties: {housingData.RowCount}");
            Debug.Log($"Columns: {string.Join(", ", housingData.ColumnNames)}");

            foreach (var column in housingData.ColumnNames)
            {
                try
                {
                    var data = housingData.GetNumericColumn(column);
                    var min = np.min(data);
                    var max = np.max(data);
                    var mean = np.mean(data);
                    Debug.Log($"{column}: min={min:F2}, max={max:F2}, mean={mean:F2}");
                }
                catch
                {
                    Debug.Log($"{column}: [non-numeric data]");
                }
            }
        }
    }
}