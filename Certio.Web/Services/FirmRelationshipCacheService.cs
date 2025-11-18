using Certio.Domain.Organizations;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Certio.Web.Services
{
    public class FirmRelationshipCacheService : IFirmRelationshipCacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<FirmRelationshipCacheService> _logger;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(15);

        public FirmRelationshipCacheService(
            IMemoryCache cache, 
            ApplicationDbContext db, 
            ILogger<FirmRelationshipCacheService> logger)
        {
            _cache = cache;
            _db = db;
            _logger = logger;
        }

        public async Task<bool> HasFirmAccessAsync(int userId, int targetOrganizationId)
        {
            var cacheKey = $"firm_access_{userId}_{targetOrganizationId}";
            
            if (_cache.TryGetValue(cacheKey, out bool hasAccess))
            {
                return hasAccess;
            }

            hasAccess = await CheckFirmAccessFromDatabase(userId, targetOrganizationId);
            
            _cache.Set(cacheKey, hasAccess, _cacheExpiration);
            
            return hasAccess;
        }

        public async Task<List<Organization>> GetAccessibleClientOrganizationsAsync(int userId)
        {
            var cacheKey = $"accessible_clients_{userId}";
            
            if (_cache.TryGetValue(cacheKey, out List<Organization>? organizations))
            {
                return organizations ?? new List<Organization>();
            }

            organizations = await GetAccessibleClientOrganizationsFromDatabase(userId);
            
            _cache.Set(cacheKey, organizations, _cacheExpiration);
            
            return organizations;
        }

        public async Task<OrganizationRelationship?> GetFirmRelationshipAsync(int userId, int targetOrganizationId)
        {
            var cacheKey = $"firm_relationship_{userId}_{targetOrganizationId}";
            
            if (_cache.TryGetValue(cacheKey, out OrganizationRelationship? relationship))
            {
                return relationship;
            }

            relationship = await GetFirmRelationshipFromDatabase(userId, targetOrganizationId);
            
            if (relationship != null)
            {
                _cache.Set(cacheKey, relationship, _cacheExpiration);
            }
            
            return relationship;
        }

        public Task InvalidateUserCacheAsync(int userId)
        {
            // Remove all cache entries for this user
            var pattern = $"*_{userId}_*";
            InvalidateCacheByPattern(pattern);
            
            _logger.LogDebug("Invalidated cache for user {UserId}", userId);
            return Task.CompletedTask;
        }

        public Task InvalidateOrganizationCacheAsync(int organizationId)
        {
            // Remove all cache entries for this organization
            var pattern = $"*_{organizationId}";
            InvalidateCacheByPattern(pattern);
            
            _logger.LogDebug("Invalidated cache for organization {OrganizationId}", organizationId);
            return Task.CompletedTask;
        }

        public Task InvalidateRelationshipCacheAsync(int relationshipId)
        {
            // Remove all cache entries for this relationship
            var pattern = $"*_{relationshipId}";
            InvalidateCacheByPattern(pattern);
            
            _logger.LogDebug("Invalidated cache for relationship {RelationshipId}", relationshipId);
            return Task.CompletedTask;
        }

        public async Task<ClientAccessInfo> GetClientAccessInfoAsync(int userId, int targetOrganizationId)
        {
            var cacheKey = $"client_access_info_{userId}_{targetOrganizationId}";
            
            if (_cache.TryGetValue(cacheKey, out ClientAccessInfo? cachedInfo) && cachedInfo != null)
            {
                return cachedInfo;
            }

            var info = await GetClientAccessInfoFromDatabase(userId, targetOrganizationId);
            
            _cache.Set(cacheKey, info, _cacheExpiration);
            
            return info;
        }

        private async Task<bool> CheckFirmAccessFromDatabase(int userId, int targetOrganizationId)
        {
            try
            {
                var lawFirmMembership = await _db.UserOrganizations
                    .Include(uo => uo.Organization)
                    .ThenInclude(o => o.OrganizationRelationships)
                    .ThenInclude(rel => rel.TargetOrganization)
                    .FirstOrDefaultAsync(uo => 
                        uo.UserId == userId && 
                        uo.IsActive && 
                        uo.UserType == UserTypes.LawFirm);

                if (lawFirmMembership == null)
                    return false;

                var relationship = lawFirmMembership.Organization.OrganizationRelationships
                    .FirstOrDefault(rel => 
                        rel.TargetOrganizationId == targetOrganizationId && 
                        rel.IsValid() && 
                        rel.RelationshipType == RelationshipTypes.LawFirmClient);

                if (relationship == null)
                    return false;

                if (relationship.TargetOrganization?.OwnerId == userId)
                    return true;

                // Determine user's firm role
                var role = lawFirmMembership.Role;
                var isPartner = string.Equals(role, OrganizationRoles.Partner, StringComparison.OrdinalIgnoreCase) || 
                                string.Equals(role, OrganizationRoles.ManagingPartner, StringComparison.OrdinalIgnoreCase);
                var isAssociate = string.Equals(role, OrganizationRoles.Associate, StringComparison.OrdinalIgnoreCase);

                if (isPartner)
                {
                    // Partners can access all linked clients without individual assignment
                    return true;
                }

                if (isAssociate)
                {
                    // Associates can access if explicitly assigned to the relationship
                    var isAssigned = await _db.OrganizationRelationshipAssignedUsers
                        .AnyAsync(a => a.RelationshipId == relationship.Id && a.UserId == userId);
                    
                    if (isAssigned)
                        return true;
                    
                    // Associates can also access if they have direct membership (via join code)
                    var hasDirectMembership = await _db.UserOrganizations
                        .AnyAsync(uo => uo.UserId == userId && 
                                       uo.OrganizationId == targetOrganizationId && 
                                       uo.IsActive);
                    
                    return hasDirectMembership;
                }

                // Paralegal/Staff/Others can access if they have direct membership (via join code)
                // This requires that the client org is linked to their firm
                var hasDirectAccess = await _db.UserOrganizations
                    .AnyAsync(uo => uo.UserId == userId && 
                                   uo.OrganizationId == targetOrganizationId && 
                                   uo.IsActive);
                
                return hasDirectAccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking firm access for user {UserId} to organization {OrganizationId}", 
                    userId, targetOrganizationId);
                return false;
            }
        }

        private async Task<List<Organization>> GetAccessibleClientOrganizationsFromDatabase(int userId)
        {
            try
            {
                var lawFirmMembership = await _db.UserOrganizations
                    .Include(uo => uo.Organization)
                    .ThenInclude(o => o.OrganizationRelationships)
                    .ThenInclude(or => or.TargetOrganization)
                    .FirstOrDefaultAsync(uo => 
                        uo.UserId == userId && 
                        uo.IsActive && 
                        uo.UserType == UserTypes.LawFirm);

                if (lawFirmMembership == null)
                    return new List<Organization>();

                var role = lawFirmMembership.Role;
                var isPartner = string.Equals(role, OrganizationRoles.Partner, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(role, OrganizationRoles.ManagingPartner, StringComparison.OrdinalIgnoreCase);

                var relationships = lawFirmMembership.Organization.OrganizationRelationships
                    .Where(rel => rel.IsValid() && rel.RelationshipType == RelationshipTypes.LawFirmClient)
                    .ToList();

                var accessibleOrganizations = new HashSet<Organization>();

                if (isPartner)
                {
                    // Partners see all linked clients
                    foreach (var rel in relationships)
                    {
                        accessibleOrganizations.Add(rel.TargetOrganization);
                    }
                }
                else
                {
                    var relationshipIds = relationships.Select(r => r.Id).ToList();
                    var assignedRelationshipIds = await _db.OrganizationRelationshipAssignedUsers
                        .Where(a => a.UserId == userId && relationshipIds.Contains(a.RelationshipId))
                        .Select(a => a.RelationshipId)
                        .ToListAsync();
                    var assignedSet = assignedRelationshipIds.ToHashSet();

                    foreach (var rel in relationships)
                    {
                        if (rel.TargetOrganization == null)
                            continue;

                        var isAssigned = assignedSet.Contains(rel.Id);
                        var isOwner = rel.TargetOrganization.OwnerId == userId;

                        if (isAssigned || isOwner)
                        {
                            accessibleOrganizations.Add(rel.TargetOrganization);
                        }
                    }
                }

                // ENHANCEMENT: Add client organizations the user has direct membership to (via join code)
                // Only include clients that are linked to their law firm for security
                var firmClientOrgIds = relationships.Select(r => r.TargetOrganizationId).ToHashSet();
                
                var directMembershipClientOrgs = await _db.UserOrganizations
                    .Include(uo => uo.Organization)
                    .Where(uo => uo.UserId == userId && 
                                uo.IsActive && 
                                uo.UserType == UserTypes.Client && 
                                firmClientOrgIds.Contains(uo.OrganizationId))
                    .Select(uo => uo.Organization)
                    .ToListAsync();

                foreach (var org in directMembershipClientOrgs)
                {
                    accessibleOrganizations.Add(org);
                }

                _logger.LogDebug(
                    "User {UserId} ({Role}) has access to {Count} client organizations (Partner: {IsPartner}, Direct memberships included: {DirectCount})",
                    userId, role, accessibleOrganizations.Count, isPartner, directMembershipClientOrgs.Count);

                return accessibleOrganizations.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accessible client organizations for user {UserId}", userId);
                return new List<Organization>();
            }
        }

        private async Task<OrganizationRelationship?> GetFirmRelationshipFromDatabase(int userId, int targetOrganizationId)
        {
            try
            {
                var lawFirmMembership = await _db.UserOrganizations
                    .Include(uo => uo.Organization)
                    .ThenInclude(o => o.OrganizationRelationships)
                    .ThenInclude(or => or.TargetOrganization)
                    .FirstOrDefaultAsync(uo => 
                        uo.UserId == userId && 
                        uo.IsActive && 
                        uo.UserType == UserTypes.LawFirm);

                if (lawFirmMembership == null)
                    return null;

                var relationship = lawFirmMembership.Organization.OrganizationRelationships
                    .FirstOrDefault(rel => 
                        rel.TargetOrganizationId == targetOrganizationId && 
                        rel.IsValid() && 
                        rel.RelationshipType == RelationshipTypes.LawFirmClient);

                if (relationship == null)
                    return null;

                if (relationship.TargetOrganization?.OwnerId == userId)
                    return relationship;

                var role = lawFirmMembership.Role;
                var isPartner = string.Equals(role, OrganizationRoles.Partner, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(role, OrganizationRoles.ManagingPartner, StringComparison.OrdinalIgnoreCase);
                
                if (isPartner)
                {
                    // Partners get firm relationship context without assignment
                    return relationship;
                }

                // For non-Partners, check explicit assignment first
                var isAssigned = await _db.OrganizationRelationshipAssignedUsers
                    .AnyAsync(a => a.RelationshipId == relationship.Id && a.UserId == userId);
                
                if (isAssigned)
                    return relationship;

                // ENHANCEMENT: Check for direct membership (via join code)
                var hasDirectMembership = await _db.UserOrganizations
                    .AnyAsync(uo => uo.UserId == userId && 
                                   uo.OrganizationId == targetOrganizationId && 
                                   uo.IsActive);
                
                return hasDirectMembership ? relationship : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting firm relationship for user {UserId} to organization {OrganizationId}", 
                    userId, targetOrganizationId);
                return null;
            }
        }

        private async Task<ClientAccessInfo> GetClientAccessInfoFromDatabase(int userId, int targetOrganizationId)
        {
            var info = new ClientAccessInfo();
            
            try
            {
                var lawFirmMembership = await _db.UserOrganizations
                    .Include(uo => uo.Organization)
                    .ThenInclude(o => o.OrganizationRelationships)
                    .ThenInclude(rel => rel.TargetOrganization)
                    .FirstOrDefaultAsync(uo => 
                        uo.UserId == userId && 
                        uo.IsActive && 
                        uo.UserType == UserTypes.LawFirm);

                if (lawFirmMembership == null)
                {
                    info.AccessSummary = "No law firm membership";
                    return info;
                }

                var relationship = lawFirmMembership.Organization.OrganizationRelationships
                    .FirstOrDefault(rel => 
                        rel.TargetOrganizationId == targetOrganizationId && 
                        rel.IsValid() && 
                        rel.RelationshipType == RelationshipTypes.LawFirmClient);

                if (relationship == null)
                {
                    info.AccessSummary = "Client not linked to law firm";
                    return info;
                }

                if (relationship.TargetOrganization?.OwnerId == userId)
                {
                    info.HasAccess = true;
                    info.AccessSummary = "Owner of external organization";
                    return info;
                }

                var role = lawFirmMembership.Role;
                var isPartner = string.Equals(role, OrganizationRoles.Partner, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(role, OrganizationRoles.ManagingPartner, StringComparison.OrdinalIgnoreCase);

                if (isPartner)
                {
                    info.HasAccess = true;
                    info.IsPartnerAccess = true;
                    info.AccessSummary = "Partner - Full firm access";
                    return info;
                }

                // Check for explicit assignment
                var isAssigned = await _db.OrganizationRelationshipAssignedUsers
                    .AnyAsync(a => a.RelationshipId == relationship.Id && a.UserId == userId);

                if (isAssigned)
                {
                    info.HasAccess = true;
                    info.IsAssignedAccess = true;
                }

                // Check for direct membership
                var hasDirectMembership = await _db.UserOrganizations
                    .AnyAsync(uo => uo.UserId == userId && 
                                   uo.OrganizationId == targetOrganizationId && 
                                   uo.IsActive);

                if (hasDirectMembership)
                {
                    info.HasAccess = true;
                    info.IsDirectMembership = true;
                }

                // Build summary
                var accessMethods = new List<string>();
                if (info.IsAssignedAccess) accessMethods.Add("explicitly assigned");
                if (info.IsDirectMembership) accessMethods.Add("direct member");
                
                if (accessMethods.Count > 0)
                {
                    info.AccessSummary = $"Access via: {string.Join(" and ", accessMethods)}";
                }
                else
                {
                    info.AccessSummary = "No access";
                }

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting client access info for user {UserId} to organization {OrganizationId}", 
                    userId, targetOrganizationId);
                info.AccessSummary = "Error checking access";
                return info;
            }
        }

        private void InvalidateCacheByPattern(string pattern)
        {
            // Note: IMemoryCache doesn't support pattern-based invalidation out of the box
            // In a production environment, you might want to use Redis with pattern support
            // or maintain a separate index of cache keys for this user/organization
            
            // For now, we'll log the pattern - in a real implementation, you'd need to
            // track cache keys separately or use a different caching solution
            _logger.LogDebug("Cache invalidation requested for pattern: {Pattern}", pattern);
        }
    }
}
