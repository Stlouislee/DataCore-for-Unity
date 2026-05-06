using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AroAro.DataCore.Events;
using UnityEngine;

namespace AroAro.DataCore.Import
{
    /// <summary>
    /// Utility for importing CSV data into ITabularDataset
    /// </summary>
    public static class CsvImporter
    {
        /// <summary>
        /// 解析 CSV 文本并导入到现有的表格数据集中
        /// </summary>
        /// <param name="csvText">CSV 文本内容</param>
        /// <param name="tabular">目标表格数据集</param>
        /// <param name="hasHeader">是否包含表头</param>
        /// <param name="delimiter">分隔符</param>
        public static void ImportToTabular(string csvText, ITabularDataset tabular, bool hasHeader = true, char delimiter = ',')
        {
            if (string.IsNullOrWhiteSpace(csvText))
            {
                Debug.LogError("CSV text is empty");
                return;
            }

            var rows = CsvParser.ParseAll(csvText, delimiter);
            if (rows.Count < (hasHeader ? 2 : 1))
            {
                Debug.LogError("CSV must have at least one data row");
                return;
            }

            int dataStartIndex = hasHeader ? 1 : 0;
            string[] headers;

            if (hasHeader)
            {
                headers = rows[0].ToArray();
            }
            else
            {
                headers = new string[rows[0].Count];
                for (int i = 0; i < rows[0].Count; i++)
                {
                    headers[i] = $"Column{i + 1}";
                }
            }

            var columnData = new Dictionary<string, List<string>>();
            foreach (var header in headers)
            {
                columnData[header] = new List<string>();
            }

            // 读取数据
            for (int i = dataStartIndex; i < rows.Count; i++)
            {
                var values = rows[i];

                for (int j = 0; j < headers.Length; j++)
                {
                    string val = (j < values.Count) ? values[j] : "";
                    columnData[headers[j]].Add(val);
                }
            }

            // 推断类型并添加列
            foreach (var header in headers)
            {
                var rawValues = columnData[header];
                if (IsNumeric(rawValues, out var doubleValues))
                {
                    tabular.AddNumericColumn(header, doubleValues.ToArray());
                }
                else
                {
                    tabular.AddStringColumn(header, rawValues.ToArray());
                }
            }
        }

        /// <summary>
        /// 从 CSV 文件解析数据并创建新的表格数据集（使用 DataCoreStore，会触发事件）
        /// </summary>
        public static ITabularDataset ImportFromFile(DataCoreStore store, string csvPath, string datasetName, bool hasHeader = true, char delimiter = ',')
        {
            if (!File.Exists(csvPath))
            {
                Debug.LogError($"CSV file not found: {csvPath}");
                return null;
            }

            var csvText = File.ReadAllText(csvPath);
            var result = store.UnderlyingStore.ExecuteInTransaction(() =>
            {
                var tabular = store.CreateTabular(datasetName);
                tabular.ImportFromCsv(csvText, hasHeader, delimiter);
                return tabular;
            });
            DataCoreEventManager.RaiseDatasetImportCompleted(result);
            return result;
        }

        /// <summary>
        /// 从 CSV 文件解析数据并创建新的表格数据集（底层 IDataStore，不触发事件）
        /// </summary>
        public static ITabularDataset ImportFromFile(IDataStore store, string csvPath, string datasetName, bool hasHeader = true, char delimiter = ',')
        {
            if (!File.Exists(csvPath))
            {
                Debug.LogError($"CSV file not found: {csvPath}");
                return null;
            }

            var csvText = File.ReadAllText(csvPath);
            return store.ExecuteInTransaction(() =>
            {
                var tabular = store.CreateTabular(datasetName);
                tabular.ImportFromCsv(csvText, hasHeader, delimiter);
                return tabular;
            });
        }

        /// <summary>
        /// 从 CSV 文本解析数据并创建新的表格数据集
        /// </summary>
        /// <param name="store">数据存储</param>
        /// <param name="csvText">CSV 文本内容</param>
        /// <param name="datasetName">数据集名称</param>
        /// <param name="hasHeader">是否包含表头</param>
        /// <param name="delimiter">分隔符</param>
        /// <returns>创建的表格数据集</returns>
        public static ITabularDataset ImportFromText(IDataStore store, string csvText, string datasetName, bool hasHeader = true, char delimiter = ',')
        {
            return store.ExecuteInTransaction(() =>
            {
                var tabular = store.CreateTabular(datasetName);
                tabular.ImportFromCsv(csvText, hasHeader, delimiter);
                return tabular;
            });
        }

        /// <summary>
        /// 使用 DataCoreStore 从 CSV 文本创建表格数据集（会触发 DatasetCreated 事件）
        /// </summary>
        public static ITabularDataset ImportFromText(DataCoreStore store, string csvText, string datasetName, bool hasHeader = true, char delimiter = ',')
        {
            var result = store.UnderlyingStore.ExecuteInTransaction(() =>
            {
                var tabular = store.CreateTabular(datasetName);
                tabular.ImportFromCsv(csvText, hasHeader, delimiter);
                return tabular;
            });
            DataCoreEventManager.RaiseDatasetImportCompleted(result);
            return result;
        }

        private static bool IsNumeric(List<string> values, out List<double> doubleValues)
        {
            doubleValues = new List<double>();
            foreach (var val in values)
            {
                if (string.IsNullOrWhiteSpace(val))
                {
                    doubleValues.Add(double.NaN);
                    continue;
                }

                if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                {
                    doubleValues.Add(d);
                }
                else
                {
                    return false;
                }
            }
            return true;
        }
    }
}
