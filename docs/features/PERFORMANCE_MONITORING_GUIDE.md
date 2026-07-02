# Performance Monitoring Guide

## Overview

This guide explains how to measure and monitor the performance metrics for the Certio application, including:
- ✅ Request response times
- ✅ L1 (Memory) cache performance (<1ms target)
- ✅ L2 (Redis) cache performance (<10ms target)
- ✅ Authorization check times (<50ms target)
- ✅ Database query times

## Architecture

### Performance Monitoring Components

1. **PerformanceMonitoringMiddleware** - Tracks HTTP request/response times
2. **CacheMetricsService** - Aggregates cache hit/miss statistics
3. **RedisCacheService** - Instrumented L1/L2 cache with timing
4. **CachedPermissionService** - Authorization timing
5. **MetricsReportingService** - Background service that logs metrics every 5 minutes
6. **MetricsController** - API endpoints to view/reset metrics

### Metrics Flow

```
HTTP Request → PerformanceMonitoringMiddleware (START timer)
    ↓
Controller Action
    ↓
Permission Check → CachedPermissionService (measure auth time)
    ↓
Cache Access → RedisCacheService (measure L1/L2 hit/miss time)
    ↓
Database Query (EF Core logging enabled)
    ↓
PerformanceMonitoringMiddleware (STOP timer, log total time)
```

## How to Use

### 1. View Real-Time Metrics in Console

When you run the application, you'll see performance logs in the console:

```bash
# Start the application
dotnet run --project Certio.Web
```

**Example Console Output:**

```
✓ Request: GET /Client/1/Matter completed in 45ms with status 200
⚡ L1 cache HIT for perm:123:1:ViewMatters in 0ms
✓ L2 cache HIT for matter_access:123:456 in 8ms
🔒 Permission check CACHED for user 123 in 1ms (Result: True)
⚠️ SLOW REQUEST: GET /Client/1/Dashboard completed in 250ms with status 200
```

### 2. API Endpoints

#### Get Current Metrics
```bash
# View cache metrics as JSON
curl http://localhost:5000/api/metrics/cache

# Example response:
{
  "timestamp": "2025-10-26T12:34:56Z",
  "metrics": {
    "L1_Memory": {
      "hits": 1250,
      "misses": 45,
      "totalRequests": 1295,
      "hitRate": 96.53,
      "averageHitTimeMs": 0.2,
      "averageMissTimeMs": 0.5
    },
    "L2_Redis": {
      "hits": 42,
      "misses": 3,
      "totalRequests": 45,
      "hitRate": 93.33,
      "averageHitTimeMs": 7.5,
      "averageMissTimeMs": 9.2
    }
  }
}
```

#### Force Metrics Report
```bash
# Trigger immediate metrics report to logs
curl -X POST http://localhost:5000/api/metrics/report
```

#### Reset Metrics
```bash
# Reset all counters (useful for focused testing)
curl -X POST http://localhost:5000/api/metrics/reset
```

### 3. Automated Metrics Reports

The `MetricsReportingService` automatically logs a full metrics report every **5 minutes**:

```
========================================
📊 PERFORMANCE METRICS REPORT
========================================
📊 Cache Metrics [L1_Memory]: Hits=1250, Misses=45, HitRate=96.53%, AvgHitTime=0.20ms, AvgMissTime=0.50ms
📊 Cache Metrics [L2_Redis]: Hits=42, Misses=3, HitRate=93.33%, AvgHitTime=7.50ms, AvgMissTime=9.20ms
========================================
```

### 4. Measure Specific Operations

#### Measure Authorization Performance

```csharp
// Check logs for timing information
// Authorization checks are automatically logged:
🔒 Permission check CACHED for user 123 in 1ms (Result: True)
🔒 Permission check COMPUTED for user 123, permission ViewMatters in 25ms (Result: True)
```

**Targets:**
- Cached permission check: <5ms
- Computed permission check: <50ms

#### Measure Cache Performance

All cache operations are automatically timed:

```
⚡ L1 cache HIT for perm:123:1:ViewMatters in 0ms
✓ L2 cache HIT for matter_access:123:456 in 8ms
❌ Cache MISS for conv:user:123:org:1 (checked in 9ms)
```

**Targets:**
- L1 (Memory) Hit: <1ms
- L2 (Redis) Hit: <10ms

### 5. Database Query Performance

EF Core query logging is enabled in `appsettings.Development.json`:

```json
"Microsoft.EntityFrameworkCore.Database.Command": "Information"
```

This will log all SQL queries with execution time:

```
Executed DbCommand (15ms) [Parameters=[@p0='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
SELECT * FROM Matters WHERE Id = @p0
```

### 6. Load Testing

Use tools to generate load and measure performance under stress:

#### Using PowerShell (Windows)

```powershell
# Simple load test - 100 requests
1..100 | ForEach-Object -Parallel {
    Measure-Command {
        Invoke-WebRequest -Uri "http://localhost:5000/Client/1/Matter" -UseBasicParsing
    } | Select-Object -ExpandProperty TotalMilliseconds
} | Measure-Object -Average -Maximum -Minimum

# View metrics after test
Invoke-RestMethod -Uri "http://localhost:5000/api/metrics/cache"
```

#### Using k6 (Recommended for serious load testing)

Install k6:
```bash
choco install k6
```

