using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Certio.Domain.Users;
using Certio.Domain.Matters;
using Certio.Domain.Tasks;
using Certio.Web.Security;
using Microsoft.Extensions.Logging;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/search")]
[Authorize]
public class UniversalSearchController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<UniversalSearchController> _logger;

    public UniversalSearchController(
        ApplicationDbContext context,
        IPermissionService permissionService,
        ILogger<UniversalSearchController> logger)
    {
        _context = context;
        _permissionService = permissionService;
        _logger = logger;
    }

    [HttpGet("universal")]
    public async Task<IActionResult> UniversalSearch([FromQuery] string query, [FromQuery] int? orgId = null)
    {
        try
        {
            // Security: Get current user and validate
            var customUser = HttpContext.Items["CustomUser"] as User;
            if (customUser == null)
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            var userId = customUser.Id;

            // Input validation
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Json(new { success = true, results = new List<object>() });
            }

            // Sanitize query to prevent injection
            query = query.Trim();
            if (query.Length > 100)
            {
                query = query.Substring(0, 100);
            }

            // Get organization ID from query or context
            if (!orgId.HasValue)
            {
                orgId = HttpContext.Items["CurrentOrganizationId"] as int?;
            }

            if (!orgId.HasValue)
            {
                return Json(new { success = false, error = "Organization not specified" });
            }

            // Security: Validate user has access to this organization
            var hasAccess = await _permissionService.IsOrganizationMemberAsync(userId, orgId.Value) ||
                           await _permissionService.HasFirmBasedAccessAsync(userId, orgId.Value);
            
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to search in unauthorized org {OrgId}", userId, orgId.Value);
                return Json(new { success = false, error = "Access denied" });
            }

            var results = new List<object>();
            var searchLower = query.ToLower();

            // Search Matters (with access control)
            var accessibleMatterIds = await GetAccessibleMatterIdsAsync(userId, orgId.Value);
            var matters = await _context.Matters
                .Where(m => m.OrganizationId == orgId.Value && 
                           accessibleMatterIds.Contains(m.Id) &&
                           !m.IsDeleted &&
                           (m.Title.ToLower().Contains(searchLower) || 
                            (m.Description != null && m.Description.ToLower().Contains(searchLower))))
                .OrderByDescending(m => m.CreatedAt)
                .Take(5)
                .Select(m => new
                {
                    type = "matter",
                    id = m.Id,
                    title = m.Title,
                    description = m.Description ?? "",
                    url = $"/Client/{orgId.Value}/Matter/{m.Id}",
                    icon = "fas fa-folder"
                })
                .ToListAsync();

            results.AddRange(matters);

            // Search Tasks (with access control)
            var tasks = await _context.TaskItems
                .Include(t => t.Matter)
                .Where(t => t.Matter != null &&
                           t.Matter.OrganizationId == orgId.Value &&
                           accessibleMatterIds.Contains(t.Matter.Id) &&
                           !t.IsDeleted &&
                           (t.Title.ToLower().Contains(searchLower) ||
                            (t.Description != null && t.Description.ToLower().Contains(searchLower))))
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .Select(t => new
                {
                    type = "task",
                    id = t.Id,
                    title = t.Title,
                    description = t.Description ?? "",
                    matterTitle = t.Matter != null ? t.Matter.Title : "",
                    url = $"/Client/{orgId.Value}/Tasks",
                    icon = "fa-solid fa-bars-progress"
                })
                .ToListAsync();

            results.AddRange(tasks);

            // Search Clients (if user has permission)
            var clients = await _context.Organizations
                .Where(o => o.Type == Certio.Domain.Organizations.OrganizationType.Client &&
                           o.Name.ToLower().Contains(searchLower) &&
                           !o.IsDeleted)
                .Join(_context.OrganizationRelationships
                    .Where(or => or.SourceOrganizationId == orgId.Value && 
                                or.RelationshipType == Certio.Domain.Organizations.RelationshipTypes.LawFirmClient &&
                                or.IsActive &&
                                !or.IsDeleted),
                    o => o.Id,
                    or => or.TargetOrganizationId,
                    (o, or) => new { Organization = o })
                .OrderByDescending(x => x.Organization.CreatedAt)
                .Take(5)
                .Select(x => new
                {
                    type = "client",
                    id = x.Organization.Id,
                    title = x.Organization.Name,
                    description = "",
                    url = $"/Client/{orgId.Value}/Clients",
                    icon = "fas fa-users"
                })
                .ToListAsync();

            results.AddRange(clients);

            // Search Navigation Items (static)
            var navigationItems = GetNavigationItems(orgId.Value, searchLower);
            results.AddRange(navigationItems);

            return Json(new { success = true, results = results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing universal search for query: {Query}", query);
            return Json(new { success = false, error = "An error occurred while searching" });
        }
    }

    private async Task<List<int>> GetAccessibleMatterIdsAsync(int userId, int organizationId)
    {
        // Get all matters user can access
        var allMatters = await _context.Matters
            .Where(m => m.OrganizationId == organizationId && !m.IsDeleted)
            .ToListAsync();

        var accessibleMatterIds = new List<int>();
        foreach (var matter in allMatters)
        {
            var canAccess = await _permissionService.CanAccessMatterAsync(userId, matter.Id);
            if (canAccess)
            {
                accessibleMatterIds.Add(matter.Id);
            }
        }

        return accessibleMatterIds;
    }

    private List<object> GetNavigationItems(int orgId, string searchLower)
    {
        var navItems = new List<object>();

        // Common navigation items
        var navOptions = new[]
        {
            new { name = "Dashboard", url = $"/Client/{orgId}/Dashboard", icon = "fas fa-home", keywords = new[] { "dashboard", "home", "main" } },
            new { name = "Tasks", url = $"/Client/{orgId}/Tasks", icon = "fa-solid fa-bars-progress", keywords = new[] { "tasks", "todo", "assignments" } },
            new { name = "Calendar", url = $"/Client/{orgId}/Calendar", icon = "far fa-calendar", keywords = new[] { "calendar", "schedule", "events" } },
            new { name = "Billing", url = $"/Client/{orgId}/Billing", icon = "fa-regular fa-credit-card", keywords = new[] { "billing", "invoices", "time", "expenses" } },
            new { name = "Documents", url = $"/Client/{orgId}/Documents", icon = "far fa-file-alt", keywords = new[] { "documents", "files", "docs" } },
            new { name = "Communications", url = $"/Client/{orgId}/Communications", icon = "far fa-comments", keywords = new[] { "communications", "messages", "chat" } },
            new { name = "History", url = $"/Client/{orgId}/History", icon = "fas fa-clock", keywords = new[] { "history", "activity", "log" } }
        };

        foreach (var item in navOptions)
        {
            if (item.name.ToLower().Contains(searchLower) ||
                item.keywords.Any(k => k.Contains(searchLower)))
            {
                navItems.Add(new
                {
                    type = "navigation",
                    id = 0,
                    title = item.name,
                    description = "",
                    url = item.url,
                    icon = item.icon
                });
            }
        }

        return navItems;
    }
}

