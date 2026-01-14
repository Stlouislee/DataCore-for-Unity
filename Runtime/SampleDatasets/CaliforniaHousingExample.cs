using UnityEngine;
using System.Linq;
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
                loader = FindFirstObjectByType<CaliforniaHousingLoader>();
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
            if (DataCoreEditorComponent.Instance != null)
            {
                var store = DataCoreEditorComponent.Instance.GetStore();
                var housingData = store.GetTabular("california-housing");
                
                if (housingData != null)
                {
                    // Custom query: Find houses with high value and high income
                    var luxuryHomes = housingData.Query()
                        .WhereGreaterThan("median_house_value", 400000)
                        .WhereGreaterThan("median_income", 6.0)
                        .ToDictionaries()
                        .ToList();

                    Debug.Log($"Luxury homes (value > $400k, income > $60k): {luxuryHomes.Count} properties");

                    // Get specific data for analysis
                    var avgValue = housingData.Mean("median_house_value");
                    var avgIncome = housingData.Mean("median_income");
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
    }
}
