using UnityEngine;
using NumSharp;

namespace AroAro.DataCore.SampleDatasets
{
    /// <summary>
    /// Example script showing how to use the California housing dataset
    /// </summary>
    public class CaliforniaHousingExample : MonoBehaviour
    {
        [SerializeField] private CaliforniaHousingLoader loader;

        private void Start()
        {
            // If loader is not assigned, try to find it
            if (loader == null)
            {
                loader = FindObjectOfType<CaliforniaHousingLoader>();
            }

            if (loader == null)
            {
                Debug.LogWarning("CaliforniaHousingLoader not found. Please add it to a GameObject.");
                return;
            }

            // Wait a moment for the dataset to load, then run queries
            Invoke(nameof(RunExampleQueries), 1f);
        }

        private void RunExampleQueries()
        {
            if (loader == null) return;

            // Show statistics
            loader.ShowStatistics();

            // Run sample queries
            loader.RunSampleQueries();

            // Example: Access the dataset directly
            if (DataCoreEditorComponent.Instance != null && 
                DataCoreEditorComponent.Instance.GetStore().TryGet("california-housing", out var dataset))
            {
                var housingData = dataset as Tabular.TabularData;
                if (housingData != null)
                {
                    // Custom query: Find houses with high value and high income
                    var luxuryHomes = housingData.Query()
                        .Where("median_house_value", Tabular.TabularOp.Gt, 400000)
                        .Where("median_income", Tabular.TabularOp.Gt, 6.0)
                        .ToRowIndices();

                    Debug.Log($"Luxury homes (value > $400k, income > $60k): {luxuryHomes.Length} properties");

                    // Get specific data for analysis
                    var houseValues = housingData.GetNumericColumn("median_house_value");
                    var incomes = housingData.GetNumericColumn("median_income");

                    // Calculate correlation (simple example)
                    var avgValue = np.mean(houseValues);
                    var avgIncome = np.mean(incomes);
                    Debug.Log($"Average house value: ${avgValue:F2}");
                    Debug.Log($"Average income: ${avgIncome:F2}");
                }
            }
        }

        [ContextMenu("Reload Dataset")]
        private void ReloadDataset()
        {
            if (loader != null)
            {
                loader.LoadDataset();
                Invoke(nameof(RunExampleQueries), 0.5f);
            }
        }

        [ContextMenu("Load Using Static Method")]
        private void LoadUsingStaticMethod()
        {
            CaliforniaHousingDataset.LoadIntoDataCore();
            Invoke(nameof(RunExampleQueries), 0.5f);
        }

        [ContextMenu("Show Statistics")]
        private void ShowStats()
        {
            if (loader != null)
            {
                loader.ShowStatistics();
            }
        }

        [ContextMenu("Run Queries")]
        private void RunQueries()
        {
            if (loader != null)
            {
                loader.RunSampleQueries();
            }
        }

        [ContextMenu("Persist Dataset")]
        private void PersistDataset()
        {
            if (DataCoreEditorComponent.Instance != null)
            {
                DataCoreEditorComponent.Instance.PersistDataset("california-housing");
                Debug.Log("Dataset persisted and will survive play mode transitions");
            }
        }

        [ContextMenu("Load Persisted Dataset")]
        private void LoadPersistedDataset()
        {
            if (DataCoreEditorComponent.Instance != null)
            {
                DataCoreEditorComponent.Instance.LoadPersistedDataset("california-housing");
                Invoke(nameof(RunExampleQueries), 0.5f);
            }
        }
    }
}