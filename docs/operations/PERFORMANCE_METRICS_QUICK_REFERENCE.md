# Performance Metrics Quick Reference Card

## 🚀 Quick Start

```powershell
# Option 1: Interactive tool
.\test_performance.ps1

# Option 2: View metrics directly
Invoke-RestMethod http://localhost:5000/api/metrics/cache | ConvertTo-Json
```

## 📊 API Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/metrics/cache` | GET | View current metrics |
| `/api/metrics/reset` | POST | Reset all counters |
| `/api/metrics/report` | POST | Force log report |
| `/healthz` | GET | Health check |

## 🎯 Performance Targets

| Metric | Target | Log Message |
|--------|--------|-------------|
| L1 Cache Hit | <1ms | `⚡ L1 cache HIT` |
| L2 Cache Hit | <10ms | `✓ L2 cache HIT` |
| Auth (Cached) | <5ms | `🔒 Permission check CACHED` |
| Auth (DB) | <50ms | `🔒 Permission check COMPUTED` |
| HTTP Request | <100ms | `✓ Request: ... completed in` |
| Slow Request | >100ms | `⚠️ SLOW REQUEST` |

## 📈 Log Symbols

| Symbol | Meaning | Good/Bad |
|--------|---------|----------|
| ⚡ | L1 cache hit (memory) | ✅ Great |
| ✓ | L2 cache hit (Redis) | ✅ Good |
| ❌ | Cache miss | ⚠️ Expected initially |
| 🔒 | Permission check | ℹ️ Info |
| ⚠️ | Slow request (>100ms) | ❌ Investigate |

## 🧪 Load Testing One-Liners

```powershell
# Simple test - 20 requests
1..20 | % { Invoke-WebRequest http://localhost:5000 -UseBasicParsing } | Out-Null

# Measure average time
(1..20 | % { (Measure-Command { Invoke-WebRequest http://localhost:5000 -UseBasicParsing }).TotalMilliseconds } | Measure-Object -Average).Average

# View metrics after
Invoke-RestMethod http://localhost:5000/api/metrics/cache | ConvertTo-Json
```

## 📁 Files Modified/Created

### Created
- `Certio.Web/Middleware/PerformanceMonitoringMiddleware.cs`
- `Certio.Web/Services/CacheMetricsService.cs`
- `Certio.Web/Services/MetricsReportingService.cs`
- `Certio.Web/Controllers/MetricsController.cs`
- `test_performance.ps1`
- `PERFORMANCE_MONITORING_GUIDE.md`
- `PERFORMANCE_MONITORING_IMPLEMENTATION_SUMMARY.md`

### Modified
- `Certio.Web/Services/RedisCacheService.cs` (added timing)
- `Certio.Web/Services/CachedPermissionService.cs` (added timing)
- `Certio.Web/Program.cs` (registered services)
- `Certio.Web/appsettings.Development.json` (enabled logging)
- `Certio.Web/Views/Shared/_ClientLayout.cshtml` (fixed logout button)

## 💡 Common Tasks

### View Real-Time Performance
```powershell
# Just run the app and watch console
dotnet run --project Certio.Web
```

### Generate Test Load
```powershell
# Run the interactive tool
.\test_performance.ps1
# Select option 3 or 4
```

### Check Current Metrics
```powershell
Invoke-RestMethod http://localhost:5000/api/metrics/cache
```

### Reset and Start Fresh
```powershell
Invoke-RestMethod -Method POST http://localhost:5000/api/metrics/reset
```

## 🎓 Understanding Metrics

### Good Performance Example
```
✓ Request: GET /Client/1/Matter completed in 45ms
⚡ L1 cache HIT in 0ms
HitRate=97.5%, AvgHitTime=0.3ms
```
✅ **Excellent!** Fast response, high hit rate, sub-millisecond cache.

### Poor Performance Example
```
⚠️ SLOW REQUEST: GET /Client/1/Matter completed in 350ms
HitRate=45.2%, AvgHitTime=25.5ms
```
❌ **Needs work.** Slow response, low hit rate, high cache latency.

## 🔧 Troubleshooting

| Problem | Solution |
|---------|----------|
| No metrics appearing | Check `appsettings.Development.json` logging config |
| Cache hit rate low | Warm up cache with repeated requests |
| Slow requests | Check EF Core logs for slow SQL queries |
| Redis errors | Verify Redis is running: `redis-cli ping` |

## 📊 Sample Metrics Output

```json
{
  "timestamp": "2025-10-26T12:34:56Z",
  "metrics": {
    "L1_Memory": {
      "hits": 1250,
      "hitRate": 96.53,
      "averageHitTimeMs": 0.2
    },
    "L2_Redis": {
      "hits": 42,
      "hitRate": 93.33,
      "averageHitTimeMs": 7.5
    }
  }
}
```

---

**For detailed documentation, see `PERFORMANCE_MONITORING_GUIDE.md`**

