using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Documents;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services.Documents;

public sealed class RagContextService : IRagContextService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IDocumentAuditService _documentAuditService;
    private readonly ILogger<RagContextService> _logger;

    public RagContextService(
        ApplicationDbContext dbContext,
        IVectorStoreService vectorStoreService,
        IDocumentAuditService documentAuditService,
        ILogger<RagContextService> logger)
    {
        _dbContext = dbContext;
        _vectorStoreService = vectorStoreService;
        _documentAuditService = documentAuditService;
        _logger = logger;
    }

    public async Task<RagContextResult> BuildContextAsync(RagContextRequest request, CancellationToken cancellationToken = default)
    {
        var hash = ComputeQueryHash(request.OrgId, request.UserId, request.Query, request.MatterId);
        var now = DateTime.UtcNow;

        var cacheEntry = await _dbContext.RagCache
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.OrgId == request.OrgId && c.UserId == request.UserId && c.QueryHash == hash && c.ExpiresAt > now, cancellationToken);

        if (cacheEntry != null)
        {
            var cached = DeserializeCachePayload(cacheEntry.ContextJson);
            var cachedVectors = await _dbContext.DocumentVectors
                .AsNoTracking()
                .Include(v => v.Document)
                .Where(v => cached.VectorIds.Contains(v.Id))
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Serving RAG context from cache for Org {OrgId} User {UserId}", request.OrgId, request.UserId);

            return new RagContextResult(
                Guid.NewGuid(),
                request.OrgId,
                request.UserId,
                request.Query,
                request.TopK,
                cachedVectors,
                cached.Context,
                cached.UsedByAgent);
        }

        var vectors = await _vectorStoreService.SearchAsync(request.OrgId, request.Query, request.TopK, request.MatterId, cancellationToken);

        var context = BuildContextDictionary(request, vectors);
        var retrievedVectorIds = vectors.Select(v => v.Id).ToList();

        var ragQuery = new RagQuery
        {
            OrgId = request.OrgId,
            UserId = request.UserId,
            QueryText = request.Query,
            TopK = request.TopK,
            RetrievedVectorIds = retrievedVectorIds,
            ContextJson = context,
            CreatedAt = DateTime.UtcNow,
            UsedByAgent = null
        };

        _dbContext.RagQueries.Add(ragQuery);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new RagContextResult(
            ragQuery.Id,
            request.OrgId,
            request.UserId,
            request.Query,
            request.TopK,
            vectors,
            context,
            ragQuery.UsedByAgent);

        await CacheContextAsync(result, cancellationToken);
        await _documentAuditService.LogAsync(new DocumentAuditEvent(
            request.OrgId,
            request.MatterId,
            null,
            null,
            request.UserId,
            "RagQuery",
            $"RAG context requested for query '{request.Query}'",
            DateTime.UtcNow), cancellationToken);

        return result;
    }

    public async Task CacheContextAsync(RagContextResult result, CancellationToken cancellationToken = default)
    {
        var hash = ComputeQueryHash(result.OrgId, result.UserId, result.Query, null);
        var payload = new RagCachePayload
        {
            VectorIds = result.Vectors.Select(v => v.Id).ToList(),
            Context = result.Context.ToDictionary(k => k.Key, v => v.Value),
            UsedByAgent = result.UsedByAgent
        };

        var json = JsonSerializer.Serialize(payload, SerializerOptions);

        var entry = await _dbContext.RagCache.FirstOrDefaultAsync(c => c.OrgId == result.OrgId && c.UserId == result.UserId && c.QueryHash == hash, cancellationToken);

        if (entry == null)
        {
            entry = new RagCacheEntry
            {
                OrgId = result.OrgId,
                UserId = result.UserId,
                QueryHash = hash,
                ContextJson = json,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            _dbContext.RagCache.Add(entry);
        }
        else
        {
            entry.ContextJson = json;
            entry.CreatedAt = DateTime.UtcNow;
            entry.ExpiresAt = DateTime.UtcNow.AddHours(1);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, string?> BuildContextDictionary(RagContextRequest request, IReadOnlyList<DocumentVector> vectors)
    {
        var context = new Dictionary<string, string?>
        {
            { "query", request.Query },
            { "generatedAt", DateTime.UtcNow.ToString("O") },
            { "vectorCount", vectors.Count.ToString() }
        };

        if (vectors.Count > 0)
        {
            var combined = new StringBuilder();
            foreach (var vector in vectors)
            {
                combined.AppendLine(vector.ContentChunk);
            }

            context["combinedContext"] = combined.ToString();
        }

        return context;
    }

    private static string ComputeQueryHash(Guid orgId, Guid userId, string query, Guid? matterId)
    {
        using var sha = SHA256.Create();
        var raw = Encoding.UTF8.GetBytes($"{orgId}|{userId}|{matterId}|{query}".ToLowerInvariant());
        var hashed = sha.ComputeHash(raw);
        return Convert.ToBase64String(hashed);
    }

    private static RagCachePayload DeserializeCachePayload(string json)
    {
        return JsonSerializer.Deserialize<RagCachePayload>(json, SerializerOptions) ?? new RagCachePayload();
    }

    private sealed record RagCachePayload
    {
        public List<Guid> VectorIds { get; init; } = new();

        public Dictionary<string, string?> Context { get; init; } = new();

        public string? UsedByAgent { get; init; }
    }
}

