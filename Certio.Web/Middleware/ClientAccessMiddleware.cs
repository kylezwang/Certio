using System.Text.RegularExpressions;
using Certio.Infrastructure.Data;
using Certio.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Web.Middleware
{
    public class ClientAccessMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ClientAccessMiddleware> _logger;

        public ClientAccessMiddleware(RequestDelegate next, ILogger<ClientAccessMiddleware> logger)
        {
            _next = next;
            _logger = logger;
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
                    _logger.LogWarning("ClientAccessMiddleware: Invalid orgId in path: {Path}", path);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Invalid organization id.");
                    return;
                }

                _logger.LogInformation("ClientAccessMiddleware: Checking access for path: {Path}, OrgId: {OrgId}", path, orgId);

                var customUser = context.Items["CustomUser"] as Certio.Domain.Users.User;
                if (customUser == null)
                {
                    _logger.LogWarning("ClientAccessMiddleware: CustomUser not found, redirecting to login. Path: {Path}", path);
                    // Redirect to login page with session expired notification
                    context.Response.Redirect("/Home/Index?sessionExpired=true");
                    return;
                }

                // Check for direct membership first
                var isDirectMember = await db.UserOrganizations.AnyAsync(uo => uo.UserId == customUser.Id && uo.OrganizationId == orgId && uo.IsActive);
                _logger.LogInformation("ClientAccessMiddleware: User {UserId} direct member check for Org {OrgId}: {IsDirectMember}", 
                    customUser.Id, orgId, isDirectMember);
                
                if (!isDirectMember)
                {
                    // Check for firm-based access using cache
                    var hasFirmAccess = await cacheService.HasFirmAccessAsync(customUser.Id, orgId);
                    _logger.LogInformation("ClientAccessMiddleware: User {UserId} firm access check for Org {OrgId}: {HasFirmAccess}", 
                        customUser.Id, orgId, hasFirmAccess);
                    
                    if (!hasFirmAccess)
                    {
                        // Log denied access attempt with IP and UserAgent
                        var ipAddress = context.Connection?.RemoteIpAddress?.ToString();
                        var userAgent = context.Request?.Headers["User-Agent"].ToString();
                        await auditService.LogFirmAccessDeniedAsync(
                            customUser.Id, 
                            orgId, 
                            "NoFirmRelationship",
                            $"FAILURE: NoFirmRelationship",
                            ipAddress,
                            userAgent);

                        _logger.LogWarning("ClientAccessMiddleware: Access DENIED for User {UserId} to Org {OrgId}. Path: {Path}", 
                            customUser.Id, orgId, path);
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync("Forbidden: You are not a member of this client and do not have firm-based access.");
                        return;
                    }
                }

                _logger.LogInformation("ClientAccessMiddleware: Access GRANTED for User {UserId} to Org {OrgId}. Path: {Path}", 
                    customUser.Id, orgId, path);
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


