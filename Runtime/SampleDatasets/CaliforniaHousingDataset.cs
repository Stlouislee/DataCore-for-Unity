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
        private static System.Collections.Generic.Dictionary<string, double[]> _cachedData;

        /// <summary>
        /// Get the built-in California housing data as a dictionary
        /// </summary>
        public static System.Collections.Generic.Dictionary<string, double[]> GetSampleData()
        {
            if (_cachedData != null) return _cachedData;

            var csvFile = Resources.Load<TextAsset>("AroAro/DataCore/california_housing_test");
            if (csvFile != null)
            {
                _cachedData = ParseCsv(csvFile.text);
            }
            else
            {
                Debug.LogWarning("Could not find California Housing CSV in Resources. Using fallback small dataset.");
                _cachedData = GetFallbackData();
            }
            return _cachedData;
        }

        /// <summary>
        /// Clear cache (for testing or forced reload)
        /// </summary>
        public static void ClearCache()
        {
            _cachedData = null;
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

            int parseErrorCount = 0;

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
                        data[headers[j]].Add(double.NaN);
                        parseErrorCount++;
                        if (parseErrorCount <= 10)
                        {
                            Debug.LogWarning($"[CaliforniaHousingDataset] Row {i}, Column '{headers[j]}': cannot parse '{values[j]}', using NaN");
                        }
                    }
                }
            }

            if (parseErrorCount > 0)
            {
                Debug.LogWarning($"[CaliforniaHousingDataset] Total parse errors: {parseErrorCount} cells could not be parsed, replaced with NaN");
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
                ["longitude"] = new double[] {
                    -122.23, -122.22, -122.24, -118.30, -118.31, -117.81, -117.82, -119.67, -119.56, -121.43,
                    -121.87, -121.09, -117.03, -118.15, -117.97, -122.42, -122.40, -121.97, -121.95, -118.01,
                    -117.90, -117.06, -122.08, -122.18, -118.37, -118.44, -117.59, -122.00, -119.78, -120.85,
                    -122.33, -118.48, -119.01, -120.50
                },
                ["latitude"] = new double[] {
                    37.88, 37.86, 37.85, 34.26, 34.25, 33.78, 33.77, 36.33, 36.51, 38.63,
                    37.35, 36.63, 32.76, 34.03, 33.81, 37.78, 37.79, 36.60, 36.57, 34.14,
                    33.89, 32.71, 37.38, 37.52, 34.20, 33.94, 33.88, 37.87, 36.73, 39.14,
                    37.82, 34.02, 35.37, 38.05
                },
                ["housing_median_age"] = new double[] {
                    41, 21, 52, 43, 27, 28, 19, 37, 43, 15,
                    25, 33, 20, 48, 35, 52, 36, 18, 42, 30,
                    22, 16, 47, 13, 34, 39, 26, 31, 24, 45,
                    29, 40, 17, 38
                },
                ["total_rooms"] = new double[] {
                    880, 7099, 1467, 1510, 3589, 67, 1241, 1018, 1009, 3080,
                    2491, 2402, 4859, 1162, 2635, 2379, 3720, 1709, 1638, 3429,
                    2991, 2108, 1452, 1648, 2908, 3715, 4687, 3051, 1644, 2198,
                    2584, 5348, 2052, 3427
                },
                ["total_bedrooms"] = new double[] {
                    129, 1106, 190, 310, 507, 15, 244, 213, 225, 617,
                    478, 453, 948, 235, 526, 480, 720, 349, 327, 689,
                    598, 413, 292, 332, 584, 743, 934, 610, 328, 437,
                    516, 1067, 410, 685
                },
                ["population"] = new double[] {
                    322, 2401, 496, 809, 1484, 49, 850, 663, 604, 1446,
                    1173, 1036, 2398, 627, 1219, 1125, 1786, 835, 789, 1662,
                    1448, 1019, 693, 791, 1406, 1799, 2268, 1470, 791, 1068,
                    1241, 2568, 995, 1660
                },
                ["households"] = new double[] {
                    126, 1138, 177, 277, 495, 11, 237, 204, 218, 599,
                    459, 428, 907, 222, 501, 469, 700, 338, 319, 674,
                    582, 401, 284, 322, 570, 727, 908, 597, 318, 429,
                    500, 1035, 396, 667
                },
                ["median_income"] = new double[] {
                    8.3252, 8.3014, 7.2574, 3.5990, 5.7934, 6.1359, 2.9375, 1.6635, 1.6641, 3.6696,
                    2.4486, 2.7750, 5.4785, 2.5417, 4.1406, 7.7283, 6.3905, 2.1875, 2.2143, 3.8529,
                    4.5625, 6.0215, 2.7109, 3.0469, 3.1528, 2.4167, 1.9167, 6.9276, 2.0833, 2.5313,
                    7.0893, 3.2031, 2.3854, 2.8542
                },
                ["median_house_value"] = new double[] {
                    452600, 358500, 352100, 176500, 270500, 330000, 81700, 67000, 67000, 194400,
                    147600, 137500, 241700, 125000, 204200, 435700, 412500, 98200, 102100, 189800,
                    221400, 258300, 137500, 154000, 172600, 225000, 104200, 375000, 85900, 118800,
                    406300, 186500, 100000, 150500
                }
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
                dataset.AddNumericColumn(column.Key, column.Value);
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
                    tabular.AddNumericColumn(column.Key, column.Value);
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
                var values = column.Value;
                var min = values.Min();
                var max = values.Max();
                var mean = values.Average();
                
                stats[column.Key] = $"min={min:F2}, max={max:F2}, mean={mean:F2}";
            }

            return stats;
        }
    }
}