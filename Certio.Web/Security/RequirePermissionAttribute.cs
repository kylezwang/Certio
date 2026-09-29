using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Certio.Application.Interfaces;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Security
{
    /// <summary>
    /// Authorization attribute that requires a specific permission
    /// Usage: [RequirePermission(Permission.EditMatters)]
    /// </summary>
    public class RequirePermissionAttribute : TypeFilterAttribute
    {
        public RequirePermissionAttribute(Permission permission) 
            : base(typeof(RequirePermissionFilter))
        {
            Arguments = new object[] { permission };
        }
    }

    /// <summary>
    /// Filter implementation for RequirePermission attribute
    /// </summary>
    public class RequirePermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly Permission _requiredPermission;
        private readonly IPermissionService _permissionService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RequirePermissionFilter> _logger;

        public RequirePermissionFilter(
            Permission requiredPermission,
            IPermissionService permissionService,
            ApplicationDbContext context,
            ILogger<RequirePermissionFilter> logger)
        {
            _requiredPermission = requiredPermission;
            _permissionService = permissionService;
            _context = context;
            _logger = logger;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            _logger.LogInformation("RequirePermissionFilter executing for permission: {Permission}, Path: {Path}", 
                _requiredPermission, context.HttpContext.Request.Path);
            
            // Check if user is authenticated
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                _logger.LogWarning("User not authenticated");
                context.Result = new UnauthorizedResult();
                return;
            }

            // Get the CustomUser from HttpContext.Items (set by UserSyncMiddleware)
            var customUser = context.HttpContext.Items["CustomUser"] as User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext.Items for permission: {Permission}", 
                    _requiredPermission);
                context.Result = new UnauthorizedResult();
                return;
            }
            
            int userId = customUser.Id;
            _logger.LogInformation("CustomUser found: UserId={UserId}, Email={Email}", userId, customUser.Email);

            // Get organization ID from multiple sources (same logic as controllers)
            int? organizationId = null;
            
            _logger.LogInformation("Looking for organization ID...");
            
            // FIRST: Try to get from HttpContext.Items (set by middleware - most reliable)
            if (context.HttpContext.Items.TryGetValue("CurrentOrganizationId", out var currentOrgIdObj) && currentOrgIdObj is int currentOrgId)
            {
                organizationId = currentOrgId;
                _logger.LogDebug("Got organization ID from CurrentOrganizationId: {OrgId}", organizationId);
            }
            
            // SECOND: Try to get from route data
            if (!organizationId.HasValue && context.RouteData.Values.TryGetValue("orgId", out var orgIdObj))
            {
                if (orgIdObj is int orgIdInt)
                {
                    organizationId = orgIdInt;
                }
                else if (int.TryParse(orgIdObj?.ToString(), out int parsedOrgId))
                {
                    organizationId = parsedOrgId;
                }
                _logger.LogDebug("Got organization ID from route data: {OrgId}", organizationId);
            }

            // THIRD: Try to get from session
            if (!organizationId.HasValue)
            {
                var orgIdSession = context.HttpContext.Session.GetInt32("OrganizationId");
                if (orgIdSession.HasValue)
                {
                    organizationId = orgIdSession.Value;
                    _logger.LogDebug("Got organization ID from session: {OrgId}", organizationId);
                }
            }

            // LAST RESORT: Try to get user's primary organization
            if (!organizationId.HasValue)
            {
                _logger.LogInformation("Trying to get organization from user's primary org...");
                var userOrg = await _context.UserOrganizations
                    .Where(uo => uo.UserId == userId && uo.IsActive)
                    .OrderBy(uo => uo.Id)
                    .FirstOrDefaultAsync();

                if (userOrg != null)
                {
                    organizationId = userOrg.OrganizationId;
                    _logger.LogInformation("Got organization ID from user's primary org: {OrgId}", organizationId);
                }
            }

            if (!organizationId.HasValue)
            {
                _logger.LogWarning("PERMISSION CHECK FAILED: Could not determine organization ID for permission check. User: {UserId}, Permission: {Permission}",
                    userId, _requiredPermission);
                context.Result = new ForbidResult();
                return;
            }

            _logger.LogInformation("Checking permission: User={UserId}, Org={OrgId}, Permission={Permission}", 
                userId, organizationId.Value, _requiredPermission);

            // Check permission
            var hasPermission = await _permissionService.HasPermissionAsync(userId, organizationId.Value, _requiredPermission);
            
            _logger.LogInformation("Permission check result: User={UserId}, Org={OrgId}, Permission={Permission}, HasPermission={HasPermission}", 
                userId, organizationId.Value, _requiredPermission, hasPermission);
            
            if (!hasPermission)
            {
                _logger.LogWarning("PERMISSION DENIED: User {UserId} does not have permission {Permission} in organization {OrgId}",
                    userId, _requiredPermission, organizationId.Value);
                context.Result = new ForbidResult();
                return;
            }

            _logger.LogInformation("PERMISSION GRANTED: User={UserId}, Org={OrgId}, Permission={Permission}",
                userId, organizationId.Value, _requiredPermission);
        }
    }
}

