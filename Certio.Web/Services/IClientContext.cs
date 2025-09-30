using Certio.Domain.Users;
using Certio.Domain.Organizations;

namespace Certio.Web.Services
{
    public interface IClientContext
    {
        int? OrganizationId { get; }
        string? OrganizationName { get; }
        UserOrganization? Membership { get; }
        bool IsValid { get; }
        
        // Firm-based access properties
        bool IsFirmBasedAccess { get; }
        int? FirmOrganizationId { get; }
        string? FirmOrganizationName { get; }
        UserOrganization? FirmMembership { get; }
        OrganizationRelationship? FirmRelationship { get; }
        string AccessLevel { get; }
        
        // Access method tracking (for auditing and display)
        bool IsDirectMembershipAccess { get; }
        bool IsPartnerAccess { get; }
        bool IsAssignedAccess { get; }
    }
}


