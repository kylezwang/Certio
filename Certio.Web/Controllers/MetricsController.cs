using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.Services;

namespace Certio.Web.Controllers;

/// <summary>
/// API endpoint for viewing performance metrics
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // Only authenticated users can view metrics
public class MetricsController : ControllerBase
{
    private readonly CacheMetricsService _metricsService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        CacheMetricsService metricsService,
        ILogger<MetricsController> logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    /// <summary>
    /// Get current cache metrics
    /// GET /api/metrics/cache
    /// </summary>
    [HttpGet("cache")]
    public IActionResult GetCacheMetrics()
    {
        try
        {
            var metrics = _metricsService.GetMetricsSummary();
            
            return Ok(new
            {
                timestamp = DateTime.UtcNow,
                metrics = metrics,
                summary = new
                {
                    message = "Cache metrics collected successfully",
                    documentation = "L1_Memory = In-memory cache (target: <1ms), L2_Redis = Redis cache (target: <10ms)"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache metrics");
            return StatusCode(500, new { error = "Failed to retrieve metrics" });
        }
    }

    /// <summary>
    /// Reset all metrics counters
    /// POST /api/metrics/reset
    /// </summary>
    [HttpPost("reset")]
    public IActionResult ResetMetrics()
    {
        try
        {
            _metricsService.Reset();
            _logger.LogInformation("📊 Metrics manually reset by user");
            
            return Ok(new
            {
                message = "Metrics reset successfully",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting metrics");
            return StatusCode(500, new { error = "Failed to reset metrics" });
        }
    }

    /// <summary>
    /// Force metrics report to be logged
    /// POST /api/metrics/report
    /// </summary>
    [HttpPost("report")]
    public IActionResult ForceReport()
    {
        try
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("📊 MANUAL PERFORMANCE METRICS REPORT");
            _logger.LogInformation("========================================");
            
            _metricsService.LogMetrics();
            
            _logger.LogInformation("========================================");
            
            return Ok(new
            {
                message = "Metrics report logged to console/logs",
                timestamp = DateTime.UtcNow,
                hint = "Check the application console or logs to see the detailed report"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating metrics report");
            return StatusCode(500, new { error = "Failed to generate report" });
        }
    }
}

