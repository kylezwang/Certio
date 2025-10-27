namespace Certio.Web.Services;

/// <summary>
/// Background service that periodically reports performance metrics
/// </summary>
public class MetricsReportingService : BackgroundService
{
    private readonly ILogger<MetricsReportingService> _logger;
    private readonly CacheMetricsService _metricsService;
    private readonly TimeSpan _reportingInterval = TimeSpan.FromMinutes(5);

    public MetricsReportingService(
        ILogger<MetricsReportingService> logger,
        CacheMetricsService metricsService)
    {
        _logger = logger;
        _metricsService = metricsService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📊 Metrics Reporting Service started. Reporting every {Interval} minutes.", 
            _reportingInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_reportingInterval, stoppingToken);
                
                _logger.LogInformation("========================================");
                _logger.LogInformation("📊 PERFORMANCE METRICS REPORT");
                _logger.LogInformation("========================================");
                
                // Log cache metrics
                _metricsService.LogMetrics();
                
                _logger.LogInformation("========================================");
            }
            catch (OperationCanceledException)
            {
                // This is expected when the service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating metrics report");
            }
        }
        
        _logger.LogInformation("📊 Metrics Reporting Service stopped.");
    }
}

