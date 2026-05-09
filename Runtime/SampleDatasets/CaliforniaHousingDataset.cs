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
                ["longitude"] = new double[] { -122.23, -122.22, -122.24, -118.3, -118.31, -117.81, -117.82, -119.67, -119.56, -121.43, -121.0558, -120.0805, -118.7976, -119.4675, -121.1439, -119.9715, -121.9829, -118.3505, -120.2785, -120.861, -121.2244, -119.1203, -118.9439, -118.6619, -118.7203, -120.4471, -120.0161, -118.3945, -121.632, -117.8105, -119.0667, -121.0923, -120.69, -119.519, -119.4566, -118.3892, -121.6921, -121.1488, -119.0592, -119.2678, -119.8219, -122.0648, -120.4776, -120.4896, -122.0241, -121.1494, -118.0425, -120.7184, -121.0051, -119.14, -121.6236, -121.4603, -122.2309, -119.848, -120.3277, -121.3258, -118.7669, -121.7352, -119.1964, -120.6791 },
                ["latitude"] = new double[] { 37.88, 37.86, 37.85, 34.26, 34.25, 33.78, 33.77, 36.33, 36.51, 38.63, 34.5728, 35.063, 37.2793, 37.1325, 38.6165, 35.6057, 34.5889, 36.656, 37.9002, 36.0376, 35.12, 38.1766, 38.0702, 36.3106, 35.7977, 35.7042, 37.1198, 35.7632, 38.3759, 35.5734, 37.9957, 34.9883, 36.9653, 37.8251, 37.5766, 34.6183, 36.5163, 37.453, 33.9079, 33.9405, 35.7104, 34.7205, 35.2508, 37.8631, 35.8375, 34.5381, 37.3532, 34.8251, 36.2287, 35.1623, 35.0003, 36.2869, 35.5696, 35.6668, 37.2369, 37.0242, 36.2413, 36.6833, 37.5663, 37.0171 },
                ["housing_median_age"] = new double[] { 41, 21, 52, 43, 27, 28, 19, 37, 43, 15, 41, 22, 17, 41, 40, 21, 25, 15, 29, 23, 26, 40, 49, 33, 28, 39, 35, 51, 27, 20, 24, 19, 21, 47, 33, 34, 34, 47, 32, 46, 47, 45, 41, 42, 34, 42, 34, 37, 24, 16, 23, 46, 33, 49, 42, 36, 29, 48, 47, 31 },
                ["total_rooms"] = new double[] { 880, 7099, 1467, 1510, 3589, 67, 1241, 1018, 1009, 3080, 1916, 2978, 6820, 1428, 4836, 3265, 2732, 3339, 1334, 1793, 2374, 668, 6101, 3765, 6343, 1444, 2243, 953, 1118, 5440, 2688, 1592, 6745, 1020, 4279, 4494, 3652, 466, 4755, 5805, 2657, 7050, 6098, 501, 5415, 6217, 2159, 4849, 1639, 2991, 1642, 2027, 1639, 4112, 6545, 6684, 3948, 4332, 3567, 1116 },
                ["total_bedrooms"] = new double[] { 129, 1106, 190, 310, 507, 15, 244, 213, 225, 617, 331, 815, 378, 1040, 1045, 30, 981, 1022, 608, 660, 547, 422, 85, 608, 217, 373, 209, 34, 337, 125, 559, 1013, 103, 554, 727, 207, 184, 810, 163, 534, 851, 706, 614, 689, 235, 1022, 717, 619, 398, 492, 624, 811, 765, 835, 421, 992, 344, 887, 611, 431 },
                ["population"] = new double[] { 322, 2401, 496, 809, 1484, 49, 850, 663, 604, 1446, 480, 74, 2061, 1618, 70, 435, 2196, 235, 261, 461, 1626, 2010, 182, 2107, 1308, 1392, 2246, 574, 1502, 824, 1497, 1876, 1576, 304, 421, 367, 934, 1953, 2250, 1773, 1162, 1256, 1838, 878, 1387, 1400, 614, 1298, 2241, 2398, 622, 2229, 1859, 410, 1359, 616, 1474, 691, 1613, 332 },
                ["households"] = new double[] { 126, 1138, 177, 277, 495, 11, 237, 204, 218, 599, 869, 81, 658, 466, 673, 629, 297, 86, 111, 585, 546, 592, 878, 98, 115, 244, 383, 227, 262, 86, 702, 718, 249, 429, 225, 671, 485, 125, 45, 1002, 535, 372, 1011, 322, 927, 312, 535, 282, 721, 633, 742, 615, 893, 1108, 636, 916, 1045, 178, 484, 613 },
                ["median_income"] = new double[] { 8.3252, 8.3014, 7.2574, 3.599, 5.7934, 6.1359, 2.9375, 1.6635, 1.6641, 3.6696, 5.1064, 3.0355, 7.4045, 1.8969, 1.8049, 5.1838, 4.6731, 2.0035, 5.7291, 6.7029, 8.0075, 4.7946, 2.035, 2.8985, 3.8767, 2.7637, 6.4002, 4.5685, 5.1998, 7.2698, 7.3503, 2.3711, 3.7948, 5.9117, 3.9008, 7.6096, 2.7355, 7.8349, 3.0765, 7.1271, 3.8419, 5.8553, 2.1936, 6.9007, 4.6093, 5.4964, 2.4711, 2.9696, 4.1581, 3.2651, 7.1939, 3.4202, 7.8329, 7.6612, 5.5227, 1.7002, 4.9237, 2.3291, 2.5704, 3.6546 },
                ["median_house_value"] = new double[] { 452600, 358500, 352100, 176500, 270500, 330000, 81700, 67000, 67000, 194400, 186000, 167800, 410200, 94000, 211200, 172000, 250600, 192500, 128500, 427200, 341300, 388200, 441500, 160100, 85400, 385800, 243000, 128100, 100500, 332100, 73000, 217900, 193900, 445900, 240300, 354400, 205100, 116400, 449600, 168700, 338100, 334500, 356200, 224200, 341100, 218100, 98300, 171900, 174900, 256600, 284500, 77500, 222200, 379400, 328500, 296700, 184300, 338300, 226300, 290100 }
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