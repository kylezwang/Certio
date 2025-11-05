using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Certio.Web.Controllers;

[ApiController]
[Route("api/webhooks")]
public class DocumentWebhooksController : ControllerBase
{
    private readonly IWebhookHandlerService _webhookHandlerService;

    public DocumentWebhooksController(IWebhookHandlerService webhookHandlerService)
    {
        _webhookHandlerService = webhookHandlerService;
    }

    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] GoogleWebhookRequest request, CancellationToken cancellationToken)
    {
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
        var notification = new MicrosoftGraphChangeNotification(
            request.Resource,
            request.SubscriptionId ?? string.Empty,
            request.Timestamp ?? DateTime.UtcNow,
            request.OrgId,
            request.UserId);

        await _webhookHandlerService.HandleMicrosoftGraphAsync(notification, cancellationToken);
        return Accepted();
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

