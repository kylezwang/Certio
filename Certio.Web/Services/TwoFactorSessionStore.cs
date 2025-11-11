using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;

namespace Certio.Web.Services;

public interface ITwoFactorSessionStore
{
    Task<string> CreateAsync(TwoFactorLoginState state, CancellationToken cancellationToken = default);
    Task<TwoFactorLoginState?> GetAsync(string token, CancellationToken cancellationToken = default);
    Task UpdateAsync(string token, TwoFactorLoginState state, CancellationToken cancellationToken = default);
    Task RemoveAsync(string token, CancellationToken cancellationToken = default);
}

public sealed record TwoFactorLoginState(
    string UserId,
    string Email,
    string ProtectedCode,
    DateTimeOffset ExpiresAtUtc,
    bool RememberMe,
    string VerificationMethod = "email",
    int Attempts = 0)
{
    public bool IsExpired() => DateTimeOffset.UtcNow > ExpiresAtUtc;
};

public sealed class DistributedTwoFactorSessionStore : ITwoFactorSessionStore
{
    private const string CachePrefix = "auth:twofactor:";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedTwoFactorSessionStore> _logger;

    public DistributedTwoFactorSessionStore(
        IDistributedCache cache,
        ILogger<DistributedTwoFactorSessionStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> CreateAsync(
        TwoFactorLoginState state,
        CancellationToken cancellationToken = default)
    {
        var token = Guid.NewGuid().ToString("N");
        await SetAsync(token, state, cancellationToken);
        return token;
    }

    public async Task<TwoFactorLoginState?> GetAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var raw = await _cache.GetStringAsync(BuildKey(token), cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var state = JsonSerializer.Deserialize<TwoFactorLoginState>(raw, SerializerOptions);
            if (state is null || state.IsExpired())
            {
                await RemoveAsync(token, cancellationToken);
                return null;
            }

            return state;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize two-factor state for token {Token}", token);
            await RemoveAsync(token, cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving two-factor state for token {Token}", token);
            throw;
        }
    }

    public async Task UpdateAsync(
        string token,
        TwoFactorLoginState state,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be null or empty.", nameof(token));
        }

        await SetAsync(token, state, cancellationToken);
    }

    public async Task RemoveAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        try
        {
            await _cache.RemoveAsync(BuildKey(token), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove two-factor state for token {Token}", token);
        }
    }

    private async Task SetAsync(
        string token,
        TwoFactorLoginState state,
        CancellationToken cancellationToken)
    {
        if (state.IsExpired())
        {
            throw new InvalidOperationException("Cannot store an already expired two-factor login state.");
        }

        var remainingLifetime = state.ExpiresAtUtc - DateTimeOffset.UtcNow;
        if (remainingLifetime <= TimeSpan.FromSeconds(5))
        {
            remainingLifetime = TimeSpan.FromSeconds(5);
        }

        var payload = JsonSerializer.Serialize(state, SerializerOptions);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = state.ExpiresAtUtc,
            SlidingExpiration = remainingLifetime
        };

        try
        {
            await _cache.SetStringAsync(BuildKey(token), payload, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store two-factor state for token {Token}", token);
            throw;
        }
    }

    private static string BuildKey(string token) => $"{CachePrefix}{token}";
}

