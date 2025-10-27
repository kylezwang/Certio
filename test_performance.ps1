# Performance Testing Script for Certio
# This script helps you quickly test and measure application performance

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Certio Performance Testing Tool" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$baseUrl = "http://localhost:5000"

# Check if the application is running
Write-Host "🔍 Checking if application is running..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/healthz" -UseBasicParsing -ErrorAction Stop
    Write-Host "✅ Application is running" -ForegroundColor Green
}
catch {
    Write-Host "❌ Application is not running. Please start it with:" -ForegroundColor Red
    Write-Host "   dotnet run --project Certio.Web" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Function to display metrics
function Show-Metrics {
    Write-Host "📊 Current Cache Metrics:" -ForegroundColor Cyan
    try {
        $metrics = Invoke-RestMethod -Uri "$baseUrl/api/metrics/cache"
        
        Write-Host ""
        Write-Host "Timestamp: $($metrics.timestamp)" -ForegroundColor Gray
        Write-Host ""
        
        foreach ($kvp in $metrics.metrics.PSObject.Properties) {
            $cacheLevel = $kvp.Name
            $data = $kvp.Value
            
            Write-Host "[$cacheLevel]" -ForegroundColor Yellow
            Write-Host "  Hits:              $($data.hits)" -ForegroundColor White
            Write-Host "  Misses:            $($data.misses)" -ForegroundColor White
            Write-Host "  Total Requests:    $($data.totalRequests)" -ForegroundColor White
            Write-Host "  Hit Rate:          $([math]::Round($data.hitRate, 2))%" -ForegroundColor $(if ($data.hitRate -gt 80) { "Green" } else { "Red" })
            Write-Host "  Avg Hit Time:      $([math]::Round($data.averageHitTimeMs, 2))ms" -ForegroundColor $(if ($data.averageHitTimeMs -lt 10) { "Green" } else { "Yellow" })
            Write-Host "  Avg Miss Time:     $([math]::Round($data.averageMissTimeMs, 2))ms" -ForegroundColor White
            Write-Host ""
        }
    }
    catch {
        Write-Host "⚠️ Could not retrieve metrics. You may need to authenticate first." -ForegroundColor Yellow
    }
}

# Menu
while ($true) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "What would you like to do?" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "1. View current metrics"
    Write-Host "2. Reset metrics (start fresh)"
    Write-Host "3. Run simple load test (20 requests)"
    Write-Host "4. Run extensive load test (100 requests)"
    Write-Host "5. Measure single request time"
    Write-Host "6. Force metrics report to logs"
    Write-Host "7. Exit"
    Write-Host ""
    
    $choice = Read-Host "Enter your choice (1-7)"
    
    switch ($choice) {
        "1" {
            Show-Metrics
        }
        "2" {
            Write-Host "🔄 Resetting metrics..." -ForegroundColor Yellow
            try {
                Invoke-RestMethod -Method POST -Uri "$baseUrl/api/metrics/reset"
                Write-Host "✅ Metrics reset successfully" -ForegroundColor Green
            }
            catch {
                Write-Host "❌ Failed to reset metrics" -ForegroundColor Red
            }
            Write-Host ""
        }
        "3" {
            Write-Host "🚀 Running 20 requests..." -ForegroundColor Yellow
            $times = @()
            
            for ($i = 1; $i -le 20; $i++) {
                Write-Progress -Activity "Load Testing" -Status "Request $i of 20" -PercentComplete (($i / 20) * 100)
                $time = (Measure-Command {
                    Invoke-WebRequest -Uri "$baseUrl/" -UseBasicParsing -ErrorAction SilentlyContinue
                }).TotalMilliseconds
                $times += $time
            }
            
            Write-Progress -Activity "Load Testing" -Completed
            
            $stats = $times | Measure-Object -Average -Maximum -Minimum
            
            Write-Host ""
            Write-Host "📈 Results:" -ForegroundColor Cyan
            Write-Host "  Average: $([math]::Round($stats.Average, 2))ms" -ForegroundColor White
            Write-Host "  Minimum: $([math]::Round($stats.Minimum, 2))ms" -ForegroundColor Green
            Write-Host "  Maximum: $([math]::Round($stats.Maximum, 2))ms" -ForegroundColor Red
            Write-Host ""
            
            Show-Metrics
        }
        "4" {
            Write-Host "🚀 Running 100 requests (this may take a while)..." -ForegroundColor Yellow
            $times = @()
            
            for ($i = 1; $i -le 100; $i++) {
                Write-Progress -Activity "Load Testing" -Status "Request $i of 100" -PercentComplete ($i)
                $time = (Measure-Command {
                    Invoke-WebRequest -Uri "$baseUrl/" -UseBasicParsing -ErrorAction SilentlyContinue
                }).TotalMilliseconds
                $times += $time
            }
            
            Write-Progress -Activity "Load Testing" -Completed
            
            $stats = $times | Measure-Object -Average -Maximum -Minimum
            
            Write-Host ""
            Write-Host "📈 Results:" -ForegroundColor Cyan
            Write-Host "  Average: $([math]::Round($stats.Average, 2))ms" -ForegroundColor White
            Write-Host "  Minimum: $([math]::Round($stats.Minimum, 2))ms" -ForegroundColor Green
            Write-Host "  Maximum: $([math]::Round($stats.Maximum, 2))ms" -ForegroundColor Red
            Write-Host ""
            
            # Calculate percentiles
            $sorted = $times | Sort-Object
            $p50 = $sorted[[math]::Floor($sorted.Count * 0.5)]
            $p95 = $sorted[[math]::Floor($sorted.Count * 0.95)]
            $p99 = $sorted[[math]::Floor($sorted.Count * 0.99)]
            
            Write-Host "  P50 (Median): $([math]::Round($p50, 2))ms" -ForegroundColor White
            Write-Host "  P95: $([math]::Round($p95, 2))ms" -ForegroundColor Yellow
            Write-Host "  P99: $([math]::Round($p99, 2))ms" -ForegroundColor Yellow
            Write-Host ""
            
            Show-Metrics
        }
        "5" {
            Write-Host "⏱️ Measuring single request..." -ForegroundColor Yellow
            
            try {
                $result = Measure-Command {
                    $response = Invoke-WebRequest -Uri "$baseUrl/" -UseBasicParsing
                }
                
                $serverTime = $response.Headers["X-Response-Time-Ms"]
                
                Write-Host ""
                Write-Host "📊 Timing:" -ForegroundColor Cyan
                Write-Host "  Total (client): $([math]::Round($result.TotalMilliseconds, 2))ms" -ForegroundColor White
                if ($serverTime) {
                    Write-Host "  Server processing: ${serverTime}ms" -ForegroundColor White
                    Write-Host "  Network overhead: $([math]::Round($result.TotalMilliseconds - [int]$serverTime, 2))ms" -ForegroundColor Gray
                }
                Write-Host ""
            }
            catch {
                Write-Host "❌ Request failed" -ForegroundColor Red
            }
        }
        "6" {
            Write-Host "📋 Forcing metrics report to logs..." -ForegroundColor Yellow
            try {
                Invoke-RestMethod -Method POST -Uri "$baseUrl/api/metrics/report"
                Write-Host "✅ Check the application console for detailed metrics report" -ForegroundColor Green
            }
            catch {
                Write-Host "❌ Failed to generate report" -ForegroundColor Red
            }
            Write-Host ""
        }
        "7" {
            Write-Host "👋 Goodbye!" -ForegroundColor Green
            exit 0
        }
        default {
            Write-Host "❌ Invalid choice. Please enter 1-7." -ForegroundColor Red
            Write-Host ""
        }
    }
}

