using System;
using System.Collections.Generic;
using System.Linq;
using AroAro.DataCore.Tabular;
using NumSharp;

namespace AroAro.DataCore.Algorithms.Tabular
{
    /// <summary>
    /// Min-Max normalization for numeric columns.
    /// Scales each selected numeric column to the range [min, max] (default [0, 1]).
    /// 
    /// Formula:  x_norm = (x - col_min) / (col_max - col_min) * (max - min) + min
    /// 
    /// Output: a new TabularData with normalized columns.
    /// Metrics: per-column original min/max values, columns processed.
    /// 
    /// Parameters:
    ///   columns    (string[], default null) – columns to normalize; null = all numeric
    ///   rangeMin   (double, default 0.0)    – target range minimum
    ///   rangeMax   (double, default 1.0)    – target range maximum
    /// </summary>
    public class MinMaxNormalizeAlgorithm : TabularAlgorithmBase
    {
        public override string Name => "MinMaxNormalize";
        public override string Description => "Scales numeric columns to a target range using min-max normalization.";

        public override IReadOnlyList<AlgorithmParameterDescriptor> Parameters { get; } =
            new List<AlgorithmParameterDescriptor>
            {
                new("columns", "Column names to normalize (null = all numeric columns)", typeof(string[]), false, null),
                new("rangeMin", "Target range minimum", typeof(double), false, 0.0),
                new("rangeMax", "Target range maximum", typeof(double), false, 1.0),
            };

        public override IReadOnlyList<string> ValidateParameters(AlgorithmContext context)
        {
            var errors = new List<string>(base.ValidateParameters(context));

            double rangeMin = context.Get("rangeMin", 0.0);
            double rangeMax = context.Get("rangeMax", 1.0);

            if (rangeMin >= rangeMax)
                errors.Add($"rangeMin ({rangeMin}) must be less than rangeMax ({rangeMax}).");

            return errors;
        }

        protected override AlgorithmResult ExecuteTabular(ITabularDataset input, AlgorithmContext context)
        {
            double rangeMin = context.Get("rangeMin", 0.0);
            double rangeMax = context.Get("rangeMax", 1.0);
            double targetRange = rangeMax - rangeMin;

            // Determine which columns to normalize
            var requestedColumns = context.Get<string[]>("columns", null);
            var allColumns = input.ColumnNames.ToList();

            List<string> numericColumns;
            if (requestedColumns != null && requestedColumns.Length > 0)
            {
                // Validate requested columns exist and are numeric
                numericColumns = new List<string>();
                foreach (var col in requestedColumns)
                {
                    if (!input.HasColumn(col))
                        throw new ArgumentException($"Column '{col}' does not exist in dataset '{input.Name}'.");
                    if (input.GetColumnType(col) != ColumnType.Numeric)
                        throw new ArgumentException($"Column '{col}' is not numeric and cannot be normalized.");
                    numericColumns.Add(col);
                }
            }
            else
            {
                // All numeric columns
                numericColumns = allColumns
                    .Where(c => input.GetColumnType(c) == ColumnType.Numeric)
                    .ToList();
            }

            if (numericColumns.Count == 0)
            {
                throw new InvalidOperationException("No numeric columns found to normalize.");
            }

            // Build output dataset
            string outputName = ResolveOutputName(input, context, "Normalized");
            var output = new TabularData(outputName);

            var columnMetrics = new Dictionary<string, object>();
            int processed = 0;

            foreach (var colName in allColumns)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var colType = input.GetColumnType(colName);

                if (colType == ColumnType.Numeric)
                {
                    NDArray data = input.GetNumericColumn(colName);
                    double[] values = data.ToArray<double>();

                    if (numericColumns.Contains(colName))
                    {
                        // Normalize this column
                        double colMin = double.MaxValue;
                        double colMax = double.MinValue;

                        for (int i = 0; i < values.Length; i++)
                        {
                            if (values[i] < colMin) colMin = values[i];
                            if (values[i] > colMax) colMax = values[i];
                        }

                        double colRange = colMax - colMin;
                        double[] normalized = new double[values.Length];

                        if (colRange > 0)
                        {
                            for (int i = 0; i < values.Length; i++)
                            {
                                normalized[i] = ((values[i] - colMin) / colRange) * targetRange + rangeMin;
                            }
                        }
                        else
                        {
                            // All values are the same → map to rangeMin
                            for (int i = 0; i < normalized.Length; i++)
                                normalized[i] = rangeMin;
                        }

                        output.AddNumericColumn(colName, normalized);

                        columnMetrics[colName] = new Dictionary<string, double>
                        {
                            ["originalMin"] = colMin,
                            ["originalMax"] = colMax,
                            ["originalRange"] = colRange,
                        };

                        processed++;
                    }
                    else
                    {
                        // Copy unchanged
                        output.AddNumericColumn(colName, values);
                    }
                }
                else if (colType == ColumnType.String)
                {
                    // Copy string columns unchanged
                    string[] data = input.GetStringColumn(colName);
                    output.AddStringColumn(colName, data.ToArray());
                }

                context.ProgressCallback?.Invoke((float)(processed + 1) / allColumns.Count);
            }

            var metrics = new Dictionary<string, object>
            {
                ["columnsNormalized"] = processed,
                ["totalColumns"] = allColumns.Count,
                ["rangeMin"] = rangeMin,
                ["rangeMax"] = rangeMax,
                ["columnDetails"] = columnMetrics,
            };

            return AlgorithmResult.Succeeded(Name, output, metrics);
        }
    }
}
