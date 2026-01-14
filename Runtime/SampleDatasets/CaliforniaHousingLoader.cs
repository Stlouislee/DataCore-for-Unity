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
            if (store.HasDataset(datasetName))
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

                // Data is auto-saved with LiteDB
                Debug.Log($"✅ Dataset auto-persisted to LiteDB");

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
        private void AddCaliforniaHousingData(ITabularDataset housingData)
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
            if (DataCoreEditorComponent.Instance == null)
            {
                Debug.LogError("DataCoreEditorComponent not found.");
                return;
            }

            var store = DataCoreEditorComponent.Instance.GetStore();
            var housingData = store.GetTabular(datasetName);
            
            if (housingData == null)
            {
                Debug.LogError("Dataset not found. Please load it first.");
                return;
            }

            Debug.Log($"=== Sample Queries for California Housing Dataset ===");

            // Query 1: High-value houses
            var highValueIndices = housingData.Where("median_house_value", QueryOp.Gt, 500000);
            Debug.Log($"High-value houses (>$500k): {highValueIndices.Length} properties");

            // Query 2: High-income areas
            var highIncomeIndices = housingData.Where("median_income", QueryOp.Gt, 8.0);
            Debug.Log($"High-income areas (>$80k): {highIncomeIndices.Length} properties");

            // Query 3: Large households
            var largeHouseholdIndices = housingData.Where("households", QueryOp.Gt, 1000);
            Debug.Log($"Large households (>1000 people): {largeHouseholdIndices.Length} properties");

            // Query 4: Old houses
            var oldHouseIndices = housingData.Where("housing_median_age", QueryOp.Gt, 50);
            Debug.Log($"Old houses (>50 years): {oldHouseIndices.Length} properties");
        }

        /// <summary>
        /// Get basic statistics for the dataset
        /// </summary>
        public void ShowStatistics()
        {
            if (DataCoreEditorComponent.Instance == null)
            {
                Debug.LogError("DataCoreEditorComponent not found.");
                return;
            }

            var store = DataCoreEditorComponent.Instance.GetStore();
            var housingData = store.GetTabular(datasetName);
            
            if (housingData == null)
            {
                Debug.LogError("Dataset not found. Please load it first.");
                return;
            }

            Debug.Log($"=== California Housing Dataset Statistics ===");
            Debug.Log($"Total properties: {housingData.RowCount}");
            Debug.Log($"Columns: {string.Join(", ", housingData.ColumnNames)}");

            foreach (var column in housingData.ColumnNames)
            {
                try
                {
                    var mean = housingData.Mean(column);
                    var min = housingData.Min(column);
                    var max = housingData.Max(column);
                    Debug.Log($"  {column}: mean={mean:F2}, min={min:F2}, max={max:F2}");
                }
                catch (System.Exception)
                {
                    Debug.Log($"  {column}: (non-numeric column)");
                }
            }
        }

        [ContextMenu("Load California Housing Dataset")]
        private void LoadDatasetMenu() => LoadDataset();

        [ContextMenu("Run Sample Queries")]
        private void RunSampleQueriesMenu() => RunSampleQueries();

        [ContextMenu("Show Statistics")]
        private void ShowStatisticsMenu() => ShowStatistics();
    }
}
