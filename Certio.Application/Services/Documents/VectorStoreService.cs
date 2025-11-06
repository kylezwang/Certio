using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Documents;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Certio.Application.Services.Documents;

public sealed class VectorStoreService : IVectorStoreService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<VectorStoreService> _logger;

    public VectorStoreService(ApplicationDbContext dbContext, ILogger<VectorStoreService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task UpsertAsync(VectorUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.DocumentVectors
            .Where(v => v.DocumentId == request.DocumentId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _dbContext.DocumentVectors.RemoveRange(existing);
        }

        var document = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken)
                       ?? throw new InvalidOperationException($"Document {request.DocumentId} not found for vector upsert");

        foreach (var chunk in request.Chunks)
        {
            var entity = new DocumentVector
            {
                DocumentId = request.DocumentId,
                VersionId = request.VersionId,
                ChunkIndex = chunk.Index,
                ContentChunk = chunk.Content,
                Embedding = chunk.Embedding,
                EmbeddingReference = chunk.EmbeddingReference,
                CreatedAt = DateTime.UtcNow,
                SourceType = document.SourceType.ToString(),
                OrgId = document.OrgId,
                MatterId = document.MatterId,
                Tags = chunk.Tags.ToDictionary(k => k.Key, v => v.Value)
            };

            _dbContext.DocumentVectors.Add(entity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Upserted {ChunkCount} vectors for document {DocumentId}", request.Chunks.Count, request.DocumentId);
    }

    public async Task<IReadOnlyList<DocumentVector>> SearchAsync(Guid orgId, string query, int topK, Guid? matterId, CancellationToken cancellationToken = default)
    {
        query = query ?? string.Empty;
        var baseQuery = _dbContext.DocumentVectors
            .AsNoTracking()
            .Where(v => v.OrgId == orgId);

        if (matterId.HasValue)
        {
            baseQuery = baseQuery.Where(v => v.MatterId == matterId);
        }

        var candidates = await baseQuery
            .Include(v => v.Document)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return await CreateDocumentFallbackAsync(orgId, topK, matterId, cancellationToken);
        }

        var scored = candidates
            .Select(v => new
            {
                Vector = v,
                Score = ComputeScore(v, query)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Vector)
            .ToList();

        if (scored.Count < topK)
        {
            var missing = topK - scored.Count;
            var scoredIds = scored.Select(s => s.Id).ToHashSet();
            var fallback = candidates
                .Where(v => !scoredIds.Contains(v.Id))
                .OrderByDescending(v => v.Document?.ModifiedAt ?? v.CreatedAt)
                .Take(missing)
                .ToList();

            scored.AddRange(fallback);
        }

        if (scored.Count == 0)
        {
            scored = candidates
                .OrderByDescending(v => v.Document?.ModifiedAt ?? v.CreatedAt)
                .Take(topK)
                .ToList();
        }

        return scored;
    }

    public async Task RemoveAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var vectors = await _dbContext.DocumentVectors
            .Where(v => v.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        if (vectors.Count == 0)
        {
            return;
        }

        _dbContext.DocumentVectors.RemoveRange(vectors);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Removed {VectorCount} vectors for document {DocumentId}", vectors.Count, documentId);
    }

    private static double ComputeScore(DocumentVector vector, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 0d;
        }

        var score = 0d;
        if (vector.ContentChunk.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 1.0;
        }

        if (vector.Tags.Values.Any(value => value != null && value.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            score += 0.5;
        }

        if (vector.Document != null)
        {
            if (!string.IsNullOrWhiteSpace(vector.Document.Title) && vector.Document.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 1.25;
            }

            if (!string.IsNullOrWhiteSpace(vector.Document.Category) && vector.Document.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.25;
            }

            if (vector.Document.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                score += 0.25;
            }
        }

        return score;
    }

    private async Task<IReadOnlyList<DocumentVector>> CreateDocumentFallbackAsync(Guid orgId, int topK, Guid? matterId, CancellationToken cancellationToken)
    {
        var documentsQuery = _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.OrgId == orgId);

        if (matterId.HasValue)
        {
            documentsQuery = documentsQuery.Where(d => d.MatterId == matterId);
        }

        var documents = await documentsQuery
            .OrderByDescending(d => d.ModifiedAt)
            .ThenByDescending(d => d.CreatedAt)
            .Take(topK)
            .ToListAsync(cancellationToken);

        var fallbackVectors = new List<DocumentVector>();
        var index = 0;

        foreach (var doc in documents)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Document Title: {doc.Title}");
            builder.AppendLine($"Category: {doc.Category}");
            builder.AppendLine($"Source: {doc.SourceType}");
            builder.AppendLine($"Status: {doc.Status}");
            builder.AppendLine($"LastModifiedUtc: {doc.ModifiedAt:O}");

            if (doc.Tags.Count > 0)
            {
                builder.AppendLine("Tags:");
                foreach (var tag in doc.Tags)
                {
                    builder.AppendLine($"#{tag}");
                }
            }

            if (doc.Metadata?.Count > 0)
            {
                builder.AppendLine("Metadata:");
                foreach (var kvp in doc.Metadata)
                {
                    builder.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
            }

            var vector = new DocumentVector
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                VersionId = null,
                ChunkIndex = index++,
                ContentChunk = builder.ToString(),
                Embedding = null,
                EmbeddingReference = null,
                CreatedAt = DateTime.UtcNow,
                SourceType = doc.SourceType.ToString(),
                OrgId = doc.OrgId,
                MatterId = doc.MatterId,
                Tags = new Dictionary<string, string?>
                {
                    ["provider"] = doc.SourceType.ToString(),
                    ["hasExtractedContent"] = "false",
                    ["fallback"] = "true"
                },
                Document = doc
            };

            fallbackVectors.Add(vector);
        }

        return fallbackVectors;
    }
}

