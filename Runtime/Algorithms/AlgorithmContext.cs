using System;
using System.Collections.Generic;
using System.Threading;

namespace AroAro.DataCore.Algorithms
{
    /// <summary>
    /// Execution context passed to algorithms.
    /// Provides parameters, cancellation, progress reporting,
    /// and optional access to the data store for multi-dataset operations.
    /// </summary>
    public class AlgorithmContext
    {
        private readonly Dictionary<string, object> _parameters;

        /// <summary>Read-only view of all supplied parameters.</summary>
        public IReadOnlyDictionary<string, object> Parameters => _parameters;

        /// <summary>Cancellation token for cooperative cancellation.</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Progress callback (0.0 → 1.0).
        /// Null when caller does not need progress updates.
        /// </summary>
        public Action<float> ProgressCallback { get; }

        /// <summary>
        /// Optional data store reference for algorithms that need
        /// to read additional datasets (e.g. joins).
        /// </summary>
        public IDataStore Store { get; }

        /// <summary>
        /// Name to assign to the output dataset.
        /// Algorithms should use this when creating their output.
        /// Falls back to a generated name when null.
        /// </summary>
        public string OutputName { get; }

        private AlgorithmContext(
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken,
            Action<float> progressCallback,
            IDataStore store,
            string outputName)
        {
            _parameters = parameters ?? new Dictionary<string, object>();
            CancellationToken = cancellationToken;
            ProgressCallback = progressCallback;
            Store = store;
            OutputName = outputName;
        }

        #region Parameter Access

        /// <summary>Get a typed parameter, throwing if missing or wrong type.</summary>
        public T GetRequired<T>(string name)
        {
            if (!_parameters.TryGetValue(name, out var value))
                throw new KeyNotFoundException($"Required parameter '{name}' not found.");

            if (value is T typed)
                return typed;

            // Attempt conversion for numeric types
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                throw new InvalidCastException(
                    $"Parameter '{name}' is {value?.GetType().Name ?? "null"}, expected {typeof(T).Name}.");
            }
        }

        /// <summary>Get a typed parameter or return <paramref name="defaultValue"/> if missing.</summary>
        public T Get<T>(string name, T defaultValue = default)
        {
            if (!_parameters.TryGetValue(name, out var value))
                return defaultValue;

            if (value is T typed)
                return typed;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>Check whether a parameter is present.</summary>
        public bool Has(string name) => _parameters.ContainsKey(name);

        #endregion

        #region Builder

        /// <summary>Create a new context builder.</summary>
        public static Builder Create() => new Builder();

        /// <summary>Fluent builder for <see cref="AlgorithmContext"/>.</summary>
        public class Builder
        {
            private readonly Dictionary<string, object> _params = new();
            private CancellationToken _ct = CancellationToken.None;
            private Action<float> _progress;
            private IDataStore _store;
            private string _outputName;

            /// <summary>Set a named parameter.</summary>
            public Builder WithParameter(string name, object value)
            {
                _params[name] = value;
                return this;
            }

            /// <summary>Set multiple parameters at once.</summary>
            public Builder WithParameters(IDictionary<string, object> parameters)
            {
                if (parameters != null)
                    foreach (var kvp in parameters)
                        _params[kvp.Key] = kvp.Value;
                return this;
            }

            /// <summary>Set the cancellation token.</summary>
            public Builder WithCancellation(CancellationToken ct)
            {
                _ct = ct;
                return this;
            }

            /// <summary>Set the progress callback.</summary>
            public Builder WithProgress(Action<float> callback)
            {
                _progress = callback;
                return this;
            }

            /// <summary>Set an optional data store reference.</summary>
            public Builder WithStore(IDataStore store)
            {
                _store = store;
                return this;
            }

            /// <summary>Set the desired name for the output dataset.</summary>
            public Builder WithOutputName(string name)
            {
                _outputName = name;
                return this;
            }

            /// <summary>Build the context.</summary>
            public AlgorithmContext Build()
            {
                return new AlgorithmContext(_params, _ct, _progress, _store, _outputName);
            }
        }

        #endregion

        /// <summary>Shorthand: create a context with no parameters.</summary>
        public static AlgorithmContext Empty => Create().Build();
    }
}
