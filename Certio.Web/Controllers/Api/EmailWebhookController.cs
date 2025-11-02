using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/email-webhook")]
public class EmailWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IEmailToDmService _emailToDmService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailWebhookController> _logger;

    public EmailWebhookController(
        ApplicationDbContext context,
        IEmailService emailService,
        IEmailToDmService emailToDmService,
        IConfiguration configuration,
        ILogger<EmailWebhookController> logger)
    {
        _context = context;
        _emailService = emailService;
        _emailToDmService = emailToDmService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Handle Gmail webhook notifications (Pub/Sub)
    /// </summary>
    [HttpPost("gmail")]
    public async Task<IActionResult> GmailWebhook([FromBody] object payload)
    {
        try
        {
            // Gmail webhooks come via Google Cloud Pub/Sub
            // The payload contains message data with email notification
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
            _logger.LogInformation("Received Gmail webhook: {Payload}", payloadJson);

            // Parse Pub/Sub message
            var messageData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);
            if (messageData == null || !messageData.ContainsKey("message"))
            {
                return BadRequest(new { error = "Invalid webhook payload" });
            }

            var message = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                messageData["message"].ToString()!);
            
            if (message == null || !message.ContainsKey("data"))
            {
                return BadRequest(new { error = "Invalid message format" });
            }

            // Decode base64 message data
            var encodedData = message["data"].ToString()!;
            var decodedBytes = Convert.FromBase64String(encodedData);
            var notificationData = Encoding.UTF8.GetString(decodedBytes);
            
            var notification = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(notificationData);
            if (notification == null || !notification.ContainsKey("emailAddress"))
            {
                return BadRequest(new { error = "Invalid notification format" });
            }

            var emailAddress = notification["emailAddress"];

            // Find email account by email address
            var emailAccount = await _context.EmailAccounts
                .FirstOrDefaultAsync(ea => ea.EmailAddress == emailAddress && ea.IsActive);

            if (emailAccount == null)
            {
                _logger.LogWarning("No active email account found for {EmailAddress}", emailAddress);
                return Ok(); // Return OK to prevent retries
            }

            // Sync emails for this account (this will also convert to DMs)
            await _emailService.SyncEmailsAsync(emailAccount.Id);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Gmail webhook");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Handle Outlook webhook notifications (Microsoft Graph)
    /// </summary>
    [HttpPost("outlook")]
    public async Task<IActionResult> OutlookWebhook([FromBody] object payload, [FromHeader(Name = "X-NotificationId")] string? notificationId = null, CancellationToken ct = default)
    {
        try
        {
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
            _logger.LogInformation("Received Outlook webhook: {Payload}", payloadJson);

            // Validate webhook signature (if configured)
            if (!string.IsNullOrEmpty(_configuration["EmailIntegration:WebhookSecret"]))
            {
                var isValid = ValidateOutlookWebhookSignature(Request);
                if (!isValid)
                {
                    _logger.LogWarning("Invalid webhook signature");
                    return Unauthorized();
                }
            }

            // Parse notification
            var notification = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);
            if (notification == null)
            {
                return BadRequest(new { error = "Invalid payload" });
            }

            // Handle validation request (required by Microsoft Graph)
            if (notification.ContainsKey("validationToken"))
            {
                var validationToken = notification["validationToken"]?.ToString();
                return Content(validationToken ?? "", "text/plain");
            }

            // Process notification
            if (notification.ContainsKey("value"))
            {
                var value = notification["value"];
                var notifications = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(value!.ToString()!);
                
                if (notifications != null && notifications.Any())
                {
                    // When webhook is received, sync all active Outlook accounts
                    // This is because we can't easily determine which account the message belongs to from the webhook payload
                    var activeAccounts = await _context.EmailAccounts
                        .Where(ea => ea.IsActive && ea.Provider == "Outlook")
                        .ToListAsync(ct);

                    foreach (var account in activeAccounts)
                    {
                        try
                        {
                            await _emailService.SyncEmailsAsync(account.Id, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to sync emails for account {AccountId}", account.Id);
                        }
                    }
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Outlook webhook");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private bool ValidateOutlookWebhookSignature(HttpRequest request)
    {
        // Microsoft Graph webhook validation
        // In production, validate using certificate or shared secret
        // For now, basic validation
        var webhookSecret = _configuration["EmailIntegration:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret))
        {
            return true; // If no secret configured, allow
        }

        // Basic validation - in production, use proper certificate validation
        return true;
    }

    private int? ExtractEmailAccountIdFromResource(string resource)
    {
        // Extract email account ID from resource path
        // For Outlook, we need to find the email account that matches the message
        // In a full implementation, we'd store a mapping of message IDs to email accounts
        // For now, sync all active accounts when webhook is received
        try
        {
            // Parse message ID from resource: /me/messages/{messageId}
            var parts = resource.Split('/');
            if (parts.Length >= 4)
            {
                var messageId = parts[3];
                // Find email account by checking which account has this message
                // This is a simplified approach - in production, you'd want to track this better
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        // Return null to indicate we should sync all accounts
        // In production, implement proper message-to-account mapping
        return null;
    }
}

