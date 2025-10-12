using Certio.Application.Interfaces;
using Certio.Domain.Users;
using Certio.Domain.Exceptions;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PermissionService> _logger;

        public PermissionService(
            ApplicationDbContext context,
            ILogger<PermissionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> HasPermissionAsync(int userId, int organizationId, Permission permission)
        {
            var permissions = await GetEffectivePermissionsAsync(userId, organizationId);
            return permissions.Contains(permission);
        }

        public async Task<List<Permission>> GetEffectivePermissionsAsync(int userId, int organizationId)
        {
            var user = await _context.Users
                .Include(u => u.UserOrganizations)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return new List<Permission>();
            }

            var membership = user.UserOrganizations
                .FirstOrDefault(uo => uo.OrganizationId == organizationId && uo.IsActive);

            if (membership == null)
            {
                return new List<Permission>();
            }

            return user.GetEffectivePermissions(organizationId);
        }

        public async Task<bool> CanAccessMatterAsync(int userId, int matterId)
        {
            try
            {
                var matter = await _context.Matters
                    .Include(m => m.Permissions)
                    .Include(m => m.Assignments)
                    .FirstOrDefaultAsync(m => m.Id == matterId);

                if (matter == null)
                {
                    return false;
                }

                // Check if user is in the organization
                var isInOrg = await IsOrganizationMemberAsync(userId, matter.OrganizationId);
                if (!isInOrg)
                {
                    return false;
                }

                // If AccessLevel is "Everyone", org membership is sufficient
                if (matter.AccessLevel == "Everyone")
                {
                    return true;
                }

                // If AccessLevel is "Specific", check MatterPermissions
                if (matter.AccessLevel == "Specific")
                {
                    var hasPermission = matter.Permissions.Any(p => 
                        p.UserId == userId && 
                        p.RevokedAt == null);

                    if (hasPermission)
                    {
                        return true;
                    }

                    // Also check if user is assigned to the matter
                    var isAssigned = matter.Assignments.Any(a => 
                        a.UserId == userId && 
                        a.RemovedAt == null);

                    return isAssigned;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking matter access for user {UserId}, matter {MatterId}", userId, matterId);
                return false;
            }
        }

        public async Task<bool> CanPerformMatterOperationAsync(int userId, int matterId, string operation)
        {
            // First check if user can access the matter
            if (!await CanAccessMatterAsync(userId, matterId))
            {
                return false;
            }

            var matter = await _context.Matters
                .FirstOrDefaultAsync(m => m.Id == matterId);

            if (matter == null)
            {
                return false;
            }

            // Map operation to permission
            Permission requiredPermission = operation.ToLower() switch
            {
                "create" => Permission.CreateMatters,
                "edit" or "update" => Permission.EditMatters,
                "delete" => Permission.DeleteMatters,
                "view" => Permission.ViewMatters,
                "manage" => Permission.ManageMatterSettings,
                _ => Permission.ViewMatters
            };

            return await HasPermissionAsync(userId, matter.OrganizationId, requiredPermission);
        }

        public async Task<bool> CanAccessTaskAsync(int userId, int taskId)
        {
            try
            {
                var task = await _context.TaskItems
                    .Include(t => t.Matter)
                    .Include(t => t.TaskAssignments)
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    return false;
                }

                // Check if user is in the task's organization
                var isInOrg = await IsOrganizationMemberAsync(userId, task.OrgId);
                if (isInOrg)
                {
                    // Also need to check matter access
                    return await CanAccessMatterAsync(userId, task.MatterId);
                }

                // Check firm-based access
                var hasFirmAccess = await HasFirmBasedAccessAsync(userId, task.OrgId);
                if (hasFirmAccess)
                {
                    return await CanAccessMatterAsync(userId, task.MatterId);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking task access for user {UserId}, task {TaskId}", userId, taskId);
                return false;
            }
        }

        public async Task<bool> CanAccessSubTaskAsync(int userId, int subTaskId)
        {
            try
            {
                var subTask = await _context.SubTaskItems
                    .FirstOrDefaultAsync(st => st.Id == subTaskId);

                if (subTask == null)
                {
                    return false;
                }

                // Access to subtask is determined by access to parent task
                return await CanAccessTaskAsync(userId, subTask.TaskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking subtask access for user {UserId}, subtask {SubTaskId}", userId, subTaskId);
                return false;
            }
        }

        public async Task<bool> IsOrganizationMemberAsync(int userId, int organizationId)
        {
            return await _context.UserOrganizations
                .AnyAsync(uo => uo.UserId == userId && 
                               uo.OrganizationId == organizationId && 
                               uo.IsActive);
        }

        public async Task<bool> HasFirmBasedAccessAsync(int userId, int organizationId)
        {
            // Get user's law firm membership
            var lawFirmMembership = await _context.UserOrganizations
                .Include(uo => uo.Organization)
                    .ThenInclude(o => o.OrganizationRelationships)
                .FirstOrDefaultAsync(uo => 
                    uo.UserId == userId && 
                    uo.IsActive && 
                    uo.UserType == UserTypes.LawFirm);

            if (lawFirmMembership == null)
            {
                return false;
            }

            // Check if there's a valid relationship to the target organization
            var hasRelationship = lawFirmMembership.Organization.OrganizationRelationships
                .Any(rel => 
                    rel.TargetOrganizationId == organizationId && 
                    rel.IsActive && 
                    !rel.IsDeleted &&
                    rel.RelationshipType == Certio.Domain.Organizations.RelationshipTypes.LawFirmClient &&
                    (!rel.ExpiresAt.HasValue || rel.ExpiresAt.Value > DateTime.UtcNow));

            return hasRelationship;
        }

        public async Task<List<int>> GetAccessibleOrganizationIdsAsync(int userId)
        {
            var organizationIds = new List<int>();

            // Add direct memberships
            var directOrgs = await _context.UserOrganizations
                .Where(uo => uo.UserId == userId && uo.IsActive)
                .Select(uo => uo.OrganizationId)
                .ToListAsync();

            organizationIds.AddRange(directOrgs);

            // Add firm-based access
            var lawFirmMembership = await _context.UserOrganizations
                .Include(uo => uo.Organization)
                    .ThenInclude(o => o.OrganizationRelationships)
                .FirstOrDefaultAsync(uo => 
                    uo.UserId == userId && 
                    uo.IsActive && 
                    uo.UserType == UserTypes.LawFirm);

            if (lawFirmMembership != null)
            {
                var clientOrgIds = lawFirmMembership.Organization.OrganizationRelationships
                    .Where(rel => 
                        rel.IsActive && 
                        !rel.IsDeleted &&
                        rel.RelationshipType == Certio.Domain.Organizations.RelationshipTypes.LawFirmClient &&
                        (!rel.ExpiresAt.HasValue || rel.ExpiresAt.Value > DateTime.UtcNow))
                    .Select(rel => rel.TargetOrganizationId)
                    .ToList();

                organizationIds.AddRange(clientOrgIds);
            }

            return organizationIds.Distinct().ToList();
        }

        public async Task ValidatePermissionOrThrowAsync(int userId, int organizationId, Permission permission, string operation)
        {
            if (!await HasPermissionAsync(userId, organizationId, permission))
            {
                throw new UnauthorizedOperationException(
                    userId, 
                    operation, 
                    permission.ToString(), 
                    $"User lacks required permission: {permission}");
            }
        }
    }
}

