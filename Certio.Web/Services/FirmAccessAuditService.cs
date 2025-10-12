using Certio.Domain.Audit;
using Certio.Domain.Organizations;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Services
{
    public class FirmAccessAuditService : IFirmAccessAuditService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<FirmAccessAuditService> _logger;

        public FirmAccessAuditService(ApplicationDbContext db, ILogger<FirmAccessAuditService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task LogFirmAccessAsync(int userId, int targetOrganizationId, string action, string? description = null, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    EntityType = "FirmAccess",
                    EntityId = targetOrganizationId,
                    Action = action,
                    UserId = userId,
                    Description = description ?? $"Firm-based access to organization {targetOrganizationId}",
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    Result = "SUCCESS",
                    Timestamp = DateTime.UtcNow
                };

                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Firm access logged: User {UserId} performed {Action} on organization {OrganizationId}", 
                    userId, action, targetOrganizationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging firm access for user {UserId} to organization {OrganizationId}", 
                    userId, targetOrganizationId);
            }
        }

        public async Task LogFirmRelationshipCreatedAsync(int relationshipId, int createdByUserId, string? description = null, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                var relationship = await _db.OrganizationRelationships
                    .Include(or => or.SourceOrganization)
                    .Include(or => or.TargetOrganization)
                    .FirstOrDefaultAsync(or => or.Id == relationshipId);

                if (relationship == null)
                {
                    _logger.LogWarning("Could not find relationship {RelationshipId} for audit logging", relationshipId);
                    return;
                }

                var auditLog = new AuditLog
                {
                    EntityType = "OrganizationRelationship",
                    EntityId = relationshipId,
                    Action = "CREATE",
                    UserId = createdByUserId,
                    Description = description ?? $"Created firm relationship between {relationship.SourceOrganization.Name} and {relationship.TargetOrganization.Name}",
                    NewValues = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        SourceOrganizationId = relationship.SourceOrganizationId,
                        TargetOrganizationId = relationship.TargetOrganizationId,
                        RelationshipType = relationship.RelationshipType,
                        AccessLevel = relationship.AccessLevel,
                        ExpiresAt = relationship.ExpiresAt
                    }),
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    Result = "SUCCESS",
                    Timestamp = DateTime.UtcNow
                };

                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Firm relationship created: {RelationshipId} by user {UserId}", 
                    relationshipId, createdByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging firm relationship creation {RelationshipId} by user {UserId}", 
                    relationshipId, createdByUserId);
            }
        }

        public async Task LogFirmRelationshipModifiedAsync(int relationshipId, int modifiedByUserId, string? description = null, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                var relationship = await _db.OrganizationRelationships
                    .Include(or => or.SourceOrganization)
                    .Include(or => or.TargetOrganization)
                    .FirstOrDefaultAsync(or => or.Id == relationshipId);

                if (relationship == null)
                {
                    _logger.LogWarning("Could not find relationship {RelationshipId} for audit logging", relationshipId);
                    return;
                }

                var auditLog = new AuditLog
                {
                    EntityType = "OrganizationRelationship",
                    EntityId = relationshipId,
                    Action = "UPDATE",
                    UserId = modifiedByUserId,
                    Description = description ?? $"Modified firm relationship between {relationship.SourceOrganization.Name} and {relationship.TargetOrganization.Name}",
                    NewValues = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        SourceOrganizationId = relationship.SourceOrganizationId,
                        TargetOrganizationId = relationship.TargetOrganizationId,
                        RelationshipType = relationship.RelationshipType,
                        AccessLevel = relationship.AccessLevel,
                        ExpiresAt = relationship.ExpiresAt,
                        IsActive = relationship.IsActive
                    }),
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    Result = "SUCCESS",
                    Timestamp = DateTime.UtcNow
                };

                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Firm relationship modified: {RelationshipId} by user {UserId}", 
                    relationshipId, modifiedByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging firm relationship modification {RelationshipId} by user {UserId}", 
                    relationshipId, modifiedByUserId);
            }
        }

        public async Task LogFirmRelationshipDeletedAsync(int relationshipId, int deletedByUserId, string? description = null, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                var relationship = await _db.OrganizationRelationships
                    .Include(or => or.SourceOrganization)
                    .Include(or => or.TargetOrganization)
                    .FirstOrDefaultAsync(or => or.Id == relationshipId);

                if (relationship == null)
                {
                    _logger.LogWarning("Could not find relationship {RelationshipId} for audit logging", relationshipId);
                    return;
                }

                var auditLog = new AuditLog
                {
                    EntityType = "OrganizationRelationship",
                    EntityId = relationshipId,
                    Action = "DELETE",
                    UserId = deletedByUserId,
                    Description = description ?? $"Deleted firm relationship between {relationship.SourceOrganization.Name} and {relationship.TargetOrganization.Name}",
                    OldValues = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        SourceOrganizationId = relationship.SourceOrganizationId,
                        TargetOrganizationId = relationship.TargetOrganizationId,
                        RelationshipType = relationship.RelationshipType,
                        AccessLevel = relationship.AccessLevel,
                        ExpiresAt = relationship.ExpiresAt
                    }),
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    Result = "SUCCESS",
                    Timestamp = DateTime.UtcNow
                };

                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Firm relationship deleted: {RelationshipId} by user {UserId}", 
                    relationshipId, deletedByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging firm relationship deletion {RelationshipId} by user {UserId}", 
                    relationshipId, deletedByUserId);
            }
        }

        public async Task LogFirmAccessDeniedAsync(int userId, int targetOrganizationId, string reason, string? description = null, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    EntityType = "FirmAccess",
                    EntityId = targetOrganizationId,
                    Action = "AUTH_FAILURE",
                    UserId = userId,
                    Description = description ?? $"FAILURE: {reason}",
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    Result = "FAILURE",
                    Timestamp = DateTime.UtcNow
                };

                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();

                _logger.LogWarning("Firm access denied: User {UserId} denied access to organization {OrganizationId} - {Reason}", 
                    userId, targetOrganizationId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging firm access denial for user {UserId} to organization {OrganizationId}", 
                    userId, targetOrganizationId);
            }
        }
    }
}
