using System.Diagnostics;

namespace Certio.Web.Middleware;

public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;

    public PerformanceMonitoringMiddleware(RequestDelegate next, ILogger<PerformanceMonitoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var requestPath = context.Request.Path.Value ?? "unknown";
        var requestMethod = context.Request.Method;

        // Always log Matter/Create requests for debugging
        if (requestPath.Contains("/Matter/Create", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("🔍 Matter/Create REQUEST DETECTED: {Method} {Path}", requestMethod, requestPath);
        }

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var elapsedMs = sw.ElapsedMilliseconds;
            var statusCode = context.Response.StatusCode;

            // Log slow requests as warnings
            if (elapsedMs > 100)
            {
                _logger.LogWarning(
                    "⚠️ SLOW REQUEST: {Method} {Path} completed in {ElapsedMs}ms with status {StatusCode}",
                    requestMethod, requestPath, elapsedMs, statusCode);
            }
            // Log very fast requests at debug level
            else if (elapsedMs < 10)
            {
                _logger.LogDebug(
                    "⚡ Fast request: {Method} {Path} completed in {ElapsedMs}ms with status {StatusCode}",
                    requestMethod, requestPath, elapsedMs, statusCode);
            }
            // Normal requests at information level
            else
            {
                _logger.LogInformation(
                    "✓ Request: {Method} {Path} completed in {ElapsedMs}ms with status {StatusCode}",
                    requestMethod, requestPath, elapsedMs, statusCode);
            }

            // Add custom header with timing information for debugging
            if (!context.Response.HasStarted)
            {
                context.Response.Headers["X-Response-Time-Ms"] = elapsedMs.ToString();
            }
        }
    }
}

