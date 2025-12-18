using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using NumSharp;
using AroAro.DataCore.Tabular;

namespace AroAro.DataCore.Import
{
    /// <summary>
    /// Utility for importing CSV data into TabularData
    /// </summary>
    public static class CsvImporter
    {
        /// <summary>
        /// Parses a CSV string into a TabularData object.
        /// Assumes the first row is the header.
        /// Tries to parse columns as doubles, falls back to strings if parsing fails.
        /// </summary>
        /// <param name="csvText">The raw CSV text content</param>
        /// <param name="datasetName">Name for the resulting dataset</param>
        /// <returns>TabularData object or null if parsing fails</returns>
        public static TabularData Parse(string csvText, string datasetName)
        {
            if (string.IsNullOrWhiteSpace(csvText))
            {
                Debug.LogError("CSV text is empty");
                return null;
            }

            var lines = csvText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                Debug.LogError("CSV must have at least a header and one data row");
                return null;
            }

            var headers = lines[0].Trim().Replace("\"", "").Split(',');
            var columnData = new Dictionary<string, List<string>>();
            
            foreach (var header in headers)
            {
                columnData[header] = new List<string>();
            }

            // Read all data as strings first
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Simple CSV split (doesn't handle commas inside quotes yet)
                // For a robust solution, a proper CSV parser state machine is needed.
                // This is a basic implementation for the sample requirement.
                var values = line.Split(',');
                
                // Handle potential mismatch in column count
                for (int j = 0; j < headers.Length; j++)
                {
                    string val = (j < values.Length) ? values[j] : "";
                    columnData[headers[j]].Add(val);
                }
            }

            var dataset = new TabularData(datasetName);

            // Infer types and populate dataset
            foreach (var header in headers)
            {
                var rawValues = columnData[header];
                if (IsNumeric(rawValues, out var doubleValues))
                {
                    dataset.AddNumericColumn(header, np.array(doubleValues.ToArray()));
                }
                else
                {
                    dataset.AddStringColumn(header, rawValues.ToArray());
                }
            }

            return dataset;
        }

        private static bool IsNumeric(List<string> values, out List<double> doubleValues)
        {
            doubleValues = new List<double>();
            foreach (var val in values)
            {
                if (string.IsNullOrWhiteSpace(val))
                {
                    doubleValues.Add(0.0); // Handle missing values as 0 for numeric columns? Or maybe NaN? Using 0 for now to match previous logic.
                    continue;
                }

                if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                {
                    doubleValues.Add(d);
                }
                else
                {
                    return false; // Found a non-numeric value
                }
            }
            return true;
        }
    }
}
