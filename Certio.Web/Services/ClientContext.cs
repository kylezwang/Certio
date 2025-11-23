using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Services
{
    public sealed class ClientContext : IClientContext
    {
        public int? OrganizationId { get; private set; }
        public string? OrganizationName { get; private set; }
        public UserOrganization? Membership { get; private set; }
        public bool IsValid => OrganizationId.HasValue && (Membership != null || IsFirmBasedAccess);
        
        // Firm-based access properties
        public bool IsFirmBasedAccess { get; private set; }
        public int? FirmOrganizationId { get; private set; }
        public string? FirmOrganizationName { get; private set; }
        public UserOrganization? FirmMembership { get; private set; }
        public OrganizationRelationship? FirmRelationship { get; private set; }
        public string AccessLevel { get; private set; } = "";
        
        // Access method tracking (for auditing and display)
        public bool IsDirectMembershipAccess { get; private set; }
        public bool IsPartnerAccess { get; private set; }
        public bool IsAssignedAccess { get; private set; }

        public static async Task<ClientContext> CreateAsync(HttpContext httpContext, ApplicationDbContext db, IFirmRelationshipCacheService cacheService, CancellationToken ct)
        {
            var context = new ClientContext();
            if (!httpContext.Items.TryGetValue("CurrentOrganizationId", out var orgObj) || orgObj is not int orgId)
            {
                return context; // invalid; fail-closed by auth policy
            }

            var customUser = httpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return context;
            }

            // First, try direct membership
            var membership = await db.UserOrganizations
                .Include(uo => uo.Organization)
                .FirstOrDefaultAsync(uo => uo.UserId == customUser.Id && uo.OrganizationId == orgId && uo.IsActive, ct);

            if (membership != null)
            {
                context.OrganizationId = orgId;
                context.OrganizationName = membership.Organization.Name;
                context.Membership = membership;
                
                // Check if this is also firm-based access and get access info
                var accessInfo = await cacheService.GetClientAccessInfoAsync(customUser.Id, orgId);
                context.IsDirectMembershipAccess = accessInfo.IsDirectMembership;
                context.IsPartnerAccess = accessInfo.IsPartnerAccess;
                context.IsAssignedAccess = accessInfo.IsAssignedAccess;
                
                return context;
            }

            // If no direct membership, check for firm-based access using cache
            var hasFirmAccess = await cacheService.HasFirmAccessAsync(customUser.Id, orgId);
            if (hasFirmAccess)
            {
                var firmRelationship = await cacheService.GetFirmRelationshipAsync(customUser.Id, orgId);
                if (firmRelationship != null)
                {
                    var accessInfo = await cacheService.GetClientAccessInfoAsync(customUser.Id, orgId);
                    
                    context.OrganizationId = orgId;
                    context.OrganizationName = firmRelationship.TargetOrganization.Name;
                    context.IsFirmBasedAccess = true;
                    context.FirmOrganizationId = firmRelationship.SourceOrganizationId;
                    context.FirmOrganizationName = firmRelationship.SourceOrganization.Name;
                    context.FirmMembership = firmRelationship.SourceOrganization.UserOrganizations
                        .FirstOrDefault(uo => uo.UserId == customUser.Id && uo.IsActive);
                    context.FirmRelationship = firmRelationship;
                    context.AccessLevel = firmRelationship.AccessLevel;
                    
                    // Track access method
                    context.IsDirectMembershipAccess = accessInfo.IsDirectMembership;
                    context.IsPartnerAccess = accessInfo.IsPartnerAccess;
                    context.IsAssignedAccess = accessInfo.IsAssignedAccess;
                }
            }

            return context;
        }

        private static async Task<OrganizationRelationship?> CheckFirmBasedAccess(
            User user, 
            int targetOrganizationId, 
            ApplicationDbContext db, 
            CancellationToken ct)
        {
            // Get user's law firm membership
            var lawFirmMembership = await db.UserOrganizations
                .Include(uo => uo.Organization)
                .ThenInclude(o => o.OrganizationRelationships)
                .ThenInclude(or => or.TargetOrganization)
                .FirstOrDefaultAsync(uo => 
                    uo.UserId == user.Id && 
                    uo.IsActive && 
                    uo.UserType == UserTypes.LawFirm, 
                    ct);

            if (lawFirmMembership == null)
                return null;

            // Check if there's a valid relationship between the law firm and target organization
            var relationship = lawFirmMembership.Organization.OrganizationRelationships
                .FirstOrDefault(rel => 
                    rel.TargetOrganizationId == targetOrganizationId && 
                    rel.IsValid() && 
                    RelationshipTypes.IsServiceProviderClient(rel.RelationshipType));

            return relationship;
        }
    }
}


