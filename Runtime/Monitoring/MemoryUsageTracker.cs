using System;
using System.Collections.Generic;

namespace DataCore.Monitoring
{
    /// <summary>
    /// Tracks memory usage for different components
    /// </summary>
    public class MemoryUsageTracker
    {
        private readonly Dictionary<string, long> _memoryUsage;
        
        public MemoryUsageTracker()
        {
            _memoryUsage = new Dictionary<string, long>();
        }
        
        /// <summary>
        /// Record memory usage for a component
        /// </summary>
        public void RecordMemoryUsage(string componentName, long bytes)
        {
            _memoryUsage[componentName] = bytes;
        }
        
        /// <summary>
        /// Get memory usage for a component
        /// </summary>
        public long GetMemoryUsage(string componentName)
        {
            return _memoryUsage.TryGetValue(componentName, out var usage) ? usage : 0;
        }
        
        /// <summary>
        /// Get total memory usage
        /// </summary>
        public long GetTotalMemoryUsage()
        {
            long total = 0;
            foreach (var usage in _memoryUsage.Values)
            {
                total += usage;
            }
            return total;
        }
        
        /// <summary>
        /// Generate memory usage report
        /// </summary>
        public MemoryUsageReport GenerateReport()
        {
            return new MemoryUsageReport(_memoryUsage);
        }
        
        /// <summary>
        /// Clear all memory usage data
        /// </summary>
        public void Clear()
        {
            _memoryUsage.Clear();
        }
    }
    
    /// <summary>
    /// Memory usage report
    /// </summary>
    public class MemoryUsageReport
    {
        public Dictionary<string, long> ComponentMemory { get; }
        public long TotalMemory { get; }
        
        public MemoryUsageReport(Dictionary<string, long> memoryUsage)
        {
            ComponentMemory = new Dictionary<string, long>(memoryUsage);
            TotalMemory = 0;
            
            foreach (var usage in memoryUsage.Values)
            {
                TotalMemory += usage;
            }
        }
    }
}