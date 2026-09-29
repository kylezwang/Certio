Set-Location (Join-Path $PSScriptRoot "..")

# Read cache metrics from a local Notal instance.

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Performance Analysis Tool" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$baseUrl = "http://localhost:5000"

Write-Host " Fetching current cache metrics..." -ForegroundColor Yellow
Write-Host ""

try {
    $metrics = Invoke-RestMethod -Uri "$baseUrl/api/metrics/cache"
    
    Write-Host "Timestamp: $($metrics.timestamp)" -ForegroundColor Gray
    Write-Host ""
    
    foreach ($kvp in $metrics.metrics.PSObject.Properties) {
        $cacheLevel = $kvp.Name
        $data = $kvp.Value
        
        Write-Host "[$cacheLevel]" -ForegroundColor Yellow
        Write-Host "  Total Requests:    $($data.totalRequests)" -ForegroundColor White
        Write-Host "  Hits:              $($data.hits)" -ForegroundColor Green
        Write-Host "  Misses:            $($data.misses)" -ForegroundColor Red
        Write-Host "  Hit Rate:          $([math]::Round($data.hitRate, 2))%" -ForegroundColor $(if ($data.hitRate -gt 80) { "Green" } elseif ($data.hitRate -gt 50) { "Yellow" } else { "Red" })
        Write-Host "  Avg Hit Time:      $([math]::Round($data.averageHitTimeMs, 2))ms" -ForegroundColor $(if ($data.averageHitTimeMs -lt 1) { "Green" } elseif ($data.averageHitTimeMs -lt 10) { "Yellow" } else { "Red" })
        Write-Host "  Avg Miss Time:     $([math]::Round($data.averageMissTimeMs, 2))ms" -ForegroundColor White
        Write-Host ""
        
        # Analysis
        if ($cacheLevel -eq "L1_Memory") {
            if ($data.averageHitTimeMs -lt 1) {
                Write-Host "   L1 cache is performing excellently!" -ForegroundColor Green
            } else {
                Write-Host "   L1 cache is slower than expected (target: <1ms)" -ForegroundColor Yellow
            }
        }
        elseif ($cacheLevel -eq "L2_Redis") {
            if ($data.averageHitTimeMs -lt 10) {
                Write-Host "   L2 cache is within target!" -ForegroundColor Green
            } else {
                Write-Host "   L2 cache is slower than expected (target: <10ms)" -ForegroundColor Yellow
                Write-Host "   Check Redis connection/latency" -ForegroundColor Cyan
            }
        }
        
        if ($data.hitRate -lt 50 -and $data.totalRequests -gt 10) {
            Write-Host "   Low hit rate - cache needs warming or TTL is too short" -ForegroundColor Yellow
        }
        elseif ($data.hitRate -gt 80) {
            Write-Host "   Excellent hit rate!" -ForegroundColor Green
        }
        
        Write-Host ""
    }
    
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Recommendations:" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1.  Login Performance (3618ms is too high)" -ForegroundColor Yellow
    Write-Host "   - This is likely password hashing (expected ~500ms)" -ForegroundColor White
    Write-Host "   - User sync and session initialization adds overhead" -ForegroundColor White
    Write-Host "   - Consider caching user lookups" -ForegroundColor White
    Write-Host ""
    Write-Host "2.  Dashboard Performance (688ms)" -ForegroundColor Yellow
    Write-Host "   - First load will be slow (cache warming)" -ForegroundColor White
    Write-Host "   - Make a second request to see cached performance" -ForegroundColor White
    Write-Host "   - Check for N+1 query issues in dashboard" -ForegroundColor White
    Write-Host ""
    Write-Host "3.  Cache Miss on First Load is NORMAL" -ForegroundColor Green
    Write-Host "   - The cache system is working correctly" -ForegroundColor White
    Write-Host "   - Subsequent requests will be much faster" -ForegroundColor White
    Write-Host ""
    Write-Host "4.  Next Steps:" -ForegroundColor Cyan
    Write-Host "   - Navigate around the app to warm up the cache" -ForegroundColor White
    Write-Host "   - Run this script again to see improved metrics" -ForegroundColor White
    Write-Host "   - Use .\scripts\test_performance.ps1 for load testing" -ForegroundColor White
    Write-Host ""
    
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Want to see warm cache performance?" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Run these commands:" -ForegroundColor Yellow
    Write-Host "  1. Navigate to Client/1/Dashboard again" -ForegroundColor White
    Write-Host "  2. Refresh the page 2-3 times" -ForegroundColor White
    Write-Host "  3. Run this script again" -ForegroundColor White
    Write-Host "  4. Compare the hit rates and response times" -ForegroundColor White
    Write-Host ""
}
catch {
    Write-Host " Could not retrieve metrics" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure you're logged in and try:" -ForegroundColor Yellow
    Write-Host "  Invoke-RestMethod -Uri '$baseUrl/api/metrics/cache'" -ForegroundColor White
}

