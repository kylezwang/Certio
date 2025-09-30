using Microsoft.AspNetCore.Authorization;

namespace Certio.Web.Security
{
    public sealed class OrgMemberRequirement : IAuthorizationRequirement
    {
        public string? RequiredRole { get; }
        public string? RequiredAccessLevel { get; }
        
        public OrgMemberRequirement(string? requiredRole = null, string? requiredAccessLevel = null)
        {
            RequiredRole = requiredRole;
            RequiredAccessLevel = requiredAccessLevel;
        }
    }
}


