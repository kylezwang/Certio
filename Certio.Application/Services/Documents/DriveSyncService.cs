using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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

public sealed class DriveSyncService : IDriveSyncService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DriveSyncService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Func<string, string> _decryptToken;
    private readonly IDocumentIndexerService _documentIndexerService;

    public DriveSyncService(
        ApplicationDbContext dbContext,
        ILogger<DriveSyncService> logger,
        IHttpClientFactory httpClientFactory,
        Func<string, string> decryptToken,
        IDocumentIndexerService documentIndexerService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _decryptToken = decryptToken;
        _documentIndexerService = documentIndexerService;
    }

    public async Task SyncGoogleDriveAsync(Guid orgId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Google Drive sync for Org {OrgId} User {UserId}", orgId, userId);

        var connection = await _dbContext.ExternalConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(ec => ec.OrgId == orgId && ec.UserId == userId && ec.Provider == ExternalConnectionProvider.Google, cancellationToken);

        if (connection == null)
        {
            _logger.LogWarning("No Google Drive connection found for Org {OrgId} User {UserId}", orgId, userId);
            return;
        }

        // Check if token needs refresh
        if (connection.TokenExpiry <= DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogInformation("Google Drive token expired, refresh not implemented yet. Skipping sync.");
            return;
        }

        var accessToken = _decryptToken(connection.AccessToken);
        var syncedCount = 0;

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Fetch files from Google Drive using Files API v3
            // Only fetch files (not folders) and exclude trashed files
            var nextPageToken = "";
            
            do
            {
                var baseUrl = "https://www.googleapis.com/drive/v3/files";
                var queryParams = new List<string>
                {
                    "pageSize=200",
                    "fields=nextPageToken,files(id,name,mimeType,size,createdTime,modifiedTime,webViewLink,webContentLink,owners,parents,shared)"
                };

                // Only get files (exclude folders) and exclude trashed files
                var query = "mimeType!='application/vnd.google-apps.folder' and trashed=false";
                queryParams.Add($"q={Uri.EscapeDataString(query)}");

                // Add page token if we have one
                if (!string.IsNullOrEmpty(nextPageToken))
                {
                    queryParams.Add($"pageToken={Uri.EscapeDataString(nextPageToken)}");
                }

                var url = $"{baseUrl}?{string.Join("&", queryParams)}";

                var response = await httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to fetch Google Drive files: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    break;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("files", out var filesArray))
                {
                    foreach (var file in filesArray.EnumerateArray())
                    {
                        try
                        {
                            var metadata = await ExtractGoogleDriveMetadataAsync(file, orgId, userId, httpClient, cancellationToken);
                            await UpsertMetadataAsync(metadata, cancellationToken);
                            syncedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to process Google Drive file: {FileId}", file.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown");
                            // Continue processing other files
                        }
                    }
                }

                // Get next page token
                nextPageToken = root.TryGetProperty("nextPageToken", out var tokenProp) ? tokenProp.GetString() : null;
            } while (!string.IsNullOrEmpty(nextPageToken));

            _logger.LogInformation("Google Drive sync completed for Org {OrgId} User {UserId}. Synced {Count} documents.", orgId, userId, syncedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google Drive sync for Org {OrgId} User {UserId}", orgId, userId);
            throw;
        }
    }

    public async Task SyncOneDriveAsync(Guid orgId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OneDrive sync for Org {OrgId} User {UserId}", orgId, userId);

        var connection = await _dbContext.ExternalConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(ec => ec.OrgId == orgId && ec.UserId == userId && ec.Provider == ExternalConnectionProvider.Microsoft, cancellationToken);

        if (connection == null)
        {
            _logger.LogWarning("No OneDrive connection found for Org {OrgId} User {UserId}", orgId, userId);
            return;
        }

        // Check if token needs refresh
        if (connection.TokenExpiry <= DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogInformation("OneDrive token expired, refresh not implemented yet. Skipping sync.");
            return;
        }

        var accessToken = _decryptToken(connection.AccessToken);
        var syncedCount = 0;

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Fetch files from OneDrive root (me/drive/root/children)
            var nextUrl = "https://graph.microsoft.com/v1.0/me/drive/root/children?$top=200&$select=id,name,size,lastModifiedDateTime,createdDateTime,webUrl,file,mimeType,createdBy,parentReference,@microsoft.graph.downloadUrl";

            while (!string.IsNullOrEmpty(nextUrl))
            {
                var response = await httpClient.GetAsync(nextUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to fetch OneDrive files: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    break;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("value", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        // Skip folders for now (only process files)
                        if (item.TryGetProperty("file", out var fileProp) && !fileProp.ValueKind.Equals(JsonValueKind.Null))
                        {
                            try
                            {
                                var metadata = ExtractOneDriveMetadata(item, orgId, userId);
                                await UpsertMetadataAsync(metadata, cancellationToken);
                                syncedCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to process OneDrive file: {FileId}", item.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown");
                                // Continue processing other files
                            }
                        }
                    }
                }

                // Get next page URL if available
                nextUrl = root.TryGetProperty("@odata.nextLink", out var nextLink) ? nextLink.GetString() : null;
            }

            _logger.LogInformation("OneDrive sync completed for Org {OrgId} User {UserId}. Synced {Count} documents.", orgId, userId, syncedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OneDrive sync for Org {OrgId} User {UserId}", orgId, userId);
            throw;
        }
    }

    private async Task<ProviderFileMetadata> ExtractGoogleDriveMetadataAsync(JsonElement item, Guid orgId, Guid userId, HttpClient httpClient, CancellationToken cancellationToken)
    {
        var id = item.GetProperty("id").GetString() ?? throw new InvalidOperationException("Google Drive item missing id");
        var name = item.GetProperty("name").GetString() ?? "Unknown";
        var size = item.TryGetProperty("size", out var sizeProp) && sizeProp.ValueKind == JsonValueKind.String
            ? long.TryParse(sizeProp.GetString(), out var parsedSize) ? parsedSize : 0
            : 0;
        var mimeType = item.TryGetProperty("mimeType", out var mimeProp) ? mimeProp.GetString() ?? "application/octet-stream" : "application/octet-stream";
        
        var modifiedTime = item.TryGetProperty("modifiedTime", out var modProp)
            ? DateTime.Parse(modProp.GetString() ?? DateTime.UtcNow.ToString("O"), null, DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow;

        var createdTime = item.TryGetProperty("createdTime", out var createdProp)
            ? DateTime.Parse(createdProp.GetString() ?? DateTime.UtcNow.ToString("O"), null, DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow;

        var webViewLink = item.TryGetProperty("webViewLink", out var webViewProp) ? webViewProp.GetString() : null;
        var webContentLink = item.TryGetProperty("webContentLink", out var webContentProp) ? webContentProp.GetString() : null;
        var downloadUrl = webContentLink ?? webViewLink;

        var ownerName = "Unknown";
        if (item.TryGetProperty("owners", out var ownersProp) && ownersProp.ValueKind == JsonValueKind.Array)
        {
            var owners = ownersProp.EnumerateArray();
            if (owners.MoveNext())
            {
                var firstOwner = owners.Current;
                if (firstOwner.TryGetProperty("displayName", out var displayNameProp))
                {
                    ownerName = displayNameProp.GetString() ?? "Unknown";
                }
                else if (firstOwner.TryGetProperty("emailAddress", out var emailProp))
                {
                    ownerName = emailProp.GetString() ?? "Unknown";
                }
            }
        }

        var isShared = item.TryGetProperty("shared", out var sharedProp) && sharedProp.GetBoolean();

        var metadata = new Dictionary<string, string?>
        {
            ["provider"] = "google",
            ["driveFileId"] = id,
            ["mimeType"] = mimeType,
            ["ownerName"] = ownerName
        };

        if (item.TryGetProperty("parents", out var parentsProp) && parentsProp.ValueKind == JsonValueKind.Array)
        {
            var parents = parentsProp.EnumerateArray().Select(p => p.GetString()).Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (parents.Any())
            {
                metadata["parentIds"] = string.Join(",", parents);
                // Get the first parent ID to fetch folder name (most files have one parent)
                var firstParentId = parents.First();
                if (!string.IsNullOrEmpty(firstParentId) && firstParentId != "root")
                {
                    // Fetch the folder name from Google Drive API (only log errors, not success)
                    try
                    {
                        var folderUrl = $"https://www.googleapis.com/drive/v3/files/{firstParentId}?fields=name";
                        var folderResponse = await httpClient.GetAsync(folderUrl, cancellationToken);
                        if (folderResponse.IsSuccessStatusCode)
                        {
                            var folderContent = await folderResponse.Content.ReadAsStringAsync(cancellationToken);
                            using var folderDoc = JsonDocument.Parse(folderContent);
                            if (folderDoc.RootElement.TryGetProperty("name", out var folderNameProp))
                            {
                                var folderName = folderNameProp.GetString();
                                if (!string.IsNullOrWhiteSpace(folderName))
                                {
                                    metadata["parentFolderName"] = folderName;
                                }
                            }
                        }
                        else
                        {
                            // Only log if there's an actual error (not 404 for deleted folders)
                            if (folderResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
                            {
                                _logger.LogWarning("Failed to fetch folder name for parent ID {ParentId}: {StatusCode}", 
                                    firstParentId, folderResponse.StatusCode);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Only log actual exceptions, reduce verbosity
                        _logger.LogDebug(ex, "Error fetching folder name for parent ID {ParentId}", firstParentId);
                    }
                }
            }
        }

        return new ProviderFileMetadata(
            OrgId: orgId,
            UserId: userId,
            SourceType: DocumentSourceType.GoogleDrive,
            ExternalFileId: id,
            Title: name,
            FileType: mimeType,
            FileSizeBytes: size,
            Status: DocumentStatus.Draft,
            Category: "General",
            MatterId: null,
            IsPrivate: !isShared,
            CreatedAt: createdTime,
            ModifiedAt: modifiedTime,
            PreviewUrl: webViewLink,
            EmbedUrl: webViewLink,
            DownloadUrl: downloadUrl,
            Metadata: metadata,
            Tags: new List<string> { "Google Drive" }
        );
    }

    private ProviderFileMetadata ExtractOneDriveMetadata(JsonElement item, Guid orgId, Guid userId)
    {
        var id = item.GetProperty("id").GetString() ?? throw new InvalidOperationException("OneDrive item missing id");
        var name = item.GetProperty("name").GetString() ?? "Unknown";
        var size = item.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
        var mimeType = item.TryGetProperty("file", out var fileProp) && fileProp.TryGetProperty("mimeType", out var mimeProp)
            ? mimeProp.GetString() ?? "application/octet-stream"
            : "application/octet-stream";
        
        var lastModified = item.TryGetProperty("lastModifiedDateTime", out var modProp)
            ? DateTime.Parse(modProp.GetString() ?? DateTime.UtcNow.ToString("O"), null, DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow;

        var created = item.TryGetProperty("createdDateTime", out var createdProp)
            ? DateTime.Parse(createdProp.GetString() ?? DateTime.UtcNow.ToString("O"), null, DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow;

        var webUrl = item.TryGetProperty("webUrl", out var webUrlProp) ? webUrlProp.GetString() : null;
        var downloadUrl = item.TryGetProperty("@microsoft.graph.downloadUrl", out var downloadProp) ? downloadProp.GetString() : null;

        var ownerName = "Unknown";
        if (item.TryGetProperty("createdBy", out var createdByProp) && 
            createdByProp.TryGetProperty("user", out var userProp) &&
            userProp.TryGetProperty("displayName", out var displayNameProp))
        {
            ownerName = displayNameProp.GetString() ?? "Unknown";
        }

        var metadata = new Dictionary<string, string?>
        {
            ["provider"] = "microsoft",
            ["oneDriveId"] = id,
            ["mimeType"] = mimeType,
            ["ownerName"] = ownerName
        };

        if (item.TryGetProperty("parentReference", out var parentRefProp))
        {
            if (parentRefProp.TryGetProperty("path", out var pathProp))
            {
                metadata["parentPath"] = pathProp.GetString();
            }

            // Capture driveId if available so we can later fetch items using /drives/{driveId}/items/{itemId}
            if (parentRefProp.TryGetProperty("driveId", out var driveIdProp))
            {
                var driveId = driveIdProp.GetString();
                if (!string.IsNullOrWhiteSpace(driveId))
                {
                    metadata["driveId"] = driveId;
                }
            }

            // Capture siteId if available (SharePoint-hosted)
            if (parentRefProp.TryGetProperty("siteId", out var siteIdProp))
            {
                var siteId = siteIdProp.GetString();
                if (!string.IsNullOrWhiteSpace(siteId))
                {
                    metadata["siteId"] = siteId;
                }
            }
        }

        return new ProviderFileMetadata(
            OrgId: orgId,
            UserId: userId,
            SourceType: DocumentSourceType.OneDrive,
            ExternalFileId: id,
            Title: name,
            FileType: mimeType,
            FileSizeBytes: size,
            Status: DocumentStatus.Draft,
            Category: "General",
            MatterId: null,
            IsPrivate: false,
            CreatedAt: created,
            ModifiedAt: lastModified,
            PreviewUrl: webUrl,
            EmbedUrl: webUrl,
            DownloadUrl: downloadUrl ?? webUrl,
            Metadata: metadata,
            Tags: new List<string> { "OneDrive" }
        );
    }

    public async Task<Guid> UpsertMetadataAsync(ProviderFileMetadata metadata, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
    {
        var document = await _dbContext.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.OrgId == metadata.OrgId
                                      && d.SourceType == metadata.SourceType
                                      && d.ExternalFileId == metadata.ExternalFileId,
                cancellationToken);

        if (document == null)
        {
            document = new Document
            {
                OrgId = metadata.OrgId,
                MatterId = metadata.MatterId,
                SourceType = metadata.SourceType,
                ExternalFileId = metadata.ExternalFileId,
                Title = metadata.Title,
                FileType = metadata.FileType,
                FileSizeBytes = metadata.FileSizeBytes,
                Status = metadata.Status,
                Category = metadata.Category,
                OwnerUserId = metadata.UserId,
                CreatedAt = metadata.CreatedAt,
                ModifiedAt = metadata.ModifiedAt,
                IsPrivate = metadata.IsPrivate,
                PreviewUrl = metadata.PreviewUrl,
                EmbedUrl = metadata.EmbedUrl,
                DownloadUrl = metadata.DownloadUrl,
                Tags = metadata.Tags.ToList(),
                Metadata = metadata.Metadata.ToDictionary(k => k.Key, v => v.Value)
            };

            _dbContext.Documents.Add(document);
        }
        else
        {
            document.Title = metadata.Title;
            document.FileType = metadata.FileType;
            document.FileSizeBytes = metadata.FileSizeBytes;
            document.Status = metadata.Status;
            document.Category = metadata.Category;
            document.MatterId = metadata.MatterId;
            document.OwnerUserId = metadata.UserId;
            document.IsPrivate = metadata.IsPrivate;
            document.PreviewUrl = metadata.PreviewUrl;
            document.EmbedUrl = metadata.EmbedUrl;
            document.DownloadUrl = metadata.DownloadUrl;
            document.ModifiedAt = metadata.ModifiedAt;
            document.Tags = metadata.Tags.ToList();
                    
                    // Merge metadata instead of replacing - preserve folder names and other existing metadata
                    var existingMetadata = document.Metadata ?? new Dictionary<string, string?>();
                    foreach (var kvp in metadata.Metadata)
                    {
                        existingMetadata[kvp.Key] = kvp.Value;
                    }
                    document.Metadata = existingMetadata;
        }

        var latestVersionNumber = document.Versions.Count == 0 ? 0 : document.Versions.Max(v => v.VersionNumber);
        var shouldAddVersion = !document.Versions.Any() || metadata.ModifiedAt > document.Versions.Max(v => v.ModifiedAt);

        if (shouldAddVersion)
        {
            document.ModifiedAt = metadata.ModifiedAt;
            var version = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = latestVersionNumber + 1,
                ModifiedAt = metadata.ModifiedAt,
                ModifiedByUserId = metadata.UserId,
                ExternalRevisionId = metadata.Metadata.TryGetValue("revisionId", out var revisionId) ? revisionId : null,
                Checksum = metadata.Metadata.TryGetValue("checksum", out var checksum) ? checksum : document.Checksum,
                StorageUrl = metadata.DownloadUrl ?? metadata.PreviewUrl ?? metadata.EmbedUrl ?? string.Empty
            };

            document.Versions.Add(version);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Only log at debug level to reduce spam during sync
        _logger.LogDebug("Upserted metadata for document {DocumentId} ({Title})", document.Id, document.Title);

        // Auto-queue document for RAG indexing (fire and forget to not slow down sync)
        _ = Task.Run(async () =>
        {
            try
            {
                var latestVersion = document.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
                await _documentIndexerService.QueueEmbeddingAsync(document.Id, latestVersion?.Id, CancellationToken.None);
                _logger.LogInformation("Auto-queued document {DocumentId} for RAG indexing", document.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-queue document {DocumentId} for indexing", document.Id);
            }
        }, cancellationToken);

        return document.Id;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    // Only log at warning level if all retries failed, but don't throw to allow sync to continue
                    _logger.LogWarning("Failed to upsert metadata after {Retries} retries for file {FileId}: {Error}", 
                        maxRetries, metadata.ExternalFileId, ex.Message);
                    return Guid.Empty; // Return empty instead of throwing to allow sync to continue
                }
                
                // Clear the change tracker and retry (silently, no logging to reduce spam)
                _dbContext.ChangeTracker.Clear();
                await Task.Delay(50 * retryCount, cancellationToken); // Small delay before retry
            }
            catch (Exception ex)
            {
                // Log other exceptions but don't break the sync
                _logger.LogWarning(ex, "Unexpected error upserting metadata for file {FileId}", metadata.ExternalFileId);
                return Guid.Empty;
            }
        }

        // Should never reach here, but just in case
        return Guid.Empty;
    }
}

