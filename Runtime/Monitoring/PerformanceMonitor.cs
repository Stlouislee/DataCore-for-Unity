using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace DataCore.Monitoring
{
    /// <summary>
    /// Simple performance monitor for memory and basic timing metrics
    /// </summary>
    public class PerformanceMonitor
    {
        private long _cacheHits;
        private long _cacheMisses;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public long CacheHits => _cacheHits;
        public long CacheMisses => _cacheMisses;

        public void Start() => _stopwatch.Start();
        public void Stop() => _stopwatch.Stop();
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public void Reset()
        {
            _stopwatch.Reset();
            Interlocked.Exchange(ref _cacheHits, 0);
            Interlocked.Exchange(ref _cacheMisses, 0);
        }

        public void RecordCacheHit(CacheLevelType level = CacheLevelType.L1)
        {
            Interlocked.Increment(ref _cacheHits);
        }

        public void RecordCacheMiss()
        {
            Interlocked.Increment(ref _cacheMisses);
        }

        public PerformanceReport GenerateReport()
        {
            return new PerformanceReport
            {
                Uptime = _stopwatch.Elapsed,
                CacheHits = CacheHits,
                CacheMisses = CacheMisses,
                CacheHitRate = (CacheHits + CacheMisses) == 0 ? 0.0 : (double)CacheHits / (CacheHits + CacheMisses)
            };
        }
    }

    public class PerformanceReport
    {
        public TimeSpan Uptime { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public double CacheHitRate { get; set; }
        public Dictionary<string, TimeSpan> Timings { get; set; } = new Dictionary<string, TimeSpan>();
        public Dictionary<string, long> MemoryUsage { get; set; } = new Dictionary<string, long>();
    }
}