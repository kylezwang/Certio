using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Certio.Application.Interfaces;
using Certio.Web.Security;
using System.Security.Claims;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/email-oauth")]
[Authorize]
public class EmailOAuthController : Controller
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailOAuthController> _logger;
    private readonly IConfiguration _configuration;

    public EmailOAuthController(
        IEmailService emailService,
        ILogger<EmailOAuthController> logger,
        IConfiguration configuration)
    {
        _emailService = emailService;
        _logger = logger;
        _configuration = configuration;
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
                return View("OAuthCallback", new ViewDataDictionary(ViewData) {
                    { "success", false },
                    { "error", error },
                    { "errorDescription", error_description },
                    { "provider", "Gmail" }
                });
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Gmail OAuth callback received without authorization code");
                return View("OAuthCallback", new ViewDataDictionary(ViewData) {
                    { "success", false },
                    { "error", "missing_code" },
                    { "errorDescription", "Authorization code is required" },
                    { "provider", "Gmail" }
                });
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
            
            return View("OAuthCallback", new ViewDataDictionary(ViewData) {
                { "success", true },
                { "provider", "Gmail" },
                { "emailAddress", emailAccount.EmailAddress }
            });
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
            
            return View("OAuthCallback", new ViewDataDictionary(ViewData) {
                { "success", false },
                { "error", "internal_error" },
                { "errorDescription", errorMessage },
                { "provider", "Gmail" }
            });
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
                return View("OAuthCallback", new ViewDataDictionary(ViewData) {
                    { "success", false },
                    { "error", error },
                    { "errorDescription", error_description },
                    { "provider", "Outlook" }
                });
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Outlook OAuth callback received without authorization code");
                return View("OAuthCallback", new ViewDataDictionary(ViewData) {
                    { "success", false },
                    { "error", "missing_code" },
                    { "errorDescription", "Authorization code is required" },
                    { "provider", "Outlook" }
                });
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
            
            return View("OAuthCallback", new ViewDataDictionary(ViewData) {
                { "success", true },
                { "provider", "Outlook" },
                { "emailAddress", emailAccount.EmailAddress }
            });
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
            
            return View("OAuthCallback", new ViewDataDictionary(ViewData) {
                { "success", false },
                { "error", "internal_error" },
                { "errorDescription", errorMessage },
                { "provider", "Outlook" }
            });
        }
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
    /// </summary>
    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox([FromQuery] int orgId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var emailAccount = await _emailService.GetEmailAccountAsync(userId);
            
            if (emailAccount == null)
            {
                return NotFound(new { success = false, error = "No email account connected" });
            }

            // Get all synced emails for this account (not just converted ones)
            using var scope = HttpContext.RequestServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var emailMessages = await context.EmailMessages
                .Where(em => em.EmailAccountId == emailAccount.Id)
                .OrderByDescending(em => em.ReceivedAt)
                .Take(100) // Increased from 50 to show more emails
                .ToListAsync();

            var inboxMessages = emailMessages.Select(em => new
            {
                id = em.DirectMessageId ?? em.Id, // Use DirectMessageId if converted, otherwise EmailMessage Id
                threadId = em.ThreadId ?? $"email-{em.Id}", // Use thread ID if available
                externalEmailId = em.ExternalEmailId, // Gmail/Outlook message ID for direct links
                subject = em.Subject ?? "(No subject)",
                fromEmail = em.FromEmail,
                fromName = em.FromName,
                senderName = em.FromName ?? em.FromEmail,
                body = em.Body ?? em.BodyText ?? "", // Prefer HTML body, fallback to plain text
                bodyText = em.BodyText ?? "", // Always include plain text version
                receivedAt = em.ReceivedAt,
                isRead = em.IsRead,
                provider = emailAccount.Provider,
                isImportant = false // TODO: Add important flag to EmailMessage if needed
            }).ToList();

            return Ok(new { success = true, messages = inboxMessages });
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
            
            var unreadCount = await context.EmailMessages
                .Where(em => em.EmailAccountId == emailAccount.Id && 
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
}

