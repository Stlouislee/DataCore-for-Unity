using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;

namespace AroAro.DataCore.Session
{
    /// <summary>
    /// DataFrame内存管理器，优化大数据集的内存使用
    /// </summary>
    public class DataFrameMemoryManager
    {
        private readonly Dictionary<string, WeakReference<DataFrame>> _weakDataFrames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Lazy<DataFrame>> _lazyDataFrames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DataFrameInfo> _dataFrameInfo = new(StringComparer.Ordinal);
        private readonly object _lock = new object();

        /// <summary>
        /// 注册DataFrame并跟踪内存使用
        /// </summary>
        public void RegisterDataFrame(string name, DataFrame df)
        {
            lock (_lock)
            {
                _weakDataFrames[name] = new WeakReference<DataFrame>(df);
                _dataFrameInfo[name] = new DataFrameInfo
                {
                    Name = name,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow,
                    EstimatedMemory = EstimateMemoryUsage(df),
                    RowCount = df.Rows.Count,
                    ColumnCount = df.Columns.Count
                };
            }
        }

        /// <summary>
        /// 注册懒加载DataFrame
        /// </summary>
        public void RegisterLazyDataFrame(string name, Func<DataFrame> factory)
        {
            lock (_lock)
            {
                _lazyDataFrames[name] = new Lazy<DataFrame>(factory);
                _dataFrameInfo[name] = new DataFrameInfo
                {
                    Name = name,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow,
                    IsLazy = true,
                    EstimatedMemory = 0 // 初始内存使用为0
                };
            }
        }

        /// <summary>
        /// 获取DataFrame（支持懒加载）
        /// </summary>
        public DataFrame GetDataFrame(string name)
        {
            lock (_lock)
            {
                if (_lazyDataFrames.TryGetValue(name, out var lazy))
                {
                    var dataFrame = lazy.Value;
                    // 更新内存信息
                    if (_dataFrameInfo.TryGetValue(name, out var info))
                    {
                        info.EstimatedMemory = EstimateMemoryUsage(dataFrame);
                        info.RowCount = dataFrame.Rows.Count;
                        info.ColumnCount = dataFrame.Columns.Count;
                        info.LastAccessed = DateTime.UtcNow;
                    }
                    return dataFrame;
                }

                if (_weakDataFrames.TryGetValue(name, out var weakRef) && weakRef.TryGetTarget(out var weakDataFrame))
                {
                    // 更新访问时间
                    if (_dataFrameInfo.TryGetValue(name, out var info))
                    {
                        info.LastAccessed = DateTime.UtcNow;
                    }
                    return weakDataFrame;
                }

                throw new KeyNotFoundException($"DataFrame '{name}' not found");
            }
        }

        /// <summary>
        /// 检查DataFrame是否存在
        /// </summary>
        public bool HasDataFrame(string name)
        {
            lock (_lock)
            {
                return _lazyDataFrames.ContainsKey(name) || 
                       (_weakDataFrames.TryGetValue(name, out var weakRef) && weakRef.TryGetTarget(out _));
            }
        }

        /// <summary>
        /// 移除DataFrame
        /// </summary>
        public bool RemoveDataFrame(string name)
        {
            lock (_lock)
            {
                var removed = false;
                
                if (_lazyDataFrames.Remove(name))
                    removed = true;
                
                if (_weakDataFrames.Remove(name))
                    removed = true;
                
                _dataFrameInfo.Remove(name);
                
                return removed;
            }
        }

        /// <summary>
        /// 清理无效的弱引用
        /// </summary>
        public int CleanupWeakReferences()
        {
            lock (_lock)
            {
                var toRemove = _weakDataFrames
                    .Where(kv => !kv.Value.TryGetTarget(out _))
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in toRemove)
                {
                    _weakDataFrames.Remove(key);
                    _dataFrameInfo.Remove(key);
                }

                return toRemove.Count;
            }
        }

        /// <summary>
        /// 清理长时间未访问的DataFrame
        /// </summary>
        public int CleanupIdleDataFrames(TimeSpan idleThreshold)
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.UtcNow - idleThreshold;
                var toRemove = _dataFrameInfo
                    .Where(kv => kv.Value.LastAccessed < cutoffTime)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in toRemove)
                {
                    _lazyDataFrames.Remove(key);
                    _weakDataFrames.Remove(key);
                    _dataFrameInfo.Remove(key);
                }

                return toRemove.Count;
            }
        }

        /// <summary>
        /// 获取内存使用统计
        /// </summary>
        public MemoryStatistics GetMemoryStatistics()
        {
            lock (_lock)
            {
                var stats = new MemoryStatistics
                {
                    TotalDataFrames = _dataFrameInfo.Count,
                    LazyDataFrames = _lazyDataFrames.Count,
                    WeakDataFrames = _weakDataFrames.Count(kv => kv.Value.TryGetTarget(out _)),
                    TotalMemoryBytes = _dataFrameInfo.Values.Sum(info => info.EstimatedMemory),
                    AverageMemoryPerDataFrame = _dataFrameInfo.Count > 0 ? 
                        _dataFrameInfo.Values.Average(info => info.EstimatedMemory) : 0
                };

                return stats;
            }
        }

        /// <summary>
        /// 获取所有DataFrame的信息
        /// </summary>
        public IReadOnlyList<DataFrameInfo> GetAllDataFrameInfo()
        {
            lock (_lock)
            {
                return _dataFrameInfo.Values.ToList();
            }
        }

        /// <summary>
        /// 估算DataFrame的内存使用量
        /// </summary>
        private long EstimateMemoryUsage(DataFrame df)
        {
            return DataFrameUtils.EstimateMemoryUsage(df);
        }

        /// <summary>
        /// 优化DataFrame内存使用
        /// </summary>
        public DataFrame OptimizeMemory(DataFrame df)
        {
            return DataFrameUtils.OptimizeMemory(df);
        }
    }

    /// <summary>
    /// DataFrame信息类
    /// </summary>
    public class DataFrameInfo
    {
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public long EstimatedMemory { get; set; }
        public long RowCount { get; set; }
        public int ColumnCount { get; set; }
        public bool IsLazy { get; set; }

        public override string ToString()
        {
            return $"DataFrame: {Name}, Memory: {FormatMemory(EstimatedMemory)}, Rows: {RowCount}, Columns: {ColumnCount}";
        }

        private string FormatMemory(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// 内存统计信息
    /// </summary>
    public class MemoryStatistics
    {
        public int TotalDataFrames { get; set; }
        public int LazyDataFrames { get; set; }
        public int WeakDataFrames { get; set; }
        public long TotalMemoryBytes { get; set; }
        public double AverageMemoryPerDataFrame { get; set; }

        public string TotalMemoryFormatted => FormatMemory(TotalMemoryBytes);
        public string AverageMemoryFormatted => FormatMemory((long)AverageMemoryPerDataFrame);

        private string FormatMemory(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public override string ToString()
        {
            return $"Total: {TotalDataFrames} DataFrames, Memory: {TotalMemoryFormatted}, Avg: {AverageMemoryFormatted}";
        }
    }
}