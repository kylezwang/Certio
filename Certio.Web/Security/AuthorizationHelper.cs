using Certio.Domain.Users;
using Certio.Domain.Matters;
using Certio.Domain.Tasks;
using Certio.Web.Data;
using Certio.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Security
{
    /// <summary>
    /// Helper class for validating user authorization and resource ownership
    /// Centralizes security checks to prevent IDOR vulnerabilities
    /// 
    /// SECURITY MODEL:
    /// ===============
    /// 
    /// MATTERS:
    /// - Users see matters where:
    ///   1. Matter.AccessLevel = "Everyone" (all org members)
    ///   2. User has MatterPermission (for "Specific" access level)
    ///   3. User has MatterAssignment (assigned to matter)
    /// 
    /// TASKS:
    /// - Users see tasks where:
    ///   1. User has TaskAssignment (assigned to task)
    ///   2. User has MatterAssignment (assigned to parent matter)
    ///   3. Parent matter has AccessLevel = "Everyone"
    ///   4. User has MatterPermission for parent matter
    /// 
    /// FILTERING:
    /// - List-level filtering is done in Controllers (Index actions)
    /// - Item-level authorization is done here (Get/Edit/Delete actions)
    /// - Both must be consistent to prevent security vulnerabilities
    /// </summary>
    public class AuthorizationHelper
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly ILogger<AuthorizationHelper> _logger;

        public AuthorizationHelper(
            ApplicationDbContext context, 
            IAuditService auditService,
            ILogger<AuthorizationHelper> logger)
        {
            _context = context;
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>
        /// Validates that user has access to a matter through their organization
        /// </summary>
        public async Task<bool> ValidateUserCanAccessMatterAsync(int matterId, int userId, int organizationId, HttpContext? httpContext = null)
        {
            try
            {
                // Check if matter exists and belongs to the organization
                var matter = await _context.Matters
                    .Where(m => m.Id == matterId && m.OrganizationId == organizationId)
                    .FirstOrDefaultAsync();

                if (matter == null)
                {
                    var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
                    var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
                    await _auditService.LogAuthorizationFailureAsync(
                        userId, organizationId, "Matter", matterId, "MatterNotInOrganization", ipAddress, userAgent);
                    return false;
                }

                // Check if matter has specific access level restrictions
                if (matter.AccessLevel == "Specific")
                {
                    var hasPermission = await _context.MatterPermissions
                        .AnyAsync(mp => mp.MatterId == matterId && 
                                       mp.UserId == userId && 
                                       mp.RevokedAt == null);

                    if (!hasPermission)
                    {
                        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
                        var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
                        await _auditService.LogAuthorizationFailureAsync(
                            userId, organizationId, "Matter", matterId, "NoSpecificPermission", ipAddress, userAgent);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating matter access for user {UserId}, matter {MatterId}", userId, matterId);
                return false;
            }
        }

        /// <summary>
        /// Validates that user can access a task through organization or matter assignment
        /// </summary>
        public async Task<bool> ValidateUserCanAccessTaskAsync(int taskId, int userId, HttpContext? httpContext = null)
        {
            try
            {
                var task = await _context.TaskItems
                    .Include(t => t.Matter)
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
                    var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
                    await _auditService.LogAuthorizationFailureAsync(
                        userId, 0, "Task", taskId, "TaskNotFound", ipAddress, userAgent);
                    return false;
                }

                // Check if user is in the task's organization
                var isInOrganization = await _context.UserOrganizations
                    .AnyAsync(uo => uo.UserId == userId && 
                                   uo.OrganizationId == task.OrgId && 
                                   uo.IsActive);

                if (isInOrganization)
                    return true;

                // Check if user has firm-based access to the organization
                var hasFirmAccess = await HasFirmBasedAccessAsync(userId, task.OrgId);
                if (hasFirmAccess)
                    return true;

                // Check if user is assigned to the task's matter
                if (task.MatterId > 0)
                {
                    var isAssignedToMatter = await _context.MatterAssignments
                        .AnyAsync(ma => ma.MatterId == task.MatterId && ma.UserId == userId);

                    if (isAssignedToMatter)
                        return true;
                }

                var ip = httpContext?.Connection?.RemoteIpAddress?.ToString();
                var ua = httpContext?.Request?.Headers["User-Agent"].ToString();
                await _auditService.LogAuthorizationFailureAsync(
                    userId, task.OrgId, "Task", taskId, "NoAccess", ip, ua);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating task access for user {UserId}, task {TaskId}", userId, taskId);
                return false;
            }
        }

        /// <summary>
        /// Validates that user can access a subtask through parent task access
        /// </summary>
        public async Task<bool> ValidateUserCanAccessSubTaskAsync(int subTaskId, int userId, HttpContext? httpContext = null)
        {
            try
            {
                var subTask = await _context.SubTaskItems
                    .FirstOrDefaultAsync(st => st.Id == subTaskId);

                if (subTask == null)
                {
                    var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
                    var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
                    await _auditService.LogAuthorizationFailureAsync(
                        userId, 0, "SubTask", subTaskId, "SubTaskNotFound", ipAddress, userAgent);
                    return false;
                }

                // Validate access to parent task
                return await ValidateUserCanAccessTaskAsync(subTask.TaskId, userId, httpContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating subtask access for user {UserId}, subtask {SubTaskId}", userId, subTaskId);
                return false;
            }
        }

        /// <summary>
        /// Validates that user can access a conversation
        /// </summary>
        public async Task<bool> ValidateUserCanAccessConversationAsync(int conversationId, int userId, int organizationId, HttpContext? httpContext = null)
        {
            try
            {
                var conversation = await _context.Conversations
                    .Include(c => c.Participants)
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                if (conversation == null)
                {
                    var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
                    var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
                    await _auditService.LogAuthorizationFailureAsync(
                        userId, organizationId, "Conversation", conversationId, "ConversationNotFound", ipAddress, userAgent);
                    return false;
                }

                // Check if conversation belongs to the organization
                if (conversation.OrganizationId != organizationId)
                {
                    var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
                    var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
                    await _auditService.LogAuthorizationFailureAsync(
                        userId, organizationId, "Conversation", conversationId, "ConversationNotInOrganization", ipAddress, userAgent);
                    return false;
                }

                // Check if user is a participant
                var isParticipant = conversation.Participants.Any(p => p.UserId == userId);
                if (!isParticipant)
                {
                    var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
                    var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
                    await _auditService.LogAuthorizationFailureAsync(
                        userId, organizationId, "Conversation", conversationId, "NotAParticipant", ipAddress, userAgent);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating conversation access for user {UserId}, conversation {ConversationId}", userId, conversationId);
                return false;
            }
        }

        /// <summary>
        /// Safely retrieves a matter with organization validation
        /// Returns null if user doesn't have access (prevents IDOR)
        /// </summary>
        public async Task<Matter?> GetMatterIfAuthorizedAsync(int matterId, int userId, int organizationId, HttpContext? httpContext = null)
        {
            var canAccess = await ValidateUserCanAccessMatterAsync(matterId, userId, organizationId, httpContext);
            if (!canAccess)
                return null;

            return await _context.Matters
                .Where(m => m.Id == matterId && m.OrganizationId == organizationId)
                .Include(m => m.Assignments)
                    .ThenInclude(a => a.User)
                .Include(m => m.Permissions)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Safely retrieves a task with authorization validation
        /// Returns null if user doesn't have access (prevents IDOR)
        /// </summary>
        public async Task<TaskItem?> GetTaskIfAuthorizedAsync(int taskId, int userId, HttpContext? httpContext = null)
        {
            var canAccess = await ValidateUserCanAccessTaskAsync(taskId, userId, httpContext);
            if (!canAccess)
                return null;

            return await _context.TaskItems
                .Include(t => t.Matter)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.User)
                .Include(t => t.Comments)
                    .ThenInclude(c => c.User)
                .Include(t => t.SubTasks)
                    .ThenInclude(st => st.Assignments)
                        .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(t => t.Id == taskId);
        }

        /// <summary>
        /// Safely retrieves a subtask with authorization validation
        /// Returns null if user doesn't have access (prevents IDOR)
        /// </summary>
        public async Task<SubTaskItem?> GetSubTaskIfAuthorizedAsync(int subTaskId, int userId, HttpContext? httpContext = null)
        {
            var canAccess = await ValidateUserCanAccessSubTaskAsync(subTaskId, userId, httpContext);
            if (!canAccess)
                return null;

            return await _context.SubTaskItems
                .Include(st => st.Assignments)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(st => st.Id == subTaskId);
        }

        /// <summary>
        /// Check if user has firm-based access to an organization
        /// </summary>
        private async Task<bool> HasFirmBasedAccessAsync(int userId, int targetOrganizationId)
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
                return false;

            // Check if there's a valid relationship
            var hasRelationship = lawFirmMembership.Organization.OrganizationRelationships
                .Any(rel => 
                    rel.TargetOrganizationId == targetOrganizationId && 
                    rel.IsActive && 
                    !rel.IsDeleted &&
                    rel.RelationshipType == Certio.Domain.Organizations.RelationshipTypes.LawFirmClient &&
                    (!rel.ExpiresAt.HasValue || rel.ExpiresAt.Value > DateTime.UtcNow));

            return hasRelationship;
        }

        /// <summary>
        /// Validates that a user is a member of an organization
        /// </summary>
        public async Task<bool> ValidateUserInOrganizationAsync(int userId, int organizationId)
        {
            return await _context.UserOrganizations
                .AnyAsync(uo => uo.UserId == userId && 
                               uo.OrganizationId == organizationId && 
                               uo.IsActive);
        }

        /// <summary>
        /// Gets all organization IDs a user has access to (direct membership + firm relationships)
        /// </summary>
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
    }
}

