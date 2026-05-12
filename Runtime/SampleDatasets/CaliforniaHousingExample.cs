using UnityEngine;
using System.Linq;
using AroAro.DataCore.Events;

namespace AroAro.DataCore.SampleDatasets
{
    /// <summary>
    /// Example script showing how to use the California housing dataset
    /// </summary>
    public class CaliforniaHousingExample : MonoBehaviour
    {
        [SerializeField] private CaliforniaHousingLoader loader;

        private void OnEnable()
        {
            // Listen for dataset load completion instead of fragile Invoke timing
            DataCoreEventManager.SubscribeDatasetLoaded(OnDatasetLoaded);
        }

        private void OnDisable()
        {
            DataCoreEventManager.UnsubscribeDatasetLoaded(OnDatasetLoaded);
        }

        private void Start()
        {
            // If loader is not assigned, try to find it
            if (loader == null)
            {
#if UNITY_2023_1_OR_NEWER
                loader = FindFirstObjectByType<CaliforniaHousingLoader>();
#else
                loader = FindObjectOfType<CaliforniaHousingLoader>();
#endif
            }

            if (loader == null)
            {
                Debug.LogWarning("CaliforniaHousingLoader not found. Please add it to a GameObject.");
                return;
            }
        }

        private void OnDatasetLoaded(object sender, DatasetLoadedEventArgs e)
        {
            if (e.Dataset.Name == "california-housing")
            {
                RunExampleQueries();
            }
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
                // No Invoke needed — OnDatasetLoaded event will trigger RunExampleQueries
            }
        }

        [ContextMenu("Load Using Static Method")]
        private void LoadUsingStaticMethod()
        {
            CaliforniaHousingDataset.LoadIntoDataCore();
            // No Invoke needed — event-driven
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
