using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataCore.Tensor;
using DataCore.DataFrame;
using DataCore.Graph;
using NumSharp;
using Microsoft.Data.Analysis;

namespace DataCore
{
    /// <summary>
    /// Data transformation pipeline for chaining operations
    /// </summary>
    public class DataPipeline
    {
        private readonly List<IPipelineStep> _steps;
        private readonly Dictionary<string, object> _context;
        private readonly TransformRegistry _transformRegistry;
        
        /// <summary>
        /// Pipeline execution context
        /// </summary>
        public IReadOnlyDictionary<string, object> Context => _context;
        
        public DataPipeline()
        {
            _steps = new List<IPipelineStep>();
            _context = new Dictionary<string, object>();
            _transformRegistry = TransformRegistry.Default;
        }
        
        /// <summary>
        /// Load data from a source
        /// </summary>
        public DataPipeline Load<T>(string source, Dictionary<string, object> parameters = null)
        {
            _steps.Add(new LoadStep<T>
            {
                Source = source,
                Parameters = parameters ?? new Dictionary<string, object>()
            });
            return this;
        }
        
        /// <summary>
        /// Load tensor data
        /// </summary>
        public DataPipeline LoadTensor(string source, Dictionary<string, object> parameters = null)
        {
            return Load<NDArray>(source, parameters);
        }
        
        /// <summary>
        /// Load DataFrame data
        /// </summary>
        public DataPipeline LoadDataFrame(string source, Dictionary<string, object> parameters = null)
        {
            return Load<Microsoft.Data.Analysis.DataFrame>(source, parameters);
        }
        
        /// <summary>
        /// Transform data using a function
        /// </summary>
        public DataPipeline Transform<T>(Func<T, T> transformFunc)
        {
            _steps.Add(new TransformStep<T>
            {
                TransformFunc = transformFunc
            });
            return this;
        }
        
        /// <summary>
        /// Transform data with parameters
        /// </summary>
        public DataPipeline Transform<T>(Func<T, Dictionary<string, object>, T> transformFunc, Dictionary<string, object> parameters)
        {
            _steps.Add(new TransformStep<T>
            {
                TransformFuncWithParams = transformFunc,
                Parameters = parameters
            });
            return this;
        }
        
        /// <summary>
        /// Convert data to a different type
        /// </summary>
        public DataPipeline Convert<TSource, TTarget>(Func<TSource, TTarget> convertFunc)
        {
            _steps.Add(new ConvertStep<TSource, TTarget>
            {
                ConvertFunc = convertFunc
            });
            return this;
        }
        
        /// <summary>
        /// Apply a registered transform
        /// </summary>
        public DataPipeline Apply(string transformName, Dictionary<string, object> parameters = null)
        {
            _steps.Add(new RegisteredTransformStep
            {
                TransformName = transformName,
                Parameters = parameters ?? new Dictionary<string, object>()
            });
            return this;
        }
        
        /// <summary>
        /// Filter data
        /// </summary>
        public DataPipeline Filter<T>(Func<T, bool> predicate)
        {
            _steps.Add(new FilterStep<T>
            {
                Predicate = predicate
            });
            return this;
        }
        
        /// <summary>
        /// Save data to a destination
        /// </summary>
        public DataPipeline Save(string destination, Dictionary<string, object> parameters = null)
        {
            _steps.Add(new SaveStep
            {
                Destination = destination,
                Parameters = parameters ?? new Dictionary<string, object>()
            });
            return this;
        }
        
        /// <summary>
        /// Execute the pipeline
        /// </summary>
        public async Task<object> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            object currentData = null;
            Type currentType = null;
            
            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var result = await step.ExecuteAsync(currentData, currentType, _context, cancellationToken);
                currentData = result.Data;
                currentType = result.DataType;
                
                // Store intermediate results in context
                if (!string.IsNullOrEmpty(step.StepId))
                {
                    _context[step.StepId] = currentData;
                }
            }
            
