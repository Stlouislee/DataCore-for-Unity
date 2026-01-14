using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

            var lines = csvText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < (hasHeader ? 2 : 1))
            {
                Debug.LogError("CSV must have at least one data row");
                return;
            }

            int dataStartIndex = hasHeader ? 1 : 0;
            string[] headers;
            
            if (hasHeader)
            {
                headers = lines[0].Trim().Replace("\"", "").Split(delimiter);
            }
            else
            {
                // 自动生成列名
                var firstLine = lines[0].Trim().Split(delimiter);
                headers = new string[firstLine.Length];
                for (int i = 0; i < firstLine.Length; i++)
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
            for (int i = dataStartIndex; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var values = line.Split(delimiter);
                
                for (int j = 0; j < headers.Length; j++)
                {
                    string val = (j < values.Length) ? values[j].Trim().Trim('"') : "";
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
        /// 从 CSV 文件解析数据并创建新的表格数据集
        /// </summary>
        /// <param name="store">数据存储</param>
        /// <param name="csvPath">CSV 文件路径</param>
        /// <param name="datasetName">数据集名称</param>
        /// <param name="hasHeader">是否包含表头</param>
        /// <param name="delimiter">分隔符</param>
        /// <returns>创建的表格数据集</returns>
        public static ITabularDataset ImportFromFile(IDataStore store, string csvPath, string datasetName, bool hasHeader = true, char delimiter = ',')
        {
            if (!File.Exists(csvPath))
            {
                Debug.LogError($"CSV file not found: {csvPath}");
                return null;
            }

            var csvText = File.ReadAllText(csvPath);
            var tabular = store.CreateTabular(datasetName);
            ImportToTabular(csvText, tabular, hasHeader, delimiter);
            return tabular;
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
            var tabular = store.CreateTabular(datasetName);
            ImportToTabular(csvText, tabular, hasHeader, delimiter);
            return tabular;
        }

        /// <summary>
        /// 使用 DataCoreStore 从 CSV 文本创建表格数据集
        /// </summary>
        public static ITabularDataset ImportFromText(DataCoreStore store, string csvText, string datasetName, bool hasHeader = true, char delimiter = ',')
        {
            var tabular = store.CreateTabular(datasetName);
            ImportToTabular(csvText, tabular, hasHeader, delimiter);
            return tabular;
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
