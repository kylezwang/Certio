using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Documents;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services.Documents;

public sealed class WebhookHandlerService : IWebhookHandlerService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentIndexerService _documentIndexerService;
    private readonly IDocumentAuditService _documentAuditService;
    private readonly ILogger<WebhookHandlerService> _logger;

    public WebhookHandlerService(
        ApplicationDbContext dbContext,
        IDocumentIndexerService documentIndexerService,
        IDocumentAuditService documentAuditService,
        ILogger<WebhookHandlerService> logger)
    {
        _dbContext = dbContext;
        _documentIndexerService = documentIndexerService;
        _documentAuditService = documentAuditService;
        _logger = logger;
    }

    public async Task HandleGoogleDriveAsync(GoogleDriveChangeNotification notification, CancellationToken cancellationToken = default)
    {
        await HandleProviderChangeAsync(DocumentSourceType.GoogleDrive, notification.ResourceId, notification.Timestamp, notification.OrgId, notification.UserId, cancellationToken);
    }

    public async Task HandleMicrosoftGraphAsync(MicrosoftGraphChangeNotification notification, CancellationToken cancellationToken = default)
    {
        await HandleProviderChangeAsync(DocumentSourceType.OneDrive, notification.Resource, notification.Timestamp, notification.OrgId, notification.UserId, cancellationToken);
    }

    private async Task HandleProviderChangeAsync(DocumentSourceType sourceType, string externalId, DateTime timestamp, Guid orgId, Guid userId, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.OrgId == orgId && d.SourceType == sourceType && d.ExternalFileId == externalId, cancellationToken);

        if (document == null)
        {
            _logger.LogWarning("Received webhook for unknown document. Provider={Provider} ExternalId={ExternalId}", sourceType, externalId);
            return;
        }

        document.ModifiedAt = timestamp;
        document.Metadata["lastWebhookAt"] = timestamp.ToString("O");

        await _dbContext.SaveChangesAsync(cancellationToken);

        var latestVersionId = document.Versions
            .OrderByDescending(v => v.ModifiedAt)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefault();

        await _documentIndexerService.QueueEmbeddingAsync(document.Id, latestVersionId, cancellationToken);

        await _documentAuditService.LogAsync(new DocumentAuditEvent(
            document.OrgId,
            document.MatterId,
            document.Id,
            latestVersionId,
            userId,
            "WebhookReceived",
            $"Change notification processed for {sourceType} document {externalId}",
            DateTime.UtcNow), cancellationToken);

        _logger.LogInformation("Processed webhook change for document {DocumentId} ({ExternalId})", document.Id, externalId);
    }
}

