using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Certio.Application.Interfaces;
using Certio.Domain.Users;
using Certio.Web.Services;

namespace Certio.Web.Security
{
    /// <summary>
    /// Attribute to enforce agent action permissions using the existing RBAC system.
    /// Uses IPermissionService to check if user has the required permission.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireAgentPermissionAttribute : TypeFilterAttribute
    {
        public RequireAgentPermissionAttribute(Permission permission) 
            : base(typeof(RequireAgentPermissionFilter))
        {
            Arguments = new object[] { permission };
        }
    }

    public class RequireAgentPermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly Permission _permission;
        private readonly IPermissionService _permissionService;
        private readonly IClientContextAccessor _clientContextAccessor;
        private readonly ILogger<RequireAgentPermissionFilter> _logger;

        public RequireAgentPermissionFilter(
            Permission permission,
            IPermissionService permissionService,
            IClientContextAccessor clientContextAccessor,
            ILogger<RequireAgentPermissionFilter> logger)
        {
            _permission = permission;
            _permissionService = permissionService;
            _clientContextAccessor = clientContextAccessor;
            _logger = logger;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var clientContext = _clientContextAccessor.Current;
            
            if (clientContext == null || !clientContext.IsValid || !clientContext.OrganizationId.HasValue)
            {
                _logger.LogDebug("Agent permission denied: invalid client context");
                context.Result = new NotFoundResult();
                return;
            }

            var userId = clientContext.UserId;
            var orgId = clientContext.OrganizationId.Value;

            var hasPermission = await _permissionService.HasPermissionAsync(userId, orgId, _permission);
            
            if (!hasPermission)
            {
                _logger.LogDebug("Agent permission {Permission} denied for user {UserId} in org {OrgId}", 
                    _permission, userId, orgId);
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}

