using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Certio.Web.Controllers;

[ApiController]
[Route("api/webhooks")]
public class DocumentWebhooksController : ControllerBase
{
    private static readonly JsonSerializerOptions WebhookSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IWebhookHandlerService _webhookHandlerService;
    private readonly IConfiguration _configuration;
    private readonly ICacheService _cacheService;
    private readonly ILogger<DocumentWebhooksController> _logger;

    public DocumentWebhooksController(
        IWebhookHandlerService webhookHandlerService,
        IConfiguration configuration,
        ICacheService cacheService,
        ILogger<DocumentWebhooksController> logger)
    {
        _webhookHandlerService = webhookHandlerService;
        _configuration = configuration;
        _cacheService = cacheService;
        _logger = logger;
    }

    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] GoogleWebhookRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await ValidateDocumentWebhookAsync(request, cancellationToken);
        if (validationResult != null)
        {
            return validationResult;
        }

        var notification = new GoogleDriveChangeNotification(
            request.ResourceId,
            request.ResourceUri ?? string.Empty,
            request.Timestamp ?? DateTime.UtcNow,
            request.OrgId,
            request.UserId);

        await _webhookHandlerService.HandleGoogleDriveAsync(notification, cancellationToken);
        return Accepted();
    }

    [HttpPost("microsoft")]
    public async Task<IActionResult> Microsoft([FromBody] MicrosoftWebhookRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await ValidateDocumentWebhookAsync(request, cancellationToken);
        if (validationResult != null)
        {
            return validationResult;
        }

        var notification = new MicrosoftGraphChangeNotification(
            request.Resource,
            request.SubscriptionId ?? string.Empty,
            request.Timestamp ?? DateTime.UtcNow,
            request.OrgId,
            request.UserId);

        await _webhookHandlerService.HandleMicrosoftGraphAsync(notification, cancellationToken);
        return Accepted();
    }

    private async Task<IActionResult?> ValidateDocumentWebhookAsync(object payload, CancellationToken cancellationToken)
    {
        var secret = _configuration["DocumentIntegration:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogError("Document webhook secret is not configured.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Webhook secret is not configured." });
        }

        var providedSecret = Request.Headers["X-Webhook-Secret"].FirstOrDefault();
        if (!IsSecretValid(secret, providedSecret))
        {
            _logger.LogWarning("Document webhook rejected due to invalid secret.");
            return Unauthorized();
        }

        var nonce = Request.Headers["X-Webhook-Nonce"].FirstOrDefault();
        var timestampHeader = Request.Headers["X-Webhook-Timestamp"].FirstOrDefault();
        var signatureHeader = Request.Headers["X-Webhook-Signature"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(nonce) || string.IsNullOrWhiteSpace(timestampHeader) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("Document webhook missing security headers (nonce/timestamp/signature).");
            return Unauthorized();
        }

        if (!DateTimeOffset.TryParse(timestampHeader, out var timestamp) ||
            Math.Abs((DateTimeOffset.UtcNow - timestamp).TotalMinutes) > 5)
        {
            _logger.LogWarning("Document webhook timestamp invalid or expired. Timestamp={Timestamp}", timestampHeader);
            return Unauthorized();
        }

        var cacheKey = $"webhook:document:{nonce}";
        if (await _cacheService.ExistsAsync(cacheKey))
        {
            _logger.LogWarning("Document webhook replay detected for nonce {Nonce}", nonce);
            return Unauthorized();
        }

        var canonicalPayload = JsonSerializer.Serialize(payload, WebhookSerializerOptions);
        var expectedSignature = ComputeSignature(secret, nonce, timestampHeader, canonicalPayload);

        if (!IsSignatureMatch(signatureHeader, expectedSignature))
        {
            _logger.LogWarning("Document webhook signature validation failed.");
            return Unauthorized();
        }

        await _cacheService.SetAsync(cacheKey, "1", TimeSpan.FromMinutes(10));
        return null;
    }

    private static bool IsSecretValid(string secret, string? provided)
    {
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(secret);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
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

    public record GoogleWebhookRequest
    {
        [Required]
        public Guid OrgId { get; init; }

        [Required]
        public Guid UserId { get; init; }

        [Required]
        public string ResourceId { get; init; } = string.Empty;

        public string? ResourceUri { get; init; }

        public DateTime? Timestamp { get; init; }
    }

    public record MicrosoftWebhookRequest
    {
        [Required]
        public Guid OrgId { get; init; }

        [Required]
        public Guid UserId { get; init; }

        [Required]
        public string Resource { get; init; } = string.Empty;

        public string? SubscriptionId { get; init; }

        public DateTime? Timestamp { get; init; }
    }
}

