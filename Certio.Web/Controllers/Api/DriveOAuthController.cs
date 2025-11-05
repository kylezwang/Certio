using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Documents;
using Certio.Infrastructure.Data;
using Certio.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/drive-oauth")]
[Authorize]
public class DriveOAuthController : Controller
{
    private static readonly string[] DefaultGoogleScopes =
    {
        "https://www.googleapis.com/auth/drive.readonly",
        "https://www.googleapis.com/auth/drive.metadata.readonly"
    };

    private static readonly string[] DefaultMicrosoftScopes =
    {
        "offline_access",
        "Files.Read.All",
        "Sites.Read.All"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDocumentAuditService _documentAuditService;
    private readonly IDriveSyncService _driveSyncService;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<DriveOAuthController> _logger;

    public DriveOAuthController(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IDocumentAuditService documentAuditService,
        IDriveSyncService driveSyncService,
        IClientContextAccessor clientContextAccessor,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<DriveOAuthController> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _dataProtectionProvider = dataProtectionProvider;
        _httpClientFactory = httpClientFactory;
        _documentAuditService = documentAuditService;
        _driveSyncService = driveSyncService;
        _clientContextAccessor = clientContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    [HttpGet("google/authorize")]
    public IActionResult AuthorizeGoogle([FromQuery] int? orgId = null, [FromQuery] string? redirectUri = null)
    {
        try
        {
            var documentOrgId = ResolveDocumentOrgId(orgId);
            var authorizationRedirectUri = NormalizeGoogleRedirectUri(redirectUri);

            var clientId = GetRequiredConfigurationValue("DocumentIntegration:GoogleDrive:ClientId", "Google Drive client ID is not configured");
            var scopes = ResolveScopes("DocumentIntegration:GoogleDrive:Scopes", DefaultGoogleScopes);

            var statePayload = BuildStatePayload(documentOrgId, provider: "google", authorizationRedirectUri);
            var protectedState = ProtectState(statePayload);

            var query = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = authorizationRedirectUri,
                ["response_type"] = "code",
                ["scope"] = string.Join(' ', scopes),
                ["access_type"] = "offline",
                ["prompt"] = "consent",
                ["state"] = protectedState
            };

            var authorizationUrl = BuildUrl("https://accounts.google.com/o/oauth2/v2/auth", query);

            _logger.LogInformation("Generated Google Drive authorization URL with redirect_uri: {RedirectUri}", authorizationRedirectUri);

            return Ok(new { success = true, authorizationUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Google Drive authorization URL");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Google Drive OAuth returned error {Error}: {Description}", error, errorDescription);
            return BuildDocumentOAuthView(false, "Google Drive", null, error, errorDescription, null);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("Google Drive OAuth callback missing authorization code");
            return BuildDocumentOAuthView(false, "Google Drive", null, "missing_code", "Authorization code is required.", null);
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            _logger.LogWarning("Google Drive OAuth callback missing state parameter");
            return BuildDocumentOAuthView(false, "Google Drive", null, "missing_state", "State parameter is required.", null);
        }

        OAuthStatePayload statePayload;
        try
        {
            statePayload = UnprotectState(state);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect Google Drive OAuth state");
            return BuildDocumentOAuthView(false, "Google Drive", null, "invalid_state", "We were unable to validate the authorization state.", null);
        }

        try
        {
            var clientId = GetRequiredConfigurationValue("DocumentIntegration:GoogleDrive:ClientId", "Google Drive client ID is not configured");
            var clientSecret = GetRequiredConfigurationValue("DocumentIntegration:GoogleDrive:ClientSecret", "Google Drive client secret is not configured");
            var scopes = ResolveScopes("DocumentIntegration:GoogleDrive:Scopes", DefaultGoogleScopes);
            var documentUserId = GetDocumentUserId();

            var tokenResponse = await ExchangeCodeForTokensAsync(
                tokenEndpoint: "https://oauth2.googleapis.com/token",
                new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = statePayload.RedirectUri,
                    ["grant_type"] = "authorization_code"
                },
                cancellationToken);

            await UpsertExternalConnectionAsync(
                statePayload.OrgId,
                documentUserId,
                ExternalConnectionProvider.Google,
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                tokenResponse.ExpiresIn,
                scopes,
                cancellationToken);

            var currentUserIdInt = GetCurrentUserId(); // Get actual int user ID for audit log
            await _documentAuditService.LogAsync(
                new DocumentAuditEvent(
                    statePayload.OrgId,
                    null,
                    null,
                    null,
                    documentUserId,
                    "ExternalConnectionConnected",
                    "Connected Google Drive for unified document system",
                    DateTime.UtcNow,
                    currentUserIdInt), // Pass actual int user ID
                cancellationToken);

            // Trigger sync in background with proper error handling using a new scope
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000); // Small delay to ensure connection is saved
                    
                    // Create a new scope for the background task to get a fresh DbContext
                    using var scope = _serviceScopeFactory.CreateScope();
                    var backgroundSyncService = scope.ServiceProvider.GetRequiredService<IDriveSyncService>();
                    await backgroundSyncService.SyncGoogleDriveAsync(statePayload.OrgId, documentUserId, CancellationToken.None);
                    
                    _logger.LogInformation("Google Drive sync completed successfully for Org {OrgId} User {UserId}", statePayload.OrgId, documentUserId);
                }
                catch (Exception syncEx)
                {
                    _logger.LogError(syncEx, "Error during Google Drive sync for Org {OrgId} User {UserId}", statePayload.OrgId, documentUserId);
                }
            }, cancellationToken);

            return BuildDocumentOAuthView(true, "Google Drive", null, null, null, statePayload.OrgId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete Google Drive OAuth callback");
            return BuildDocumentOAuthView(false, "Google Drive", null, "internal_error", ex.Message, statePayload.OrgId);
        }
    }

    [HttpGet("onedrive/authorize")]
    public IActionResult AuthorizeOneDrive([FromQuery] int? orgId = null, [FromQuery] string? redirectUri = null)
    {
        try
        {
            var documentOrgId = ResolveDocumentOrgId(orgId);
            var authorizationRedirectUri = NormalizeMicrosoftRedirectUri(redirectUri);

            var clientId = GetRequiredConfigurationValue("DocumentIntegration:OneDrive:ClientId", "Microsoft client ID is not configured");
            var scopes = ResolveScopes("DocumentIntegration:OneDrive:Scopes", DefaultMicrosoftScopes);

            var statePayload = BuildStatePayload(documentOrgId, provider: "microsoft", authorizationRedirectUri);
            var protectedState = ProtectState(statePayload);

            var query = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = authorizationRedirectUri,
                ["response_type"] = "code",
                ["scope"] = string.Join(' ', scopes),
                ["state"] = protectedState,
                ["response_mode"] = "query"
            };

            var authorizationUrl = BuildUrl("https://login.microsoftonline.com/common/oauth2/v2.0/authorize", query);

            _logger.LogInformation("Generated Microsoft OneDrive authorization URL with redirect_uri: {RedirectUri}", authorizationRedirectUri);

            return Ok(new { success = true, authorizationUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Microsoft authorization URL");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("onedrive/callback")]
    public async Task<IActionResult> OneDriveCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Microsoft OAuth returned error {Error}: {Description}", error, errorDescription);
            return BuildDocumentOAuthView(false, "Microsoft OneDrive", null, error, errorDescription, null);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("Microsoft OAuth callback missing authorization code");
            return BuildDocumentOAuthView(false, "Microsoft OneDrive", null, "missing_code", "Authorization code is required.", null);
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            _logger.LogWarning("Microsoft OAuth callback missing state parameter");
            return BuildDocumentOAuthView(false, "Microsoft OneDrive", null, "missing_state", "State parameter is required.", null);
        }

        OAuthStatePayload statePayload;
        try
        {
            statePayload = UnprotectState(state);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect Microsoft OAuth state");
            return BuildDocumentOAuthView(false, "Microsoft OneDrive", null, "invalid_state", "We were unable to validate the authorization state.", null);
        }

        try
        {
            var clientId = GetRequiredConfigurationValue("DocumentIntegration:OneDrive:ClientId", "Microsoft client ID is not configured");
            var clientSecret = GetRequiredConfigurationValue("DocumentIntegration:OneDrive:ClientSecret", "Microsoft client secret is not configured");
            var scopes = ResolveScopes("DocumentIntegration:OneDrive:Scopes", DefaultMicrosoftScopes);
            var documentUserId = GetDocumentUserId();

            var tokenResponse = await ExchangeCodeForTokensAsync(
                tokenEndpoint: "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["code"] = code,
                    ["redirect_uri"] = statePayload.RedirectUri,
                    ["grant_type"] = "authorization_code",
                    ["scope"] = string.Join(' ', scopes)
                },
                cancellationToken);

            await UpsertExternalConnectionAsync(
                statePayload.OrgId,
                documentUserId,
                ExternalConnectionProvider.Microsoft,
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                tokenResponse.ExpiresIn,
                scopes,
                cancellationToken);

            var currentUserIdInt = GetCurrentUserId(); // Get actual int user ID for audit log
            await _documentAuditService.LogAsync(
                new DocumentAuditEvent(
                    statePayload.OrgId,
                    null,
                    null,
                    null,
                    documentUserId,
                    "ExternalConnectionConnected",
                    "Connected Microsoft OneDrive for unified document system",
                    DateTime.UtcNow,
                    currentUserIdInt), // Pass actual int user ID
                cancellationToken);

            // Trigger sync in background with proper error handling using a new scope
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000); // Small delay to ensure connection is saved
                    
                    // Create a new scope for the background task to get a fresh DbContext
                    using var scope = _serviceScopeFactory.CreateScope();
                    var backgroundSyncService = scope.ServiceProvider.GetRequiredService<IDriveSyncService>();
                    await backgroundSyncService.SyncOneDriveAsync(statePayload.OrgId, documentUserId, CancellationToken.None);
                    
                    _logger.LogInformation("OneDrive sync completed successfully for Org {OrgId} User {UserId}", statePayload.OrgId, documentUserId);
                }
                catch (Exception syncEx)
                {
                    _logger.LogError(syncEx, "Error during OneDrive sync for Org {OrgId} User {UserId}", statePayload.OrgId, documentUserId);
                }
            }, cancellationToken);

            return BuildDocumentOAuthView(true, "Microsoft OneDrive", null, null, null, statePayload.OrgId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete Microsoft OAuth callback");
            return BuildDocumentOAuthView(false, "Microsoft OneDrive", null, "internal_error", ex.Message, statePayload.OrgId);
        }
    }

    private Guid ResolveDocumentOrgId(int? providedOrgId)
    {
        int? resolvedOrgId = null;

        // First: Try provided orgId from query string
        if (providedOrgId.HasValue && providedOrgId.Value > 0)
        {
            resolvedOrgId = providedOrgId.Value;
            _logger.LogDebug("Using provided orgId from query string: {OrgId}", resolvedOrgId);
        }
        // Second: Try CurrentOrganizationId from middleware (set from route or query)
        else if (HttpContext.Items.TryGetValue("CurrentOrganizationId", out var value) && value is int organizationId && organizationId > 0)
        {
            resolvedOrgId = organizationId;
            _logger.LogDebug("Derived document OrgId from CurrentOrganizationId {OrgId}", resolvedOrgId);
        }
        // Third: Try ClientContext (set by middleware)
        else if (_clientContextAccessor.ClientContext?.OrganizationId.HasValue == true)
        {
            resolvedOrgId = _clientContextAccessor.ClientContext.OrganizationId;
            _logger.LogDebug("Derived document OrgId from ClientContext {OrgId}", resolvedOrgId);
        }
        // Fourth: Try from query string (middleware should have set it, but double-check)
        else if (Request.Query.TryGetValue("orgId", out var orgIdValue) && int.TryParse(orgIdValue.FirstOrDefault(), out var parsedOrgId) && parsedOrgId > 0)
        {
            resolvedOrgId = parsedOrgId;
            _logger.LogDebug("Derived document OrgId from query string: {OrgId}", resolvedOrgId);
        }

        if (resolvedOrgId.HasValue && resolvedOrgId.Value > 0)
        {
            return CreateDeterministicGuid("certio:organization", resolvedOrgId.Value);
        }

        _logger.LogError("Unable to resolve organization ID. ProvidedOrgId: {ProvidedOrgId}, CurrentOrganizationId in Items: {HasCurrentOrgId}, ClientContext OrgId: {ClientContextOrgId}",
            providedOrgId, HttpContext.Items.ContainsKey("CurrentOrganizationId"), _clientContextAccessor.ClientContext?.OrganizationId);
        throw new InvalidOperationException("Organization ID is required for drive oauth. Please ensure you're accessing this from a client context (e.g., /Client/{orgId}/Documents) or provide orgId as a query parameter.");
    }

    private Guid GetDocumentUserId()
    {
        var currentUserId = GetCurrentUserId();
        return CreateDeterministicGuid("certio:user", currentUserId);
    }

    private static Guid CreateDeterministicGuid(string namespacePrefix, int value)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{namespacePrefix}:{value.ToString(CultureInfo.InvariantCulture)}"));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40); // Version 4
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // Variant RFC 4122
        return new Guid(guidBytes);
    }

    private string NormalizeGoogleRedirectUri(string? provided)
    {
        var configured = _configuration["DocumentIntegration:GoogleDrive:RedirectUri"];
        return NormalizeRedirectUri(provided, configured, "/api/drive-oauth/google/callback");
    }

    private string NormalizeMicrosoftRedirectUri(string? provided)
    {
        var configured = _configuration["DocumentIntegration:OneDrive:RedirectUri"];
        return NormalizeRedirectUri(provided, configured, "/api/drive-oauth/onedrive/callback");
    }

    private string NormalizeRedirectUri(string? provided, string? configured, string fallbackPath)
    {
        var candidate = provided?.Trim();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains("${", StringComparison.Ordinal))
        {
            candidate = configured?.Trim();
        }

        if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains("${", StringComparison.Ordinal))
        {
            var scheme = Request.Scheme;
            var host = Request.Host.HasValue ? Request.Host.Value : "localhost:5092";
            candidate = $"{scheme}://{host}{fallbackPath}";
        }

        // Remove any query parameters or fragments that might have been accidentally included
        if (candidate != null)
        {
            var uriIndex = candidate.IndexOf('?');
            if (uriIndex >= 0)
            {
                candidate = candidate.Substring(0, uriIndex);
            }
            var fragmentIndex = candidate.IndexOf('#');
            if (fragmentIndex >= 0)
            {
                candidate = candidate.Substring(0, fragmentIndex);
            }
            candidate = candidate.Trim();
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            _logger.LogError("Invalid redirect URI candidate: '{Candidate}'", candidate);
            throw new InvalidOperationException($"Invalid redirect URI '{candidate}'");
        }

        var normalizedUri = uri.ToString();
        _logger.LogDebug("Normalized redirect URI: '{RedirectUri}'", normalizedUri);
        return normalizedUri;
    }

    private static string BuildUrl(string baseUrl, IReadOnlyDictionary<string, string> query)
    {
        var builder = new StringBuilder(baseUrl);
        builder.Append('?');
        var first = true;
        foreach (var kvp in query)
        {
            if (!first)
            {
                builder.Append('&');
            }
            builder.Append(Uri.EscapeDataString(kvp.Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(kvp.Value));
            first = false;
        }

        return builder.ToString();
    }

    private OAuthStatePayload BuildStatePayload(Guid orgId, string provider, string redirectUri)
    {
        return new OAuthStatePayload
        {
            OrgId = orgId,
            Provider = provider,
            RedirectUri = redirectUri,
            IssuedAtUtc = DateTime.UtcNow
        };
    }

    private string ProtectState(OAuthStatePayload payload)
    {
        var protector = _dataProtectionProvider.CreateProtector("DriveOAuthState");
        var json = JsonSerializer.Serialize(payload);
        return protector.Protect(json);
    }

    private OAuthStatePayload UnprotectState(string protectedState)
    {
        var protector = _dataProtectionProvider.CreateProtector("DriveOAuthState");
        var json = protector.Unprotect(protectedState);
        var payload = JsonSerializer.Deserialize<OAuthStatePayload>(json);
        if (payload == null || payload.OrgId == Guid.Empty)
        {
            throw new InvalidOperationException("Invalid state payload");
        }

        return payload;
    }

    private async Task UpsertExternalConnectionAsync(
        Guid orgId,
        Guid userId,
        ExternalConnectionProvider provider,
        string accessToken,
        string? refreshToken,
        int expiresInSeconds,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Access token is missing from provider response");
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            var existing = await _dbContext.ExternalConnections
                .FirstOrDefaultAsync(ec => ec.OrgId == orgId && ec.UserId == userId && ec.Provider == provider, cancellationToken);

            if (existing == null)
            {
                throw new InvalidOperationException("Provider did not return a refresh token. Ensure offline access is granted.");
            }

            refreshToken = DecryptToken(existing.RefreshToken);
        }

        var encryptedAccessToken = EncryptToken(accessToken);
        var encryptedRefreshToken = EncryptToken(refreshToken!);

        var connection = await _dbContext.ExternalConnections
            .FirstOrDefaultAsync(ec => ec.OrgId == orgId && ec.UserId == userId && ec.Provider == provider, cancellationToken);

        if (connection == null)
        {
            connection = new ExternalConnection
            {
                OrgId = orgId,
                UserId = userId,
                Provider = provider,
                AccessToken = encryptedAccessToken,
                RefreshToken = encryptedRefreshToken,
                TokenExpiry = DateTime.UtcNow.AddSeconds(expiresInSeconds),
                Scopes = scopes.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.ExternalConnections.Add(connection);
        }
        else
        {
            connection.AccessToken = encryptedAccessToken;
            connection.RefreshToken = encryptedRefreshToken;
            connection.TokenExpiry = DateTime.UtcNow.AddSeconds(expiresInSeconds);
            connection.Scopes = scopes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            connection.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TokenExchangeResult> ExchangeCodeForTokensAsync(string tokenEndpoint, Dictionary<string, string> formValues, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(formValues)
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Token exchange failed with status {StatusCode}: {Content}", response.StatusCode, content);
            throw new InvalidOperationException($"Token exchange failed: {content}");
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var accessToken = root.GetPropertyOrDefault("access_token");
            var refreshToken = root.GetPropertyOrDefault("refresh_token");
            var expiresIn = root.GetPropertyOrDefault("expires_in");

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Provider response did not include an access token");
            }

            if (!int.TryParse(expiresIn, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresInSeconds) || expiresInSeconds <= 0)
            {
                expiresInSeconds = 3600;
            }

            return new TokenExchangeResult(accessToken, refreshToken, expiresInSeconds);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse token exchange response: {Content}", content);
            throw new InvalidOperationException("Unable to parse token response");
        }
    }

    private string EncryptToken(string token)
    {
        var protector = _dataProtectionProvider.CreateProtector("DriveOAuthTokens");
        return protector.Protect(token);
    }

    private string DecryptToken(string encrypted)
    {
        var protector = _dataProtectionProvider.CreateProtector("DriveOAuthTokens");
        return protector.Unprotect(encrypted);
    }

    private int GetCurrentUserId()
    {
        if (HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int value)
        {
            return value;
        }

        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(claim) && int.TryParse(claim, out var fromClaim))
        {
            return fromClaim;
        }

        throw new UnauthorizedAccessException("User ID not found in context or claims");
    }

    private string GetRequiredConfigurationValue(string key, string errorMessage)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.Contains("${", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value;
    }

    private IReadOnlyCollection<string> ResolveScopes(string configPath, IReadOnlyCollection<string> defaults)
    {
        var configured = _configuration.GetSection(configPath).Get<string[]>();
        if (configured != null && configured.Length > 0)
        {
            return configured;
        }

        return defaults;
    }

    /// <summary>
    /// Disconnect Google Drive
    /// </summary>
    [HttpDelete("google")]
    public async Task<IActionResult> DisconnectGoogleDrive([FromQuery] int? orgId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var documentOrgId = ResolveDocumentOrgId(orgId);
            var documentUserId = GetDocumentUserId();

            var connection = await _dbContext.ExternalConnections
                .FirstOrDefaultAsync(ec => ec.OrgId == documentOrgId && ec.UserId == documentUserId && ec.Provider == ExternalConnectionProvider.Google, cancellationToken);

            if (connection == null)
            {
                return NotFound(new { success = false, error = "Google Drive connection not found" });
            }

            _dbContext.ExternalConnections.Remove(connection);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var currentUserIdInt = GetCurrentUserId();
            await _documentAuditService.LogAsync(
                new DocumentAuditEvent(
                    documentOrgId,
                    null,
                    null,
                    null,
                    documentUserId,
                    "ExternalConnectionDisconnected",
                    "Disconnected Google Drive",
                    DateTime.UtcNow,
                    currentUserIdInt),
                cancellationToken);

            _logger.LogInformation("Disconnected Google Drive for Org {OrgId} User {UserId}", documentOrgId, documentUserId);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting Google Drive");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = "Failed to disconnect Google Drive" });
        }
    }

    /// <summary>
    /// Disconnect OneDrive
    /// </summary>
    [HttpDelete("onedrive")]
    public async Task<IActionResult> DisconnectOneDrive([FromQuery] int? orgId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var documentOrgId = ResolveDocumentOrgId(orgId);
            var documentUserId = GetDocumentUserId();

            var connection = await _dbContext.ExternalConnections
                .FirstOrDefaultAsync(ec => ec.OrgId == documentOrgId && ec.UserId == documentUserId && ec.Provider == ExternalConnectionProvider.Microsoft, cancellationToken);

            if (connection == null)
            {
                return NotFound(new { success = false, error = "OneDrive connection not found" });
            }

            _dbContext.ExternalConnections.Remove(connection);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var currentUserIdInt = GetCurrentUserId();
            await _documentAuditService.LogAsync(
                new DocumentAuditEvent(
                    documentOrgId,
                    null,
                    null,
                    null,
                    documentUserId,
                    "ExternalConnectionDisconnected",
                    "Disconnected Microsoft OneDrive",
                    DateTime.UtcNow,
                    currentUserIdInt),
                cancellationToken);

            _logger.LogInformation("Disconnected OneDrive for Org {OrgId} User {UserId}", documentOrgId, documentUserId);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting OneDrive");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = "Failed to disconnect OneDrive" });
        }
    }

    /// <summary>
    /// Manually trigger sync for a provider
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncDocuments([FromQuery] int? orgId = null, [FromQuery] string? provider = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var documentOrgId = ResolveDocumentOrgId(orgId);
            var documentUserId = GetDocumentUserId();

            if (string.IsNullOrEmpty(provider) || provider.Equals("google", StringComparison.OrdinalIgnoreCase))
            {
                await _driveSyncService.SyncGoogleDriveAsync(documentOrgId, documentUserId, cancellationToken);
            }

            if (string.IsNullOrEmpty(provider) || provider.Equals("onedrive", StringComparison.OrdinalIgnoreCase))
            {
                await _driveSyncService.SyncOneDriveAsync(documentOrgId, documentUserId, cancellationToken);
            }

            return Ok(new { success = true, message = "Sync started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering document sync");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = "Failed to start sync" });
        }
    }

    /// <summary>
    /// Get connected document provider status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] int? orgId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var documentOrgId = ResolveDocumentOrgId(orgId);
            var documentUserId = GetDocumentUserId();

            var connections = await _dbContext.ExternalConnections
                .AsNoTracking()
                .Where(ec => ec.OrgId == documentOrgId && ec.UserId == documentUserId)
                .Select(ec => new { ec.Provider, ec.CreatedAt, ec.UpdatedAt })
                .ToListAsync(cancellationToken);

            var googleConnection = connections.FirstOrDefault(c => c.Provider == ExternalConnectionProvider.Google);
            var microsoftConnection = connections.FirstOrDefault(c => c.Provider == ExternalConnectionProvider.Microsoft);

            return Ok(new
            {
                success = true,
                googleDrive = googleConnection != null 
                    ? new { connected = true, connectedAt = (DateTime?)googleConnection.CreatedAt } 
                    : new { connected = false, connectedAt = (DateTime?)null },
                oneDrive = microsoftConnection != null 
                    ? new { connected = true, connectedAt = (DateTime?)microsoftConnection.CreatedAt } 
                    : new { connected = false, connectedAt = (DateTime?)null }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document connection status");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = "Failed to get connection status" });
        }
    }

    private IActionResult BuildDocumentOAuthView(bool success, string provider, string? accountDisplay, string? error, string? errorDescription, Guid? orgId)
    {
        ViewData["success"] = success;
        ViewData["provider"] = provider;
        ViewData["connectedAccount"] = accountDisplay;
        ViewData["error"] = error;
        ViewData["errorDescription"] = errorDescription;
        ViewData["orgId"] = orgId;
        ViewData["Title"] = "Document Connection";

        return View("~/Views/Shared/DocumentOAuthCallback.cshtml");
    }

    private sealed record TokenExchangeResult(string AccessToken, string? RefreshToken, int ExpiresIn);

    private sealed class OAuthStatePayload
    {
        public Guid OrgId { get; init; }
        public string Provider { get; init; } = string.Empty;
        public string RedirectUri { get; init; } = string.Empty;
        public DateTime IssuedAtUtc { get; init; }
    }
}

internal static class JsonElementExtensions
{
    public static string? GetPropertyOrDefault(this JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => property.GetRawText()
            };
        }

        return null;
    }
}

