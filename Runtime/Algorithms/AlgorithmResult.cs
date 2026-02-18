using System;
using System.Collections.Generic;

namespace AroAro.DataCore.Algorithms
{
    /// <summary>
    /// Immutable wrapper returned by every algorithm execution.
    /// Contains the output dataset (if any), computed metrics,
    /// execution metadata, and error information.
    /// </summary>
    public class AlgorithmResult
    {
        /// <summary>Whether the algorithm completed successfully.</summary>
        public bool Success { get; }

        /// <summary>
        /// The output dataset produced by the algorithm.
        /// May be <c>null</c> when the algorithm only computes metrics
        /// (e.g. centrality scores returned solely as metrics).
        /// </summary>
        public IDataSet OutputDataset { get; }

        /// <summary>
        /// Algorithm-specific computed values.
        /// Examples: "iterations" → 42, "converged" → true, "modularity" → 0.73
        /// </summary>
        public IReadOnlyDictionary<string, object> Metrics { get; }

        /// <summary>
        /// Execution metadata.
        /// Always contains "algorithmName", "duration" (TimeSpan).
        /// May include "inputRows", "inputNodes", "parametersUsed", etc.
        /// </summary>
        public IReadOnlyDictionary<string, object> Metadata { get; }

        /// <summary>Wall-clock execution time.</summary>
        public TimeSpan Duration { get; }

        /// <summary>Error message when <see cref="Success"/> is <c>false</c>.</summary>
        public string Error { get; }

        /// <summary>Name of the algorithm that produced this result.</summary>
        public string AlgorithmName { get; }

        private AlgorithmResult(
            bool success,
            string algorithmName,
            IDataSet outputDataset,
            IReadOnlyDictionary<string, object> metrics,
            IReadOnlyDictionary<string, object> metadata,
            TimeSpan duration,
            string error)
        {
            Success = success;
            AlgorithmName = algorithmName;
            OutputDataset = outputDataset;
            Metrics = metrics ?? new Dictionary<string, object>();
            Metadata = metadata ?? new Dictionary<string, object>();
            Duration = duration;
            Error = error;
        }

        #region Factory Methods

        /// <summary>
        /// Create a successful result with an output dataset and metrics.
        /// </summary>
        public static AlgorithmResult Succeeded(
            string algorithmName,
            IDataSet outputDataset,
            Dictionary<string, object> metrics = null,
            Dictionary<string, object> metadata = null,
            TimeSpan duration = default)
        {
            var meta = metadata ?? new Dictionary<string, object>();
            meta["algorithmName"] = algorithmName;
            meta["duration"] = duration;

            return new AlgorithmResult(
                success: true,
                algorithmName: algorithmName,
                outputDataset: outputDataset,
                metrics: metrics,
                metadata: meta,
                duration: duration,
                error: null);
        }

        /// <summary>
        /// Create a successful result with metrics only (no output dataset).
        /// </summary>
        public static AlgorithmResult MetricsOnly(
            string algorithmName,
            Dictionary<string, object> metrics,
            Dictionary<string, object> metadata = null,
            TimeSpan duration = default)
        {
            return Succeeded(algorithmName, null, metrics, metadata, duration);
        }

        /// <summary>
        /// Create a failed result.
        /// </summary>
        public static AlgorithmResult Failed(
            string algorithmName,
            string error,
            TimeSpan duration = default)
        {
            return new AlgorithmResult(
                success: false,
                algorithmName: algorithmName,
                outputDataset: null,
                metrics: null,
                metadata: new Dictionary<string, object>
                {
                    ["algorithmName"] = algorithmName,
                    ["duration"] = duration,
                },
                duration: duration,
                error: error);
        }

        #endregion

        public override string ToString()
        {
            if (Success)
                return $"[{AlgorithmName}] Succeeded in {Duration.TotalMilliseconds:F1}ms" +
                       (OutputDataset != null ? $" → {OutputDataset.Name}" : " (metrics only)");
            return $"[{AlgorithmName}] Failed: {Error}";
        }
    }
}
