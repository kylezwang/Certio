using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
    private readonly SecureDocumentExtractionOptions _options;
    private readonly DocumentIntelligenceClient? _documentIntelligenceClient;
    private readonly ILogger<DocumentContentService> _logger;

    public DocumentContentService(
        ApplicationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        Func<string, string> decryptToken,
        IOptions<SecureDocumentExtractionOptions> options,
        DocumentIntelligenceClient? documentIntelligenceClient,
        ILogger<DocumentContentService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _decryptToken = decryptToken;
        _options = options.Value;
        _documentIntelligenceClient = documentIntelligenceClient;
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
                DocumentSourceType.InternalUpload => DocumentContentResult.Empty("internal_upload_not_supported"),
                _ => DocumentContentResult.Empty("unsupported_source")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract content for document {DocumentId}", document.Id);
            return DocumentContentResult.Empty("extraction_error");
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
            var (data, contentType, downloadMetadata) = await DownloadGoogleDriveAsync(document, connection, cancellationToken).ConfigureAwait(false);
            return await ExtractContentAsync(data, contentType, "google", downloadMetadata, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Google Drive content download failed for document {DocumentId}: {Message}", document.Id, ex.Message);
            return DocumentContentResult.Empty(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error downloading Google Drive content for document {DocumentId}", document.Id);
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

        var accessToken = _decryptToken(connection.AccessToken);
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var driveId = GetMetadataValue(document, "driveId");
        var itemUrl = !string.IsNullOrWhiteSpace(driveId)
            ? $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{document.ExternalFileId}/content"
            : $"https://graph.microsoft.com/v1.0/me/drive/items/{document.ExternalFileId}/content";

        try
        {
            var response = await httpClient.GetAsync(itemUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("OneDrive download failed for document {DocumentId}: {StatusCode} - {Error}", document.Id, response.StatusCode, errorContent);
                return DocumentContentResult.Empty($"onedrive_download_failed_{(int)response.StatusCode}");
            }

            var effectiveMime = response.Content.Headers.ContentType?.MediaType ?? document.FileType;
            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var baseMetadata = new Dictionary<string, string?>
            {
                ["provider"] = "microsoft",
                ["contentType"] = effectiveMime,
                ["externalFileId"] = document.ExternalFileId,
                ["driveId"] = driveId
            };

            return await ExtractContentAsync(data, effectiveMime, "microsoft", baseMetadata, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error downloading OneDrive content for document {DocumentId}", document.Id);
            return DocumentContentResult.Empty("onedrive_download_exception");
        }
    }

    private async Task<(byte[] Data, string ContentType, Dictionary<string, string?> Metadata)> DownloadGoogleDriveAsync(Document document, ExternalConnection connection, CancellationToken cancellationToken)
    {
        var accessToken = _decryptToken(connection.AccessToken);
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var mimeType = GetMetadataValue(document, "mimeType") ?? document.FileType;
        var metadata = new Dictionary<string, string?>
        {
            ["provider"] = "google",
            ["mimeType"] = mimeType,
            ["externalFileId"] = document.ExternalFileId
        };

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

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Google Drive download failed for document {DocumentId}: {StatusCode} - {Error}", document.Id, response.StatusCode, errorContent);
            throw new InvalidOperationException($"google_download_failed_{(int)response.StatusCode}");
        }

        var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        metadata["contentType"] = effectiveMime;

        return (data, effectiveMime ?? "application/octet-stream", metadata);
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

        var limitMb = Math.Max(_options.MaxDocumentSizeMb, 0);
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

        // Azure Document Intelligence not configured - return empty result
        if (_documentIntelligenceClient == null)
        {
            _logger.LogDebug("Azure Document Intelligence not configured. Skipping extraction for {ContentType}", contentType);
            baseMetadata["analysisProvider"] = "not_configured";
            return DocumentContentResult.Empty("azure_document_intelligence_not_configured");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.OperationTimeout > TimeSpan.Zero)
        {
            linkedCts.CancelAfter(_options.OperationTimeout);
        }

        try
        {
            var analyzeOperation = await _documentIntelligenceClient.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                _options.ModelId,
                BinaryData.FromBytes(data),
                linkedCts.Token).ConfigureAwait(false);

            var result = analyzeOperation.Value;
            var extractedText = result.Content ?? string.Empty;
            var sanitized = EnforceLimit(extractedText, out var isPartial);

            baseMetadata["partial"] = isPartial ? "true" : "false";
            baseMetadata["analysisProvider"] = "azure-document-intelligence";
            baseMetadata["analysisModelId"] = _options.ModelId;
            baseMetadata["pageCount"] = result.Pages.Count.ToString();

            // Calculate average confidence from word-level confidences
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

            return new DocumentContentResult(sanitized, isPartial, baseMetadata);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Document intelligence extraction timed out for provider {Provider}", provider);
            return DocumentContentResult.Empty("analysis_timeout");
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Document intelligence request failed with status {Status}", ex.Status);
            return DocumentContentResult.Empty("analysis_failed");
        }
    }

    private bool IsContentTypeAllowed(string contentType)
    {
        var allowed = _options.AllowedContentTypes ?? Array.Empty<string>();

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

        if (text.Length <= _options.MaxCharacters)
        {
            isPartial = false;
            return text;
        }

        isPartial = true;
        return text[.._options.MaxCharacters];
    }
}

