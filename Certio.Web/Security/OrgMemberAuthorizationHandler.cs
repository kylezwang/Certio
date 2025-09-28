using Certio.Web.Services;
using Microsoft.AspNetCore.Authorization;

namespace Certio.Web.Security
{
    public sealed class OrgMemberAuthorizationHandler : AuthorizationHandler<OrgMemberRequirement>
    {
        private readonly IClientContextAccessor _clientContextAccessor;
        private readonly ILogger<OrgMemberAuthorizationHandler> _logger;

        public OrgMemberAuthorizationHandler(IClientContextAccessor clientContextAccessor, ILogger<OrgMemberAuthorizationHandler> logger)
        {
            _clientContextAccessor = clientContextAccessor;
            _logger = logger;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OrgMemberRequirement requirement)
        {
            var clientContext = _clientContextAccessor.Current;

            if (clientContext == null || !clientContext.IsValid)
            {
                _logger.LogDebug("Authorization denied: missing or invalid client context for user {User}.", context.User?.Identity?.Name);
                return Task.CompletedTask; // Fail: will be translated to 404 by custom result handler
            }

            if (requirement.RequiredRole != null)
            {
                if (!string.Equals(clientContext.Membership!.Role, requirement.RequiredRole, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Authorization denied: user {User} lacks required role {Role} in org {OrgId}.", context.User?.Identity?.Name, requirement.RequiredRole, clientContext.OrganizationId);
                    return Task.CompletedTask;
                }
            }

            context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}


