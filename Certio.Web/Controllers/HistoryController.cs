using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Application.Interfaces;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers
{
    public class HistoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITeamService _teamService;
        private readonly IAuditService _auditService;
        private readonly IMatterService _matterService;
        private readonly ILogger<HistoryController> _logger;

        public HistoryController(
            ApplicationDbContext context,
            ITeamService teamService,
            IAuditService auditService,
            IMatterService matterService,
            ILogger<HistoryController> logger)
        {
            _context = context;
            _teamService = teamService;
            _auditService = auditService;
            _matterService = matterService;
            _logger = logger;
        }

        // GET: /Client/{orgId}/History
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/History")]
        public async Task<IActionResult> Index(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Set ViewBag for layout and client-side
            ViewBag.OrganizationId = orgId;
            ViewBag.CurrentUserId = user.Id;
            ViewBag.CurrentUserName = $"{user.FirstName} {user.LastName}";
            ViewBag.CurrentUserInitials = $"{user.FirstName[0]}{user.LastName[0]}".ToUpper();
            ViewBag.CurrentUserEmail = user.Email ?? "";

            // Get organization name
            var org = await _context.Organizations
                .Where(o => o.Id == orgId)
                .FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            ViewBag.OrganizationType = org?.Type ?? Certio.Domain.Organizations.OrganizationType.Client;

            return View("~/Views/Client/History.cshtml");
        }

        // GET: /Client/{orgId}/History/Activities - Get activity history
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/History/Activities")]
        public async Task<IActionResult> GetActivities(
            int orgId,
            [FromQuery] DateTime? start,
            [FromQuery] DateTime? end,
            [FromQuery] string? userIds,
            [FromQuery] string? activityTypes)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            try
            {
                // Get user's role in this organization
                var userOrg = await _context.UserOrganizations
                    .FirstOrDefaultAsync(uo => uo.UserId == user.Id && uo.OrganizationId == orgId);

                if (userOrg == null)
                {
                    return Unauthorized();
                }

                // Determine if user has admin/partner privileges
                bool hasFullAccess = userOrg.UserType == UserTypes.LawFirm && 
                                    (userOrg.Role == OrganizationRoles.Partner || 
                                     userOrg.Role == OrganizationRoles.Owner) ||
                                    userOrg.UserType == UserTypes.Certio && 
                                    userOrg.Role == OrganizationRoles.Admin ||
                                    userOrg.Role == OrganizationRoles.Owner ||
                                    userOrg.Role == OrganizationRoles.Admin;

                // Get organization-scoped audit logs for this org
                var auditLogs = await _auditService.GetOrganizationAuditLogsAsync(orgId, start, end);

                // Get related organization IDs (for law firms viewing client matters, etc.)
                var relatedOrgIds = await _context.OrganizationRelationships
                    .Where(or => (or.SourceOrganizationId == orgId || or.TargetOrganizationId == orgId) && or.IsActive)
                    .Select(or => or.SourceOrganizationId == orgId ? or.TargetOrganizationId : or.SourceOrganizationId)
                    .Distinct()
                    .ToListAsync();

                // Get audit logs from related organizations
                if (relatedOrgIds.Any())
                {
                    var relatedAuditLogs = await _context.AuditLogs
                        .Where(al => relatedOrgIds.Contains(al.OrganizationId ?? 0) &&
                                    (!start.HasValue || al.Timestamp >= start.Value) &&
                                    (!end.HasValue || al.Timestamp <= end.Value))
                        .OrderByDescending(al => al.Timestamp)
                        .ToListAsync();
                    
                    auditLogs = auditLogs.Concat(relatedAuditLogs).OrderByDescending(al => al.Timestamp).ToList();
                }

                // Get user-scoped system activities
                List<Domain.Audit.AuditLog> userSystemLogs;
                
                if (hasFullAccess)
                {
                    // Partners/Admins see ALL user activities in the organization
                    // Get all org members' user-scoped activities
                    var orgUserIds = await _context.UserOrganizations
                        .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                        .Select(uo => uo.UserId)
                        .ToListAsync();

                    userSystemLogs = await _context.AuditLogs
                        .Where(al => al.OrganizationId == null && 
                                    orgUserIds.Contains(al.UserId ?? 0) &&
                                    (!start.HasValue || al.Timestamp >= start.Value) &&
                                    (!end.HasValue || al.Timestamp <= end.Value))
                        .OrderByDescending(al => al.Timestamp)
                        .ToListAsync();
                }
                else
                {
                    // Regular users see only their own user-scoped activities
                    userSystemLogs = await _context.AuditLogs
                        .Where(al => al.OrganizationId == null && 
                                    al.UserId == user.Id &&
                                    (!start.HasValue || al.Timestamp >= start.Value) &&
                                    (!end.HasValue || al.Timestamp <= end.Value))
                        .OrderByDescending(al => al.Timestamp)
                        .ToListAsync();
                }

                // Combine both lists
                auditLogs = auditLogs.Concat(userSystemLogs).OrderByDescending(al => al.Timestamp).ToList();

                // Filter by user IDs if specified
                if (!string.IsNullOrEmpty(userIds))
                {
                    var userIdList = userIds.Split(',')
                        .Select(id => int.TryParse(id, out var userId) ? userId : (int?)null)
                        .Where(id => id.HasValue)
                        .Select(id => id.Value)
                        .ToList();

                    if (userIdList.Any())
                    {
                        auditLogs = auditLogs.Where(log => 
                            userIdList.Contains(log.UserId ?? 0) || 
                            (userIdList.Contains(0) && log.IsAIAction) // 0 = Notal AI
                        ).ToList();
                    }
                }

                // Filter by activity types if specified
                if (!string.IsNullOrEmpty(activityTypes))
                {
                    var types = activityTypes.Split(',').Select(t => t.Trim()).ToList();
                    auditLogs = auditLogs.Where(log => types.Contains(log.Action)).ToList();
                }

                // Get matter titles for logs that have matter IDs
                var matterIds = auditLogs.Where(log => log.MatterId.HasValue).Select(log => log.MatterId!.Value).Distinct().ToList();
                var matterTitles = new Dictionary<int, string>();
                
                foreach (var matterId in matterIds)
                {
                    var matterResult = await _matterService.GetMatterAsync(user.Id, matterId);
                    if (matterResult.Success && matterResult.Data != null)
                    {
                        matterTitles[matterId] = matterResult.Data.Title;
                    }
                }

                // Format activities for the frontend
                var activities = auditLogs.Select(log => new
                {
                    id = log.Id,
                    timestamp = log.Timestamp,
                    userId = log.UserId ?? (log.IsAIAction ? 0 : (int?)null),
                    userName = log.IsAIAction ? "Notal AI" : (log.UserName ?? "Unknown"),
                    userInitials = log.IsAIAction ? "AI" : GetInitialsFromName(log.UserName ?? "Unknown"),
                    actionType = FormatActionType(log.Action),
                    action = log.Description ?? log.Action,
                    description = log.Description ?? FormatDescription(log),
                    matterId = log.MatterId,
                    matterTitle = log.MatterId.HasValue && matterTitles.ContainsKey(log.MatterId.Value) 
                        ? matterTitles[log.MatterId.Value] 
                        : null,
                    entityType = log.EntityType,
                    entityId = log.EntityId,
                    result = log.Result,
                    ipAddress = log.IPAddress,
                    userAgent = log.UserAgent,
                    details = FormatDetails(log)
                }).OrderByDescending(a => a.timestamp).ToList();

                return Json(new { success = true, activities });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching activity history for org {OrgId}", orgId);
                return Json(new { success = false, message = "Error fetching activity history" });
            }
        }

        // Helper method to format action type for display
        private string FormatActionType(string action)
        {
            return action switch
            {
                "Create" => "Create",
                "Update" => "Update",
                "Delete" => "Delete",
                "SoftDelete" => "Delete",
                "View" => "View",
                "Export" => "Export",
                "Login" => "Login",
                "Logout" => "Logout",
                "LoginFailed" => "Login Failed",
                "AIGenerated" => "AI Generated",
                "AIApproved" => "AI Approved",
                "AIRejected" => "AI Rejected",
                _ => action
            };
        }

        // Helper method to format description
        private string FormatDescription(Certio.Domain.Audit.AuditLog log)
        {
            if (!string.IsNullOrEmpty(log.Description))
                return log.Description;

            return $"{log.Action} {log.EntityType} (ID: {log.EntityId})";
        }

        // Helper method to format details
        private string? FormatDetails(Certio.Domain.Audit.AuditLog log)
        {
            var details = new List<string>();

            if (!string.IsNullOrEmpty(log.OldValues))
                details.Add($"Old Values: {log.OldValues}");

            if (!string.IsNullOrEmpty(log.NewValues))
                details.Add($"New Values: {log.NewValues}");

            if (log.IsAIAction && !string.IsNullOrEmpty(log.AIAgentType))
                details.Add($"AI Agent: {log.AIAgentType}");

            if (!string.IsNullOrEmpty(log.AIContext))
                details.Add($"AI Context: {log.AIContext}");

            return details.Any() ? string.Join("\n", details) : null;
        }

        // Helper method to get initials from name
        private string GetInitialsFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "??";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            
            return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
        }

        // GET: /Client/{orgId}/Users - Get org users for filter
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/History/Users")]
        public async Task<IActionResult> GetUsers(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            try
            {
                // Get related organization IDs (for law firms viewing client matters, etc.)
                var relatedOrgIds = await _context.OrganizationRelationships
                    .Where(or => (or.SourceOrganizationId == orgId || or.TargetOrganizationId == orgId) && or.IsActive)
                    .Select(or => or.SourceOrganizationId == orgId ? or.TargetOrganizationId : or.SourceOrganizationId)
                    .Distinct()
                    .ToListAsync();

                // Include current org
                var allOrgIds = new List<int> { orgId };
                allOrgIds.AddRange(relatedOrgIds);

                // Get all users from current org and related orgs
                var userOrgs = await _context.UserOrganizations
                    .Include(uo => uo.User)
                    .Where(uo => allOrgIds.Contains(uo.OrganizationId) && uo.IsActive)
                    .Select(uo => uo.User)
                    .Distinct()
                    .ToListAsync();

                // Map to expected format
                var users = userOrgs
                    .Select(u => new
                    {
                        id = u.Id,
                        name = $"{u.FirstName} {u.LastName}".Trim(),
                        initials = GetInitials(u.FirstName, u.LastName),
                        email = u.Email ?? ""
                    })
                    .OrderBy(u => u.name)
                    .ToList();

                // Add Notal AI as a user option
                users.Insert(0, new
                {
                    id = 0, // Special ID for AI
                    name = "Notal AI",
                    initials = "AI",
                    email = ""
                });

                return Json(new { success = true, users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users for org {OrgId}", orgId);
                return BadRequest(new { success = false, message = "Error loading users" });
            }
        }

        // GET: /Client/{orgId}/History/Matters - Get matters for filter
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/History/Matters")]
        public async Task<IActionResult> GetMatters(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            try
            {
                // Get matters from current org
                var result = await _matterService.ListMattersAsync(user.Id, orgId);
                var allMatters = result.Success ? result.Data! : new List<Application.DTOs.MatterDto>();

                // Get related organization IDs and their matters too (for law firms viewing client matters)
                var relatedOrgIds = await _context.OrganizationRelationships
                    .Where(or => (or.SourceOrganizationId == orgId || or.TargetOrganizationId == orgId) && or.IsActive)
                    .Select(or => or.SourceOrganizationId == orgId ? or.TargetOrganizationId : or.SourceOrganizationId)
                    .Distinct()
                    .ToListAsync();

                // Get matters from related orgs
                foreach (var relatedOrgId in relatedOrgIds)
                {
                    var relatedResult = await _matterService.ListMattersAsync(user.Id, relatedOrgId);
                    if (relatedResult.Success)
                    {
                        allMatters.AddRange(relatedResult.Data!);
                    }
                }

                var matters = allMatters
                    .Select(m => new
                    {
                        id = m.Id,
                        name = m.Title
                    })
                    .OrderBy(m => m.name)
                    .ToList();

                return Json(new { success = true, matters });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading matters for org {OrgId}", orgId);
                return BadRequest(new { success = false, message = "Error loading matters" });
            }
        }

        // GET: /Client/{orgId}/History/Actions - Get available actions for filter
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/History/Actions")]
        public async Task<IActionResult> GetActions(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            try
            {
                // Get related organization IDs (for law firms viewing client matters, etc.)
                var relatedOrgIds = await _context.OrganizationRelationships
                    .Where(or => (or.SourceOrganizationId == orgId || or.TargetOrganizationId == orgId) && or.IsActive)
                    .Select(or => or.SourceOrganizationId == orgId ? or.TargetOrganizationId : or.SourceOrganizationId)
                    .Distinct()
                    .ToListAsync();

                // Include current org
                var allOrgIds = new List<int> { orgId };
                allOrgIds.AddRange(relatedOrgIds);

                // Get distinct actions from audit logs for this organization and related orgs
                var actions = await _context.AuditLogs
                    .Where(al => al.OrganizationId.HasValue && allOrgIds.Contains(al.OrganizationId.Value))
                    .Select(al => al.Action)
                    .Distinct()
                    .OrderBy(a => a)
                    .ToListAsync();

                return Json(new { success = true, actions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading actions for org {OrgId}", orgId);
                return BadRequest(new { success = false, message = "Error loading actions" });
            }
        }

        // GET: /Client/{orgId}/History/EntityTypes - Get available entity types for filter
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/History/EntityTypes")]
        public async Task<IActionResult> GetEntityTypes(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            try
            {
                // Get related organization IDs (for law firms viewing client matters, etc.)
                var relatedOrgIds = await _context.OrganizationRelationships
                    .Where(or => (or.SourceOrganizationId == orgId || or.TargetOrganizationId == orgId) && or.IsActive)
                    .Select(or => or.SourceOrganizationId == orgId ? or.TargetOrganizationId : or.SourceOrganizationId)
                    .Distinct()
                    .ToListAsync();

                // Include current org
                var allOrgIds = new List<int> { orgId };
                allOrgIds.AddRange(relatedOrgIds);

                // Get distinct entity types from audit logs for this organization and related orgs
                var entityTypes = await _context.AuditLogs
                    .Where(al => al.OrganizationId.HasValue && allOrgIds.Contains(al.OrganizationId.Value) && al.EntityType != null)
                    .Select(al => al.EntityType!)
                    .Distinct()
                    .OrderBy(et => et)
                    .ToListAsync();

                return Json(new { success = true, entityTypes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entity types for org {OrgId}", orgId);
                return BadRequest(new { success = false, message = "Error loading entity types" });
            }
        }

        // Helper method to generate user initials
        private string GetInitials(string firstName, string lastName)
        {
            var first = !string.IsNullOrWhiteSpace(firstName) ? firstName[0].ToString().ToUpper() : "";
            var last = !string.IsNullOrWhiteSpace(lastName) ? lastName[0].ToString().ToUpper() : "";
            return first + last;
        }

        // Helper methods
        private (User?, int) GetUserContext()
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            var orgId = customUser?.GetPrimaryOrganization()?.OrganizationId ?? 0;
            return (customUser, orgId);
        }

        private string? GetIpAddress() =>
            HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? GetUserAgent() =>
            HttpContext.Request.Headers["User-Agent"].ToString();
    }
}

