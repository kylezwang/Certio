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

            // Check direct membership
            var membership = user.UserOrganizations
                .FirstOrDefault(uo => uo.OrganizationId == organizationId && uo.IsActive);

            var directPermissions = membership != null 
                ? user.GetEffectivePermissions(organizationId) 
                : new List<Permission>();

            // Check firm-based access for partner lawyers
            var firmPermissions = new List<Permission>();
            if (await HasFirmBasedAccessAsync(userId, organizationId))
            {
                var relationship = await GetFirmRelationshipAsync(userId, organizationId);
                if (relationship != null)
                {
                    // Grant permissions based on AccessLevel from the relationship
                    firmPermissions = GetPartnerPermissions(relationship.AccessLevel);
                    _logger.LogInformation(
                        "Firm-based access found for user {UserId} to organization {OrgId} with {AccessLevel} level ({PermissionCount} permissions)",
                        userId, organizationId, relationship.AccessLevel, firmPermissions.Count);
                }
                else
                {
                    _logger.LogWarning(
                        "HasFirmBasedAccessAsync returned true but GetFirmRelationshipAsync returned null for user {UserId} to org {OrgId}",
                        userId, organizationId);
                }
            }

            // Use whichever grants more permissions (union of both)
            var effectivePermissions = directPermissions.Union(firmPermissions).ToList();
            
            if (effectivePermissions.Any())
            {
                _logger.LogInformation(
                    "Effective permissions for user {UserId} in org {OrgId}: Direct={DirectCount}, Firm={FirmCount}, Final={FinalCount}",
                    userId, organizationId, directPermissions.Count, firmPermissions.Count, effectivePermissions.Count);
                return effectivePermissions;
            }

            _logger.LogWarning(
                "No permissions found for user {UserId} in organization {OrgId} - no direct membership or partner access",
                userId, organizationId);
            return new List<Permission>();
        }
        
        public async Task<Certio.Domain.Organizations.OrganizationRelationship?> GetFirmRelationshipAsync(int userId, int organizationId)
        {
            var lawFirmMembership = await _context.UserOrganizations
                .Include(uo => uo.Organization)
                    .ThenInclude(o => o.OrganizationRelationships)
                        .ThenInclude(r => r.TargetOrganization)
                .FirstOrDefaultAsync(uo => 
                    uo.UserId == userId && 
                    uo.IsActive && 
                    uo.UserType == UserTypes.LawFirm);

            if (lawFirmMembership == null)
            {
                return null;
            }

            var relationship = lawFirmMembership.Organization.OrganizationRelationships
                .FirstOrDefault(rel => 
                    rel.TargetOrganizationId == organizationId && 
                    rel.IsActive && 
                    !rel.IsDeleted &&
                    Certio.Domain.Organizations.RelationshipTypes.IsServiceProviderClient(rel.RelationshipType) &&
                    (!rel.ExpiresAt.HasValue || rel.ExpiresAt.Value > DateTime.UtcNow));

            return relationship;
        }
        
        private List<Permission> GetPartnerPermissions(string accessLevel)
        {
            // Grant permissions based on the relationship's AccessLevel
            var permissions = accessLevel switch
            {
                "Full" or "FullAccess" => new List<Permission>
                {
                    // Full access - all matter and document permissions
                    Permission.ViewMatters,
                    Permission.CreateMatters,
                    Permission.EditMatters,
                    Permission.DeleteMatters,
                    Permission.ManageMatterSettings,
                    Permission.ViewDocuments,
                    Permission.DownloadDocuments,
                    Permission.UploadDocuments,
                    Permission.DeleteDocuments,
                    Permission.CommentOnDocuments,
                    Permission.ViewMessages,
                    Permission.SendMessages,
                    Permission.ManageThreads
                },
                "Limited" or "LimitedAccess" => new List<Permission>
                {
                    // Limited access - view and basic operations
                    Permission.ViewMatters,
                    Permission.CreateMatters,
                    Permission.EditMatters,
                    Permission.ManageMatterSettings,
                    Permission.ViewDocuments,
                    Permission.DownloadDocuments,
                    Permission.UploadDocuments,
                    Permission.CommentOnDocuments,
                    Permission.ViewMessages,
                    Permission.SendMessages
                },
                "MatterSpecific" => new List<Permission>
                {
                    // Matter-specific access - can create and manage matters, view documents
                    Permission.ViewMatters,
                    Permission.CreateMatters,
                    Permission.EditMatters,
                    Permission.ManageMatterSettings,
                    Permission.ViewDocuments,
                    Permission.DownloadDocuments,
                    Permission.ViewMessages
                },
                "DocumentOnly" => new List<Permission>
                {
                    // Document-only access - can view/manage documents but not create matters
                    Permission.ViewMatters,
                    Permission.ViewDocuments,
                    Permission.DownloadDocuments,
                    Permission.UploadDocuments,
                    Permission.CommentOnDocuments,
                    Permission.ViewMessages
                },
                "ReadOnly" or "ReadOnlyAccess" => new List<Permission>
                {
                    // Read-only access
                    Permission.ViewMatters,
                    Permission.ViewDocuments,
                    Permission.DownloadDocuments,
                    Permission.ViewMessages
                },
                _ => new List<Permission>()
            };
            
            _logger.LogDebug(
                "GetPartnerPermissions: AccessLevel={AccessLevel}, PermissionCount={Count}, Permissions={Permissions}",
                accessLevel, permissions.Count, string.Join(", ", permissions));
            
            return permissions;
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

                // Check if user has access to the organization (direct membership or firm-based access)
                var isInOrg = await IsOrganizationMemberAsync(userId, matter.OrganizationId);
                var hasFirmAccess = await HasFirmBasedAccessAsync(userId, matter.OrganizationId);
                
                if (!isInOrg && !hasFirmAccess)
                {
                    _logger.LogWarning("User {UserId} has no access to organization {OrgId} for matter {MatterId}", 
                        userId, matter.OrganizationId, matterId);
                    return false;
                }

                // Matter creators always have access to their own matters
                if (matter.CreatedById.HasValue && matter.CreatedById.Value == userId)
                {
                    _logger.LogDebug("User {UserId} granted access to matter {MatterId} as creator", userId, matterId);
                    return true;
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

                // Check if user has access to the task's organization (direct or firm-based)
                var isInOrg = await IsOrganizationMemberAsync(userId, task.OrgId);
                var hasFirmAccess = await HasFirmBasedAccessAsync(userId, task.OrgId);
                
                if (!isInOrg && !hasFirmAccess)
                {
                    return false;
                }

                // Check if user is directly assigned to the task
                var isDirectlyAssigned = task.TaskAssignments.Any(a => 
                    a.UserId == userId && 
                    a.RemovedAt == null);
                
                if (isDirectlyAssigned)
                {
                    _logger.LogDebug("User {UserId} granted access to task {TaskId} via direct assignment", userId, taskId);
                    return true;
                }

                // Also need to check matter access
                return await CanAccessMatterAsync(userId, task.MatterId);
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
            // Get user's law firm or event planner membership
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
                    Certio.Domain.Organizations.RelationshipTypes.IsServiceProviderClient(rel.RelationshipType) &&
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
                        Certio.Domain.Organizations.RelationshipTypes.IsServiceProviderClient(rel.RelationshipType) &&
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

        public async Task ValidateMatterAccessOrThrowAsync(int userId, int matterId, string operation)
        {
            if (!await CanAccessMatterAsync(userId, matterId))
            {
                throw new UnauthorizedOperationException(
                    userId, 
                    operation, 
                    "MatterAccess", 
                    $"User does not have access to matter {matterId}");
            }
        }

        public async Task ValidateTaskAccessOrThrowAsync(int userId, int taskId, string operation)
        {
            if (!await CanAccessTaskAsync(userId, taskId))
            {
                throw new UnauthorizedOperationException(
                    userId, 
                    operation, 
                    "TaskAccess", 
                    $"User does not have access to task {taskId}");
            }
        }

        public async Task<bool> CanPerformOperationAsync(int userId, int matterId, Permission permission)
        {
            // First check if user can access the matter
            if (!await CanAccessMatterAsync(userId, matterId))
            {
                return false;
            }

            // Then check if user has the required permission
            var matter = await _context.Matters
                .FirstOrDefaultAsync(m => m.Id == matterId);

            if (matter == null)
            {
                return false;
            }

            return await HasPermissionAsync(userId, matter.OrganizationId, permission);
        }

        public async Task<bool> CanPerformTaskOperationAsync(int userId, int taskId, Permission permission)
        {
            // First check if user can access the task
            if (!await CanAccessTaskAsync(userId, taskId))
            {
                return false;
            }

            // Get the task and its organization
            var task = await _context.TaskItems
                .Include(t => t.Matter)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                return false;
            }

            // Check if user has the required permission in the organization
            return await HasPermissionAsync(userId, task.OrgId, permission);
        }
    }
}

