using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentsApiController> _logger;

    public DocumentsApiController(
        ApplicationDbContext dbContext,
        IVectorStoreService vectorStoreService,
        IRagContextService ragContextService,
        IDocumentIndexerService documentIndexerService,
        IDocumentAuditService documentAuditService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DocumentsApiController> logger)
    {
        _dbContext = dbContext;
        _vectorStoreService = vectorStoreService;
        _ragContextService = ragContextService;
        _documentIndexerService = documentIndexerService;
        _documentAuditService = documentAuditService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
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

        // Audit: document search (non-blocking)
        try
        {
            int currentUserId;
            if (HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int userId)
            {
                currentUserId = userId;
            }
            else
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                currentUserId = (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var claimUserId)) ? claimUserId : 0;
            }
            var documentUserId = CreateDeterministicGuid("certio:user", currentUserId);
            await _documentAuditService.LogAsync(
                new DocumentAuditEvent(
                    request.OrgId,
                    request.MatterId,
                    null,
                    null,
                    documentUserId,
                    "Search",
                    "Searched documents",
                    DateTime.UtcNow,
                    currentUserId),
                cancellationToken);
        }
        catch { }

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

        // Audit: reindex queued (non-blocking)
        try
        {
            int currentUserId = HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int u ? u : (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var c) ? c : 0);
            var documentUserId = CreateDeterministicGuid("certio:user", currentUserId);
            await _documentAuditService.LogAsync(
                new DocumentAuditEvent(
                    orgId,
                    null,
                    null,
                    null,
                    documentUserId,
                    "ReindexQueued",
                    $"Queued embeddings for {documents.Count} documents",
                    DateTime.UtcNow,
                    currentUserId),
                cancellationToken);
        }
        catch { }

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

    /// <summary>
    /// Get a fresh embed URL for OneDrive/Office documents
    /// This endpoint fetches a current download URL from Microsoft Graph since download URLs expire
    /// </summary>
    [AllowAnonymous] // Bypass class-level OrgMember policy since route doesn't have orgId
    [HttpGet("{documentId:guid}/embed-url")]
    public async Task<IActionResult> GetEmbedUrl(Guid documentId, CancellationToken cancellationToken)
    {
        // Manual authentication check since we're using AllowAnonymous
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized();
        }

        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.DeletedAt == null, cancellationToken);

        if (document == null)
        {
            return NotFound();
        }

        // For OneDrive documents, fetch a fresh download URL
        if (document.SourceType == DocumentSourceType.OneDrive && !string.IsNullOrWhiteSpace(document.ExternalFileId))
        {
            try
            {
                // Get the user's OneDrive connection
                var connection = await _dbContext.ExternalConnections
                    .AsNoTracking()
                    .Where(ec => ec.OrgId == document.OrgId && ec.Provider == ExternalConnectionProvider.Microsoft)
                    .OrderByDescending(ec => ec.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (connection == null)
                {
                    _logger.LogWarning("No Microsoft connection found for document {DocumentId} in org {OrgId}", documentId, document.OrgId);
                }
                else if (connection.TokenExpiry <= DateTime.UtcNow.AddMinutes(5))
                {
                    _logger.LogWarning("Microsoft connection token expired for document {DocumentId}. Expires: {TokenExpiry}", documentId, connection.TokenExpiry);
                }
                else
                {
                    var accessToken = DecryptToken(connection.AccessToken);
                    
                    using var httpClient = _httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    // Prefer using driveId when available; fallback to /me/drive
                    var driveIdFromMetadata = document.Metadata != null && document.Metadata.TryGetValue("driveId", out var driveIdValue)
                        ? driveIdValue
                        : null;

                    // Fetch fresh file metadata including download URL
                    var graphUrl = !string.IsNullOrWhiteSpace(driveIdFromMetadata)
                        ? $"https://graph.microsoft.com/v1.0/drives/{driveIdFromMetadata}/items/{document.ExternalFileId}?$select=@microsoft.graph.downloadUrl,webUrl"
                        : $"https://graph.microsoft.com/v1.0/me/drive/items/{document.ExternalFileId}?$select=@microsoft.graph.downloadUrl,webUrl";
                    _logger.LogInformation("Fetching fresh OneDrive URL for document {DocumentId} from Microsoft Graph", documentId);
                    var response = await httpClient.GetAsync(graphUrl, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        using var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                        var root = jsonDoc.RootElement;

                        var downloadUrl = root.TryGetProperty("@microsoft.graph.downloadUrl", out var dlProp) ? dlProp.GetString() : null;
                        var webUrl = root.TryGetProperty("webUrl", out var webProp) ? webProp.GetString() : null;

                        _logger.LogInformation("Microsoft Graph returned downloadUrl: {HasDownloadUrl}, webUrl: {HasWebUrl}", 
                            !string.IsNullOrWhiteSpace(downloadUrl), !string.IsNullOrWhiteSpace(webUrl));

                        if (!string.IsNullOrWhiteSpace(downloadUrl))
                        {
                            // Use Office Online embed viewer with fresh download URL
                            var embedUrl = $"https://view.officeapps.live.com/op/embed.aspx?src={Uri.EscapeDataString(downloadUrl)}";
                            _logger.LogInformation("Returning Office Online embed URL for document {DocumentId}", documentId);
                            // Audit: embed URL fetched (non-blocking)
                            try
                            {
                                int currentUserId = HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int u ? u : (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var c) ? c : 0);
                                var documentUserIdAudit = CreateDeterministicGuid("certio:user", currentUserId);
                                await _documentAuditService.LogAsync(
                                    new DocumentAuditEvent(
                                        document.OrgId,
                                        document.MatterId,
                                        document.Id,
                                        null,
                                        documentUserIdAudit,
                                        "GetEmbedUrl",
                                        "Fetched fresh embed URL",
                                        DateTime.UtcNow,
                                        currentUserId),
                                    cancellationToken);
                            }
                            catch { }
                            return Ok(new { embedUrl, openUrl = webUrl ?? document.PreviewUrl });
                        }
                        else
                        {
                            _logger.LogWarning("Microsoft Graph did not return a download URL for document {DocumentId}", documentId);
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogError("Microsoft Graph API failed with status {StatusCode}: {Error}", response.StatusCode, errorContent);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching fresh OneDrive URL for document {DocumentId}", documentId);
            }
        }

        // Fallback to stored URLs for Google Drive or if OneDrive fetch failed
        var fallbackEmbedUrl = document.EmbedUrl ?? document.PreviewUrl ?? document.DownloadUrl ?? string.Empty;
        var fallbackOpenUrl = document.PreviewUrl ?? document.EmbedUrl ?? document.DownloadUrl ?? string.Empty;
        
        // For OneDrive documents, even if fetch failed, try to use Office Online viewer with stored download URL
        if (document.SourceType == DocumentSourceType.OneDrive && !string.IsNullOrWhiteSpace(document.DownloadUrl))
        {
            // Check if this looks like a direct download URL (not already an embed URL)
            if (!document.DownloadUrl.Contains("officeapps.live.com") && !document.DownloadUrl.Contains("/embed"))
            {
                _logger.LogInformation("Using fallback Office Online embed viewer for document {DocumentId} with stored download URL", documentId);
                fallbackEmbedUrl = $"https://view.officeapps.live.com/op/embed.aspx?src={Uri.EscapeDataString(document.DownloadUrl)}";
            }
        }
        
        _logger.LogInformation("Returning fallback embed URL for document {DocumentId}: {EmbedUrl}", documentId, fallbackEmbedUrl.Substring(0, Math.Min(50, fallbackEmbedUrl.Length)));
        // Audit: fallback embed URL (non-blocking)
        try
        {
            int currentUserId = HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int u ? u : (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var c) ? c : 0);
            var documentUserIdAudit = CreateDeterministicGuid("certio:user", currentUserId);
            await _documentAuditService.LogAsync(
                new DocumentAuditEvent(
                    document.OrgId,
                    document.MatterId,
                    document.Id,
                    null,
                    documentUserIdAudit,
                    "GetEmbedUrl",
                    "Returned fallback embed URL",
                    DateTime.UtcNow,
                    currentUserId),
                cancellationToken);
        }
        catch { }
        return Ok(new { embedUrl = fallbackEmbedUrl, openUrl = fallbackOpenUrl });
    }

    private string DecryptToken(string encryptedToken)
    {
        var encryptionKey = _configuration["Security:EncryptionKey"] ?? throw new InvalidOperationException("Encryption key not configured");
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(encryptionKey.PadRight(32).Substring(0, 32));

        var parts = encryptedToken.Split(':');
        if (parts.Length != 2)
        {
            throw new InvalidOperationException("Invalid encrypted token format");
        }

        var iv = Convert.FromBase64String(parts[0]);
        var cipherText = Convert.FromBase64String(parts[1]);

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = keyBytes;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var msDecrypt = new System.IO.MemoryStream(cipherText);
        using var csDecrypt = new System.Security.Cryptography.CryptoStream(msDecrypt, decryptor, System.Security.Cryptography.CryptoStreamMode.Read);
        using var srDecrypt = new System.IO.StreamReader(csDecrypt);

        return srDecrypt.ReadToEnd();
    }

    /// <summary>
    /// Get documents with pagination (lazy loading)
    /// Similar to email inbox - loads documents on demand
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] int orgId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25,
        [FromQuery] string? sourceType = null,
        [FromQuery] string? search = null,
        [FromQuery] string? fileTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert int orgId to Guid (same as DocumentsController)
            var documentOrgId = CreateDeterministicGuid("certio:organization", orgId);
            
            var query = _dbContext.Documents
                .AsNoTracking()
                .Where(d => d.OrgId == documentOrgId && d.DeletedAt == null);

            // Filter by source type if provided
            if (!string.IsNullOrWhiteSpace(sourceType))
            {
                if (Enum.TryParse<DocumentSourceType>(sourceType, true, out var sourceTypeEnum))
                {
                    query = query.Where(d => d.SourceType == sourceTypeEnum);
                }
            }

            // Search filter if provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLowerInvariant();
                query = query.Where(d => 
                    d.Title.ToLower().Contains(searchLower) ||
                    (d.Metadata != null && d.Metadata.ContainsKey("ownerName") && d.Metadata["ownerName"] != null && d.Metadata["ownerName"].ToLower().Contains(searchLower)));
            }

            // File type filter if provided
            if (!string.IsNullOrWhiteSpace(fileTypeFilter) && fileTypeFilter != "all")
            {
                query = fileTypeFilter switch
                {
                    "google-docs" => query.Where(d => d.FileType == "application/vnd.google-apps.document"),
                    "google-sheets" => query.Where(d => d.FileType == "application/vnd.google-apps.spreadsheet"),
                    "google-slides" => query.Where(d => d.FileType == "application/vnd.google-apps.presentation"),
                    "pdf" => query.Where(d => d.FileType == "application/pdf"),
                    "word" => query.Where(d => d.FileType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document" || d.FileType == "application/msword"),
                    "excel" => query.Where(d => d.FileType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" || d.FileType == "application/vnd.ms-excel"),
                    "powerpoint" => query.Where(d => d.FileType == "application/vnd.openxmlformats-officedocument.presentationml.presentation" || d.FileType == "application/vnd.ms-powerpoint"),
                    "text" => query.Where(d => d.FileType.StartsWith("text/")),
                    _ => query
                };
            }

            // Order by modified date (newest first)
            query = query.OrderByDescending(d => d.ModifiedAt);

            // Get total count for pagination
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply pagination
            var documents = await query
                .Skip(skip)
                .Take(take + 1) // Get one extra to check if there's more
                .Select(d => new
                {
                    id = d.Id,
                    title = d.Title,
                    name = d.Title,
                    type = d.FileType,
                    fileType = d.FileType,
                    fileSizeBytes = d.FileSizeBytes,
                    status = d.Status.ToString(),
                    sourceType = d.SourceType.ToString(),
                    author = d.Metadata != null && d.Metadata.ContainsKey("ownerName") ? d.Metadata["ownerName"] : "Unknown",
                    modifiedAt = d.ModifiedAt,
                    modified = d.ModifiedAt,
                    isPrivate = d.IsPrivate,
                    previewUrl = d.PreviewUrl,
                    embedUrl = d.EmbedUrl,
                    downloadUrl = d.DownloadUrl,
                    externalFileId = d.ExternalFileId,
                    metadata = d.Metadata,
                    icon = d.SourceType == DocumentSourceType.GoogleDrive ? "File" :
                           d.SourceType == DocumentSourceType.OneDrive ? "File" : "FileText"
                })
                .ToListAsync(cancellationToken);

            var hasMore = documents.Count > take;
            if (hasMore)
            {
                documents = documents.Take(take).ToList();
            }

            // Audit: list documents (non-blocking)
            try
            {
                int currentUserId = HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int u ? u : (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var c) ? c : 0);
                var documentUserId = CreateDeterministicGuid("certio:user", currentUserId);
                await _documentAuditService.LogAsync(
                    new DocumentAuditEvent(
                        documentOrgId,
                        null,
                        null,
                        null,
                        documentUserId,
                        "List",
                        "Listed documents",
                        DateTime.UtcNow,
                        currentUserId),
                    cancellationToken);
            }
            catch { }

            return Ok(new
            {
                success = true,
                documents,
                hasMore,
                total = totalCount,
                skip,
                take
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading documents for org {OrgId}", orgId);
            return StatusCode(500, new { success = false, error = "Failed to load documents" });
        }
    }

    /// <summary>
    /// Update document status (for RAG pipeline integration - e.g., Notal)
    /// Cycles through: Draft -> Review -> Final -> Draft
    /// </summary>
    [HttpPost("{documentId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid documentId,
        [FromBody] UpdateDocumentStatusRequest request,
        [FromQuery] int? orgId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var documentOrgId = orgId.HasValue 
                ? CreateDeterministicGuid("certio:organization", orgId.Value)
                : (Guid?)null;

            var document = await _dbContext.Documents
                .FirstOrDefaultAsync(d => d.Id == documentId && 
                    (documentOrgId == null || d.OrgId == documentOrgId) && 
                    d.DeletedAt == null, cancellationToken);

            if (document == null)
            {
                return NotFound(new { success = false, error = "Document not found" });
            }

            // Validate status - also accept string status and convert to enum
            // Handle JsonElement (when deserializing JSON primitives into object)
            DocumentStatus newStatus = DocumentStatus.Draft; // Initialize to satisfy compiler
            string? statusString = null;
            bool needsParsing = false;
            
            if (request.Status is DocumentStatus enumStatus)
            {
                newStatus = enumStatus;
            }
            else if (request.Status is JsonElement jsonElement)
            {
                // Extract value from JsonElement
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    statusString = jsonElement.GetString();
                    needsParsing = true;
                }
                else if (jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out var intValue))
                {
                    if (Enum.IsDefined(typeof(DocumentStatus), intValue))
                    {
                        newStatus = (DocumentStatus)intValue;
                    }
                    else
                    {
                        _logger.LogWarning("Invalid status integer value: {IntValue}", intValue);
                        return BadRequest(new { success = false, error = $"Invalid status value: {intValue}. Valid values are: 0 (Draft), 1 (Review), 2 (Final), 3 (Published)" });
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid JsonElement value kind: {ValueKind}", jsonElement.ValueKind);
                    return BadRequest(new { success = false, error = $"Invalid status value format. Expected string or number, got: {jsonElement.ValueKind}" });
                }
            }
            else if (request.Status is string str)
            {
                statusString = str;
                needsParsing = true;
            }
            else if (request.Status is int statusInt && Enum.IsDefined(typeof(DocumentStatus), statusInt))
            {
                newStatus = (DocumentStatus)statusInt;
            }
            else
            {
                _logger.LogWarning("Invalid status value type: {Type}, Value: {Value}", request.Status?.GetType().Name, request.Status);
                return BadRequest(new { success = false, error = $"Invalid status value. Expected string (Draft, Review, Final, Published) or enum, got: {request.Status?.GetType().Name ?? "null"}" });
            }
            
            // Parse status string if we extracted one
            if (needsParsing && statusString != null)
            {
                if (Enum.TryParse<DocumentStatus>(statusString, true, out var parsedStatus))
                {
                    newStatus = parsedStatus;
                }
                else
                {
                    _logger.LogWarning("Invalid status string value: {StatusString}. Valid values are: Draft, Review, Final, Published", statusString);
                    return BadRequest(new { success = false, error = $"Invalid status value: '{statusString}'. Valid values are: Draft, Review, Final, Published" });
                }
            }

            var oldStatus = document.Status;
            document.Status = newStatus;
            document.ModifiedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Log status change for RAG pipeline context
            int currentUserId;
            
            // First try to get the custom user ID from context (set by UserSyncMiddleware)
            if (HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int userId)
            {
                currentUserId = userId;
            }
            else
            {
                // Fallback to claims (for backwards compatibility)
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var claimUserId))
                {
                    currentUserId = claimUserId;
                }
                else
                {
                    _logger.LogWarning("Could not determine user ID for status change audit");
                    currentUserId = 0; // Default to 0 if we can't determine the user
                }
            }
            
            var documentUserId = CreateDeterministicGuid("certio:user", currentUserId);
            
            await _documentAuditService.LogAsync(
                new DocumentAuditEvent(
                    document.OrgId,
                    documentId,
                    null,
                    null,
                    documentUserId,
                    "DocumentStatusChanged",
                    $"Status changed from {oldStatus} to {newStatus}",
                    DateTime.UtcNow,
                    currentUserId),
                cancellationToken);

            _logger.LogInformation("Document {DocumentId} status updated from {OldStatus} to {NewStatus} by user {UserId}",
                documentId, oldStatus, newStatus, currentUserId);

            return Ok(new
            {
                success = true,
                documentId = document.Id,
                status = document.Status.ToString(),
                modifiedAt = document.ModifiedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document status for {DocumentId}", documentId);
            return StatusCode(500, new { success = false, error = "Failed to update document status" });
        }
    }

    private static Guid CreateDeterministicGuid(string namespacePrefix, int value)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{namespacePrefix}:{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40); // Version 4
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // Variant RFC 4122
        return new Guid(guidBytes);
    }
}

public class UpdateDocumentStatusRequest
{
    [Required]
    [JsonPropertyName("status")]
    public object Status { get; set; } = null!; // Accepts both DocumentStatus enum and string
}

