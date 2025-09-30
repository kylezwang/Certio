using Certio.Domain.Organizations;

namespace Certio.Web.Services
{
    public interface IFirmRelationshipCacheService
    {
        Task<bool> HasFirmAccessAsync(int userId, int targetOrganizationId);
        Task<List<Organization>> GetAccessibleClientOrganizationsAsync(int userId);
        Task<OrganizationRelationship?> GetFirmRelationshipAsync(int userId, int targetOrganizationId);
        Task<ClientAccessInfo> GetClientAccessInfoAsync(int userId, int targetOrganizationId);
        Task InvalidateUserCacheAsync(int userId);
        Task InvalidateOrganizationCacheAsync(int organizationId);
        Task InvalidateRelationshipCacheAsync(int relationshipId);
    }

    /// <summary>
    /// Provides detailed information about how a user has access to a client organization
    /// </summary>
    public class ClientAccessInfo
    {
        public bool HasAccess { get; set; }
        public bool IsPartnerAccess { get; set; }
        public bool IsAssignedAccess { get; set; }
        public bool IsDirectMembership { get; set; }
        public string? AccessSummary { get; set; }
    }
}
