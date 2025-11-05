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
    private readonly IDocumentAuditService _documentAuditService;
    private readonly IEmbeddingJobQueue _jobQueue;
    private readonly ILogger<DocumentIndexerService> _logger;

    public DocumentIndexerService(
        ApplicationDbContext dbContext,
        IVectorStoreService vectorStoreService,
        IDocumentAuditService documentAuditService,
        IEmbeddingJobQueue jobQueue,
        ILogger<DocumentIndexerService> logger)
    {
        _dbContext = dbContext;
        _vectorStoreService = vectorStoreService;
        _documentAuditService = documentAuditService;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task QueueEmbeddingAsync(Guid documentId, Guid? versionId, CancellationToken cancellationToken = default)
    {
        var request = await BuildIndexRequestAsync(documentId, versionId, cancellationToken);
        await _jobQueue.EnqueueAsync(request, cancellationToken);
        _logger.LogInformation("Queued document {DocumentId} (Version {VersionId}) for embedding", documentId, versionId);
    }

    public async Task<DocumentIndexRequest> BuildIndexRequestAsync(Guid documentId, Guid? versionId, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new InvalidOperationException($"Document {documentId} not found");

        string content = BuildSyntheticContent(document);

        var request = new DocumentIndexRequest(
            document.Id,
            document.OrgId,
            versionId,
            content,
            document.Tags,
            document.Metadata);

        return request;
    }

    public async Task ProcessIndexRequestAsync(DocumentIndexRequest request, CancellationToken cancellationToken = default)
    {
        var chunks = ChunkContent(request.Content)
            .Select(chunk => new DocumentVectorChunk(
                chunk.Index,
                chunk.Content,
                GeneratePlaceholderEmbedding(chunk.Content),
                null,
                new Dictionary<string, string?>
                {
                    { "chunkLength", chunk.Content.Length.ToString() }
                }))
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

        await _documentAuditService.LogAsync(new DocumentAuditEvent(
            document?.OrgId ?? request.OrgId,
            document?.MatterId,
            request.DocumentId,
            request.VersionId,
            document?.OwnerUserId,
            "EmbeddingGenerated",
            "Document embeddings refreshed",
            DateTime.UtcNow), cancellationToken);

        _logger.LogInformation("Processed embedding request for document {DocumentId}", request.DocumentId);
    }

    private static string BuildSyntheticContent(Document document)
    {
        var builder = new StringBuilder();
        builder.AppendLine(document.Title);
        builder.AppendLine($"Category: {document.Category}");
        builder.AppendLine($"Source: {document.SourceType}");
        builder.AppendLine($"Status: {document.Status}");
        builder.AppendLine($"LastModified: {document.ModifiedAt:O}");

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

        return builder.ToString();
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

