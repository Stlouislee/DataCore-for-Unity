using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;
using NumSharp;

namespace AroAro.DataCore.Session
{
    /// <summary>
    /// TabularData与DataFrame之间的转换工具
    /// </summary>
    public static class DataFrameConverter
    {
        /// <summary>
        /// TabularData转换为DataFrame
        /// </summary>
        public static DataFrame TabularToDataFrame(Tabular.TabularData tabular)
        {
            if (tabular == null)
                throw new ArgumentNullException(nameof(tabular));

            var df = new DataFrame();

            foreach (var columnName in tabular.ColumnNames)
            {
                try
                {
                    // 优先尝试数值列
                    var numericData = tabular.GetNumericColumn(columnName);
                    var doubleData = numericData.ToArray<double>();
                    df.Columns.Add(new DoubleDataFrameColumn(columnName, doubleData));
                }
                catch
                {
                    // 如果数值列失败，尝试字符串列
                    try
                    {
                        var stringData = tabular.GetStringColumn(columnName);
                        df.Columns.Add(new StringDataFrameColumn(columnName, stringData));
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"Failed to convert column {columnName}: {ex.Message}");
                        // 创建空字符串列作为后备
                        df.Columns.Add(new StringDataFrameColumn(columnName, Enumerable.Repeat("", tabular.RowCount).ToArray()));
                    }
                }
            }

            return df;
        }

        /// <summary>
        /// DataFrame转换为TabularData
        /// </summary>
        public static Tabular.TabularData DataFrameToTabular(DataFrame df, string name)
        {
            if (df == null)
                throw new ArgumentNullException(nameof(df));

            var tabular = new Tabular.TabularData(name);

            foreach (var column in df.Columns)
            {
                try
                {
                    if (column is PrimitiveDataFrameColumn<double> doubleColumn)
                    {
                        var values = doubleColumn.ToArray();
                        tabular.AddNumericColumn(column.Name, np.array(values));
                    }
                    else if (column is PrimitiveDataFrameColumn<float> floatColumn)
                    {
                        var values = floatColumn.ToArray().Select(v => (double)v).ToArray();
                        tabular.AddNumericColumn(column.Name, np.array(values));
                    }
                    else if (column is PrimitiveDataFrameColumn<int> intColumn)
                    {
                        var values = intColumn.ToArray().Select(v => (double)v).ToArray();
                        tabular.AddNumericColumn(column.Name, np.array(values));
                    }
                    else if (column is StringDataFrameColumn stringColumn)
                    {
                        var values = stringColumn.ToArray();
                        tabular.AddStringColumn(column.Name, values);
                    }
                    else if (column is BooleanDataFrameColumn boolColumn)
                    {
                        var values = boolColumn.ToArray().Select(v => v.HasValue ? (v.Value ? "True" : "False") : "").ToArray();
                        tabular.AddStringColumn(column.Name, values);
                    }
                    else if (column is DateTimeDataFrameColumn dateColumn)
                    {
                        var values = dateColumn.ToArray().Select(d => d.HasValue ? d.Value.ToString("yyyy-MM-dd HH:mm:ss") : "").ToArray();
                        tabular.AddStringColumn(column.Name, values);
                    }
                    else
                    {
                        // 通用处理：转换为字符串
                        var values = new string[column.Length];
                        for (long i = 0; i < column.Length; i++)
                        {
                            var value = column[i];
                            values[i] = value?.ToString() ?? "";
                        }
                        tabular.AddStringColumn(column.Name, values);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"Failed to convert DataFrame column {column.Name}: {ex.Message}");
                }
            }

            return tabular;
        }

        /// <summary>
        /// 批量转换TabularData列表为DataFrame列表
        /// </summary>
        public static List<DataFrame> BatchTabularToDataFrame(IEnumerable<Tabular.TabularData> tabularList)
        {
            return tabularList.Select(t => TabularToDataFrame(t)).ToList();
        }

        /// <summary>
        /// 批量转换DataFrame列表为TabularData列表
        /// </summary>
        public static List<Tabular.TabularData> BatchDataFrameToTabular(IEnumerable<DataFrame> dfList, Func<DataFrame, string> nameSelector)
        {
            return dfList.Select(df => DataFrameToTabular(df, nameSelector(df))).ToList();
        }

