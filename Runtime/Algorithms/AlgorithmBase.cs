using System;
using System.Collections.Generic;
using System.Diagnostics;
using AroAro.DataCore.Events;

namespace AroAro.DataCore.Algorithms
{
    /// <summary>
    /// Convenience base class that handles timing, validation,
    /// cancellation checks, and event firing so concrete algorithms
    /// only need to implement <see cref="ExecuteCore"/>.
    /// </summary>
    public abstract class AlgorithmBase : IAlgorithm
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract AlgorithmKind Kind { get; }

        /// <summary>
        /// Override to declare the parameters this algorithm accepts.
        /// Default: empty list.
        /// </summary>
        public virtual IReadOnlyList<AlgorithmParameterDescriptor> Parameters { get; }
            = Array.Empty<AlgorithmParameterDescriptor>();

        /// <summary>
        /// Validate that the input dataset is compatible.
        /// Default implementation checks <see cref="Kind"/> against
        /// <see cref="IDataSet.Kind"/>.
        /// </summary>
        public virtual bool CanExecute(IDataSet input)
        {
            if (input == null) return false;
            if (Kind == AlgorithmKind.Any) return true;
            return (int)input.Kind == (int)Kind;
        }

        /// <summary>
        /// Validate parameter values. Override to add custom rules.
        /// Default: validates required parameters are present.
        /// </summary>
        public virtual IReadOnlyList<string> ValidateParameters(AlgorithmContext context)
        {
            var errors = new List<string>();
            foreach (var p in Parameters)
            {
                if (p.Required && !context.Has(p.Name))
                    errors.Add($"Required parameter '{p.Name}' is missing.");
            }
            return errors;
        }

        /// <summary>
        /// Template-method implementation: validates, times, and wraps
        /// the call to <see cref="ExecuteCore"/>.
        /// </summary>
        public AlgorithmResult Execute(IDataSet input, AlgorithmContext context)
        {
            context = context ?? AlgorithmContext.Empty;
            var sw = Stopwatch.StartNew();

            try
            {
                // Pre-flight checks
                if (!CanExecute(input))
                {
                    return AlgorithmResult.Failed(Name,
                        $"Dataset '{input?.Name}' (kind={input?.Kind}) is not compatible with {Name} (expects {Kind}).",
                        sw.Elapsed);
                }

                var validationErrors = ValidateParameters(context);
                if (validationErrors.Count > 0)
                {
                    return AlgorithmResult.Failed(Name,
                        $"Parameter validation failed: {string.Join("; ", validationErrors)}",
                        sw.Elapsed);
                }

                context.CancellationToken.ThrowIfCancellationRequested();

                // Fire started event
                DataCoreEventManager.RaiseAlgorithmStarted(Name, input);

                // Execute the actual algorithm
                var result = ExecuteCore(input, context);

                sw.Stop();

                // Patch duration into result metadata
                var metadata = new Dictionary<string, object>(
                    result.Metadata as IDictionary<string, object> ?? new Dictionary<string, object>());
                metadata["algorithmName"] = Name;
                metadata["duration"] = sw.Elapsed;
                metadata["inputDataset"] = input?.Name;

                // Fire completed event
                DataCoreEventManager.RaiseAlgorithmCompleted(
                    Name, input, result.OutputDataset, true, sw.Elapsed);

                return AlgorithmResult.Succeeded(
                    Name,
                    result.OutputDataset,
                    result.Metrics as Dictionary<string, object>,
                    metadata,
                    sw.Elapsed);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                DataCoreEventManager.RaiseAlgorithmCompleted(Name, input, null, false, sw.Elapsed, "Cancelled");
                return AlgorithmResult.Failed(Name, "Algorithm execution was cancelled.", sw.Elapsed);
            }
            catch (Exception ex)
            {
                sw.Stop();
                DataCoreEventManager.RaiseAlgorithmCompleted(Name, input, null, false, sw.Elapsed, ex.Message);
                return AlgorithmResult.Failed(Name, ex.Message, sw.Elapsed);
            }
        }

        /// <summary>
        /// Core algorithm logic. Implement your algorithm here.
        /// Throw on error — the base class catches and wraps.
        /// </summary>
        protected abstract AlgorithmResult ExecuteCore(IDataSet input, AlgorithmContext context);

        /// <summary>
        /// Helper to resolve the output dataset name.
        /// Uses context.OutputName if set, otherwise generates from the input name.
        /// </summary>
        protected string ResolveOutputName(IDataSet input, AlgorithmContext context, string suffix = null)
        {
            if (!string.IsNullOrEmpty(context.OutputName))
                return context.OutputName;

            suffix = suffix ?? Name.Replace(" ", "");
            return $"{input.Name}_{suffix}";
        }
    }

    /// <summary>
    /// Typed base class for tabular algorithms.
    /// </summary>
    public abstract class TabularAlgorithmBase : AlgorithmBase, ITabularAlgorithm
    {
        public sealed override AlgorithmKind Kind => AlgorithmKind.Tabular;

        public AlgorithmResult Execute(ITabularDataset input, AlgorithmContext context)
        {
            return Execute((IDataSet)input, context);
        }

        protected sealed override AlgorithmResult ExecuteCore(IDataSet input, AlgorithmContext context)
        {
            return ExecuteTabular((ITabularDataset)input, context);
        }

        /// <summary>Implement tabular algorithm logic here.</summary>
        protected abstract AlgorithmResult ExecuteTabular(ITabularDataset input, AlgorithmContext context);
    }

    /// <summary>
    /// Typed base class for graph algorithms.
    /// </summary>
    public abstract class GraphAlgorithmBase : AlgorithmBase, IGraphAlgorithm
    {
        public sealed override AlgorithmKind Kind => AlgorithmKind.Graph;

        public AlgorithmResult Execute(IGraphDataset input, AlgorithmContext context)
        {
            return Execute((IDataSet)input, context);
        }

        protected sealed override AlgorithmResult ExecuteCore(IDataSet input, AlgorithmContext context)
        {
            return ExecuteGraph((IGraphDataset)input, context);
        }

        /// <summary>Implement graph algorithm logic here.</summary>
        protected abstract AlgorithmResult ExecuteGraph(IGraphDataset input, AlgorithmContext context);
    }
}
