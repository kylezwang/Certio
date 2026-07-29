using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Certio.Web.HealthChecks
{
    /// <summary>
    /// Describes which distributed cache backend the app actually resolved at startup, so the health
    /// check can report it and so "Redis is down" is distinguishable from "Redis was never configured".
    /// </summary>
    public sealed record CacheBackendInfo(string Backend)
    {
        public const string Redis = "Redis";
        public const string InMemory = "InMemory";

        public bool IsRedis => Backend == Redis;
    }

    /// <summary>
    /// Readiness probe for the distributed cache. Deliberately probes through IDistributedCache rather
    /// than opening its own Redis connection, so it exercises the same client the application uses
    /// instead of a parallel one that could succeed while the real one is broken.
    ///
    /// When the app is configured for the in-memory distributed cache, Redis is not a dependency and this
    /// check reports Healthy - it still round-trips a value, which catches a misregistered cache.
    /// </summary>
    public class DistributedCacheHealthCheck : IHealthCheck
    {
        private const string ProbeKey = "certio:healthcheck:probe";

        private readonly IDistributedCache _cache;
        private readonly CacheBackendInfo _backend;
        private readonly ILogger<DistributedCacheHealthCheck> _logger;

        public DistributedCacheHealthCheck(
            IDistributedCache cache,
            CacheBackendInfo backend,
            ILogger<DistributedCacheHealthCheck> logger)
        {
            _cache = cache;
            _backend = backend;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object> { ["backend"] = _backend.Backend };

            try
            {
                var written = DateTime.UtcNow.ToString("O");
                await _cache.SetStringAsync(
                    ProbeKey,
                    written,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) },
                    cancellationToken);

                var readBack = await _cache.GetStringAsync(ProbeKey, cancellationToken);
                if (readBack != written)
                {
                    return HealthCheckResult.Unhealthy(
                        $"{_backend.Backend} cache round-trip returned unexpected value", data: data);
                }

                return HealthCheckResult.Healthy($"{_backend.Backend} cache round-trip succeeded", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Distributed cache health check failed for backend {Backend}", _backend.Backend);

                // A broken in-memory cache means the process is misconfigured, not that a remote
                // dependency blipped. Both are unhealthy, but the message should say which.
                return HealthCheckResult.Unhealthy(
                    _backend.IsRedis ? "Redis unreachable" : "In-memory distributed cache failed",
                    ex,
                    data);
            }
        }
    }
}
