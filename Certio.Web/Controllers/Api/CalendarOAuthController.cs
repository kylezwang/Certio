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
using Certio.Domain.Calendar;
using Certio.Infrastructure.Data;
using Certio.Web.Security;
using Certio.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/calendar-oauth")]
[Authorize(Policy = "OrgMember")]
public class CalendarOAuthController : Controller
{
    private static readonly string[] DefaultGoogleScopes =
    {
        "https://www.googleapis.com/auth/calendar", // Full read/write access to calendars
        "https://www.googleapis.com/auth/calendar.events", // Full read/write access to events
        "https://www.googleapis.com/auth/userinfo.email",
        "https://www.googleapis.com/auth/userinfo.profile"
    };

    private static readonly string[] DefaultOutlookScopes =
    {
        "offline_access",
        "Calendars.ReadWrite", // Read and write access to calendars
        "User.Read"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<CalendarOAuthController> _logger;
    private readonly AuthorizationHelper _authorizationHelper;

    public CalendarOAuthController(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IClientContextAccessor clientContextAccessor,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<CalendarOAuthController> logger,
        AuthorizationHelper authorizationHelper)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _dataProtectionProvider = dataProtectionProvider;
        _httpClientFactory = httpClientFactory;
        _clientContextAccessor = clientContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _authorizationHelper = authorizationHelper;
    }

    #region Google Calendar OAuth

    [HttpGet("google/authorize")]
    public IActionResult AuthorizeGoogle([FromQuery] int? orgId = null, [FromQuery] string? redirectUri = null)
    {
        try
        {
            var calendarOrgId = ResolveCalendarOrgId(orgId);
            var authorizationRedirectUri = NormalizeGoogleRedirectUri(redirectUri);

            var clientId = GetRequiredConfigurationValue("CalendarIntegration:GoogleCalendar:ClientId", "Google Calendar client ID is not configured");
            var scopes = ResolveScopes("CalendarIntegration:GoogleCalendar:Scopes", DefaultGoogleScopes);
            var currentUserId = GetCurrentUserId();

            var statePayload = BuildStatePayload(calendarOrgId, currentUserId, provider: "google", authorizationRedirectUri);
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

            _logger.LogInformation("Generated Google Calendar authorization URL with redirect_uri: {RedirectUri}", authorizationRedirectUri);

            return Ok(new { success = true, authorizationUrl });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Google Calendar authorization URL");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Google Calendar OAuth error: {Error} - {ErrorDescription}", error, errorDescription);
            return BuildCalendarOAuthView(false, "Google Calendar", null, error, errorDescription, null);
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            _logger.LogWarning("Google Calendar callback missing code or state");
            return BuildCalendarOAuthView(false, "Google Calendar", null, "missing_parameters", "Required parameters are missing", null);
        }

