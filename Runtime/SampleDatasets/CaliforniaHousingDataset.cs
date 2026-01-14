using NumSharp;
using UnityEngine;
using System.Linq;

namespace AroAro.DataCore.SampleDatasets
{
    /// <summary>
    /// Static California housing dataset provider
    /// Contains built-in sample data for demonstration and testing
    /// </summary>
    public static class CaliforniaHousingDataset
    {
        /// <summary>
        /// Get the built-in California housing data as a dictionary
        /// </summary>
        public static System.Collections.Generic.Dictionary<string, double[]> GetSampleData()
        {
            var csvFile = Resources.Load<TextAsset>("AroAro/DataCore/california_housing_test");
            if (csvFile != null)
            {
                return ParseCsv(csvFile.text);
            }
            
            Debug.LogWarning("Could not find California Housing CSV in Resources. Using fallback small dataset.");
            return GetFallbackData();
        }

        private static System.Collections.Generic.Dictionary<string, double[]> ParseCsv(string csvText)
        {
            var lines = csvText.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return new System.Collections.Generic.Dictionary<string, double[]>();

            var headers = lines[0].Trim().Replace("\"", "").Split(',');
            var data = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<double>>();

            foreach (var header in headers)
            {
                data[header] = new System.Collections.Generic.List<double>();
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var values = line.Split(',');
                if (values.Length != headers.Length) continue;

                for (int j = 0; j < headers.Length; j++)
                {
                    if (double.TryParse(values[j], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
                    {
                        data[headers[j]].Add(val);
                    }
                    else
                    {
                        data[headers[j]].Add(0.0);
                    }
                }
            }

            var result = new System.Collections.Generic.Dictionary<string, double[]>();
            foreach (var kvp in data)
            {
                result[kvp.Key] = kvp.Value.ToArray();
            }
            return result;
        }

        private static System.Collections.Generic.Dictionary<string, double[]> GetFallbackData()
        {
            return new System.Collections.Generic.Dictionary<string, double[]>
            {
                ["longitude"] = new double[] { -122.23, -122.22, -122.24, -118.30, -118.31, -117.81, -117.82, -119.67, -119.56, -121.43 },
                ["latitude"] = new double[] { 37.88, 37.86, 37.85, 34.26, 34.25, 33.78, 33.77, 36.33, 36.51, 38.63 },
                ["housing_median_age"] = new double[] { 41, 21, 52, 43, 27, 28, 19, 37, 43, 15 },
                ["total_rooms"] = new double[] { 880, 7099, 1467, 1510, 3589, 67, 1241, 1018, 1009, 3080 },
                ["total_bedrooms"] = new double[] { 129, 1106, 190, 310, 507, 15, 244, 213, 225, 617 },
                ["population"] = new double[] { 322, 2401, 496, 809, 1484, 49, 850, 663, 604, 1446 },
                ["households"] = new double[] { 126, 1138, 177, 277, 495, 11, 237, 204, 218, 599 },
                ["median_income"] = new double[] { 8.3252, 8.3014, 7.2574, 3.5990, 5.7934, 6.1359, 2.9375, 1.6635, 1.6641, 3.6696 },
                ["median_house_value"] = new double[] { 452600, 358500, 352100, 176500, 270500, 330000, 81700, 67000, 67000, 194400 }
            };
        }

        /// <summary>
        /// Create and populate a TabularData instance with California housing data
        /// </summary>
        public static Tabular.TabularData CreateDataset(string datasetName = "california-housing")
        {
            var data = GetSampleData();
            var dataset = new Tabular.TabularData(datasetName);

            foreach (var column in data)
            {
                dataset.AddNumericColumn(column.Key, np.array(column.Value));
            }

            return dataset;
        }

        /// <summary>
        /// Load the California housing dataset into the shared DataCore store
        /// </summary>
        public static bool LoadIntoDataCore(string datasetName = "california-housing")
        {
            if (DataCoreEditorComponent.Instance == null)
            {
                UnityEngine.Debug.LogError("DataCoreEditorComponent not found in scene!");
                return false;
            }

            var store = DataCoreEditorComponent.Instance.GetStore();
            
            // Remove existing dataset if it exists
            if (store.HasDataset(datasetName))
            {
                store.Delete(datasetName);
            }

            try
            {
                // Create tabular dataset in the store
                var tabular = store.CreateTabular(datasetName);
                var data = GetSampleData();
                
                foreach (var column in data)
                {
                    tabular.AddNumericColumn(column.Key, np.array(column.Value));
                }
                
                UnityEngine.Debug.Log($"✅ Loaded California housing dataset with {tabular.RowCount} rows");
                return true;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to load California housing dataset: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get sample queries for the California housing dataset
        /// </summary>
        public static string[] GetSampleQueries()
        {
            return new string[]
            {
                "Find houses with median value > $500,000",
                "Find houses with median income > $80,000", 
                "Find houses with population > 1000",
                "Find houses older than 50 years",
                "Find houses with more than 5 bedrooms"
            };
        }

        /// <summary>
        /// Get dataset statistics
        /// </summary>
        public static System.Collections.Generic.Dictionary<string, string> GetStatistics()
        {
            var data = GetSampleData();
            var stats = new System.Collections.Generic.Dictionary<string, string>();

            foreach (var column in data)
            {
                var values = np.array(column.Value);
                var min = np.min(values);
                var max = np.max(values);
                var mean = np.mean(values);
                
                stats[column.Key] = $"min={min:F2}, max={max:F2}, mean={mean:F2}";
            }

            return stats;
        }
    }
}