        /// <summary>
        /// 获取DataFrame的统计信息
        /// </summary>
        public static Dictionary<string, object> GetDataFrameStatistics(DataFrame df)
        {
            var stats = new Dictionary<string, object>
            {
                ["RowCount"] = df.Rows.Count,
                ["ColumnCount"] = df.Columns.Count,
                ["ColumnNames"] = df.Columns.Select(c => c.Name).ToArray(),
                ["MemoryUsage"] = EstimateMemoryUsage(df)
            };

            // 为数值列添加详细统计信息
            foreach (var column in df.Columns)
            {
                var columnStats = new Dictionary<string, object>
                {
                    ["DataType"] = column.DataType.Name,
                    ["NullCount"] = column.NullCount,
                    ["NonNullCount"] = column.Length - column.NullCount
                };

                if (column is PrimitiveDataFrameColumn<double> numericColumn)
                {
                    columnStats["Min"] = numericColumn.Min();
                    columnStats["Max"] = numericColumn.Max();
                    columnStats["Mean"] = numericColumn.Mean();
                    columnStats["Sum"] = numericColumn.Sum();
                    columnStats["StdDev"] = CalculateStandardDeviation(numericColumn);
                }
                else if (column is StringDataFrameColumn stringColumn)
                {
                    var nonNullValues = stringColumn.Where(v => !string.IsNullOrEmpty(v)).ToArray();
                    columnStats["MinLength"] = nonNullValues.Any() ? nonNullValues.Min(v => v.Length) : 0;
                    columnStats["MaxLength"] = nonNullValues.Any() ? nonNullValues.Max(v => v.Length) : 0;
                    columnStats["AvgLength"] = nonNullValues.Any() ? nonNullValues.Average(v => v.Length) : 0;
                }

                stats[column.Name] = columnStats;
            }

            return stats;
        }

        /// <summary>
        /// 估算DataFrame的内存使用量
        /// </summary>
        private static long EstimateMemoryUsage(DataFrame df)
        {
            long totalBytes = 0;

            foreach (var column in df.Columns)
            {
                if (column is PrimitiveDataFrameColumn<double> doubleColumn)
                {
                    totalBytes += doubleColumn.Length * sizeof(double);
                }
                else if (column is PrimitiveDataFrameColumn<float> floatColumn)
                {
                    totalBytes += floatColumn.Length * sizeof(float);
                }
                else if (column is PrimitiveDataFrameColumn<int> intColumn)
                {
                    totalBytes += intColumn.Length * sizeof(int);
                }
                else if (column is StringDataFrameColumn stringColumn)
                {
                    totalBytes += stringColumn.Sum(s => s?.Length * sizeof(char) ?? 0);
                }
                else if (column is BooleanDataFrameColumn boolColumn)
                {
                    totalBytes += boolColumn.Length * sizeof(bool);
                }
                else
                {
                    // 通用估算
                    totalBytes += column.Length * 16; // 保守估计
                }
            }

            return totalBytes;
        }

        /// <summary>
        /// 计算标准差
        /// </summary>
        private static double CalculateStandardDeviation(PrimitiveDataFrameColumn<double> column)
        {
            if (column.Length == 0) return 0;
            
            var mean = column.Mean();
            var sumSquaredDifferences = column.Sum(v => Math.Pow((v ?? 0.0) - mean, 2));
            return Math.Sqrt((double)sumSquaredDifferences / column.Length);
        }

        /// <summary>
        /// 检查DataFrame是否与TabularData兼容
        /// </summary>
        public static bool AreCompatible(DataFrame df, Tabular.TabularData tabular)
        {
            if (df.Rows.Count != tabular.RowCount)
                return false;

            var dfColumns = df.Columns.Select(c => c.Name).ToHashSet();
            var tabularColumns = tabular.ColumnNames.ToHashSet();

            return dfColumns.SetEquals(tabularColumns);
        }

        /// <summary>
        /// 优化DataFrame内存使用
        /// </summary>
        public static DataFrame OptimizeMemory(DataFrame df)
        {
            var optimizedDf = new DataFrame();

            foreach (var column in df.Columns)
            {
                if (column is PrimitiveDataFrameColumn<double> doubleColumn)
                {
                    // 检查是否可以转换为更小的类型
                    var min = (double)doubleColumn.Min();
                    var max = (double)doubleColumn.Max();
                    
                    if (min >= float.MinValue && max <= float.MaxValue)
                    {
                        var floatData = doubleColumn.ToArray().Select(v => (float)v).ToArray();
                        optimizedDf.Columns.Add(new SingleDataFrameColumn(column.Name, floatData));
                    }
                    else
                    {
                        optimizedDf.Columns.Add(doubleColumn.Clone());
                    }
                }
                else if (column is StringDataFrameColumn stringColumn)
                {
                    // 使用Arrow字符串列节省内存
                    var stringData = stringColumn.ToArray();
                    optimizedDf.Columns.Add(new StringDataFrameColumn(column.Name, stringData));
                }
                else
                {
                    // 保持原样
                    optimizedDf.Columns.Add(column.Clone());
                }
            }

            return optimizedDf;
        }
    }
}