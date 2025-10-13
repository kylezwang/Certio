using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Certio.Application.Interfaces;
using Certio.Domain.Users;

namespace Certio.Web.Security
{
    /// <summary>
    /// Authorization attribute that validates user can perform a specific operation on a matter
    /// Combines both matter access check and permission check
    /// Usage: [RequireMatterOperation(Permission.EditMatters, "matterId")]
    /// </summary>
    public class RequireMatterOperationAttribute : TypeFilterAttribute
    {
        public RequireMatterOperationAttribute(Permission permission, string matterIdParameter = "matterId") 
            : base(typeof(RequireMatterOperationFilter))
        {
            Arguments = new object[] { permission, matterIdParameter };
        }
    }

    /// <summary>
    /// Filter implementation for RequireMatterOperation attribute
    /// </summary>
    public class RequireMatterOperationFilter : IAsyncAuthorizationFilter
    {
        private readonly Permission _requiredPermission;
        private readonly string _matterIdParameter;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<RequireMatterOperationFilter> _logger;

        public RequireMatterOperationFilter(
            Permission requiredPermission,
            string matterIdParameter,
            IPermissionService permissionService,
            ILogger<RequireMatterOperationFilter> logger)
        {
            _requiredPermission = requiredPermission;
            _matterIdParameter = matterIdParameter;
            _permissionService = permissionService;
            _logger = logger;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Check if user is authenticated
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Get user ID
            var userIdClaim = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Get matter ID
            int? matterId = null;

            // Try route data
            if (context.RouteData.Values.TryGetValue(_matterIdParameter, out var routeMatterId))
            {
                if (routeMatterId is int matterIdInt)
                {
                    matterId = matterIdInt;
                }
                else if (int.TryParse(routeMatterId?.ToString(), out int parsedMatterId))
                {
                    matterId = parsedMatterId;
                }
            }

            // Try query string
            if (!matterId.HasValue && context.HttpContext.Request.Query.TryGetValue(_matterIdParameter, out var queryMatterId))
            {
                if (int.TryParse(queryMatterId.FirstOrDefault(), out int parsedQueryMatterId))
                {
                    matterId = parsedQueryMatterId;
                }
            }

            // Try form data
            if (!matterId.HasValue && context.HttpContext.Request.HasFormContentType)
            {
                if (context.HttpContext.Request.Form.TryGetValue(_matterIdParameter, out var formMatterId))
                {
                    if (int.TryParse(formMatterId.FirstOrDefault(), out int parsedFormMatterId))
                    {
                        matterId = parsedFormMatterId;
                    }
                }
            }

            if (!matterId.HasValue)
            {
                _logger.LogWarning("Matter ID parameter '{Parameter}' not found for operation check. User: {UserId}",
                    _matterIdParameter, userId);
                context.Result = new BadRequestObjectResult(new { error = $"Matter ID parameter '{_matterIdParameter}' is required" });
                return;
            }

            // Check if user can perform the operation (includes both access and permission check)
            var canPerform = await _permissionService.CanPerformOperationAsync(userId, matterId.Value, _requiredPermission);
            
            if (!canPerform)
            {
                _logger.LogWarning("Matter operation denied. User: {UserId}, Matter: {MatterId}, Permission: {Permission}",
                    userId, matterId.Value, _requiredPermission);
                context.Result = new ForbidResult();
                return;
            }

            _logger.LogDebug("Matter operation granted. User: {UserId}, Matter: {MatterId}, Permission: {Permission}",
                userId, matterId.Value, _requiredPermission);
        }
    }
}

