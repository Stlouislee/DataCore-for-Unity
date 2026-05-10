using System;
using System.Linq;
using Microsoft.Data.Analysis;

namespace AroAro.DataCore.Session
{
    /// <summary>
    /// Shared utilities for DataFrame operations — memory estimation and optimization.
    /// Extracted from DataFrameConverter and DataFrameMemoryManager to eliminate duplication (#98).
    /// </summary>
    internal static class DataFrameUtils
    {
        /// <summary>
        /// Estimate memory usage of a DataFrame in bytes
        /// </summary>
        public static long EstimateMemoryUsage(DataFrame df)
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
                else if (column is PrimitiveDataFrameColumn<long> longColumn)
                {
                    totalBytes += longColumn.Length * sizeof(long);
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
                    // Conservative estimate for unknown types
                    totalBytes += column.Length * 16;
                }
            }

            return totalBytes;
        }

        /// <summary>
        /// Optimize DataFrame memory by downgrading double columns to float where safe
        /// </summary>
        public static DataFrame OptimizeMemory(DataFrame df)
        {
            var optimizedDf = new DataFrame();

            foreach (var column in df.Columns)
            {
                if (column is PrimitiveDataFrameColumn<double> doubleColumn)
                {
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
                    var stringData = stringColumn.ToArray();
                    optimizedDf.Columns.Add(new StringDataFrameColumn(column.Name, stringData));
                }
                else
                {
                    optimizedDf.Columns.Add(column.Clone());
                }
            }

            return optimizedDf;
        }
    }
}
