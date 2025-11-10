using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Certio.Web.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/email-webhook")]
public class EmailWebhookController : ControllerBase
{
    private static readonly JsonSerializerOptions WebhookSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IEmailToDmService _emailToDmService;
    private readonly ICacheService _cacheService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailWebhookController> _logger;
    private readonly IWebHostEnvironment _environment;

    public EmailWebhookController(
        ApplicationDbContext context,
        IEmailService emailService,
        IEmailToDmService emailToDmService,
        ICacheService cacheService,
        IConfiguration configuration,
        ILogger<EmailWebhookController> logger,
        IWebHostEnvironment? environment = null)
    {
        _context = context;
        _emailService = emailService;
        _emailToDmService = emailToDmService;
        _cacheService = cacheService;
        _configuration = configuration;
        _logger = logger;
        _environment = environment ?? new NoopWebHostEnvironment();
    }

    /// <summary>
    /// Handle Gmail webhook notifications (Pub/Sub)
    /// </summary>
    [HttpPost("gmail")]
    public async Task<IActionResult> GmailWebhook([FromBody] object payload, CancellationToken cancellationToken)
    {
        var validationResult = await ValidateEmailWebhookAsync("gmail", payload, cancellationToken);
        if (validationResult != null)
        {
            return validationResult;
        }

        try
        {
            // Validate shared secret - REQUIRED in production
            var expectedToken = _configuration["EmailIntegration:GmailVerificationToken"];
            var isProduction = !_environment.IsDevelopment();
            
            if (isProduction && string.IsNullOrWhiteSpace(expectedToken))
            {
                _logger.LogError("Gmail webhook secret is required in production but is missing");
                return StatusCode(500, new { error = "Webhook secret configuration error" });
            }
            
            // In production, secret validation is mandatory
            if (isProduction || !string.IsNullOrWhiteSpace(expectedToken))
            {
                var headerToken = Request.Headers["X-Goog-Channel-Token"].FirstOrDefault();
                if (string.IsNullOrEmpty(headerToken))
                {
                    _logger.LogWarning("Gmail webhook missing channel token header");
                    return Unauthorized();
                }

                if (!IsSecureMatch(expectedToken, headerToken))
                {
                    _logger.LogWarning("Gmail webhook token mismatch");
                    return Unauthorized();
                }
            }

            // Gmail webhooks come via Google Cloud Pub/Sub
            // The payload contains message data with email notification
            var payloadJson = JsonSerializer.Serialize(payload);
            _logger.LogInformation("Received Gmail webhook: {Payload}", payloadJson);

            // Parse Pub/Sub message
            var messageData = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);
            if (messageData == null || !messageData.ContainsKey("message"))
            {
                return BadRequest(new { error = "Invalid webhook payload" });
            }

            var message = JsonSerializer.Deserialize<Dictionary<string, object>>(
                messageData["message"].ToString()!);
            
            if (message == null || !message.ContainsKey("data"))
            {
                return BadRequest(new { error = "Invalid message format" });
            }

            if (!string.IsNullOrWhiteSpace(expectedToken) && message.TryGetValue("attributes", out var attributesObj))
            {
                var attributes = JsonSerializer.Deserialize<Dictionary<string, object>>(attributesObj.ToString()!);
                if (attributes == null || !attributes.TryGetValue("token", out var attributeToken) || !IsSecureMatch(expectedToken, attributeToken?.ToString() ?? string.Empty))
                {
                    _logger.LogWarning("Gmail webhook attribute token mismatch");
                    return Unauthorized();
                }
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
            await _emailService.SyncEmailsAsync(emailAccount.Id, cancellationToken);

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
        var validationResult = await ValidateEmailWebhookAsync("outlook", payload, ct);
        if (validationResult != null)
        {
            return validationResult;
        }

        try
        {
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
            _logger.LogInformation("Received Outlook webhook: {Payload}", payloadJson);

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
                    var secret = _configuration["EmailIntegration:WebhookSecret"];
                    var isProduction = !_environment.IsDevelopment();
                    
                    // In production, secret validation is mandatory
                    if (isProduction && string.IsNullOrWhiteSpace(secret))
                    {
                        _logger.LogError("Outlook webhook secret is required in production but is missing");
                        return StatusCode(500, new { error = "Webhook secret configuration error" });
                    }
                    
                    // Validate secret if configured (mandatory in production)
                    if (isProduction || !string.IsNullOrWhiteSpace(secret))
                    {
                        foreach (var item in notifications)
                        {
                            if (!item.TryGetValue("clientState", out var clientStateObj))
                            {
                                _logger.LogWarning("Outlook webhook missing clientState");
                                return Unauthorized();
                            }

                            var clientState = clientStateObj?.ToString();
                            if (string.IsNullOrEmpty(clientState) || !IsSecureMatch(secret, clientState))
                            {
                                _logger.LogWarning("Outlook webhook clientState mismatch");
                                return Unauthorized();
                            }
                        }
                    }

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

    private static bool IsSecureMatch(string expected, string actual)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
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

    private sealed class NoopWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Certio";
        public string WebRootPath { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private async Task<IActionResult?> ValidateEmailWebhookAsync(string providerKey, object payload, CancellationToken ct)
    {
        var secret = _configuration["EmailIntegration:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogError("Email webhook secret is not configured.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Webhook secret is not configured." });
        }

        var providedSecret = Request.Headers["X-Webhook-Secret"].FirstOrDefault();
        if (!IsSecretValid(secret, providedSecret))
        {
            _logger.LogWarning("Email webhook rejected due to invalid secret.");
            return Unauthorized();
        }

        var nonce = Request.Headers["X-Webhook-Nonce"].FirstOrDefault();
        var timestampHeader = Request.Headers["X-Webhook-Timestamp"].FirstOrDefault();
        var signatureHeader = Request.Headers["X-Webhook-Signature"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(nonce) || string.IsNullOrWhiteSpace(timestampHeader) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("Email webhook missing security headers (nonce/timestamp/signature).");
            return Unauthorized();
        }

        if (!DateTimeOffset.TryParse(timestampHeader, out var timestamp) ||
            Math.Abs((DateTimeOffset.UtcNow - timestamp).TotalMinutes) > 5)
        {
            _logger.LogWarning("Email webhook timestamp invalid or expired. Timestamp={Timestamp}", timestampHeader);
            return Unauthorized();
        }

        var cacheKey = $"webhook:email:{providerKey}:{nonce}";
        if (await _cacheService.ExistsAsync(cacheKey))
        {
            _logger.LogWarning("Email webhook replay detected for provider {ProviderKey} with nonce {Nonce}", providerKey, nonce);
            return Unauthorized();
        }

        var canonicalPayload = JsonSerializer.Serialize(payload, WebhookSerializerOptions);
        var expectedSignature = ComputeSignature(secret, nonce, timestampHeader, canonicalPayload);

        if (!IsSignatureMatch(signatureHeader, expectedSignature))
        {
            _logger.LogWarning("Email webhook signature validation failed for provider {ProviderKey}.", providerKey);
            return Unauthorized();
        }

        await _cacheService.SetAsync(cacheKey, "1", TimeSpan.FromMinutes(10));
        return null;
    }

    private static bool IsSecretValid(string secret, string? providedSecret)
    {
        if (string.IsNullOrWhiteSpace(providedSecret))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(secret);
        var providedBytes = Encoding.UTF8.GetBytes(providedSecret);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static bool IsSignatureMatch(string providedSignature, string expectedSignature)
    {
        if (string.IsNullOrWhiteSpace(providedSignature))
        {
            return false;
        }

        var cleaned = providedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? providedSignature.Substring("sha256=".Length)
            : providedSignature;

        var providedBytes = Encoding.UTF8.GetBytes(cleaned);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private static string ComputeSignature(string secret, string nonce, string timestamp, string payload)
    {
        var message = $"{nonce}.{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

