using System;
using System.Collections.Generic;
using System.Diagnostics;
using AroAro.DataCore.Events;

namespace AroAro.DataCore.Algorithms
{
    /// <summary>
    /// Composable pipeline that chains algorithms sequentially.
    /// The output dataset of step N becomes the input of step N+1.
    /// Each step's result (metrics, metadata) is preserved in
    /// <see cref="PipelineResult.StepResults"/>.
    /// </summary>
    public class AlgorithmPipeline
    {
        private readonly List<PipelineStep> _steps = new();

        /// <summary>The ordered steps in this pipeline.</summary>
        public IReadOnlyList<PipelineStep> Steps => _steps.AsReadOnly();

        /// <summary>Optional human-readable name for the pipeline.</summary>
        public string Name { get; set; }

        public AlgorithmPipeline(string name = null)
        {
            Name = name ?? "Pipeline";
        }

        #region Fluent Builder

        /// <summary>
        /// Add an algorithm step with its own parameter overrides.
        /// </summary>
        public AlgorithmPipeline Add(IAlgorithm algorithm, Action<AlgorithmContext.Builder> configure = null)
        {
            _steps.Add(new PipelineStep(algorithm, configure));
            return this;
        }

        /// <summary>
        /// Add an algorithm step with pre-built parameters.
        /// </summary>
        public AlgorithmPipeline Add(IAlgorithm algorithm, IDictionary<string, object> parameters)
        {
            _steps.Add(new PipelineStep(algorithm, builder =>
            {
                builder.WithParameters(parameters);
            }));
            return this;
        }

        #endregion

        /// <summary>
        /// Execute the entire pipeline.
        /// Each step receives the previous step's output dataset as input.
        /// Stops immediately on the first failure.
        /// </summary>
        public PipelineResult Execute(IDataSet input, AlgorithmContext baseContext = null)
        {
            baseContext = baseContext ?? AlgorithmContext.Empty;

            var stepResults = new List<AlgorithmResult>();
            var sw = Stopwatch.StartNew();
            var currentInput = input;

            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];

                baseContext.CancellationToken.ThrowIfCancellationRequested();

                // Build step-specific context (inherits base cancellation + store)
                var stepBuilder = AlgorithmContext.Create()
                    .WithCancellation(baseContext.CancellationToken)
                    .WithStore(baseContext.Store);

                // Apply step-specific configuration
                step.Configure?.Invoke(stepBuilder);

                // Progress: partition the 0→1 range across steps
                if (baseContext.ProgressCallback != null)
                {
                    float stepStart = (float)i / _steps.Count;
                    float stepRange = 1f / _steps.Count;
                    stepBuilder.WithProgress(p =>
                        baseContext.ProgressCallback(stepStart + p * stepRange));
                }

                var stepContext = stepBuilder.Build();

                // Execute
                var result = step.Algorithm.Execute(currentInput, stepContext);
                stepResults.Add(result);

                if (!result.Success)
                {
                    sw.Stop();
                    var failResult = PipelineResult.Failed(Name, stepResults, i, result.Error, sw.Elapsed);
                    DataCoreEventManager.RaisePipelineCompleted(Name, _steps.Count, false, sw.Elapsed, i);
                    return failResult;
                }

                // Chain: output becomes next input (if available)
                if (result.OutputDataset != null)
                {
                    currentInput = result.OutputDataset;
                }
                // If a step returns metrics only, the same input continues
            }

            sw.Stop();
            var pipelineResult = PipelineResult.Succeeded(Name, currentInput, stepResults, sw.Elapsed);
            DataCoreEventManager.RaisePipelineCompleted(Name, _steps.Count, true, sw.Elapsed);
            return pipelineResult;
        }
    }

    /// <summary>
    /// A single step in the pipeline.
    /// </summary>
    public class PipelineStep
    {
        public IAlgorithm Algorithm { get; }
        internal Action<AlgorithmContext.Builder> Configure { get; }

        public PipelineStep(IAlgorithm algorithm, Action<AlgorithmContext.Builder> configure = null)
        {
            Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
            Configure = configure;
        }
    }

    /// <summary>
    /// Result from executing an entire pipeline.
    /// </summary>
    public class PipelineResult
    {
        /// <summary>Whether all steps succeeded.</summary>
        public bool Success { get; }

        /// <summary>Pipeline name.</summary>
        public string PipelineName { get; }

        /// <summary>The final output dataset (from the last step that produced one).</summary>
        public IDataSet FinalOutput { get; }

        /// <summary>Individual results from each step, in execution order.</summary>
        public IReadOnlyList<AlgorithmResult> StepResults { get; }

        /// <summary>Index of the failed step (-1 if all succeeded).</summary>
        public int FailedStepIndex { get; }

        /// <summary>Error message from the failed step.</summary>
        public string Error { get; }

        /// <summary>Total pipeline wall-clock duration.</summary>
        public TimeSpan TotalDuration { get; }

        private PipelineResult(
            bool success,
            string pipelineName,
            IDataSet finalOutput,
            IReadOnlyList<AlgorithmResult> stepResults,
            int failedStepIndex,
            string error,
            TimeSpan totalDuration)
        {
            Success = success;
            PipelineName = pipelineName;
            FinalOutput = finalOutput;
            StepResults = stepResults;
            FailedStepIndex = failedStepIndex;
            Error = error;
            TotalDuration = totalDuration;
        }

        public static PipelineResult Succeeded(
            string name,
            IDataSet finalOutput,
            List<AlgorithmResult> stepResults,
            TimeSpan duration)
        {
            return new PipelineResult(true, name, finalOutput, stepResults, -1, null, duration);
        }

        public static PipelineResult Failed(
            string name,
            List<AlgorithmResult> stepResults,
            int failedStep,
            string error,
            TimeSpan duration)
        {
            return new PipelineResult(false, name, null, stepResults, failedStep, error, duration);
        }

        /// <summary>
        /// Convenience: aggregate all metrics from all steps into one dictionary.
        /// Keys are prefixed with step index: "0.iterations", "1.modularity", etc.
        /// </summary>
        public Dictionary<string, object> GetAllMetrics()
        {
            var all = new Dictionary<string, object>();
            for (int i = 0; i < StepResults.Count; i++)
            {
                foreach (var kvp in StepResults[i].Metrics)
                {
                    all[$"{i}.{StepResults[i].AlgorithmName}.{kvp.Key}"] = kvp.Value;
                }
            }
            return all;
        }

        public override string ToString()
        {
            if (Success)
                return $"[{PipelineName}] Completed {StepResults.Count} steps in {TotalDuration.TotalMilliseconds:F1}ms";
            return $"[{PipelineName}] Failed at step {FailedStepIndex}: {Error}";
        }
    }
}