        StatePayload statePayload;
        try
        {
            statePayload = UnprotectState(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect state parameter");
            return BuildCalendarOAuthView(false, "Google Calendar", null, "invalid_state", "State parameter is invalid or expired", null);
        }

        try
        {
            var clientId = GetRequiredConfigurationValue("CalendarIntegration:GoogleCalendar:ClientId", "Google Calendar client ID is not configured");
            var clientSecret = GetRequiredConfigurationValue("CalendarIntegration:GoogleCalendar:ClientSecret", "Google Calendar client secret is not configured");
            var scopes = ResolveScopes("CalendarIntegration:GoogleCalendar:Scopes", DefaultGoogleScopes);

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

            // Get user info from Google
            var userInfo = await GetGoogleUserInfoAsync(tokenResponse.AccessToken, cancellationToken);

            await UpsertCalendarIntegrationAsync(
                statePayload.OrgId,
                statePayload.UserId,
                "Google",
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                tokenResponse.ExpiresIn,
                scopes,
                userInfo.Email,
                userInfo.Name,
                cancellationToken);

            _logger.LogInformation("Google Calendar connected successfully for Org {OrgId} User {UserId}", statePayload.OrgId, statePayload.UserId);

            // Trigger sync in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000);
                    
                    using var scope = _serviceScopeFactory.CreateScope();
                    var calendarSyncService = scope.ServiceProvider.GetService<ICalendarSyncService>();
                    if (calendarSyncService != null)
                    {
                        await calendarSyncService.SyncGoogleCalendarAsync(statePayload.OrgId, statePayload.UserId, CancellationToken.None);
                        _logger.LogInformation("Google Calendar sync completed for Org {OrgId} User {UserId}", statePayload.OrgId, statePayload.UserId);
                    }
                }
                catch (Exception syncEx)
                {
                    _logger.LogError(syncEx, "Error during Google Calendar sync for Org {OrgId} User {UserId}", statePayload.OrgId, statePayload.UserId);
                }
            }, cancellationToken);

            return BuildCalendarOAuthView(true, "Google Calendar", userInfo.Email, null, null, statePayload.OrgId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete Google Calendar OAuth callback");
            return BuildCalendarOAuthView(false, "Google Calendar", null, "internal_error", ex.Message, statePayload.OrgId);
        }
    }

    #endregion

    #region Outlook Calendar OAuth

    [HttpGet("outlook/authorize")]
    public IActionResult AuthorizeOutlook([FromQuery] int? orgId = null, [FromQuery] string? redirectUri = null)
    {
        try
        {
            var calendarOrgId = ResolveCalendarOrgId(orgId);
            var authorizationRedirectUri = NormalizeOutlookRedirectUri(redirectUri);

            var clientId = GetRequiredConfigurationValue("CalendarIntegration:OutlookCalendar:ClientId", "Outlook Calendar client ID is not configured");
            var scopes = ResolveScopes("CalendarIntegration:OutlookCalendar:Scopes", DefaultOutlookScopes);
            var currentUserId = GetCurrentUserId();

            var statePayload = BuildStatePayload(calendarOrgId, currentUserId, provider: "outlook", authorizationRedirectUri);
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

            _logger.LogInformation("Generated Outlook Calendar authorization URL with redirect_uri: {RedirectUri}", authorizationRedirectUri);

            return Ok(new { success = true, authorizationUrl });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Outlook Calendar authorization URL");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("outlook/callback")]
    public async Task<IActionResult> OutlookCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Outlook Calendar OAuth error: {Error} - {ErrorDescription}", error, errorDescription);
            return BuildCalendarOAuthView(false, "Outlook Calendar", null, error, errorDescription, null);
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            _logger.LogWarning("Outlook Calendar callback missing code or state");
            return BuildCalendarOAuthView(false, "Outlook Calendar", null, "missing_parameters", "Required parameters are missing", null);
        }

        StatePayload statePayload;
        try
        {
            statePayload = UnprotectState(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect state parameter");
            return BuildCalendarOAuthView(false, "Outlook Calendar", null, "invalid_state", "State parameter is invalid or expired", null);
        }

        try
        {
            var clientId = GetRequiredConfigurationValue("CalendarIntegration:OutlookCalendar:ClientId", "Outlook Calendar client ID is not configured");
            var clientSecret = GetRequiredConfigurationValue("CalendarIntegration:OutlookCalendar:ClientSecret", "Outlook Calendar client secret is not configured");
            var scopes = ResolveScopes("CalendarIntegration:OutlookCalendar:Scopes", DefaultOutlookScopes);

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

            // Get user info from Microsoft Graph
            var userInfo = await GetMicrosoftUserInfoAsync(tokenResponse.AccessToken, cancellationToken);

            await UpsertCalendarIntegrationAsync(
                statePayload.OrgId,
                statePayload.UserId,
                "Outlook",
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                tokenResponse.ExpiresIn,
                scopes,
                userInfo.Email,
                userInfo.Name,
                cancellationToken);

            _logger.LogInformation("Outlook Calendar connected successfully for Org {OrgId} User {UserId}", statePayload.OrgId, statePayload.UserId);

            // Trigger sync in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000);
                    
                    using var scope = _serviceScopeFactory.CreateScope();
                    var calendarSyncService = scope.ServiceProvider.GetService<ICalendarSyncService>();
                    if (calendarSyncService != null)
                    {
                        await calendarSyncService.SyncOutlookCalendarAsync(statePayload.OrgId, statePayload.UserId, CancellationToken.None);
                        _logger.LogInformation("Outlook Calendar sync completed for Org {OrgId} User {UserId}", statePayload.OrgId, statePayload.UserId);
                    }
                }
                catch (Exception syncEx)
                {
                    _logger.LogError(syncEx, "Error during Outlook Calendar sync for Org {OrgId} User {UserId}", statePayload.OrgId, statePayload.UserId);
                }
            }, cancellationToken);

            return BuildCalendarOAuthView(true, "Outlook Calendar", userInfo.Email, null, null, statePayload.OrgId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete Outlook Calendar OAuth callback");
            return BuildCalendarOAuthView(false, "Outlook Calendar", null, "internal_error", ex.Message, statePayload.OrgId);
        }
    }

    #endregion

    #region Status and Disconnect Endpoints

    [HttpGet("status")]
    public async Task<IActionResult> GetConnectionStatus([FromQuery] int? orgId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedOrgId = ResolveCalendarOrgId(orgId);
            var userId = GetCurrentUserId();

            var integrations = await _dbContext.CalendarIntegrations
                .Where(ci => ci.OrgId == resolvedOrgId && ci.UserId == userId && ci.Status == "Active")
                .Select(ci => new
                {
                    ci.Provider,
                    ci.Email,
                    ci.DisplayName,
                    ci.ConnectedAt,
                    ci.LastSyncedAt
                })
                .ToListAsync(cancellationToken);

            return Ok(new { success = true, integrations });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get calendar integration status");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = ex.Message });
        }
    }

    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect([FromBody] DisconnectRequest request, [FromQuery] int? orgId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedOrgId = ResolveCalendarOrgId(orgId);
            var userId = GetCurrentUserId();

            var integration = await _dbContext.CalendarIntegrations
                .FirstOrDefaultAsync(ci => ci.OrgId == resolvedOrgId && ci.UserId == userId && ci.Provider == request.Provider, cancellationToken);

            if (integration != null)
            {
                _dbContext.CalendarIntegrations.Remove(integration);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Disconnected {Provider} calendar for Org {OrgId} User {UserId}", request.Provider, orgId, userId);
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect calendar integration");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, error = ex.Message });
        }
    }

    #endregion

    #region Helper Methods

    private int ResolveCalendarOrgId(int? providedOrgId)
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
            _logger.LogDebug("Derived calendar OrgId from CurrentOrganizationId {OrgId}", resolvedOrgId);
        }
        // Third: Try ClientContext (set by middleware)
        else if (_clientContextAccessor.ClientContext?.OrganizationId.HasValue == true)
        {
            resolvedOrgId = _clientContextAccessor.ClientContext.OrganizationId;
            _logger.LogDebug("Derived calendar OrgId from ClientContext {OrgId}", resolvedOrgId);
        }
        // Fourth: Try from query string (middleware should have set it, but double-check)
        else if (Request.Query.TryGetValue("orgId", out var orgIdValue) && int.TryParse(orgIdValue.FirstOrDefault(), out var parsedOrgId) && parsedOrgId > 0)
        {
            resolvedOrgId = parsedOrgId;
            _logger.LogDebug("Derived calendar OrgId from query string: {OrgId}", resolvedOrgId);
        }

        if (resolvedOrgId.HasValue && resolvedOrgId.Value > 0)
        {
            var currentUserId = GetCurrentUserId();
            List<int> accessibleOrganizations;
            try
            {
                accessibleOrganizations = _authorizationHelper.GetAccessibleOrganizationIdsAsync(currentUserId).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve accessible organizations for user {UserId}", currentUserId);
                throw;
            }

            if (!accessibleOrganizations.Contains(resolvedOrgId.Value))
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to access calendar OAuth for unauthorized organization {OrgId}", currentUserId, resolvedOrgId.Value);
                throw new UnauthorizedAccessException("User is not authorized for the requested organization.");
            }

            return resolvedOrgId.Value;
        }

        _logger.LogError("Unable to resolve organization ID. ProvidedOrgId: {ProvidedOrgId}, CurrentOrganizationId in Items: {HasCurrentOrgId}, ClientContext OrgId: {ClientContextOrgId}",
            providedOrgId, HttpContext.Items.ContainsKey("CurrentOrganizationId"), _clientContextAccessor.ClientContext?.OrganizationId);
        throw new InvalidOperationException("Organization ID is required for calendar OAuth. Please ensure you're accessing this from a client context or provide orgId as a query parameter.");
    }

    private int GetCurrentUserId()
    {
        if (HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int value)
        {
            return value;
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        if (!int.TryParse(userIdString, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID format");
        }

        return userId;
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

    private string[] ResolveScopes(string configKey, string[] defaultScopes)
    {
        var scopesConfig = _configuration[configKey];
        return string.IsNullOrWhiteSpace(scopesConfig) 
            ? defaultScopes 
            : scopesConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private string NormalizeGoogleRedirectUri(string? redirectUri)
    {
        // Check if provided redirectUri is valid (not null, not placeholder)
        if (!string.IsNullOrEmpty(redirectUri) && !redirectUri.Contains("${", StringComparison.Ordinal))
        {
            return redirectUri;
        }

        // Check if configured URI is valid (not null, not placeholder)
        var configuredUri = _configuration["CalendarIntegration:GoogleCalendar:RedirectUri"];
        if (!string.IsNullOrEmpty(configuredUri) && !configuredUri.Contains("${", StringComparison.Ordinal))
        {
            return configuredUri;
        }

        // Fall back to constructing from request
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return $"{baseUrl}/api/calendar-oauth/google/callback";
    }

    private string NormalizeOutlookRedirectUri(string? redirectUri)
    {
        // Check if provided redirectUri is valid (not null, not placeholder)
        if (!string.IsNullOrEmpty(redirectUri) && !redirectUri.Contains("${", StringComparison.Ordinal))
        {
            return redirectUri;
        }

        // Check if configured URI is valid (not null, not placeholder)
        var configuredUri = _configuration["CalendarIntegration:OutlookCalendar:RedirectUri"];
        if (!string.IsNullOrEmpty(configuredUri) && !configuredUri.Contains("${", StringComparison.Ordinal))
        {
            return configuredUri;
        }

        // Fall back to constructing from request
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return $"{baseUrl}/api/calendar-oauth/outlook/callback";
    }

    private StatePayload BuildStatePayload(int orgId, int userId, string provider, string redirectUri)
    {
        return new StatePayload
        {
            OrgId = orgId,
            UserId = userId,
            Provider = provider,
            RedirectUri = redirectUri,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    private string ProtectState(StatePayload payload)
    {
        var protector = _dataProtectionProvider.CreateProtector("CalendarOAuth.StateProtection");
        var json = JsonSerializer.Serialize(payload);
        return protector.Protect(json);
    }

    private StatePayload UnprotectState(string protectedState)
    {
        var protector = _dataProtectionProvider.CreateProtector("CalendarOAuth.StateProtection");
        var json = protector.Unprotect(protectedState);
        var payload = JsonSerializer.Deserialize<StatePayload>(json) 
            ?? throw new InvalidOperationException("Failed to deserialize state payload");

        var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - payload.Timestamp;
        if (age > 600)
        {
            throw new InvalidOperationException("State has expired");
        }

        return payload;
    }

    private string BuildUrl(string baseUrl, Dictionary<string, string> queryParams)
    {
        var query = string.Join('&', queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{baseUrl}?{query}";
    }

    private async Task<TokenResponse> ExchangeCodeForTokensAsync(string tokenEndpoint, Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(parameters);
        var response = await httpClient.PostAsync(tokenEndpoint, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Token exchange failed: {StatusCode} - {ResponseBody}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Token exchange failed: {response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize token response");

        return tokenResponse;
    }

    private async Task<UserInfo> GetGoogleUserInfoAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        
        var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo", cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get Google user info: {StatusCode} - {ResponseBody}", response.StatusCode, responseBody);
            return new UserInfo { Email = "Unknown", Name = "Unknown" };
        }

        var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return new UserInfo 
        { 
            Email = userInfo?.Email ?? "Unknown", 
            Name = userInfo?.Name ?? "Unknown" 
        };
    }

    private async Task<UserInfo> GetMicrosoftUserInfoAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        
        var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me", cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get Microsoft user info: {StatusCode} - {ResponseBody}", response.StatusCode, responseBody);
            return new UserInfo { Email = "Unknown", Name = "Unknown" };
        }

        var userInfo = JsonSerializer.Deserialize<MicrosoftUserInfo>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return new UserInfo 
        { 
            Email = userInfo?.UserPrincipalName ?? userInfo?.Mail ?? "Unknown", 
            Name = userInfo?.DisplayName ?? "Unknown" 
        };
    }

    private async Task UpsertCalendarIntegrationAsync(
        int orgId, 
        int userId, 
        string provider, 
        string accessToken, 
        string? refreshToken, 
        int? expiresIn,
        string[] scopes,
        string? email,
        string? displayName,
        CancellationToken cancellationToken)
    {
        var integration = await _dbContext.CalendarIntegrations
            .FirstOrDefaultAsync(ci => ci.OrgId == orgId && ci.UserId == userId && ci.Provider == provider, cancellationToken);

        var tokenExpiresAt = expiresIn.HasValue 
            ? DateTime.UtcNow.AddSeconds(expiresIn.Value) 
            : (DateTime?)null;

        if (integration == null)
        {
            integration = new CalendarIntegration
            {
                OrgId = orgId,
                UserId = userId,
                Provider = provider,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenExpiresAt = tokenExpiresAt,
                Scopes = string.Join(',', scopes),
                Email = email,
                DisplayName = displayName,
                ConnectedAt = DateTime.UtcNow,
                Status = "Active"
            };
            _dbContext.CalendarIntegrations.Add(integration);
        }
        else
        {
            integration.AccessToken = accessToken;
            integration.RefreshToken = refreshToken ?? integration.RefreshToken;
            integration.TokenExpiresAt = tokenExpiresAt;
            integration.Scopes = string.Join(',', scopes);
            integration.Email = email;
            integration.DisplayName = displayName;
            integration.Status = "Active";
            integration.LastErrorMessage = null;
            integration.LastErrorAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    #region Security Helpers

    /// <summary>
    /// Encrypts an OAuth token for secure storage
    /// </summary>
    private string EncryptToken(string token)
    {
        var protector = _dataProtectionProvider.CreateProtector("CalendarOAuthTokens");
        return protector.Protect(token);
    }

    /// <summary>
    /// Decrypts an OAuth token from storage
    /// </summary>
    private string DecryptToken(string encrypted)
    {
        var protector = _dataProtectionProvider.CreateProtector("CalendarOAuthTokens");
        return protector.Unprotect(encrypted);
    }

    #endregion

    private IActionResult BuildCalendarOAuthView(bool success, string provider, string? email, string? error, string? errorDescription, int? orgId)
    {
        ViewData["success"] = success;
        ViewData["Provider"] = provider;
        ViewData["Email"] = email;
        ViewData["error"] = error;
        ViewData["errorDescription"] = errorDescription;
        ViewData["orgId"] = orgId;
        ViewData["Title"] = "Calendar Connection";

        return View("~/Views/Shared/CalendarOAuthCallback.cshtml");
    }

    #endregion

    #region DTOs

    private class StatePayload
    {
        public int OrgId { get; set; }
        public int UserId { get; set; }
        public string Provider { get; set; } = "";
        public string RedirectUri { get; set; } = "";
        public long Timestamp { get; set; }
    }

    private class TokenResponse
    {
        public string Access_Token { get; set; } = "";
        public string? Refresh_Token { get; set; }
        public int? Expires_In { get; set; }
        
        public string AccessToken => Access_Token;
        public string? RefreshToken => Refresh_Token;
        public int? ExpiresIn => Expires_In;
    }

    private class GoogleUserInfo
    {
        public string? Email { get; set; }
        public string? Name { get; set; }
    }

    private class MicrosoftUserInfo
    {
        public string? UserPrincipalName { get; set; }
        public string? Mail { get; set; }
        public string? DisplayName { get; set; }
    }

    private class UserInfo
    {
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class DisconnectRequest
    {
        public string Provider { get; set; } = "";
    }

    #endregion
}

