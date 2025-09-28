using Microsoft.AspNetCore.Authorization;

namespace Certio.Web.Security
{
    public sealed class OrgMemberRequirement : IAuthorizationRequirement
    {
        public string? RequiredRole { get; }
        public OrgMemberRequirement(string? requiredRole = null)
        {
            RequiredRole = requiredRole;
        }
    }
}


