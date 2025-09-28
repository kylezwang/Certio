using System.Text.RegularExpressions;
using Certio.Web.Data;
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

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
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

                var isMember = await db.UserOrganizations.AnyAsync(uo => uo.UserId == customUser.Id && uo.OrganizationId == orgId && uo.IsActive);
                if (!isMember)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Forbidden: You are not a member of this client.");
                    return;
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


