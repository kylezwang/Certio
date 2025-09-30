using Certio.Web.Services;
using Microsoft.AspNetCore.Authorization;

namespace Certio.Web.Security
{
    public sealed class OrgMemberAuthorizationHandler : AuthorizationHandler<OrgMemberRequirement>
    {
        private readonly IClientContextAccessor _clientContextAccessor;
        private readonly IFirmAccessAuditService _auditService;
        private readonly ILogger<OrgMemberAuthorizationHandler> _logger;

        public OrgMemberAuthorizationHandler(
            IClientContextAccessor clientContextAccessor, 
            IFirmAccessAuditService auditService,
            ILogger<OrgMemberAuthorizationHandler> logger)
        {
            _clientContextAccessor = clientContextAccessor;
            _auditService = auditService;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrgMemberRequirement requirement)
        {
            var clientContext = _clientContextAccessor.Current;

            if (clientContext == null || !clientContext.IsValid)
            {
                _logger.LogDebug("Authorization denied: missing or invalid client context for user {User}.", context.User?.Identity?.Name);
                return; // Fail: will be translated to 404 by custom result handler
            }

            // Check role requirements
            if (requirement.RequiredRole != null)
            {
                var hasRequiredRole = false;
                
                if (clientContext.IsFirmBasedAccess)
                {
                    // For firm-based access, check the firm membership role
                    hasRequiredRole = clientContext.FirmMembership != null && 
                                    string.Equals(clientContext.FirmMembership.Role, requirement.RequiredRole, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    // For direct membership, check the direct membership role
                    hasRequiredRole = clientContext.Membership != null && 
                                    string.Equals(clientContext.Membership.Role, requirement.RequiredRole, StringComparison.OrdinalIgnoreCase);
                }

                if (!hasRequiredRole)
                {
                    _logger.LogDebug("Authorization denied: user {User} lacks required role {Role} in org {OrgId} (firm-based: {IsFirmBased}).", 
                        context.User?.Identity?.Name, requirement.RequiredRole, clientContext.OrganizationId, clientContext.IsFirmBasedAccess);
                    return;
                }
            }

            // Additional firm-based access validation
            if (clientContext.IsFirmBasedAccess)
            {
                // Validate that the firm relationship is still valid
                if (clientContext.FirmRelationship == null || !clientContext.FirmRelationship.IsValid())
                {
                    _logger.LogDebug("Authorization denied: invalid or expired firm relationship for user {User} accessing org {OrgId}.", 
                        context.User?.Identity?.Name, clientContext.OrganizationId);
                    return;
                }

                // Check access level restrictions if needed
                if (requirement.RequiredAccessLevel != null)
                {
                    if (!string.Equals(clientContext.AccessLevel, requirement.RequiredAccessLevel, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Authorization denied: user {User} lacks required access level {AccessLevel} for org {OrgId}.", 
                            context.User?.Identity?.Name, requirement.RequiredAccessLevel, clientContext.OrganizationId);
                        return;
                    }
                }
            }

            // Log successful access
            if (clientContext.IsFirmBasedAccess && clientContext.OrganizationId.HasValue)
            {
                await _auditService.LogFirmAccessAsync(
                    clientContext.FirmMembership?.UserId ?? 0, 
                    clientContext.OrganizationId.Value, 
                    "AccessGranted",
                    $"Firm-based access granted via {clientContext.FirmOrganizationName}");
            }

            context.Succeed(requirement);
        }
    }
}


