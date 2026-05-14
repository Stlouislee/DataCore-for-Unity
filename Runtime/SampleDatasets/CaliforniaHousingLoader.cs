using System.Collections.Generic;
using UnityEngine;
using AroAro.DataCore.Events;

namespace AroAro.DataCore.SampleDatasets
{
    /// <summary>
    /// Provides query and statistics access to the California housing dataset.
    /// Loading is handled exclusively by <see cref="SampleDatasetManager"/> to prevent
    /// duplicate loading when both components are present in a scene.
    /// </summary>
    public class CaliforniaHousingLoader : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string datasetName = "california-housing";

        private void Start()
        {
            if (DataCoreEditorComponent.Instance == null)
            {
                Debug.LogWarning("DataCoreEditorComponent not found in scene. Dataset queries will not work.");
                return;
            }

            var store = DataCoreEditorComponent.Instance.GetStore();
            if (!store.HasDataset(datasetName))
            {
                Debug.LogWarning(
                    $"Dataset '{datasetName}' not loaded. " +
                    $"Add a SampleDatasetManager to your scene or call CaliforniaHousingDataset.LoadIntoDataCore() manually.");
            }
        }

        /// <summary>
        /// Run sample queries against the loaded dataset
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
                Debug.LogError("Dataset not found. Add a SampleDatasetManager to your scene to load it.");
                return;
            }

            Debug.Log($"=== Sample Queries for California Housing Dataset ===");

            // Query 1: High-value houses
            var highValueIndices = housingData.Query().Where("median_house_value", QueryOp.Gt, 500000).ToRowIndices();
            Debug.Log($"High-value houses (>$500k): {highValueIndices.Length} properties");

            // Query 2: High-income areas
            var highIncomeIndices = housingData.Query().Where("median_income", QueryOp.Gt, 8.0).ToRowIndices();
            Debug.Log($"High-income areas (>$80k): {highIncomeIndices.Length} properties");

            // Query 3: Large households
            var largeHouseholdIndices = housingData.Query().Where("households", QueryOp.Gt, 1000).ToRowIndices();
            Debug.Log($"Large households (>1000 people): {largeHouseholdIndices.Length} properties");

            // Query 4: Old houses
            var oldHouseIndices = housingData.Query().Where("housing_median_age", QueryOp.Gt, 50).ToRowIndices();
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
                Debug.LogError("Dataset not found. Add a SampleDatasetManager to your scene to load it.");
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

        [ContextMenu("Run Sample Queries")]
        private void RunSampleQueriesMenu() => RunSampleQueries();

        [ContextMenu("Show Statistics")]
        private void ShowStatisticsMenu() => ShowStatistics();
    }
}
