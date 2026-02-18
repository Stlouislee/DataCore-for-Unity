using System;
using System.Collections.Generic;
using System.Linq;

namespace AroAro.DataCore.Algorithms
{
    /// <summary>
    /// Central registry for discovering and looking up algorithm components.
    /// Acts as a service locator: algorithms register themselves (or are
    /// registered at startup), and consumers query by name, kind, or tag.
    /// Thread-safe for read operations after initial registration.
    /// </summary>
    public class AlgorithmRegistry
    {
        private static AlgorithmRegistry _default;

        /// <summary>
        /// Shared default registry instance.
        /// Built-in algorithms are registered here automatically.
        /// </summary>
        public static AlgorithmRegistry Default
        {
            get
            {
                if (_default == null)
                {
                    _default = new AlgorithmRegistry();
                    _default.RegisterBuiltIns();
                }
                return _default;
            }
        }

        private readonly Dictionary<string, IAlgorithm> _algorithms = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        /// <summary>Register an algorithm. Overwrites if same name exists.</summary>
        public void Register(IAlgorithm algorithm)
        {
            if (algorithm == null) throw new ArgumentNullException(nameof(algorithm));
            lock (_lock)
            {
                _algorithms[algorithm.Name] = algorithm;
            }
        }

        /// <summary>Unregister an algorithm by name.</summary>
        public bool Unregister(string name)
        {
            lock (_lock)
            {
                return _algorithms.Remove(name);
            }
        }

        /// <summary>Get an algorithm by exact name (case-insensitive).</summary>
        public IAlgorithm Get(string name)
        {
            if (_algorithms.TryGetValue(name, out var algo))
                return algo;
            throw new KeyNotFoundException($"Algorithm '{name}' is not registered.");
        }

        /// <summary>Try to get an algorithm by name.</summary>
        public bool TryGet(string name, out IAlgorithm algorithm)
        {
            return _algorithms.TryGetValue(name, out algorithm);
        }

        /// <summary>All registered algorithms.</summary>
        public IReadOnlyList<IAlgorithm> GetAll()
        {
            return _algorithms.Values.ToList().AsReadOnly();
        }

        /// <summary>Get all algorithms matching a kind.</summary>
        public IReadOnlyList<IAlgorithm> GetByKind(AlgorithmKind kind)
        {
            return _algorithms.Values
                .Where(a => a.Kind == kind || a.Kind == AlgorithmKind.Any)
                .ToList().AsReadOnly();
        }

        /// <summary>Get all algorithm names.</summary>
        public IReadOnlyList<string> GetNames()
        {
            return _algorithms.Keys.ToList().AsReadOnly();
        }

        /// <summary>Check if an algorithm is registered.</summary>
        public bool Contains(string name) => _algorithms.ContainsKey(name);

        /// <summary>Number of registered algorithms.</summary>
        public int Count => _algorithms.Count;

        /// <summary>Clear all registrations.</summary>
        public void Clear()
        {
            lock (_lock)
            {
                _algorithms.Clear();
            }
        }

        /// <summary>Reset the default registry (useful for testing).</summary>
        public static void ResetDefault()
        {
            _default = null;
        }

        /// <summary>
        /// Register all built-in algorithms shipped with DataCore.
        /// Called automatically when <see cref="Default"/> is first accessed.
        /// </summary>
        private void RegisterBuiltIns()
        {
            // Graph algorithms
            Register(new Graph.PageRankAlgorithm());
            Register(new Graph.ConnectedComponentsAlgorithm());

            // Tabular algorithms
            Register(new Tabular.MinMaxNormalizeAlgorithm());
        }
    }
}
