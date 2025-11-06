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

        var isDocumentListQuery = IsDocumentListQuery(request.Query);
        IReadOnlyList<DocumentVector> vectors;

        if (isDocumentListQuery)
        {
            vectors = await FetchDocumentOverviewVectorsAsync(request.OrgId, request.MatterId, request.TopK, cancellationToken);
        }
        else
        {
            vectors = await _vectorStoreService.SearchAsync(request.OrgId, request.Query, request.TopK, request.MatterId, cancellationToken);

            if (vectors.Count == 0)
            {
                vectors = await FetchDocumentOverviewVectorsAsync(request.OrgId, request.MatterId, request.TopK, cancellationToken);
            }
        }

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
        
        // Skip audit logging - the Guid UserId doesn't match the int-based Users table FK constraint
        // RAG queries are already logged via RagQueries table (line 87)
        
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

        if (vectors.Count == 0)
        {
            context["combinedContext"] = string.Empty;
            context["documentList"] = "[]";
            return context;
        }

        // Build combined context with rich document metadata
        var combined = new StringBuilder();
        var documentMap = new Dictionary<Guid, DocumentInfo>();

        foreach (var vector in vectors)
        {
            if (vector.Document != null && !documentMap.ContainsKey(vector.Document.Id))
            {
                var doc = vector.Document;
                var provider = vector.Tags.TryGetValue("provider", out var p) ? p : doc.SourceType.ToString();
                var contentType = vector.Tags.TryGetValue("contentType", out var ct) ? ct : doc.FileType;
                var hasContent = vector.Tags.TryGetValue("hasExtractedContent", out var hec) && hec == "true";
                var confidence = vector.Tags.TryGetValue("confidence", out var conf) ? conf : null;

                documentMap[doc.Id] = new DocumentInfo
                {
                    Id = doc.Id,
                    Title = doc.Title,
                    Provider = provider ?? "unknown",
                    ContentType = contentType ?? "unknown",
                    ModifiedAt = doc.ModifiedAt,
                    Category = doc.Category,
                    HasExtractedContent = hasContent,
                    Confidence = confidence,
                    ChunkCount = 1
                };
            }
            else if (vector.Document != null && documentMap.ContainsKey(vector.Document.Id))
            {
                documentMap[vector.Document.Id].ChunkCount++;
            }

            combined.AppendLine($"[Document: {vector.Document?.Title ?? "Unknown"}]");
            combined.AppendLine(vector.ContentChunk);
            combined.AppendLine();
        }

        context["combinedContext"] = combined.ToString();

        // Serialize document list for AI reference
        var documentList = documentMap.Values
            .OrderByDescending(d => d.ChunkCount)
            .Select(d => new
            {
                id = d.Id,
                title = d.Title,
                provider = d.Provider,
                contentType = d.ContentType,
                category = d.Category,
                modifiedAt = d.ModifiedAt.ToString("O"),
                chunkCount = d.ChunkCount,
                hasContent = d.HasExtractedContent,
                confidence = d.Confidence
            })
            .ToList();

        context["documentList"] = JsonSerializer.Serialize(documentList, SerializerOptions);
        context["documentCount"] = documentMap.Count.ToString();

        // Add provider breakdown
        var providerGroups = documentMap.Values
            .GroupBy(d => d.Provider)
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();
        context["providerBreakdown"] = string.Join(", ", providerGroups);

        return context;
    }

    private async Task<IReadOnlyList<DocumentVector>> FetchDocumentOverviewVectorsAsync(Guid orgId, Guid? matterId, int requestedTopK, CancellationToken cancellationToken)
    {
        var limit = requestedTopK > 0 ? Math.Min(requestedTopK, 50) : 20;

        var vectorCandidatesQuery = _dbContext.DocumentVectors
            .AsNoTracking()
            .Include(v => v.Document)
            .Where(v => v.OrgId == orgId);

        if (matterId.HasValue)
        {
            vectorCandidatesQuery = vectorCandidatesQuery.Where(v => v.MatterId == matterId);
        }

        var sampleSize = Math.Max(limit * 5, limit);

        var vectorCandidates = await vectorCandidatesQuery
            .OrderByDescending(v => v.Document != null ? v.Document.ModifiedAt : v.CreatedAt)
            .ThenBy(v => v.ChunkIndex)
            .Take(sampleSize)
            .ToListAsync(cancellationToken);

        var orderedVectors = vectorCandidates
            .Where(v => v.Document != null)
            .GroupBy(v => v.DocumentId)
            .Select(g => g.OrderBy(v => v.ChunkIndex).First())
            .Select(v => new
            {
                Vector = v,
                HasExtractedContent = HasUsableContent(v)
            })
            .OrderByDescending(v => v.HasExtractedContent)
            .ThenByDescending(v => v.Vector.Document?.ModifiedAt ?? v.Vector.CreatedAt)
            .Take(limit)
            .Select(v => v.Vector)
            .ToList();

        if (orderedVectors.Count > 0)
        {
            return orderedVectors;
        }

        var documentQuery = _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.OrgId == orgId && d.DeletedAt == null);

        if (matterId.HasValue)
        {
            documentQuery = documentQuery.Where(d => d.MatterId == matterId);
        }

        var documents = await documentQuery
            .OrderByDescending(d => d.ModifiedAt)
            .ThenByDescending(d => d.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var fallbackVectors = new List<DocumentVector>();
        var index = 0;

        foreach (var document in documents)
        {
            var summary = BuildDocumentSummary(document);
            fallbackVectors.Add(new DocumentVector
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                VersionId = null,
                ChunkIndex = index++,
                ContentChunk = summary,
                Embedding = null,
                EmbeddingReference = null,
                CreatedAt = document.ModifiedAt,
                UpdatedAt = document.ModifiedAt,
                SourceType = document.SourceType.ToString(),
                OrgId = document.OrgId,
                MatterId = document.MatterId,
                Tags = new Dictionary<string, string?>
                {
                    ["provider"] = document.SourceType.ToString(),
                    ["contentType"] = document.FileType,
                    ["hasExtractedContent"] = document.Metadata != null && document.Metadata.TryGetValue("hasExtractedContent", out var extracted) && string.Equals(extracted, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false",
                    ["fallback"] = "document_overview"
                },
                Document = document
            });
        }

        return fallbackVectors;
    }

    private static string BuildDocumentSummary(Document document)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Title: {document.Title}");
        builder.AppendLine($"Category: {document.Category}");
        builder.AppendLine($"Source: {document.SourceType}");
        builder.AppendLine($"Status: {document.Status}");
        builder.AppendLine($"LastModifiedUtc: {document.ModifiedAt:O}");

        if (document.Tags.Count > 0)
        {
            builder.AppendLine("Tags:");
            foreach (var tag in document.Tags)
            {
                builder.AppendLine($"- {tag}");
            }
        }

        if (document.Metadata != null && document.Metadata.Count > 0)
        {
            builder.AppendLine("Metadata:");
            foreach (var kvp in document.Metadata)
            {
                builder.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }
        }

        var hasSummary = document.Metadata != null && document.Metadata.TryGetValue("extractionStatus", out var extractionStatus)
            && string.Equals(extractionStatus, "azure_document_intelligence_not_configured", StringComparison.OrdinalIgnoreCase);

        if (hasSummary)
        {
            builder.AppendLine("Note: Full text extraction unavailable; showing metadata only.");
        }

        return builder.ToString();
    }

    private static bool HasUsableContent(DocumentVector vector)
    {
        if (!string.IsNullOrWhiteSpace(vector.ContentChunk) && !vector.ContentChunk.Contains("[Content unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (vector.Tags != null && vector.Tags.TryGetValue("hasExtractedContent", out var extracted) && string.Equals(extracted, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsDocumentListQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var normalized = query.ToLowerInvariant();
        return normalized.Contains("list")
            || normalized.Contains("show me")
            || normalized.Contains("what documents")
            || normalized.Contains("see my documents")
            || normalized.Contains("which documents")
            || normalized.Contains("display documents");
    }

    private sealed record DocumentInfo
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Provider { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public DateTime ModifiedAt { get; init; }
        public string Category { get; init; } = string.Empty;
        public bool HasExtractedContent { get; init; }
        public string? Confidence { get; init; }
        public int ChunkCount { get; set; }
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

