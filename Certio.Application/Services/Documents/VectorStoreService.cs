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
        query ??= string.Empty;
        var normalizedQuery = NormalizeSearchText(query);
        var queryTokens = SplitTokens(normalizedQuery);

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
                Score = ComputeScore(v, query, normalizedQuery, queryTokens)
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

    private static double ComputeScore(DocumentVector vector, string query, string normalizedQuery, string[] tokens)
    {
        if (tokens.Length == 0 && string.IsNullOrWhiteSpace(query))
        {
            return 0d;
        }

        var score = 0d;

        if (!string.IsNullOrWhiteSpace(vector.ContentChunk))
        {
            var normalizedChunk = NormalizeSearchText(vector.ContentChunk);
            var chunkMatches = CountTokenMatches(normalizedChunk, tokens);
            if (chunkMatches > 0)
            {
                score += chunkMatches * 0.55;
            }
            else if (!string.IsNullOrEmpty(query) && vector.ContentChunk.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.4;
            }
        }

        if (vector.Tags.Count > 0)
        {
            foreach (var value in vector.Tags.Values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var normalizedValue = NormalizeSearchText(value);
                var tagMatches = CountTokenMatches(normalizedValue, tokens);
                if (tagMatches > 0)
                {
                    score += Math.Min(tagMatches * 0.35, 0.7);
                    break;
                }
            }
        }

        if (vector.Document != null)
        {
            var document = vector.Document;

            if (!string.IsNullOrWhiteSpace(document.Title))
            {
                var normalizedTitle = NormalizeSearchText(document.Title);
                var titleMatches = CountTokenMatches(normalizedTitle, tokens);
                if (titleMatches > 0)
                {
                    score += titleMatches * 0.95;
                    if (!string.IsNullOrEmpty(normalizedQuery) && normalizedTitle.Equals(normalizedQuery, StringComparison.Ordinal))
                    {
                        score += 0.6;
                    }
                }
                else if (!string.IsNullOrEmpty(query) && document.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.6;
                }
            }

            if (!string.IsNullOrWhiteSpace(document.Category))
            {
                var normalizedCategory = NormalizeSearchText(document.Category);
                var categoryMatches = CountTokenMatches(normalizedCategory, tokens);
                if (categoryMatches > 0)
                {
                    score += 0.2 * categoryMatches;
                }
            }

            if (document.Tags.Count > 0)
            {
                foreach (var tag in document.Tags)
                {
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        continue;
                    }

                    var normalizedTag = NormalizeSearchText(tag);
                    var docTagMatches = CountTokenMatches(normalizedTag, tokens);
                    if (docTagMatches > 0)
                    {
                        score += Math.Min(docTagMatches * 0.25, 0.5);
                        break;
                    }
                }
            }

            if (document.Metadata.Count > 0)
            {
                foreach (var kvp in document.Metadata)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        continue;
                    }

                    var normalizedMetadataValue = NormalizeSearchText(kvp.Value);
                    var metadataMatches = CountTokenMatches(normalizedMetadataValue, tokens);
                    if (metadataMatches > 0)
                    {
                        score += Math.Min(metadataMatches * 0.2, 0.4);
                        break;
                    }
                }
            }
        }

        return score;
    }

    private static string NormalizeSearchText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var previousSpace = false;

        foreach (var ch in text)
        {
            char normalizedChar;
            if (char.IsLetterOrDigit(ch))
            {
                normalizedChar = char.ToLowerInvariant(ch);
            }
            else if (char.IsWhiteSpace(ch) || ch == '_' || ch == '-' || ch == '.' || ch == '/' || ch == '\\')
            {
                normalizedChar = ' ';
            }
            else if (char.IsPunctuation(ch) || char.IsSymbol(ch))
            {
                normalizedChar = ' ';
            }
            else
            {
                normalizedChar = ' ';
            }

            if (normalizedChar == ' ')
            {
                if (previousSpace)
                {
                    continue;
                }

                builder.Append(' ');
                previousSpace = true;
            }
            else
            {
                builder.Append(normalizedChar);
                previousSpace = false;
            }
        }

        return builder.ToString().Trim();
    }

    private static string[] SplitTokens(string normalizedText)
    {
        if (string.IsNullOrEmpty(normalizedText))
        {
            return Array.Empty<string>();
        }

        return normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static int CountTokenMatches(string normalizedTarget, string[] tokens)
    {
        if (tokens.Length == 0 || string.IsNullOrEmpty(normalizedTarget))
        {
            return 0;
        }

        var matches = 0;
        foreach (var token in tokens)
        {
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            if (normalizedTarget.Contains(token, StringComparison.Ordinal))
            {
                matches++;
            }
        }

        return matches;
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

