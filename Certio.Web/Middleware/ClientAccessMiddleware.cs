using System.Text.RegularExpressions;
using Certio.Web.Data;
using Certio.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Middleware
{
    public class ClientAccessMiddleware
    {
        private readonly RequestDelegate _next;

        public ClientAccessMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, IFirmRelationshipCacheService cacheService, IFirmAccessAuditService auditService)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            // Match /Client/{orgId}/...
            var match = Regex.Match(path, @"^/Client/(?<orgId>\d+)(/|$)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (!int.TryParse(match.Groups["orgId"].Value, out var orgId))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Invalid organization id.");
                    return;
                }

                var customUser = context.Items["CustomUser"] as Certio.Domain.Users.User;
                if (customUser == null)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                // Check for direct membership first
                var isDirectMember = await db.UserOrganizations.AnyAsync(uo => uo.UserId == customUser.Id && uo.OrganizationId == orgId && uo.IsActive);
                
                if (!isDirectMember)
                {
                    // Check for firm-based access using cache
                    var hasFirmAccess = await cacheService.HasFirmAccessAsync(customUser.Id, orgId);
                    if (!hasFirmAccess)
                    {
                        // Log denied access attempt
                        await auditService.LogFirmAccessDeniedAsync(
                            customUser.Id, 
                            orgId, 
                            "No firm relationship found",
                            $"User attempted to access organization {orgId} but has no firm-based access");

                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync("Forbidden: You are not a member of this client and do not have firm-based access.");
                        return;
                    }
                }

                // Stash current org in Items for downstream use
                context.Items["CurrentOrganizationId"] = orgId;
            }

            await _next(context);
        }

    }

    public static class ClientAccessMiddlewareExtensions
    {
        public static IApplicationBuilder UseClientAccessGuard(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ClientAccessMiddleware>();
        }
    }
}