Create test script `load-test.js`:
```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '30s', target: 10 },  // Ramp up to 10 users
    { duration: '1m', target: 10 },   // Stay at 10 users
    { duration: '30s', target: 0 },   // Ramp down
  ],
};

export default function() {
  let response = http.get('http://localhost:5000/Client/1/Matter');
  
  check(response, {
    'status is 200': (r) => r.status === 200,
    'response time < 200ms': (r) => r.timings.duration < 200,
  });
  
  sleep(1);
}
```

Run test:
```bash
k6 run load-test.js
```

### 7. Response Time Headers

Every response includes a custom header with the processing time:

```bash
curl -I http://localhost:5000/Client/1/Matter

# Response headers include:
X-Response-Time-Ms: 45
```

You can use this in browser DevTools or automated tests.

## Performance Targets

Based on the architecture documentation:

| Metric | Target | Acceptable Max | How to Measure |
|--------|--------|----------------|----------------|
| L1 Cache Hit | <1ms | 2ms | Check logs: `L1 cache HIT` |
| L2 Cache Hit | <10ms | 20ms | Check logs: `L2 cache HIT` |
| Permission Check (Cached) | <5ms | 10ms | Check logs: `Permission check CACHED` |
| Permission Check (Computed) | <50ms | 100ms | Check logs: `Permission check COMPUTED` |
| Simple GET Request | <100ms | 200ms | Check logs: `Request: GET ... completed in` |
| Database Query | <50ms | 100ms | Check EF Core logs |

## Analyzing Results

### Good Performance Example

```
✓ Request: GET /Client/1/Matter completed in 45ms with status 200
⚡ L1 cache HIT for perm:123:1:ViewMatters in 0ms
🔒 Permission check CACHED for user 123 in 1ms

Cache Metrics [L1_Memory]: HitRate=97.5%, AvgHitTime=0.3ms
Cache Metrics [L2_Redis]: HitRate=95.0%, AvgHitTime=7.8ms
```

✅ **This is excellent performance!**
- Request: 45ms (well under 100ms target)
- L1 cache: 0ms hit
- Permission: 1ms (cached)
- Cache hit rates: >95%

### Poor Performance Example

```
⚠️ SLOW REQUEST: GET /Client/1/Dashboard completed in 350ms with status 200
🔒 Permission check COMPUTED for user 123 in 85ms
❌ Cache MISS for matter_access:123:456 (checked in 12ms)

Cache Metrics [L1_Memory]: HitRate=45.2%, AvgHitTime=0.8ms
Cache Metrics [L2_Redis]: HitRate=52.3%, AvgHitTime=25.5ms
```

❌ **This needs optimization:**
- Request: 350ms (exceeds 200ms acceptable max)
- Permission: 85ms (exceeds 50ms target)
- Cache hit rates: <60% (target >80%)
- L2 cache: 25.5ms (exceeds 10ms target)

**Potential fixes:**
1. Investigate slow database queries
2. Improve cache warming
3. Check Redis connection latency
4. Review query optimization
5. Consider adding database indexes

## Troubleshooting

### No Metrics Appearing

1. Check that logging is configured in `appsettings.Development.json`
2. Verify middleware is registered in `Program.cs`
3. Ensure you're running in Development mode

### Cache Hit Rate Too Low

1. Warm the cache by making repeated requests
2. Check Redis is running: `redis-cli ping`
3. Review cache expiration times
4. Check for cache key collisions

### Slow Database Queries

1. Review EF Core logs for query execution time
2. Add missing indexes
3. Consider query optimization
4. Use `.AsNoTracking()` for read-only queries

## Real-World Testing Scenario

```powershell
# 1. Reset metrics to start fresh
Invoke-RestMethod -Method POST -Uri "http://localhost:5000/api/metrics/reset"

# 2. Make 20 requests to warm up cache
1..20 | ForEach-Object {
    Invoke-WebRequest -Uri "http://localhost:5000/Client/1/Matter" -UseBasicParsing | Out-Null
    Start-Sleep -Milliseconds 100
}

# 3. Make 100 requests for actual measurement
$times = 1..100 | ForEach-Object {
    (Measure-Command {
        Invoke-WebRequest -Uri "http://localhost:5000/Client/1/Matter" -UseBasicParsing
    }).TotalMilliseconds
}

# 4. Calculate statistics
$times | Measure-Object -Average -Maximum -Minimum -StandardDeviation

# 5. View cache metrics
Invoke-RestMethod -Uri "http://localhost:5000/api/metrics/cache" | ConvertTo-Json -Depth 10

# 6. Force detailed report to logs
Invoke-RestMethod -Method POST -Uri "http://localhost:5000/api/metrics/report"
```

## Next Steps

1. ✅ Run the application and observe console logs
2. ✅ Use the API endpoints to view real-time metrics
3. ✅ Perform load testing with k6 or PowerShell
4. ✅ Analyze results against targets
5. ✅ Optimize based on findings
6. ⚠️ Consider adding Application Insights for production monitoring
7. ⚠️ Set up alerting for slow requests

## Production Monitoring

For production, consider integrating:

1. **Application Insights** - Azure monitoring
2. **Prometheus + Grafana** - Open-source metrics
3. **ELK Stack** - Elasticsearch, Logstash, Kibana
4. **Custom Dashboard** - Build admin panel showing metrics

---

**You now have full visibility into your application's performance!** 🎉

