using System;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Audit;
using Certio.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services.Documents;

public sealed class DocumentAuditService : IDocumentAuditService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DocumentAuditService> _logger;

    public DocumentAuditService(ApplicationDbContext dbContext, ILogger<DocumentAuditService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAsync(DocumentAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            EntityType = "Document",
            EntityId = auditEvent.DocumentId.HasValue ? auditEvent.DocumentId.Value.GetHashCode() : 0,
            Action = auditEvent.EventType,
            Description = auditEvent.Description,
            // Use UserIdInt if provided (actual int), otherwise fall back to Guid conversion
            UserId = auditEvent.UserIdInt ?? (auditEvent.UserId.HasValue ? ConvertGuidToNullableInt(auditEvent.UserId.Value) : null),
            OrganizationId = auditEvent.OrgId != Guid.Empty ? ConvertGuidToNullableInt(auditEvent.OrgId) : null,
            MatterId = auditEvent.MatterId.HasValue ? ConvertGuidToNullableInt(auditEvent.MatterId.Value) : null,
            Timestamp = auditEvent.Timestamp
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Logged document audit event {EventType} for document {DocumentId}", auditEvent.EventType, auditEvent.DocumentId);
    }

    private static int? ConvertGuidToNullableInt(Guid value)
    {
        unchecked
        {
            return value.GetHashCode();
        }
    }
}

