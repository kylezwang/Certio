using System.Collections.Concurrent;

namespace Certio.Web.Services;

public class CacheMetricsService
{
    private readonly ConcurrentDictionary<string, CacheMetric> _metrics = new();
    private readonly ILogger<CacheMetricsService> _logger;

    public CacheMetricsService(ILogger<CacheMetricsService> logger)
    {
        _logger = logger;
    }

    public void RecordHit(string cacheLevel, string key, long elapsedMs)
    {
        var metric = _metrics.GetOrAdd(cacheLevel, _ => new CacheMetric(cacheLevel));
        metric.RecordHit(elapsedMs);
    }

    public void RecordMiss(string cacheLevel, string key, long elapsedMs)
    {
        var metric = _metrics.GetOrAdd(cacheLevel, _ => new CacheMetric(cacheLevel));
        metric.RecordMiss(elapsedMs);
    }

    public void LogMetrics()
    {
        foreach (var kvp in _metrics)
        {
            var metric = kvp.Value;
            var hitRate = metric.TotalRequests > 0 
                ? (metric.Hits * 100.0 / metric.TotalRequests) 
                : 0;

            _logger.LogInformation(
                " Cache Metrics [{CacheLevel}]: Hits={Hits}, Misses={Misses}, HitRate={HitRate:F2}%, AvgHitTime={AvgHitMs:F2}ms, AvgMissTime={AvgMissMs:F2}ms",
                kvp.Key,
                metric.Hits,
                metric.Misses,
                hitRate,
                metric.AverageHitTime,
                metric.AverageMissTime
            );
        }
    }

    public Dictionary<string, object> GetMetricsSummary()
    {
        var summary = new Dictionary<string, object>();
        
        foreach (var kvp in _metrics)
        {
            var metric = kvp.Value;
            var hitRate = metric.TotalRequests > 0 
                ? (metric.Hits * 100.0 / metric.TotalRequests) 
                : 0;

            summary[kvp.Key] = new
            {
                Hits = metric.Hits,
                Misses = metric.Misses,
                TotalRequests = metric.TotalRequests,
                HitRate = hitRate,
                AverageHitTimeMs = metric.AverageHitTime,
                AverageMissTimeMs = metric.AverageMissTime
            };
        }

        return summary;
    }

    public void Reset()
    {
        _metrics.Clear();
        _logger.LogInformation("Cache metrics reset");
    }
}

public class CacheMetric
{
    private long _hits;
    private long _misses;
    private long _totalHitTime;
    private long _totalMissTime;

    public string CacheLevel { get; }
    
    public long Hits => _hits;
    public long Misses => _misses;
    public long TotalRequests => _hits + _misses;
    
    public double AverageHitTime => _hits > 0 ? (double)_totalHitTime / _hits : 0;
    public double AverageMissTime => _misses > 0 ? (double)_totalMissTime / _misses : 0;

    public CacheMetric(string cacheLevel)
    {
        CacheLevel = cacheLevel;
    }

    public void RecordHit(long elapsedMs)
    {
        Interlocked.Increment(ref _hits);
        Interlocked.Add(ref _totalHitTime, elapsedMs);
    }

    public void RecordMiss(long elapsedMs)
    {
        Interlocked.Increment(ref _misses);
        Interlocked.Add(ref _totalMissTime, elapsedMs);
    }
}

