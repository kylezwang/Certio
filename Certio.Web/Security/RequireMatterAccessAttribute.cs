using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Certio.Application.Interfaces;

namespace Certio.Web.Security
{
    /// <summary>
    /// Authorization attribute that validates user has access to a matter
    /// Usage: [RequireMatterAccess("matterId")]
    /// </summary>
    public class RequireMatterAccessAttribute : TypeFilterAttribute
    {
        public RequireMatterAccessAttribute(string matterIdParameter = "matterId") 
            : base(typeof(RequireMatterAccessFilter))
        {
            Arguments = new object[] { matterIdParameter };
        }
    }

    /// <summary>
    /// Filter implementation for RequireMatterAccess attribute
    /// </summary>
    public class RequireMatterAccessFilter : IAsyncAuthorizationFilter
    {
        private readonly string _matterIdParameter;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<RequireMatterAccessFilter> _logger;

        public RequireMatterAccessFilter(
            string matterIdParameter,
            IPermissionService permissionService,
            ILogger<RequireMatterAccessFilter> logger)
        {
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

            // Get matter ID from route data, query string, or form data
            int? matterId = null;

            // Try route data first
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

            // Try form data (for POST requests)
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
                _logger.LogWarning("Matter ID parameter '{Parameter}' not found in request. User: {UserId}",
                    _matterIdParameter, userId);
                context.Result = new BadRequestObjectResult(new { error = $"Matter ID parameter '{_matterIdParameter}' is required" });
                return;
            }

            // Check matter access
            var hasAccess = await _permissionService.CanAccessMatterAsync(userId, matterId.Value);
            
            if (!hasAccess)
            {
                _logger.LogWarning("Matter access denied. User: {UserId}, Matter: {MatterId}",
                    userId, matterId.Value);
                context.Result = new ForbidResult();
                return;
            }

            _logger.LogDebug("Matter access granted. User: {UserId}, Matter: {MatterId}",
                userId, matterId.Value);
        }
    }
}

