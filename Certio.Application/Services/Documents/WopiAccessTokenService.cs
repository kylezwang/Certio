using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services.Documents;

public sealed class WopiAccessTokenService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<WopiAccessTokenService> _logger;
    private static readonly ConcurrentDictionary<string, WopiAccessTokenInfo> _tokenCache = new();

    public WopiAccessTokenService(ApplicationDbContext dbContext, ILogger<WopiAccessTokenService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string GenerateAccessToken(Guid documentId, Guid orgId, Guid userId, TimeSpan? expiresIn = null)
    {
        var expiry = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromHours(8));
        
        var tokenInfo = new WopiAccessTokenInfo
        {
            DocumentId = documentId,
            OrgId = orgId,
            UserId = userId,
            ExpiresAt = expiry,
            CreatedAt = DateTime.UtcNow
        };

        // Generate a cryptographically secure random token
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var token = Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        _tokenCache[token] = tokenInfo;
        
        _logger.LogInformation("Generated WOPI access token for document {DocumentId}, expires at {ExpiresAt}", 
            documentId, expiry);

        return token;
    }

    public async Task<WopiAccessTokenInfo?> ValidateAccessTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Empty WOPI access token provided");
            return null;
        }

        if (!_tokenCache.TryGetValue(token, out var tokenInfo))
        {
            _logger.LogWarning("WOPI access token not found in cache");
            return null;
        }

        if (tokenInfo.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("WOPI access token expired for document {DocumentId}", tokenInfo.DocumentId);
            _tokenCache.TryRemove(token, out _);
            return null;
        }

        // Verify document exists and belongs to the org
        var documentExists = await _dbContext.Documents
            .AsNoTracking()
            .AnyAsync(d => d.Id == tokenInfo.DocumentId && d.OrgId == tokenInfo.OrgId && d.DeletedAt == null, 
                cancellationToken);

        if (!documentExists)
        {
            _logger.LogWarning("Document {DocumentId} not found or deleted", tokenInfo.DocumentId);
            _tokenCache.TryRemove(token, out _);
            return null;
        }

        return tokenInfo;
    }

    public void CleanupExpiredTokens()
    {
        var expiredTokens = new System.Collections.Generic.List<string>();
        var now = DateTime.UtcNow;

        foreach (var kvp in _tokenCache)
        {
            if (kvp.Value.ExpiresAt < now)
            {
                expiredTokens.Add(kvp.Key);
            }
        }

        foreach (var token in expiredTokens)
        {
            _tokenCache.TryRemove(token, out _);
        }

        if (expiredTokens.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired WOPI access tokens", expiredTokens.Count);
        }
    }
}

public sealed class WopiAccessTokenInfo
{
    public Guid DocumentId { get; set; }
    public Guid OrgId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

