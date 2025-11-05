using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Documents;
using Certio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers;

[Authorize(Policy = "OrgMember")]
[ApiController]
[Route("api/documents")]
public class DocumentsApiController : ControllerBase
{
    private const long EmbeddingSizeLimitBytes = 10 * 1024 * 1024;

    private readonly ApplicationDbContext _dbContext;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IRagContextService _ragContextService;
    private readonly IDocumentIndexerService _documentIndexerService;
    private readonly IDocumentAuditService _documentAuditService;

    public DocumentsApiController(
        ApplicationDbContext dbContext,
        IVectorStoreService vectorStoreService,
        IRagContextService ragContextService,
        IDocumentIndexerService documentIndexerService,
        IDocumentAuditService documentAuditService)
    {
        _dbContext = dbContext;
        _vectorStoreService = vectorStoreService;
        _ragContextService = ragContextService;
        _documentIndexerService = documentIndexerService;
        _documentAuditService = documentAuditService;
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] DocumentSearchRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var query = request.Query?.Trim() ?? string.Empty;

        var metadataMatches = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.OrgId == request.OrgId && d.DeletedAt == null)
            .Where(d => d.Title.Contains(query) || d.Category.Contains(query) || d.Tags.Any(tag => tag.Contains(query)))
            .Select(d => new DocumentSearchResult(d, Array.Empty<DocumentVector>(), 0.6))
            .Take(request.TopK)
            .ToListAsync(cancellationToken);

        var vectorMatches = await _vectorStoreService.SearchAsync(request.OrgId, query, request.TopK, request.MatterId, cancellationToken);

        var grouped = new Dictionary<Guid, DocumentSearchResult>();

        void AddOrUpdate(DocumentSearchResult item)
        {
            if (grouped.TryGetValue(item.Document.Id, out var existing))
            {
                var combinedVectors = existing.ContextVectors.Concat(item.ContextVectors).Distinct().ToList();
                grouped[item.Document.Id] = new DocumentSearchResult(existing.Document, combinedVectors, Math.Max(existing.Score, item.Score));
            }
            else
            {
                grouped[item.Document.Id] = item;
            }
        }

        foreach (var metadata in metadataMatches)
        {
            AddOrUpdate(metadata);
        }

        foreach (var vector in vectorMatches)
        {
            AddOrUpdate(new DocumentSearchResult(vector.Document, new[] { vector }, 1.0));
        }

        var ordered = grouped.Values
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Document.ModifiedAt)
            .Take(request.TopK)
            .Select(r => new
            {
                id = r.Document.Id,
                title = r.Document.Title,
                category = r.Document.Category,
                status = r.Document.Status,
                source = r.Document.SourceType,
                modifiedAt = r.Document.ModifiedAt,
                score = r.Score,
                previewUrl = r.Document.PreviewUrl,
                downloadUrl = r.Document.DownloadUrl,
                tags = r.Document.Tags,
                context = r.ContextVectors.Select(v => new { v.Id, v.ChunkIndex, v.ContentChunk })
            });

        return Ok(new { items = ordered });
    }

    [HttpPost("upload")]
    [RequestSizeLimit(EmbeddingSizeLimitBytes)]
    public async Task<IActionResult> Upload([FromForm] DocumentUploadRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.File.Length == 0)
        {
            return BadRequest("File is empty");
        }

        if (request.File.Length > EmbeddingSizeLimitBytes)
        {
            return BadRequest("File exceeds 10 MB embedding limit");
        }

        var document = new Document
        {
            OrgId = request.OrgId,
            MatterId = request.MatterId,
            SourceType = DocumentSourceType.InternalUpload,
            Title = request.File.FileName,
            FileType = request.File.ContentType,
            FileSizeBytes = request.File.Length,
            Status = DocumentStatus.Draft,
            Category = request.Category ?? "General",
            IsPrivate = request.IsPrivate,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Tags = request.Tags?.ToList() ?? new List<string>(),
            Metadata = new Dictionary<string, string?>
            {
                { "originalFileName", request.File.FileName }
            },
            DownloadUrl = $"/api/documents/{Guid.NewGuid()}/download"
        };

        _dbContext.Documents.Add(document);

        var version = new DocumentVersion
        {
            Document = document,
            VersionNumber = 1,
            ModifiedAt = document.ModifiedAt,
            ModifiedByUserId = Guid.Empty,
            StorageUrl = document.DownloadUrl ?? string.Empty
        };

        _dbContext.DocumentVersions.Add(version);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _documentIndexerService.QueueEmbeddingAsync(document.Id, version.Id, cancellationToken);

        await _documentAuditService.LogAsync(new DocumentAuditEvent(
            document.OrgId,
            document.MatterId,
            document.Id,
            version.Id,
            null,
            "Upload",
            $"Document '{document.Title}' uploaded",
            DateTime.UtcNow), cancellationToken);

        return CreatedAtAction(nameof(Search), new { id = document.Id }, new { document.Id });
    }

    [HttpPost("reindex/{orgId:guid}")]
    public async Task<IActionResult> Reindex(Guid orgId, CancellationToken cancellationToken)
    {
        var documents = await _dbContext.Documents
            .Include(d => d.Versions)
            .Where(d => d.OrgId == orgId && d.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var document in documents)
        {
            var latestVersionId = document.Versions
                .OrderByDescending(v => v.ModifiedAt)
                .Select(v => (Guid?)v.Id)
                .FirstOrDefault();

            await _documentIndexerService.QueueEmbeddingAsync(document.Id, latestVersionId, cancellationToken);
        }

        return Accepted(new { queued = documents.Count });
    }

    public record DocumentSearchRequest
    {
        [Required]
        public Guid OrgId { get; init; }

        public Guid? MatterId { get; init; }

        [Required]
        [StringLength(500)]
        public string Query { get; init; } = string.Empty;

        [Range(1, 50)]
        public int TopK { get; init; } = 10;
    }

    public record DocumentUploadRequest
    {
        [Required]
        public Guid OrgId { get; init; }

        public Guid? MatterId { get; init; }

        public bool IsPrivate { get; init; } = true;

        [StringLength(120)]
        public string? Category { get; init; }

        public IEnumerable<string>? Tags { get; init; }

        [Required]
        public IFormFile File { get; init; } = null!;
    }
}

