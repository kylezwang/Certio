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

        _logger.LogInformation("Upserted {ChunkCount} vectors for document {DocumentId}", request.Chunks.Count, request.DocumentId);
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

        if (vector.Document != null && vector.Document.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            score += 0.25;
        }

        return score;
    }
}

