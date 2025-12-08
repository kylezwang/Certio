using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.DocumentIntelligence;
using Certio.Application.Configuration;
using Certio.Application.Interfaces;
using Certio.Domain.Documents;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Certio.Application.Services.Documents;

public sealed class DocumentContentService : IDocumentContentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Func<string, string> _decryptToken;
    private readonly Func<string, string> _encryptToken;
    private readonly SecureDocumentExtractionOptions _extractionOptions;
    private readonly DocumentIntegrationOptions _integrationOptions;
    private readonly DocumentIntelligenceClient? _documentIntelligenceClient;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<DocumentContentService> _logger;
    
    // Rate limiting for Azure Document Intelligence
    private static readonly SemaphoreSlim _extractionSemaphore = new(2, 2); // Max 2 concurrent extractions
    private static DateTime _rateLimitedUntil = DateTime.MinValue;
    private static int _consecutiveTimeouts = 0;
    private static int _consecutiveErrors = 0;
    private const int MaxConsecutiveErrors = 5; // Stop logging warnings after this many consecutive errors

    public DocumentContentService(
        ApplicationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        Func<string, string> decryptToken,
        Func<string, string> encryptToken,
        IOptions<SecureDocumentExtractionOptions> extractionOptions,
        IOptions<DocumentIntegrationOptions> integrationOptions,
        DocumentIntelligenceClient? documentIntelligenceClient,
        IFileStorageService fileStorageService,
        ILogger<DocumentContentService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _decryptToken = decryptToken;
        _encryptToken = encryptToken;
        _extractionOptions = extractionOptions.Value;
        _integrationOptions = integrationOptions.Value;
        _documentIntelligenceClient = documentIntelligenceClient;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<DocumentContentResult> FetchContentAsync(Document document, Guid? versionId, CancellationToken cancellationToken)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        try
        {
            return document.SourceType switch
            {
                DocumentSourceType.GoogleDrive => await FetchFromGoogleDriveAsync(document, cancellationToken).ConfigureAwait(false),
                DocumentSourceType.OneDrive => await FetchFromOneDriveAsync(document, cancellationToken).ConfigureAwait(false),
                DocumentSourceType.InternalUpload => await FetchFromInternalUploadAsync(document, versionId, cancellationToken).ConfigureAwait(false),
                _ => DocumentContentResult.Empty("unsupported_source")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract content for document {DocumentId}", document.Id);
            return DocumentContentResult.Empty("extraction_error");
        }
    }

    private async Task<DocumentContentResult> FetchFromInternalUploadAsync(Document document, Guid? versionId, CancellationToken cancellationToken)
    {
        try
        {
            // Get the version to find the storage path
            DocumentVersion? version;
            if (versionId.HasValue)
            {
                version = await _dbContext.DocumentVersions
                    .FirstOrDefaultAsync(v => v.Id == versionId.Value && v.DocumentId == document.Id, cancellationToken);
            }
            else
            {
                version = await _dbContext.DocumentVersions
                    .Where(v => v.DocumentId == document.Id)
                    .OrderByDescending(v => v.VersionNumber)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (version == null || string.IsNullOrWhiteSpace(version.StorageUrl))
            {
                _logger.LogWarning("No file storage path found for internal upload document {DocumentId}", document.Id);
                return DocumentContentResult.Empty("no_storage_path");
            }

            // Check if file exists
            var fileExists = await _fileStorageService.FileExistsAsync(version.StorageUrl, cancellationToken);
            if (!fileExists)
            {
                _logger.LogWarning("File not found at storage path {Path} for document {DocumentId}", version.StorageUrl, document.Id);
                return DocumentContentResult.Empty("file_not_found");
            }

            // Retrieve file content
            var fileData = await _fileStorageService.RetrieveFileAsync(version.StorageUrl, cancellationToken);
            
            _logger.LogInformation("Retrieved {Size} bytes for internal upload document {DocumentId}", fileData.Length, document.Id);

            // Process with Azure Document Intelligence
            var metadata = new Dictionary<string, string?>
            {
                { "source", "internal_upload" },
                { "contentType", document.FileType }
            };
            return await ExtractContentAsync(fileData, document.FileType ?? "application/octet-stream", "internal_upload", metadata, cancellationToken);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "File not found for internal upload document {DocumentId}", document.Id);
            return DocumentContentResult.Empty("file_not_found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch content from internal upload for document {DocumentId}", document.Id);
            return DocumentContentResult.Empty("fetch_error");
        }
    }

    private async Task<DocumentContentResult> FetchFromGoogleDriveAsync(Document document, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.ExternalFileId))
        {
            return DocumentContentResult.Empty("missing_external_id");
        }

        var connection = await ResolveConnectionAsync(document.OrgId, document.OwnerUserId, ExternalConnectionProvider.Google, cancellationToken).ConfigureAwait(false);
        if (connection == null)
        {
            _logger.LogWarning("No Google Drive connection available for document {DocumentId} in org {OrgId}", document.Id, document.OrgId);
            return DocumentContentResult.Empty("missing_google_connection");
        }

        try
        {
            var (data, contentType, metadata) = await DownloadGoogleDriveAsync(document, connection, cancellationToken).ConfigureAwait(false);
            return await ExtractContentAsync(data, contentType, "google", metadata, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Google Drive content download failed for document {DocumentId}: {Message}", document.Id, ex.Message);
            return DocumentContentResult.Empty(ex.Message);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogDebug("Google Drive download timed out for document {DocumentId} - file may be too large", document.Id);
            return DocumentContentResult.Empty("google_download_timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Network error downloading Google Drive content for document {DocumentId}", document.Id);
            return DocumentContentResult.Empty("google_network_error");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error downloading Google Drive content for document {DocumentId}", document.Id);
            return DocumentContentResult.Empty("google_download_exception");
        }
    }

    private async Task<DocumentContentResult> FetchFromOneDriveAsync(Document document, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.ExternalFileId))
        {
            return DocumentContentResult.Empty("missing_external_id");
        }

        var connection = await ResolveConnectionAsync(document.OrgId, document.OwnerUserId, ExternalConnectionProvider.Microsoft, cancellationToken).ConfigureAwait(false);
        if (connection == null)
        {
            _logger.LogWarning("No OneDrive connection available for document {DocumentId} in org {OrgId}", document.Id, document.OrgId);
            return DocumentContentResult.Empty("missing_onedrive_connection");
        }

        try
        {
            var (data, contentType, metadata) = await DownloadOneDriveAsync(document, connection, cancellationToken).ConfigureAwait(false);
            return await ExtractContentAsync(data, contentType, "microsoft", metadata, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("OneDrive content download failed for document {DocumentId}: {Message}", document.Id, ex.Message);
            return DocumentContentResult.Empty(ex.Message);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogDebug("OneDrive download timed out for document {DocumentId} - file may be too large", document.Id);
            return DocumentContentResult.Empty("onedrive_download_timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Network error downloading OneDrive content for document {DocumentId}", document.Id);
            return DocumentContentResult.Empty("onedrive_network_error");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error downloading OneDrive content for document {DocumentId}", document.Id);
            return DocumentContentResult.Empty("onedrive_download_exception");
        }
    }

    private async Task<(byte[] Data, string ContentType, Dictionary<string, string?> Metadata)> DownloadGoogleDriveAsync(Document document, ExternalConnection connection, CancellationToken cancellationToken)
    {
        var mimeType = GetMetadataValue(document, "mimeType") ?? document.FileType;
        var metadata = new Dictionary<string, string?>
        {
            ["provider"] = "google",
            ["mimeType"] = mimeType,
            ["externalFileId"] = document.ExternalFileId
        };

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var accessToken = await EnsureAccessTokenAsync(connection, cancellationToken, attempt > 0).ConfigureAwait(false);

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            httpClient.Timeout = TimeSpan.FromMinutes(2); // Increased for large files

            HttpResponseMessage response;
            string? effectiveMime;

            if (IsGoogleWorkspaceMime(mimeType))
            {
                var exportMime = ResolveGoogleExportMime(mimeType!);
                metadata["exportMimeType"] = exportMime;
                var exportUrl = $"https://www.googleapis.com/drive/v3/files/{document.ExternalFileId}/export?mimeType={Uri.EscapeDataString(exportMime)}";
                response = await httpClient.GetAsync(exportUrl, cancellationToken).ConfigureAwait(false);
                effectiveMime = exportMime;
            }
            else
            {
                var downloadUrl = $"https://www.googleapis.com/drive/v3/files/{document.ExternalFileId}?alt=media";
                response = await httpClient.GetAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
                effectiveMime = response.Content.Headers.ContentType?.MediaType ?? mimeType;
            }

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                metadata["contentType"] = effectiveMime;
                return (data, effectiveMime ?? "application/octet-stream", metadata);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Google Drive returned unauthorized for document {DocumentId}: {Error}. Attempting token refresh.", document.Id, errorContent);
                continue;
            }

            var failureContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Google Drive download failed for document {DocumentId}: {StatusCode} - {Error}", document.Id, response.StatusCode, failureContent);
            throw new InvalidOperationException($"google_download_failed_{(int)response.StatusCode}");
        }

        throw new InvalidOperationException("google_download_failed_401");
    }

    private async Task<(byte[] Data, string ContentType, Dictionary<string, string?> Metadata)> DownloadOneDriveAsync(Document document, ExternalConnection connection, CancellationToken cancellationToken)
    {
        var driveId = GetMetadataValue(document, "driveId");
        var itemUrl = !string.IsNullOrWhiteSpace(driveId)
            ? $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{document.ExternalFileId}/content"
            : $"https://graph.microsoft.com/v1.0/me/drive/items/{document.ExternalFileId}/content";

        var metadata = new Dictionary<string, string?>
        {
            ["provider"] = "microsoft",
            ["externalFileId"] = document.ExternalFileId,
            ["driveId"] = driveId
        };

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var accessToken = await EnsureAccessTokenAsync(connection, cancellationToken, attempt > 0).ConfigureAwait(false);

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.Timeout = TimeSpan.FromMinutes(2); // Increased for large files

            var response = await httpClient.GetAsync(itemUrl, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var effectiveMime = response.Content.Headers.ContentType?.MediaType ?? document.FileType;
                var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                metadata["contentType"] = effectiveMime;
                return (data, effectiveMime, metadata);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                _logger.LogDebug("OneDrive returned unauthorized for document {DocumentId}, attempting token refresh", document.Id);
                continue;
            }

            var failureContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("OneDrive download failed for document {DocumentId}: {StatusCode} - {Error}", document.Id, response.StatusCode, failureContent);
            throw new InvalidOperationException($"onedrive_download_failed_{(int)response.StatusCode}");
        }

        throw new InvalidOperationException("onedrive_download_failed_401");
    }

    private async Task<string> EnsureAccessTokenAsync(ExternalConnection connection, CancellationToken cancellationToken, bool forceRefresh = false)
    {
        var safetyWindow = GetSafetyWindow(connection.Provider);
        var needsRefresh = forceRefresh || connection.TokenExpiry <= DateTime.UtcNow.Add(safetyWindow);

        if (!needsRefresh)
        {
            return _decryptToken(connection.AccessToken);
        }

        return await RefreshAccessTokenAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> RefreshAccessTokenAsync(ExternalConnection connection, CancellationToken cancellationToken)
    {
        var trackedConnection = await _dbContext.ExternalConnections
            .FirstOrDefaultAsync(ec => ec.Id == connection.Id, cancellationToken)
            .ConfigureAwait(false);

        if (trackedConnection == null)
        {
            throw new InvalidOperationException("external_connection_not_found");
        }

        var decryptedRefreshToken = _decryptToken(trackedConnection.RefreshToken);

        TokenRefreshResult refreshedTokens;
        try
        {
            refreshedTokens = trackedConnection.Provider switch
            {
                ExternalConnectionProvider.Google => await RefreshGoogleAccessTokenAsync(decryptedRefreshToken, cancellationToken).ConfigureAwait(false),
                ExternalConnectionProvider.Microsoft => await RefreshMicrosoftAccessTokenAsync(decryptedRefreshToken, trackedConnection.Scopes ?? new List<string>(), cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"refresh_not_supported_{trackedConnection.Provider}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token refresh failed for provider {Provider} Org {OrgId} User {UserId}", trackedConnection.Provider, trackedConnection.OrgId, trackedConnection.UserId);
            throw;
        }

        trackedConnection.AccessToken = _encryptToken(refreshedTokens.AccessToken);
        if (!string.IsNullOrWhiteSpace(refreshedTokens.RefreshToken))
        {
            trackedConnection.RefreshToken = _encryptToken(refreshedTokens.RefreshToken);
        }

        var expiresInSeconds = Math.Max(refreshedTokens.ExpiresInSeconds, 60);
        trackedConnection.TokenExpiry = DateTime.UtcNow.AddSeconds(expiresInSeconds);
        trackedConnection.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        connection.AccessToken = trackedConnection.AccessToken;
        connection.RefreshToken = trackedConnection.RefreshToken;
        connection.TokenExpiry = trackedConnection.TokenExpiry;

        _logger.LogInformation("Refreshed access token for provider {Provider} Org {OrgId} User {UserId}", trackedConnection.Provider, trackedConnection.OrgId, trackedConnection.UserId);

        return refreshedTokens.AccessToken;
    }

    private async Task<TokenRefreshResult> RefreshGoogleAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var clientId = _integrationOptions.GoogleDrive.ClientId;
        var clientSecret = _integrationOptions.GoogleDrive.ClientSecret;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("google_refresh_configuration_missing");
        }

        var payload = new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token"
        };

        return await RefreshTokenAsync("https://oauth2.googleapis.com/token", payload, "google", cancellationToken).ConfigureAwait(false);
    }

    private async Task<TokenRefreshResult> RefreshMicrosoftAccessTokenAsync(string refreshToken, IReadOnlyCollection<string> scopes, CancellationToken cancellationToken)
    {
        var clientId = _integrationOptions.OneDrive.ClientId;
        var clientSecret = _integrationOptions.OneDrive.ClientSecret;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("onedrive_refresh_configuration_missing");
        }

        var payload = new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token"
        };

        if (scopes != null && scopes.Count > 0)
        {
            payload["scope"] = string.Join(' ', scopes);
        }

        return await RefreshTokenAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token", payload, "onedrive", cancellationToken).ConfigureAwait(false);
    }

    private async Task<TokenRefreshResult> RefreshTokenAsync(string endpoint, Dictionary<string, string> formValues, string providerKey, CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(formValues)
        };

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("{Provider} token refresh failed with status {StatusCode}: {Content}", providerKey, response.StatusCode, content);
            throw new InvalidOperationException($"{providerKey}_refresh_failed_{(int)response.StatusCode}");
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (!root.TryGetProperty("access_token", out var accessTokenElement))
            {
                throw new InvalidOperationException($"{providerKey}_refresh_missing_access_token");
            }

            var accessToken = accessTokenElement.GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException($"{providerKey}_refresh_missing_access_token");
            }

            string? refreshToken = null;
            if (root.TryGetProperty("refresh_token", out var refreshElement))
            {
                refreshToken = refreshElement.GetString();
            }

            var expiresInSeconds = 3600;
            if (root.TryGetProperty("expires_in", out var expiresElement))
            {
                switch (expiresElement.ValueKind)
                {
                    case JsonValueKind.Number when expiresElement.TryGetInt32(out var numeric) && numeric > 0:
                        expiresInSeconds = numeric;
                        break;
                    case JsonValueKind.String:
                        var expiresString = expiresElement.GetString();
                        if (!string.IsNullOrWhiteSpace(expiresString) && int.TryParse(expiresString, out var parsed) && parsed > 0)
                        {
                            expiresInSeconds = parsed;
                        }

                        break;
                }
            }

            return new TokenRefreshResult(accessToken, refreshToken, expiresInSeconds);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "{Provider} token refresh returned invalid JSON: {Content}", providerKey, content);
            throw new InvalidOperationException($"{providerKey}_refresh_parse_error");
        }
    }

    private async Task<ExternalConnection?> ResolveConnectionAsync(Guid orgId, Guid? ownerUserId, ExternalConnectionProvider provider, CancellationToken cancellationToken)
    {
        var query = _dbContext.ExternalConnections
            .AsNoTracking()
            .Where(ec => ec.OrgId == orgId && ec.Provider == provider)
            .OrderByDescending(ec => ec.CreatedAt);

        if (ownerUserId.HasValue)
        {
            var byOwner = await query.FirstOrDefaultAsync(ec => ec.UserId == ownerUserId.Value, cancellationToken).ConfigureAwait(false);
            if (byOwner != null)
            {
                return byOwner;
            }
        }

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? GetMetadataValue(Document document, string key)
    {
        if (document.Metadata != null && document.Metadata.TryGetValue(key, out var value))
        {
            return value;
        }

        return null;
    }

    private static bool IsGoogleWorkspaceMime(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return false;
        }

        return mimeType.StartsWith("application/vnd.google-apps", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveGoogleExportMime(string mimeType)
    {
        return mimeType switch
        {
            "application/vnd.google-apps.document" => "text/plain",
            "application/vnd.google-apps.presentation" => "application/pdf",
            "application/vnd.google-apps.spreadsheet" => "application/pdf",
            "application/vnd.google-apps.form" => "text/plain",
            _ => "text/plain"
        };
    }

    private async Task<DocumentContentResult> ExtractContentAsync(byte[] data, string contentType, string provider, Dictionary<string, string?> baseMetadata, CancellationToken cancellationToken)
    {
        if (data.Length == 0)
        {
            return DocumentContentResult.Empty("empty_payload");
        }

        if (!IsContentTypeAllowed(contentType))
        {
            _logger.LogDebug("Rejected unsupported content type {ContentType} from provider {Provider}", contentType, provider);
            return DocumentContentResult.Empty("unsupported_content_type");
        }

        var limitMb = Math.Max(_extractionOptions.MaxDocumentSizeMb, 0);
        if (limitMb > 0)
        {
            var maxBytes = (long)limitMb * 1024 * 1024;
            if (data.Length > maxBytes)
            {
                _logger.LogWarning("Document payload exceeds secure extraction limit. Size={Size} Limit={Limit}", data.Length, maxBytes);
                return DocumentContentResult.Empty("file_too_large");
            }
        }

        if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            var text = Encoding.UTF8.GetString(data);
            var sanitized = EnforceLimit(text, out var isPartial);
            baseMetadata["partial"] = isPartial ? "true" : "false";
            baseMetadata["analysisProvider"] = "local-utf8";
            return new DocumentContentResult(sanitized, isPartial, baseMetadata);
        }

        if (_documentIntelligenceClient == null)
        {
            _logger.LogDebug("Azure Document Intelligence not configured. Skipping extraction for {ContentType}", contentType);
            baseMetadata["analysisProvider"] = "not_configured";
            return DocumentContentResult.Empty("azure_document_intelligence_not_configured");
        }

        // Check if we're currently rate limited
        if (DateTime.UtcNow < _rateLimitedUntil)
        {
            var waitTime = (_rateLimitedUntil - DateTime.UtcNow).TotalSeconds;
            _logger.LogDebug("Skipping extraction - rate limited for {Seconds} more seconds", waitTime);
            return DocumentContentResult.Empty("rate_limited");
        }

        // Use semaphore to limit concurrent extractions
        if (!await _extractionSemaphore.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            _logger.LogDebug("Extraction semaphore timeout - too many concurrent extractions");
            return DocumentContentResult.Empty("extraction_queue_full");
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_extractionOptions.OperationTimeout > TimeSpan.Zero)
            {
                linkedCts.CancelAfter(_extractionOptions.OperationTimeout);
            }

            var analyzeOperation = await _documentIntelligenceClient.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                _extractionOptions.ModelId,
                BinaryData.FromBytes(data),
                linkedCts.Token).ConfigureAwait(false);

            var result = analyzeOperation.Value;
            var extractedText = result.Content ?? string.Empty;
            var sanitized = EnforceLimit(extractedText, out var isPartial);

            baseMetadata["partial"] = isPartial ? "true" : "false";
            baseMetadata["analysisProvider"] = "azure-document-intelligence";
            baseMetadata["analysisModelId"] = _extractionOptions.ModelId;
            baseMetadata["pageCount"] = result.Pages.Count.ToString();

            var avgConfidence = result.Pages
                .SelectMany(p => p.Words)
                .Where(w => w.Confidence > 0f)
                .Select(w => w.Confidence)
                .DefaultIfEmpty(0f)
                .Average();

            if (avgConfidence > 0f)
            {
                baseMetadata["confidence"] = avgConfidence.ToString("0.000");
            }

            // Reset error counters on success
            Interlocked.Exchange(ref _consecutiveTimeouts, 0);
            Interlocked.Exchange(ref _consecutiveErrors, 0);

            return new DocumentContentResult(sanitized, isPartial, baseMetadata);
        }
        catch (OperationCanceledException)
        {
            Interlocked.Increment(ref _consecutiveTimeouts);
            // Only log warning for first few timeouts to reduce spam
            if (_consecutiveTimeouts <= MaxConsecutiveErrors)
            {
                _logger.LogWarning("Document intelligence extraction timed out for provider {Provider} (timeout #{Count})", provider, _consecutiveTimeouts);
            }
            else if (_consecutiveTimeouts == MaxConsecutiveErrors + 1)
            {
                _logger.LogWarning("Document intelligence extraction continues to timeout for provider {Provider}. Suppressing further timeout warnings.", provider);
            }
            return DocumentContentResult.Empty("analysis_timeout");
        }
        catch (RequestFailedException ex)
        {
            Interlocked.Increment(ref _consecutiveErrors);
            
            // Handle rate limiting (429) - respect Retry-After header
            if (ex.Status == 429)
            {
                // Parse Retry-After header if available, default to 60 seconds
                var retryAfterSeconds = 60;
                if (ex.GetRawResponse()?.Headers.TryGetValue("Retry-After", out var retryAfterHeader) == true
                    && int.TryParse(retryAfterHeader, out var parsedSeconds))
                {
                    retryAfterSeconds = parsedSeconds;
                }
                
                _rateLimitedUntil = DateTime.UtcNow.AddSeconds(retryAfterSeconds + 5); // Add buffer
                
                if (_consecutiveErrors <= MaxConsecutiveErrors)
                {
                    _logger.LogWarning("Azure Document Intelligence rate limited. Will retry after {Seconds} seconds.", retryAfterSeconds);
                }
                return DocumentContentResult.Empty("rate_limited");
            }
            
            // Only log warning for first few errors to reduce spam
            if (_consecutiveErrors <= MaxConsecutiveErrors)
            {
                _logger.LogWarning(ex, "Document intelligence request failed with status {Status} (error #{Count})", ex.Status, _consecutiveErrors);
            }
            else if (_consecutiveErrors == MaxConsecutiveErrors + 1)
            {
                _logger.LogWarning("Document intelligence continues to fail. Suppressing further error warnings until success.");
            }
            return DocumentContentResult.Empty("analysis_failed");
        }
        finally
        {
            _extractionSemaphore.Release();
        }
    }

    private bool IsContentTypeAllowed(string contentType)
    {
        var allowed = _extractionOptions.AllowedContentTypes ?? Array.Empty<string>();

        if (allowed.Length == 0)
        {
            return true;
        }

        return allowed.Any(candidate => string.Equals(candidate, contentType, StringComparison.OrdinalIgnoreCase));
    }

    private string EnforceLimit(string text, out bool isPartial)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            isPartial = false;
            return string.Empty;
        }

        if (text.Length <= _extractionOptions.MaxCharacters)
        {
            isPartial = false;
            return text;
        }

        isPartial = true;
        return text[.._extractionOptions.MaxCharacters];
    }

    private TimeSpan GetSafetyWindow(ExternalConnectionProvider provider)
    {
        var window = provider switch
        {
            ExternalConnectionProvider.Google => _integrationOptions.GoogleDrive.TokenExpirySafetyWindow,
            ExternalConnectionProvider.Microsoft => _integrationOptions.OneDrive.TokenExpirySafetyWindow,
            _ => TimeSpan.FromMinutes(5)
        };

        return window <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : window;
    }

    private sealed record TokenRefreshResult(string AccessToken, string? RefreshToken, int ExpiresInSeconds);
}