            return currentData;
        }
        
        /// <summary>
        /// Execute the pipeline and return typed result
        /// </summary>
        public async Task<T> ExecuteAsync<T>(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteAsync(cancellationToken);
            if (result is T typedResult)
            {
                return typedResult;
            }
            
            throw new InvalidCastException($"Cannot cast pipeline result to type {typeof(T).Name}");
        }
        
        /// <summary>
        /// Clear all steps
        /// </summary>
        public void Clear()
        {
            _steps.Clear();
            _context.Clear();
        }
        
        /// <summary>
        /// Get pipeline information
        /// </summary>
        public PipelineInfo GetInfo()
        {
            return new PipelineInfo
            {
                StepCount = _steps.Count,
                Steps = _steps.Select(s => s.GetInfo()).ToList(),
                ContextKeys = _context.Keys.ToList()
            };
        }
    }
    
    /// <summary>
    /// Pipeline step interface
    /// </summary>
    public interface IPipelineStep
    {
        string StepId { get; set; }
        Task<StepResult> ExecuteAsync(object data, Type dataType, Dictionary<string, object> context, CancellationToken cancellationToken);
        StepInfo GetInfo();
    }
    
    /// <summary>
    /// Pipeline step result
    /// </summary>
    public class StepResult
    {
        public object Data { get; set; }
        public Type DataType { get; set; }
    }
    
    /// <summary>
    /// Load step
    /// </summary>
    public class LoadStep<T> : IPipelineStep
    {
        public string StepId { get; set; }
        public string Source { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        
        public async Task<StepResult> ExecuteAsync(object data, Type dataType, Dictionary<string, object> context, CancellationToken cancellationToken)
        {
            // In a real implementation, this would load from file, database, etc.
            // For now, throw not implemented
            throw new NotImplementedException($"Load from {Source} not implemented");
        }
        
        public StepInfo GetInfo()
        {
            return new StepInfo
            {
                Type = "Load",
                Description = $"Load from {Source}",
                Parameters = Parameters
            };
        }
    }
    
    /// <summary>
    /// Transform step
    /// </summary>
    public class TransformStep<T> : IPipelineStep
    {
        public string StepId { get; set; }
        public Func<T, T> TransformFunc { get; set; }
        public Func<T, Dictionary<string, object>, T> TransformFuncWithParams { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        
        public Task<StepResult> ExecuteAsync(object data, Type dataType, Dictionary<string, object> context, CancellationToken cancellationToken)
        {
            if (data == null)
            {
                return Task.FromResult(new StepResult { Data = null, DataType = typeof(T) });
            }
            
            if (data is T typedData)
            {
                T result;
                if (TransformFunc != null)
                {
                    result = TransformFunc(typedData);
                }
                else if (TransformFuncWithParams != null)
                {
                    result = TransformFuncWithParams(typedData, Parameters);
                }
                else
                {
                    throw new InvalidOperationException("No transform function specified");
                }
                
                return Task.FromResult(new StepResult { Data = result, DataType = typeof(T) });
            }
            
            throw new InvalidCastException($"Cannot cast data to type {typeof(T).Name}");
        }
        
        public StepInfo GetInfo()
        {
            return new StepInfo
            {
                Type = "Transform",
                Description = "Transform data",
                Parameters = Parameters
            };
        }
    }
    
    /// <summary>
    /// Convert step
    /// </summary>
    public class ConvertStep<TSource, TTarget> : IPipelineStep
    {
        public string StepId { get; set; }
        public Func<TSource, TTarget> ConvertFunc { get; set; }
        
        public Task<StepResult> ExecuteAsync(object data, Type dataType, Dictionary<string, object> context, CancellationToken cancellationToken)
        {
            if (data == null)
            {
                return Task.FromResult(new StepResult { Data = null, DataType = typeof(TTarget) });
            }
            
            if (data is TSource typedData)
            {
                var result = ConvertFunc(typedData);
                return Task.FromResult(new StepResult { Data = result, DataType = typeof(TTarget) });
            }
            
            throw new InvalidCastException($"Cannot cast data to type {typeof(TSource).Name}");
        }
        
        public StepInfo GetInfo()
        {
            return new StepInfo
            {
                Type = "Convert",
                Description = $"Convert from {typeof(TSource).Name} to {typeof(TTarget).Name}",
                Parameters = new Dictionary<string, object>()
            };
        }
    }
    
    /// <summary>
    /// Registered transform step
    /// </summary>
    public class RegisteredTransformStep : IPipelineStep
    {
        public string StepId { get; set; }
        public string TransformName { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        
        public Task<StepResult> ExecuteAsync(object data, Type dataType, Dictionary<string, object> context, CancellationToken cancellationToken)
        {
            // This would use the TransformRegistry to find and execute the transform
            throw new NotImplementedException($"Registered transform {TransformName} not implemented");
        }
        
        public StepInfo GetInfo()
        {
            return new StepInfo
            {
                Type = "RegisteredTransform",
                Description = $"Apply {TransformName}",
                Parameters = Parameters
            };
        }
    }
    
    /// <summary>
    /// Filter step
    /// </summary>
    public class FilterStep<T> : IPipelineStep
    {
        public string StepId { get; set; }
        public Func<T, bool> Predicate { get; set; }
        
        public Task<StepResult> ExecuteAsync(object data, Type dataType, Dictionary<string, object> context, CancellationToken cancellationToken)
        {
            if (data == null)
            {
                return Task.FromResult(new StepResult { Data = null, DataType = typeof(T) });
            }
            
            if (data is T typedData)
            {
                // For DataFrame filtering
                if (typedData is Microsoft.Data.Analysis.DataFrame dataframe)
                {
                    // This would apply the predicate to filter rows
                    throw new NotImplementedException("DataFrame filtering not implemented");
                }
                
                // For NDArray filtering
                if (typedData is NDArray ndarray)
                {
                    // This would apply the predicate to filter elements
                    throw new NotImplementedException("NDArray filtering not implemented");
                }
                
                // For other types, just return as-is (filtering not supported)
                return Task.FromResult(new StepResult { Data = typedData, DataType = typeof(T) });
            }
            
            throw new InvalidCastException($"Cannot cast data to type {typeof(T).Name}");
        }
        
        public StepInfo GetInfo()
        {
            return new StepInfo
            {
                Type = "Filter",
                Description = "Filter data",
                Parameters = new Dictionary<string, object>()
            };
        }
    }
    
    /// <summary>
    /// Save step
    /// </summary>
    public class SaveStep : IPipelineStep
    {
        public string StepId { get; set; }
        public string Destination { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        
        public async Task<StepResult> ExecuteAsync(object data, Type dataType, Dictionary<string, object> context, CancellationToken cancellationToken)
        {
            // In a real implementation, this would save to file, database, etc.
            throw new NotImplementedException($"Save to {Destination} not implemented");
        }
        
        public StepInfo GetInfo()
        {
            return new StepInfo
            {
                Type = "Save",
                Description = $"Save to {Destination}",
                Parameters = Parameters
            };
        }
    }
    
    /// <summary>
    /// Step information
    /// </summary>
    public class StepInfo
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }
    
    /// <summary>
    /// Pipeline information
    /// </summary>
    public class PipelineInfo
    {
        public int StepCount { get; set; }
        public List<StepInfo> Steps { get; set; }
        public List<string> ContextKeys { get; set; }
    }
    
    /// <summary>
    /// Transform registry for custom transforms
    /// </summary>
    public class TransformRegistry
    {
        private static TransformRegistry _default;
        private readonly Dictionary<string, ITransform> _transforms;
        
        public static TransformRegistry Default
        {
            get
            {
                if (_default == null)
                {
                    _default = new TransformRegistry();
                    RegisterDefaultTransforms(_default);
                }
                return _default;
            }
        }
        
        public TransformRegistry()
        {
            _transforms = new Dictionary<string, ITransform>();
        }
        
        public void Register<TSource, TTarget>(string name, Func<TSource, Dictionary<string, object>, TTarget> transformFunc)
        {
            _transforms[name] = new Transform<TSource, TTarget>(transformFunc);
        }
        
        public ITransform Get(string name)
        {
            return _transforms.TryGetValue(name, out var transform) ? transform : null;
        }
        
        private static void RegisterDefaultTransforms(TransformRegistry registry)
        {
            // DataFrame to NDArray (feature matrix extraction)
            registry.Register<DataFrame, NDArray>("dataframe_to_tensor", (df, parameters) =>
            {
                var columnNames = parameters["columns"] as string[];
                if (columnNames == null)
                {
                    // Use all numeric columns
                    columnNames = df.Columns
                        .Where(c => c.DataType == typeof(double) || c.DataType == typeof(float) || c.DataType == typeof(int))
                        .Select(c => c.Name)
                        .ToArray();
                }
                
                var rows = (int)df.Rows.Count;
                var cols = columnNames.Length;
                var array = np.zeros((rows, cols), typeof(double));
                
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        array[i, j] = Convert.ToDouble(df[columnNames[j]][i]);
                    }
                }
                
                return array;
            });
            
            // NDArray to DataFrame
            registry.Register<NDArray, DataFrame>("tensor_to_dataframe", (array, parameters) =>
            {
                var columnNames = parameters["columnNames"] as string[];
                if (columnNames == null)
                {
                    // Generate default column names
                    columnNames = Enumerable.Range(0, array.shape[1])
                        .Select(i => $"Column{i}")
                        .ToArray();
                }
                
                var columns = new List<DataFrameColumn>();
                for (int j = 0; j < array.shape[1]; j++)
                {
                    var columnData = new double[array.shape[0]];
                    for (int i = 0; i < array.shape[0]; i++)
                    {
                        columnData[i] = array[i, j];
                    }
                    columns.Add(new DoubleDataFrameColumn(columnNames[j], columnData));
                }
                
                return new DataFrame(columns);
            });
            
            // Normalize NDArray
            registry.Register<NDArray, NDArray>("normalize", (array, parameters) =>
            {
                var axis = parameters.ContainsKey("axis") ? (int)parameters["axis"] : -1;
                var epsilon = parameters.ContainsKey("epsilon") ? (double)parameters["epsilon"] : 1e-8;
                
                if (axis == -1)
                {
                    // Global normalization
                    var mean = array.mean();
                    var std = array.std();
                    return (array - mean) / (std + epsilon);
                }
                else
                {
                    // Axis-wise normalization
                    var mean = array.mean(axis, keepdims: true);
                    var std = array.std(axis, keepdims: true);
                    return (array - mean) / (std + epsilon);
                }
            });
            
            // Standardize NDArray
            registry.Register<NDArray, NDArray>("standardize", (array, parameters) =>
            {
                var axis = parameters.ContainsKey("axis") ? (int)parameters["axis"] : -1;
                
                if (axis == -1)
                {
                    // Global standardization
                    var min = array.min();
                    var max = array.max();
                    return (array - min) / (max - min);
                }
                else
                {
                    // Axis-wise standardization
                    var min = array.min(axis, keepdims: true);
                    var max = array.max(axis, keepdims: true);
                    return (array - min) / (max - min);
                }
            });
        }
    }
    
    /// <summary>
    /// Transform interface
    /// </summary>
    public interface ITransform
    {
        object ExecuteTransform(object data, Dictionary<string, object> parameters);
        Type SourceType { get; }
        Type TargetType { get; }
    }
    
    /// <summary>
    /// Generic transform implementation
    /// </summary>
    public class Transform<TSource, TTarget> : ITransform
    {
        private readonly Func<TSource, Dictionary<string, object>, TTarget> _transformFunc;
        
        public Transform(Func<TSource, Dictionary<string, object>, TTarget> transformFunc)
        {
            _transformFunc = transformFunc;
        }
        
        public object ExecuteTransform(object data, Dictionary<string, object> parameters)
        {
            if (data is TSource typedData)
            {
                return _transformFunc(typedData, parameters);
            }
            
            throw new InvalidCastException($"Cannot cast data to type {typeof(TSource).Name}");
        }
        
        public Type SourceType => typeof(TSource);
        public Type TargetType => typeof(TTarget);
    }
}