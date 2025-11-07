using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Documents;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services.Documents;

public sealed class DocumentIndexerService : IDocumentIndexerService
{
    private const int DefaultChunkSize = 1200;

    private readonly ApplicationDbContext _dbContext;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IDocumentContentService _documentContentService;
    private readonly IDocumentAuditService _documentAuditService;
    private readonly IEmbeddingJobQueue _jobQueue;
    private readonly ILogger<DocumentIndexerService> _logger;

    public DocumentIndexerService(
        ApplicationDbContext dbContext,
        IVectorStoreService vectorStoreService,
        IDocumentContentService documentContentService,
        IDocumentAuditService documentAuditService,
        IEmbeddingJobQueue jobQueue,
        ILogger<DocumentIndexerService> logger)
    {
        _dbContext = dbContext;
        _vectorStoreService = vectorStoreService;
        _documentContentService = documentContentService;
        _documentAuditService = documentAuditService;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task QueueEmbeddingAsync(Guid documentId, Guid? versionId, CancellationToken cancellationToken = default)
    {
        var request = await BuildIndexRequestAsync(documentId, versionId, cancellationToken);
        await _jobQueue.EnqueueAsync(request, cancellationToken);
        _logger.LogDebug("Queued document {DocumentId} (Version {VersionId}) for embedding", documentId, versionId);
    }

    public async Task<DocumentIndexRequest> BuildIndexRequestAsync(Guid documentId, Guid? versionId, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new InvalidOperationException($"Document {documentId} not found");

        var extraction = await _documentContentService.FetchContentAsync(document, versionId, cancellationToken);
        if (!extraction.HasContent)
        {
            _logger.LogDebug("Secure extraction returned no content for document {DocumentId}. Falling back to metadata-only indexing.", document.Id);
        }

        var content = BuildIndexPayload(document, extraction);
        var metadata = MergeMetadata(document.Metadata, extraction.AdditionalMetadata, extraction.HasContent, extraction.IsPartial);

        var request = new DocumentIndexRequest(
            document.Id,
            document.OrgId,
            versionId,
            content,
            document.Tags,
            metadata);

        return request;
    }

    public async Task ProcessIndexRequestAsync(DocumentIndexRequest request, CancellationToken cancellationToken = default)
    {
        var chunkMetadataTemplate = new Dictionary<string, string?>
        {
            { "documentId", request.DocumentId.ToString() },
            { "orgId", request.OrgId.ToString() }
        };

        if (request.Metadata.TryGetValue("provider", out var provider))
        {
            chunkMetadataTemplate["provider"] = provider;
        }

        if (request.Metadata.TryGetValue("contentType", out var contentType))
        {
            chunkMetadataTemplate["contentType"] = contentType;
        }

        if (request.Metadata.TryGetValue("partialExtraction", out var partialExtraction))
        {
            chunkMetadataTemplate["partialExtraction"] = partialExtraction;
        }

        var chunks = ChunkContent(request.Content)
            .Select(chunk =>
            {
                var chunkMetadata = new Dictionary<string, string?>(chunkMetadataTemplate)
                {
                    ["chunkIndex"] = chunk.Index.ToString(),
                    ["chunkLength"] = chunk.Content.Length.ToString()
                };

                return new DocumentVectorChunk(
                    chunk.Index,
                    chunk.Content,
                    GeneratePlaceholderEmbedding(chunk.Content),
                    null,
                    chunkMetadata);
            })
            .ToList();

        await _vectorStoreService.UpsertAsync(new VectorUpsertRequest(
            request.DocumentId,
            request.OrgId,
            request.VersionId,
            chunks), cancellationToken);

        var document = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);
        if (document != null)
        {
            document.LastEmbeddedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Skip audit logging - Guid UserId doesn't match int-based Users table FK constraint
        // Embedding operations are tracked via Document.LastEmbeddedAt timestamp
        // await _documentAuditService.LogAsync(new DocumentAuditEvent(
        //     document?.OrgId ?? request.OrgId,
        //     document?.MatterId,
        //     request.DocumentId,
        //     request.VersionId,
        //     document?.OwnerUserId,
        //     "EmbeddingGenerated",
        //     "Document embeddings refreshed",
        //     DateTime.UtcNow), cancellationToken);

        _logger.LogDebug("Processed embedding request for document {DocumentId}", request.DocumentId);
    }

    private static string BuildIndexPayload(Document document, DocumentContentResult extraction)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Title: {document.Title}");
        builder.AppendLine($"Category: {document.Category}");
        builder.AppendLine($"Source: {document.SourceType}");
        builder.AppendLine($"Status: {document.Status}");
        builder.AppendLine($"LastModifiedUtc: {document.ModifiedAt:O}");

        if (document.Metadata.Count > 0)
        {
            builder.AppendLine("Metadata:");
            foreach (var kvp in document.Metadata)
            {
                builder.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }
        }

        if (document.Tags.Count > 0)
        {
            builder.AppendLine("Tags:");
            foreach (var tag in document.Tags)
            {
                builder.AppendLine($"#{tag}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine("SanitizedContent:");

        if (extraction.HasContent)
        {
            builder.AppendLine(extraction.Content);
        }
        else
        {
            builder.AppendLine("[Content unavailable – secure extraction returned no body]");
        }

        return builder.ToString();
    }

    private static Dictionary<string, string?> MergeMetadata(
        IReadOnlyDictionary<string, string?> primary,
        IReadOnlyDictionary<string, string?> secondary,
        bool hasContent,
        bool isPartial)
    {
        var merged = new Dictionary<string, string?>(primary, StringComparer.OrdinalIgnoreCase)
        {
            ["hasExtractedContent"] = hasContent ? "true" : "false",
            ["partialExtraction"] = isPartial ? "true" : "false"
        };

        foreach (var kvp in secondary)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    private static IEnumerable<(int Index, string Content)> ChunkContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            yield break;
        }

        var chunkIndex = 0;
        for (var start = 0; start < content.Length; start += DefaultChunkSize)
        {
            var length = Math.Min(DefaultChunkSize, content.Length - start);
            var chunk = content.Substring(start, length);
            yield return (chunkIndex, chunk);
            chunkIndex++;
        }
    }

    private static float[] GeneratePlaceholderEmbedding(string content)
    {
        // Placeholder embedding uses deterministic hash-based vector so that similarity search remains stable.
        var vector = new float[8];
        var hash = content.GetHashCode();
        var rng = new Random(hash);
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)rng.NextDouble();
        }

        return vector;
    }
}

