using System;
using System.Collections.Generic;

namespace AroAro.DataCore.Algorithms
{
    /// <summary>
    /// Algorithm category — determines which dataset types an algorithm can process.
    /// </summary>
    public enum AlgorithmKind
    {
        /// <summary>Operates on <see cref="ITabularDataset"/>.</summary>
        Tabular = 1,

        /// <summary>Operates on <see cref="IGraphDataset"/>.</summary>
        Graph = 2,

        /// <summary>Operates on any <see cref="IDataSet"/>.</summary>
        Any = 3,
    }

    /// <summary>
    /// Base contract for every algorithm component in DataCore.
    /// Algorithms are stateless processors: they receive a dataset + context and
    /// return an <see cref="AlgorithmResult"/> containing the output dataset,
    /// computed metrics, and execution metadata.
    /// </summary>
    public interface IAlgorithm
    {
        /// <summary>Unique, human-readable name (e.g. "PageRank").</summary>
        string Name { get; }

        /// <summary>Short description of what the algorithm does.</summary>
        string Description { get; }

        /// <summary>Category of dataset this algorithm targets.</summary>
        AlgorithmKind Kind { get; }

        /// <summary>
        /// Declared parameter descriptors so callers and UIs know what to supply.
        /// </summary>
        IReadOnlyList<AlgorithmParameterDescriptor> Parameters { get; }

        /// <summary>
        /// Quick pre-flight check — returns <c>true</c> when the given dataset
        /// is compatible with this algorithm.
        /// </summary>
        bool CanExecute(IDataSet input);

        /// <summary>
        /// Validate parameter values before execution.
        /// Returns a list of validation error messages (empty = valid).
        /// </summary>
        IReadOnlyList<string> ValidateParameters(AlgorithmContext context);

        /// <summary>
        /// Execute this algorithm on <paramref name="input"/>.
        /// The input dataset is not mutated; results are returned in the
        /// <see cref="AlgorithmResult"/> wrapper.
        /// </summary>
        AlgorithmResult Execute(IDataSet input, AlgorithmContext context);
    }

    /// <summary>
    /// Typed convenience interface for algorithms that specifically target
    /// <see cref="ITabularDataset"/>.
    /// </summary>
    public interface ITabularAlgorithm : IAlgorithm
    {
        /// <summary>
        /// Typed execution entry point for tabular data.
        /// </summary>
        AlgorithmResult Execute(ITabularDataset input, AlgorithmContext context);
    }

    /// <summary>
    /// Typed convenience interface for algorithms that specifically target
    /// <see cref="IGraphDataset"/>.
    /// </summary>
    public interface IGraphAlgorithm : IAlgorithm
    {
        /// <summary>
        /// Typed execution entry point for graph data.
        /// </summary>
        AlgorithmResult Execute(IGraphDataset input, AlgorithmContext context);
    }

    /// <summary>
    /// Describes a single parameter that an algorithm accepts.
    /// Used by UIs and pipeline builders to present/validate parameters.
    /// </summary>
    public class AlgorithmParameterDescriptor
    {
        public string Name { get; }
        public string Description { get; }
        public Type ValueType { get; }
        public bool Required { get; }
        public object DefaultValue { get; }

        public AlgorithmParameterDescriptor(
            string name,
            string description,
            Type valueType,
            bool required = false,
            object defaultValue = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            Required = required;
            DefaultValue = defaultValue;
        }
    }
}
