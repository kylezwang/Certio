using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Certio.Application.Interfaces;
using Certio.Web.Security;
using System.Security.Claims;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Certio.Domain.Services;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/email-oauth")]
[Authorize]
public class EmailOAuthController : Controller
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailOAuthController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public EmailOAuthController(
        IEmailService emailService,
        ILogger<EmailOAuthController> logger,
        IConfiguration configuration,
        IServiceScopeFactory serviceScopeFactory)
    {
        _emailService = emailService;
        _logger = logger;
        _configuration = configuration;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Initiate Gmail OAuth authorization
    /// </summary>
    [HttpGet("gmail/authorize")]
    public IActionResult AuthorizeGmail([FromQuery] string? redirectUri = null)
    {
        try
        {
            // Build redirect URI with fallback
            var baseRedirectUri = redirectUri;
            
            if (string.IsNullOrEmpty(baseRedirectUri))
            {
                baseRedirectUri = _configuration["EmailIntegration:Gmail:RedirectUri"];
            }
            
            // Remove placeholder if it wasn't replaced
            if (string.IsNullOrEmpty(baseRedirectUri) || baseRedirectUri.Contains("${"))
            {
                // Fallback to Request-based URI
                var scheme = Request.Scheme;
                var host = Request.Host.Value;
                
                if (string.IsNullOrEmpty(host))
                {
                    host = "localhost:5092"; // Default for localhost
                }
                
                baseRedirectUri = $"{scheme}://{host}/api/email-oauth/gmail/callback";
            }
            
            // Validate URI
            if (!Uri.TryCreate(baseRedirectUri, UriKind.Absolute, out var validatedUri))
            {
                _logger.LogError("Invalid redirect URI generated: {RedirectUri}", baseRedirectUri);
                return StatusCode(500, new { success = false, error = $"Invalid redirect URI: {baseRedirectUri}" });
            }
            
            _logger.LogInformation("Generating Gmail OAuth URL with redirect URI: {RedirectUri}", validatedUri.ToString());
            
            var authUrl = _emailService.GetGmailAuthUrl(validatedUri.ToString());
            return Ok(new { success = true, authorizationUrl = authUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Gmail authorization URL");
            return StatusCode(500, new { success = false, error = "Failed to generate authorization URL" });
        }
    }

    /// <summary>
    /// Handle Gmail OAuth callback
    /// </summary>
    [HttpGet("gmail/callback")]
    public async Task<IActionResult> GmailCallback([FromQuery] string? code, [FromQuery] string? error, [FromQuery] string? error_description, [FromQuery] string? state = null)
    {
        try
        {
            // Check for OAuth error
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Gmail OAuth error: {Error} - {Description}", error, error_description);
                return BuildOAuthCallbackView(success: false, provider: "Gmail", error: error, errorDescription: error_description);
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Gmail OAuth callback received without authorization code");
                return BuildOAuthCallbackView(success: false, provider: "Gmail", error: "missing_code", errorDescription: "Authorization code is required");
            }

            var userId = GetCurrentUserId();
            
            // Build the same redirect URI that was used in the authorization URL
            var redirectUri = _configuration["EmailIntegration:Gmail:RedirectUri"];
            if (string.IsNullOrEmpty(redirectUri) || redirectUri.Contains("${"))
            {
                var scheme = Request.Scheme;
                var host = Request.Host.Value;
                if (string.IsNullOrEmpty(host))
                {
                    host = "localhost:5092";
                }
                redirectUri = $"{scheme}://{host}/api/email-oauth/gmail/callback";
            }
            
            var emailAccount = await _emailService.ConnectGmailAccountAsync(userId, code, redirectUri);
            
            _logger.LogInformation("Successfully connected Gmail account {Email} for user {UserId}", emailAccount.EmailAddress, userId);
            
            return BuildOAuthCallbackView(success: true, provider: "Gmail", emailAddress: emailAccount.EmailAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Gmail OAuth callback");
            
            // Extract full error message including inner exceptions
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" {ex.InnerException.Message}";
            }
            
            return BuildOAuthCallbackView(success: false, provider: "Gmail", error: "internal_error", errorDescription: errorMessage);
        }
    }

    /// <summary>
    /// Initiate Outlook OAuth authorization
    /// </summary>
    [HttpGet("outlook/authorize")]
    public IActionResult AuthorizeOutlook([FromQuery] string? redirectUri = null)
    {
        try
        {
            // Build redirect URI with fallback
            var baseRedirectUri = redirectUri;
            
            if (string.IsNullOrEmpty(baseRedirectUri))
            {
                baseRedirectUri = _configuration["EmailIntegration:Outlook:RedirectUri"];
            }
            
            // Remove placeholder if it wasn't replaced
            if (string.IsNullOrEmpty(baseRedirectUri) || baseRedirectUri.Contains("${"))
            {
                // Fallback to Request-based URI
                var scheme = Request.Scheme;
                var host = Request.Host.Value;
                
                if (string.IsNullOrEmpty(host))
                {
                    host = "localhost:5092"; // Default for localhost
                }
                
                baseRedirectUri = $"{scheme}://{host}/api/email-oauth/outlook/callback";
            }
            
            // Validate URI
            if (!Uri.TryCreate(baseRedirectUri, UriKind.Absolute, out var validatedUri))
            {
                _logger.LogError("Invalid redirect URI generated: {RedirectUri}", baseRedirectUri);
                return StatusCode(500, new { success = false, error = $"Invalid redirect URI: {baseRedirectUri}" });
            }
            
            _logger.LogInformation("Generating Outlook OAuth URL with redirect URI: {RedirectUri}", validatedUri.ToString());
            
            var authUrl = _emailService.GetOutlookAuthUrl(validatedUri.ToString());
            return Ok(new { success = true, authorizationUrl = authUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Outlook authorization URL");
            return StatusCode(500, new { success = false, error = "Failed to generate authorization URL" });
        }
    }

    /// <summary>
    /// Handle Outlook OAuth callback
    /// </summary>
    [HttpGet("outlook/callback")]
    public async Task<IActionResult> OutlookCallback([FromQuery] string? code, [FromQuery] string? error, [FromQuery] string? error_description, [FromQuery] string? state = null)
    {
        try
        {
            // Check for OAuth error
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Outlook OAuth error: {Error} - {Description}", error, error_description);
                return BuildOAuthCallbackView(success: false, provider: "Outlook", error: error, errorDescription: error_description);
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Outlook OAuth callback received without authorization code");
                return BuildOAuthCallbackView(success: false, provider: "Outlook", error: "missing_code", errorDescription: "Authorization code is required");
            }

            var userId = GetCurrentUserId();
            
            // Build the same redirect URI that was used in the authorization URL
            var redirectUri = _configuration["EmailIntegration:Outlook:RedirectUri"];
            if (string.IsNullOrEmpty(redirectUri) || redirectUri.Contains("${"))
            {
                var scheme = Request.Scheme;
                var host = Request.Host.Value;
                if (string.IsNullOrEmpty(host))
                {
                    host = "localhost:5092";
                }
                redirectUri = $"{scheme}://{host}/api/email-oauth/outlook/callback";
            }
            
            var emailAccount = await _emailService.ConnectOutlookAccountAsync(userId, code, redirectUri);
            
            _logger.LogInformation("Successfully connected Outlook account {Email} for user {UserId}", emailAccount.EmailAddress, userId);
            
            return BuildOAuthCallbackView(success: true, provider: "Outlook", emailAddress: emailAccount.EmailAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Outlook OAuth callback");
            
            // Extract full error message including inner exceptions
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" {ex.InnerException.Message}";
            }
            
            return BuildOAuthCallbackView(success: false, provider: "Outlook", error: "internal_error", errorDescription: errorMessage);
        }
    }

    private IActionResult BuildOAuthCallbackView(bool success, string provider, string? emailAddress = null, string? error = null, string? errorDescription = null)
    {
        ViewData["success"] = success;
        ViewData["provider"] = provider;
        ViewData["emailAddress"] = emailAddress;
        ViewData["error"] = error;
        ViewData["errorDescription"] = errorDescription;

        return View("OAuthCallback");
    }

    /// <summary>
    /// Get connected email account status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var userId = GetCurrentUserId();
            var emailAccount = await _emailService.GetEmailAccountAsync(userId);
            
            if (emailAccount == null)
            {
                return Ok(new { success = true, connected = false });
            }

            return Ok(new { success = true, connected = true, emailAccount = new {
                id = emailAccount.Id,
                provider = emailAccount.Provider,
                emailAddress = emailAccount.EmailAddress,
                isActive = emailAccount.IsActive,
                lastSyncAt = emailAccount.LastSyncAt
            }});
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting email account status");
            return StatusCode(500, new { success = false, error = "Failed to get email account status" });
        }
    }

    /// <summary>
    /// Disconnect email account
    /// </summary>
    [HttpDelete("{accountId}")]
    public async Task<IActionResult> Disconnect(int accountId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Verify the account belongs to the user
            var emailAccount = await _emailService.GetEmailAccountAsync(userId);
            if (emailAccount == null || emailAccount.Id != accountId)
            {
                return NotFound(new { success = false, error = "Email account not found" });
            }

            await _emailService.DisconnectEmailAccountAsync(userId);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting email account {AccountId}", accountId);
            return StatusCode(500, new { success = false, error = "Failed to disconnect email account" });
        }
    }

    /// <summary>
    /// Manually trigger email sync
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncEmails()
    {
        try
        {
            var userId = GetCurrentUserId();
            var emailAccount = await _emailService.GetEmailAccountAsync(userId);
            
            if (emailAccount == null)
            {
                return NotFound(new { success = false, error = "No email account connected" });
            }

            await _emailService.SyncEmailsAsync(emailAccount.Id);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing emails");
            return StatusCode(500, new { success = false, error = "Failed to sync emails" });
        }
    }

    /// <summary>
    /// Get inbox messages (all synced emails, not just those converted to Direct Messages)
    /// Note: Email accounts are user-scoped (not org-scoped), so no org authorization is needed.
    /// The orgId parameter is for frontend context only.
    /// </summary>
    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox([FromQuery] int orgId, [FromQuery] int skip = 0, [FromQuery] int take = 25, [FromQuery] string? search = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Email accounts belong to users, not organizations
            // We validate the user is authenticated, but don't need org-level authorization
            var emailAccount = await _emailService.GetEmailAccountAsync(userId);
            
            if (emailAccount == null)
            {
                return NotFound(new { success = false, error = "No email account connected" });
            }

            // Trigger background sync if needed, but don't wait for it to load emails
            // Only trigger sync if skip == 0 (first page) AND sync is stale
            // Add debouncing check to prevent rapid firing
            if (skip == 0)
            {
                var needsInitialSync = !emailAccount.LastSyncAt.HasValue;
                var syncIsStale = emailAccount.LastSyncAt.HasValue &&
                    emailAccount.LastSyncAt.Value < DateTime.UtcNow.AddMinutes(-15); // Increased threshold to 15 minutes

                // Additional check: don't sync if sync completed very recently (within last 30 seconds)
                var syncJustCompleted = emailAccount.LastSyncAt.HasValue &&
                    emailAccount.LastSyncAt.Value > DateTime.UtcNow.AddSeconds(-30);

                if ((needsInitialSync || syncIsStale) && !syncJustCompleted)
                {
                    // Fire and forget - don't block the request
                    // Use proper scoping for background task
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var backgroundEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                            await backgroundEmailService.SyncEmailsAsync(emailAccount.Id, CancellationToken.None);
                            _logger.LogInformation("Background email sync completed for account {AccountId}", emailAccount.Id);
                        }
                        catch (Exception syncEx)
                        {
                            _logger.LogWarning(syncEx, "Background sync failed for email account {AccountId}", emailAccount.Id);
                        }
                    });
                }
            }

            // Get synced emails for ALL of the user's email accounts (active or inactive)
            // This ensures historical emails from previous connections remain visible
            // Direct DbContext access is used here for performance (simple query, no business logic)
            // Service layer is used for email sync operations (complex business logic in IEmailService)
            using var scope = HttpContext.RequestServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Gather all email account IDs for this user
            var userAccountIds = await context.EmailAccounts
                .Where(ea => ea.UserId == userId)
                .Select(ea => ea.Id)
                .ToListAsync();

            // Base query: email messages across all accounts
            // For search, use narrower time window for better performance
            var isSearching = !string.IsNullOrWhiteSpace(search) && search!.Trim().Length >= 2;
            var timeBaseline = isSearching 
                ? DateTime.UtcNow.AddDays(-30)  // 30 days for search
                : DateTime.UtcNow.AddDays(-7);   // 7 days for normal browsing
            
            var emailQuery = context.EmailMessages
                .AsNoTracking() // Read-only query, better performance
                .Where(em => userAccountIds.Contains(em.EmailAccountId) && em.ReceivedAt >= timeBaseline);

            if (isSearching)
            {
                var trimmed = search!.Trim();
                var searchPattern = $"%{EscapeLikePattern(trimmed)}%";

                // Optimize search: exclude large HTML Body field, focus on indexed fields
                // Search only Subject, FromName, FromEmail, and BodyText (plain text)
                emailQuery = emailQuery.Where(em =>
                    (em.Subject != null && EF.Functions.Like(em.Subject, searchPattern)) ||
                    (em.FromName != null && EF.Functions.Like(em.FromName, searchPattern)) ||
                    (em.FromEmail != null && EF.Functions.Like(em.FromEmail, searchPattern)) ||
                    (em.BodyText != null && EF.Functions.Like(em.BodyText, searchPattern))
                );
            }

            // Order and project only needed fields to minimize payload (exclude HTML Body)
            var orderedProjectedQuery = emailQuery
                .OrderByDescending(em => em.ReceivedAt)
                .Select(em => new
                {
                    em.Id,
                    em.DirectMessageId,
                    em.ThreadId,
                    em.ExternalEmailId,
                    em.Subject,
                    em.FromEmail,
                    em.FromName,
                    em.BodyText,
                    em.ReceivedAt,
                    em.IsRead,
                    Provider = em.EmailAccount.Provider
                });

            // Helper to trim long previews safely
            static string TrimPreview(string? text)
            {
                if (string.IsNullOrEmpty(text)) return "";
                return text.Length > 500 ? text.Substring(0, 500) : text;
            }

            // Fetch with efficient pagination
            var pageSize = Math.Min(take, 25);
            if (isSearching)
            {
                const int maxSearchResults = 50;
                var rawItems = await orderedProjectedQuery
                    .Take(maxSearchResults)
                    .ToListAsync(HttpContext.RequestAborted);

                var inboxMessages = rawItems.Select(item => new
                {
                    id = item.DirectMessageId ?? item.Id,
                    threadId = item.ThreadId ?? $"email-{item.Id}",
                    externalEmailId = item.ExternalEmailId,
                    subject = item.Subject ?? "(No subject)",
                    fromEmail = item.FromEmail,
                    fromName = item.FromName,
                    senderName = item.FromName ?? item.FromEmail,
                    body = TrimPreview(item.BodyText),
                    bodyText = TrimPreview(item.BodyText),
                    receivedAt = item.ReceivedAt,
                    isRead = item.IsRead,
                    provider = item.Provider,
                    isImportant = false
                }).ToList();

                return Ok(new
                {
                    success = true,
                    messages = inboxMessages,
                    hasMore = false,
                    total = inboxMessages.Count
                });
            }
            else
            {
                var rawItems = await orderedProjectedQuery
                    .Skip(skip)
                    .Take(pageSize + 1)
                    .ToListAsync(HttpContext.RequestAborted);

                var hasMore = rawItems.Count > pageSize;
                if (hasMore)
                {
                    rawItems = rawItems.Take(pageSize).ToList();
                }

                var inboxMessages = rawItems.Select(item => new
                {
                    id = item.DirectMessageId ?? item.Id,
                    threadId = item.ThreadId ?? $"email-{item.Id}",
                    externalEmailId = item.ExternalEmailId,
                    subject = item.Subject ?? "(No subject)",
                    fromEmail = item.FromEmail,
                    fromName = item.FromName,
                    senderName = item.FromName ?? item.FromEmail,
                    body = TrimPreview(item.BodyText),
                    bodyText = TrimPreview(item.BodyText),
                    receivedAt = item.ReceivedAt,
                    isRead = item.IsRead,
                    provider = item.Provider,
                    isImportant = false
                }).ToList();

                var approxTotal = skip + inboxMessages.Count + (hasMore ? 1 : 0);

                return Ok(new
                {
                    success = true,
                    messages = inboxMessages,
                    hasMore,
                    total = approxTotal
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading inbox");
            return StatusCode(500, new { success = false, error = "Failed to load inbox" });
        }
    }

    /// <summary>
    /// Get inbox unread count
    /// </summary>
    [HttpGet("inbox/count")]
    public async Task<IActionResult> GetInboxCount()
    {
        try
        {
            var userId = GetCurrentUserId();
            var emailAccount = await _emailService.GetEmailAccountAsync(userId);
            
            if (emailAccount == null)
            {
                return Ok(new { success = true, unreadCount = 0 });
            }

            using var scope = HttpContext.RequestServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Count unread DMs originating from any of the user's connected email accounts
            var userAccountIds = await context.EmailAccounts
                .Where(ea => ea.UserId == GetCurrentUserId())
                .Select(ea => ea.Id)
                .ToListAsync();

            var unreadCount = await context.EmailMessages
                .Where(em => userAccountIds.Contains(em.EmailAccountId) && 
                            em.DirectMessageId != null && 
                            !em.IsRead)
                .CountAsync();

            return Ok(new { success = true, unreadCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inbox count");
            return StatusCode(500, new { success = false, error = "Failed to get inbox count" });
        }
    }

    private int GetCurrentUserId()
    {
        // First try to get the custom user ID from context (set by UserSyncMiddleware)
        if (HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int userId)
        {
            return userId;
        }
        
        // Fallback to claims (for backwards compatibility)
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var claimUserId))
        {
            return claimUserId;
        }
        
        throw new UnauthorizedAccessException("User ID not found in context or claims");
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
    }
}

