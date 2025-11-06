using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.Services.Documents;
using Certio.Domain.Documents;
using Certio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Certio.Web.Controllers;

[ApiController]
[Route("wopi/files/{fileId}")]
[AllowAnonymous] // WOPI uses access tokens in query string instead of cookies
public class WopiController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly WopiAccessTokenService _wopiTokenService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WopiController> _logger;

    public WopiController(
        ApplicationDbContext dbContext,
        WopiAccessTokenService wopiTokenService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<WopiController> logger)
    {
        _dbContext = dbContext;
        _wopiTokenService = wopiTokenService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// CheckFileInfo - Returns information about the file and user permissions
    /// This is called by Office Online to get file metadata
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckFileInfo(string fileId, [FromQuery(Name = "access_token")] string accessToken, CancellationToken cancellationToken)
    {
        _logger.LogInformation("WOPI CheckFileInfo called for file {FileId}", fileId);

        if (!Guid.TryParse(fileId, out var documentId))
        {
            _logger.LogWarning("Invalid file ID format: {FileId}", fileId);
            return BadRequest(new { error = "Invalid file ID" });
        }

        // Validate access token
        var tokenInfo = await _wopiTokenService.ValidateAccessTokenAsync(accessToken, cancellationToken);
        if (tokenInfo == null || tokenInfo.DocumentId != documentId)
        {
            _logger.LogWarning("Invalid or expired WOPI access token for file {FileId}", fileId);
            return Unauthorized(new { error = "Invalid access token" });
        }

        // Get document
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.OrgId == tokenInfo.OrgId && d.DeletedAt == null, 
                cancellationToken);

        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} not found", documentId);
            return NotFound(new { error = "File not found" });
        }

        // Get file size
        long fileSize = document.FileSizeBytes > 0 
            ? document.FileSizeBytes 
            : await GetFileSizeAsync(document, tokenInfo.OrgId, cancellationToken);

        // Get file extension
        var extension = document.FileType switch
        {
            var ft when ft?.Contains("word") == true => ".docx",
            var ft when ft?.Contains("excel") == true => ".xlsx",
            var ft when ft?.Contains("powerpoint") == true => ".pptx",
            var ft when ft?.Contains("pdf") == true => ".pdf",
            _ => Path.GetExtension(document.Title) ?? ".docx"
        };

        // Build the CheckFileInfo response
        // See: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo
        var checkFileInfo = new
        {
            // Required properties
            BaseFileName = document.Title ?? "Untitled",
            OwnerId = tokenInfo.UserId.ToString(),
            Size = fileSize,
            UserId = tokenInfo.UserId.ToString(),
            Version = document.ModifiedAt.Ticks.ToString(),

            // User permissions - EDITING ENABLED
            UserCanWrite = true, // Enable editing
            UserCanNotWriteRelative = true,
            ReadOnly = false, // Allow editing
            UserCanPresent = true,
            UserCanAttend = true,

            // File capabilities - EDITING ENABLED
            SupportsUpdate = true, // Enable saving changes
            SupportsLocks = true, // Enable file locking
            SupportsGetLock = true,
            SupportsExtendedLockLength = false,
            SupportsCobalt = false, // Cobalt is for legacy Office versions
            SupportsFolders = false,
            SupportsRename = false,

            // File metadata
            LastModifiedTime = document.ModifiedAt.ToString("o"),
            SHA256 = string.Empty, // Optional: can compute file hash

            // User info
            UserFriendlyName = "User", // Could fetch from user table if needed
            IsAnonymousUser = false,
            IsEduUser = false,

            // Breadcrumb
            BreadcrumbBrandName = "Certio",
            BreadcrumbBrandUrl = GetBaseUrl(),
            BreadcrumbDocName = document.Title,
            BreadcrumbFolderName = "Documents",

            // Hosting capabilities
            CloseButtonClosesWindow = true,
            CloseUrl = $"{GetBaseUrl()}/Client/{tokenInfo.OrgId}/Documents",
            DownloadUrl = await GetDownloadUrlAsync(document, tokenInfo.OrgId, cancellationToken),
            FileExtension = extension,
            FileNameMaxLength = 255,
            HostEditUrl = string.Empty,
            HostViewUrl = $"{GetBaseUrl()}/Documents/View/{documentId}",
            
            // Disable problematic features
            DisablePrint = false,
            DisableTranslation = true,
            FileUrl = await GetDownloadUrlAsync(document, tokenInfo.OrgId, cancellationToken)
        };

        _logger.LogInformation("WOPI CheckFileInfo succeeded for document {DocumentId}, size: {Size} bytes", 
            documentId, fileSize);

        return Ok(checkFileInfo);
    }

    /// <summary>
    /// GetFile - Returns the file contents
    /// This is called by Office Online to download the file for editing/viewing
    /// </summary>
    [HttpGet("contents")]
    public async Task<IActionResult> GetFile(string fileId, [FromQuery(Name = "access_token")] string accessToken, CancellationToken cancellationToken)
    {
        _logger.LogInformation("WOPI GetFile called for file {FileId}", fileId);

        if (!Guid.TryParse(fileId, out var documentId))
        {
            _logger.LogWarning("Invalid file ID format: {FileId}", fileId);
            return BadRequest(new { error = "Invalid file ID" });
        }

        // Validate access token
        var tokenInfo = await _wopiTokenService.ValidateAccessTokenAsync(accessToken, cancellationToken);
        if (tokenInfo == null || tokenInfo.DocumentId != documentId)
        {
            _logger.LogWarning("Invalid or expired WOPI access token for file {FileId}", fileId);
            return Unauthorized(new { error = "Invalid access token" });
        }

        // Get document
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.OrgId == tokenInfo.OrgId && d.DeletedAt == null, 
                cancellationToken);

        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} not found", documentId);
            return NotFound(new { error = "File not found" });
        }

        // For OneDrive documents, we need to fetch the file from Microsoft Graph
        if (document.SourceType == DocumentSourceType.OneDrive && !string.IsNullOrWhiteSpace(document.ExternalFileId))
        {
            try
            {
                var fileContent = await DownloadOneDriveFileAsync(document, tokenInfo.OrgId, cancellationToken);
                if (fileContent != null)
                {
                    var contentType = document.FileType ?? "application/octet-stream";
                    _logger.LogInformation("WOPI GetFile succeeded for OneDrive document {DocumentId}, size: {Size} bytes", 
                        documentId, fileContent.Length);
                    
                    Response.Headers["X-WOPI-ItemVersion"] = document.ModifiedAt.Ticks.ToString();
                    return File(fileContent, contentType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading OneDrive file for document {DocumentId}", documentId);
            }
        }

        // Fallback for internal files or if OneDrive download failed
        _logger.LogWarning("Cannot download file content for document {DocumentId} - not implemented for source type {SourceType}", 
            documentId, document.SourceType);
        return StatusCode(500, new { error = "File download not available" });
    }

    /// <summary>
    /// PutFile - Saves changes to the file
    /// This is called by Office Online when user saves changes
    /// </summary>
    [HttpPost("contents")]
    [HttpPut("contents")]
    public async Task<IActionResult> PutFile(string fileId, [FromQuery(Name = "access_token")] string accessToken, CancellationToken cancellationToken)
    {
        _logger.LogInformation("WOPI PutFile called for file {FileId}", fileId);

        if (!Guid.TryParse(fileId, out var documentId))
        {
            _logger.LogWarning("Invalid file ID format: {FileId}", fileId);
            return BadRequest(new { error = "Invalid file ID" });
        }

        // Validate access token
        var tokenInfo = await _wopiTokenService.ValidateAccessTokenAsync(accessToken, cancellationToken);
        if (tokenInfo == null || tokenInfo.DocumentId != documentId)
        {
            _logger.LogWarning("Invalid or expired WOPI access token for file {FileId}", fileId);
            return Unauthorized(new { error = "Invalid access token" });
        }

        // Get document
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.OrgId == tokenInfo.OrgId && d.DeletedAt == null, 
                cancellationToken);

        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} not found", documentId);
            return NotFound(new { error = "File not found" });
        }

        // Read the file content from request body
        byte[] fileContent;
        using (var memoryStream = new MemoryStream())
        {
            await Request.Body.CopyToAsync(memoryStream, cancellationToken);
            fileContent = memoryStream.ToArray();
        }

        if (fileContent.Length == 0)
        {
            _logger.LogWarning("Empty file content received for document {DocumentId}", documentId);
            return BadRequest(new { error = "Empty file content" });
        }

        _logger.LogInformation("Received {Size} bytes to save for document {DocumentId}", fileContent.Length, documentId);

        // For OneDrive documents, upload back to Microsoft Graph
        if (document.SourceType == DocumentSourceType.OneDrive && !string.IsNullOrWhiteSpace(document.ExternalFileId))
        {
            try
            {
                var success = await UploadToOneDriveAsync(document, tokenInfo.OrgId, fileContent, cancellationToken);
                
                if (success)
                {
                    // Update document metadata
                    document.ModifiedAt = DateTime.UtcNow;
                    document.FileSizeBytes = fileContent.Length;
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Successfully saved document {DocumentId} to OneDrive", documentId);
                    
                    // Return success response with new version info
                    return Ok(new
                    {
                        Name = document.Title,
                        Version = document.ModifiedAt.Ticks.ToString()
                    });
                }
                else
                {
                    _logger.LogError("Failed to upload document {DocumentId} to OneDrive", documentId);
                    return StatusCode(500, new { error = "Failed to save file to OneDrive" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document {DocumentId} to OneDrive", documentId);
                return StatusCode(500, new { error = "Failed to save file" });
            }
        }

        // For other document types, return not supported
        _logger.LogWarning("PutFile not supported for document type {SourceType}", document.SourceType);
        return StatusCode(501, new { error = "Editing not supported for this document type" });
    }

    /// <summary>
    /// Lock - Locks a file for editing
    /// </summary>
    [HttpPost("lock")]
    public async Task<IActionResult> Lock(string fileId, [FromQuery(Name = "access_token")] string accessToken, [FromHeader(Name = "X-WOPI-Lock")] string lockId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("WOPI Lock called for file {FileId} with lock {LockId}", fileId, lockId);

        if (!Guid.TryParse(fileId, out var documentId))
        {
            return BadRequest(new { error = "Invalid file ID" });
        }

        // Validate access token
        var tokenInfo = await _wopiTokenService.ValidateAccessTokenAsync(accessToken, cancellationToken);
        if (tokenInfo == null || tokenInfo.DocumentId != documentId)
        {
            return Unauthorized(new { error = "Invalid access token" });
        }

        // For simplicity, we'll allow all locks (no conflict detection)
        // In production, you'd store lock info and check for conflicts
        _logger.LogInformation("Lock granted for document {DocumentId}", documentId);
        return Ok();
    }

    /// <summary>
    /// Unlock - Unlocks a file
    /// </summary>
    [HttpPost("unlock")]
    public async Task<IActionResult> Unlock(string fileId, [FromQuery(Name = "access_token")] string accessToken, [FromHeader(Name = "X-WOPI-Lock")] string lockId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("WOPI Unlock called for file {FileId} with lock {LockId}", fileId, lockId);

        if (!Guid.TryParse(fileId, out var documentId))
        {
            return BadRequest(new { error = "Invalid file ID" });
        }

        // Validate access token
        var tokenInfo = await _wopiTokenService.ValidateAccessTokenAsync(accessToken, cancellationToken);
        if (tokenInfo == null || tokenInfo.DocumentId != documentId)
        {
            return Unauthorized(new { error = "Invalid access token" });
        }

        // Release the lock
        _logger.LogInformation("Lock released for document {DocumentId}", documentId);
        return Ok();
    }

    /// <summary>
    /// RefreshLock - Extends the lock duration
    /// </summary>
    [HttpPost("refreshlock")]
    public async Task<IActionResult> RefreshLock(string fileId, [FromQuery(Name = "access_token")] string accessToken, [FromHeader(Name = "X-WOPI-Lock")] string lockId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("WOPI RefreshLock called for file {FileId} with lock {LockId}", fileId, lockId);

        if (!Guid.TryParse(fileId, out var documentId))
        {
            return BadRequest(new { error = "Invalid file ID" });
        }

        // Validate access token
        var tokenInfo = await _wopiTokenService.ValidateAccessTokenAsync(accessToken, cancellationToken);
        if (tokenInfo == null || tokenInfo.DocumentId != documentId)
        {
            return Unauthorized(new { error = "Invalid access token" });
        }

        // Refresh the lock
        _logger.LogInformation("Lock refreshed for document {DocumentId}", documentId);
        return Ok();
    }

    /// <summary>
    /// GetLock - Gets the current lock status
    /// </summary>
    [HttpPost("getlock")]
    public async Task<IActionResult> GetLock(string fileId, [FromQuery(Name = "access_token")] string accessToken, CancellationToken cancellationToken)
    {
        _logger.LogInformation("WOPI GetLock called for file {FileId}", fileId);

        if (!Guid.TryParse(fileId, out var documentId))
        {
            return BadRequest(new { error = "Invalid file ID" });
        }

        // Validate access token
        var tokenInfo = await _wopiTokenService.ValidateAccessTokenAsync(accessToken, cancellationToken);
        if (tokenInfo == null || tokenInfo.DocumentId != documentId)
        {
            return Unauthorized(new { error = "Invalid access token" });
        }

        // Return no lock (unlocked)
        Response.Headers["X-WOPI-Lock"] = string.Empty;
        return Ok();
    }

    private async Task<byte[]?> DownloadOneDriveFileAsync(Document document, Guid orgId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.ExternalFileId))
            return null;

        // Get the Microsoft connection
        var connection = await _dbContext.ExternalConnections
            .AsNoTracking()
            .Where(ec => ec.OrgId == orgId && ec.Provider == ExternalConnectionProvider.Microsoft)
            .OrderByDescending(ec => ec.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection == null || connection.TokenExpiry <= DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogWarning("No valid Microsoft connection found for org {OrgId}", orgId);
            return null;
        }

        var accessToken = DecryptToken(connection.AccessToken);

        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Get download URL from Microsoft Graph
        var driveId = document.Metadata?.GetValueOrDefault("driveId");
        var graphUrl = !string.IsNullOrWhiteSpace(driveId)
            ? $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{document.ExternalFileId}/content"
            : $"https://graph.microsoft.com/v1.0/me/drive/items/{document.ExternalFileId}/content";

        _logger.LogInformation("Downloading file from Microsoft Graph: {Url}", graphUrl);
        var response = await httpClient.GetAsync(graphUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to download file from Microsoft Graph: {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<long> GetFileSizeAsync(Document document, Guid orgId, CancellationToken cancellationToken)
    {
        // Return stored file size if available
        if (document.FileSizeBytes > 0)
            return document.FileSizeBytes;

        // For OneDrive files, fetch from Microsoft Graph
        if (document.SourceType == DocumentSourceType.OneDrive && !string.IsNullOrWhiteSpace(document.ExternalFileId))
        {
            try
            {
                var connection = await _dbContext.ExternalConnections
                    .AsNoTracking()
                    .Where(ec => ec.OrgId == orgId && ec.Provider == ExternalConnectionProvider.Microsoft)
                    .OrderByDescending(ec => ec.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (connection != null && connection.TokenExpiry > DateTime.UtcNow.AddMinutes(5))
                {
                    var accessToken = DecryptToken(connection.AccessToken);

                    using var httpClient = _httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var driveId = document.Metadata?.GetValueOrDefault("driveId");
                    var graphUrl = !string.IsNullOrWhiteSpace(driveId)
                        ? $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{document.ExternalFileId}?$select=size"
                        : $"https://graph.microsoft.com/v1.0/me/drive/items/{document.ExternalFileId}?$select=size";

                    var response = await httpClient.GetAsync(graphUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        using var jsonDoc = JsonDocument.Parse(content);
                        if (jsonDoc.RootElement.TryGetProperty("size", out var sizeProperty))
                        {
                            return sizeProperty.GetInt64();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching file size from Microsoft Graph for document {DocumentId}", document.Id);
            }
        }

        // Default to a reasonable size if we can't determine it
        return 1024 * 1024; // 1 MB default
    }

    private async Task<string?> GetDownloadUrlAsync(Document document, Guid orgId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(document.DownloadUrl))
            return document.DownloadUrl;

        // For OneDrive files, fetch fresh download URL
        if (document.SourceType == DocumentSourceType.OneDrive && !string.IsNullOrWhiteSpace(document.ExternalFileId))
        {
            try
            {
                var connection = await _dbContext.ExternalConnections
                    .AsNoTracking()
                    .Where(ec => ec.OrgId == orgId && ec.Provider == ExternalConnectionProvider.Microsoft)
                    .OrderByDescending(ec => ec.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (connection != null && connection.TokenExpiry > DateTime.UtcNow.AddMinutes(5))
                {
                    var accessToken = DecryptToken(connection.AccessToken);

                    using var httpClient = _httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var driveId = document.Metadata?.GetValueOrDefault("driveId");
                    var graphUrl = !string.IsNullOrWhiteSpace(driveId)
                        ? $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{document.ExternalFileId}?$select=@microsoft.graph.downloadUrl"
                        : $"https://graph.microsoft.com/v1.0/me/drive/items/{document.ExternalFileId}?$select=@microsoft.graph.downloadUrl";

                    var response = await httpClient.GetAsync(graphUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        using var jsonDoc = JsonDocument.Parse(content);
                        if (jsonDoc.RootElement.TryGetProperty("@microsoft.graph.downloadUrl", out var urlProperty))
                        {
                            return urlProperty.GetString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching download URL from Microsoft Graph for document {DocumentId}", document.Id);
            }
        }

        return null;
    }

    private string GetBaseUrl()
    {
        var scheme = Request.Scheme;
        var host = Request.Host.ToString();
        return $"{scheme}://{host}";
    }

    private async Task<bool> UploadToOneDriveAsync(Document document, Guid orgId, byte[] fileContent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.ExternalFileId))
            return false;

        // Get the Microsoft connection
        var connection = await _dbContext.ExternalConnections
            .AsNoTracking()
            .Where(ec => ec.OrgId == orgId && ec.Provider == ExternalConnectionProvider.Microsoft)
            .OrderByDescending(ec => ec.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection == null || connection.TokenExpiry <= DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogWarning("No valid Microsoft connection found for org {OrgId}", orgId);
            return false;
        }

        var accessToken = DecryptToken(connection.AccessToken);

        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Upload file to Microsoft Graph
        var driveId = document.Metadata?.GetValueOrDefault("driveId");
        var graphUrl = !string.IsNullOrWhiteSpace(driveId)
            ? $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{document.ExternalFileId}/content"
            : $"https://graph.microsoft.com/v1.0/me/drive/items/{document.ExternalFileId}/content";

        _logger.LogInformation("Uploading {Size} bytes to Microsoft Graph: {Url}", fileContent.Length, graphUrl);

        // For files larger than 4MB, use upload session
        if (fileContent.Length > 4 * 1024 * 1024)
        {
            return await UploadLargeFileToOneDriveAsync(httpClient, graphUrl, fileContent, cancellationToken);
        }

        // For smaller files, use simple upload
        using var content = new ByteArrayContent(fileContent);
        content.Headers.ContentType = new MediaTypeHeaderValue(document.FileType ?? "application/octet-stream");

        var response = await httpClient.PutAsync(graphUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to upload file to Microsoft Graph: {StatusCode} - {Error}", 
                response.StatusCode, errorContent);
            return false;
        }

        _logger.LogInformation("Successfully uploaded file to OneDrive");
        return true;
    }

    private async Task<bool> UploadLargeFileToOneDriveAsync(HttpClient httpClient, string uploadUrl, byte[] fileContent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Using upload session for large file ({Size} bytes)", fileContent.Length);

        // Create upload session
        var sessionUrl = uploadUrl.Replace("/content", "/createUploadSession");
        var sessionResponse = await httpClient.PostAsync(sessionUrl, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"), cancellationToken);

        if (!sessionResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to create upload session: {StatusCode}", sessionResponse.StatusCode);
            return false;
        }

        var sessionContent = await sessionResponse.Content.ReadAsStringAsync(cancellationToken);
        using var sessionDoc = JsonDocument.Parse(sessionContent);
        var uploadSessionUrl = sessionDoc.RootElement.GetProperty("uploadUrl").GetString();

        if (string.IsNullOrEmpty(uploadSessionUrl))
        {
            _logger.LogError("No upload URL in session response");
            return false;
        }

        // Upload in chunks (10MB per chunk)
        const int chunkSize = 10 * 1024 * 1024;
        var totalBytes = fileContent.Length;
        var offset = 0;

        while (offset < totalBytes)
        {
            var currentChunkSize = Math.Min(chunkSize, totalBytes - offset);
            var chunk = new byte[currentChunkSize];
            Array.Copy(fileContent, offset, chunk, 0, currentChunkSize);

            using var chunkContent = new ByteArrayContent(chunk);
            chunkContent.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + currentChunkSize - 1, totalBytes);

            var chunkResponse = await httpClient.PutAsync(uploadSessionUrl, chunkContent, cancellationToken);

            if (!chunkResponse.IsSuccessStatusCode && chunkResponse.StatusCode != System.Net.HttpStatusCode.Created)
            {
                _logger.LogError("Failed to upload chunk at offset {Offset}: {StatusCode}", 
                    offset, chunkResponse.StatusCode);
                return false;
            }

            offset += currentChunkSize;
            _logger.LogInformation("Uploaded {Offset}/{Total} bytes", offset, totalBytes);
        }

        _logger.LogInformation("Successfully uploaded large file to OneDrive");
        return true;
    }

    private string DecryptToken(string encryptedToken)
    {
        // Reuse the decryption logic from DocumentsApiController
        // For now, assume tokens are stored in plain text (not ideal for production)
        // In production, implement proper encryption/decryption
        return encryptedToken;
    }
}

