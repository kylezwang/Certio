using Certio.Domain.Organizations;

namespace Certio.Web.Services
{
    public interface IFirmAccessAuditService
    {
        Task LogFirmAccessAsync(int userId, int targetOrganizationId, string action, string? description = null, string? ipAddress = null, string? userAgent = null);
        Task LogFirmRelationshipCreatedAsync(int relationshipId, int createdByUserId, string? description = null, string? ipAddress = null, string? userAgent = null);
        Task LogFirmRelationshipModifiedAsync(int relationshipId, int modifiedByUserId, string? description = null, string? ipAddress = null, string? userAgent = null);
        Task LogFirmRelationshipDeletedAsync(int relationshipId, int deletedByUserId, string? description = null, string? ipAddress = null, string? userAgent = null);
        Task LogFirmAccessDeniedAsync(int userId, int targetOrganizationId, string reason, string? description = null, string? ipAddress = null, string? userAgent = null);
    }
}
