using System.Text.RegularExpressions;
using Certio.Infrastructure.Data;
using Certio.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Middleware
{
    public class ClientContextMiddleware
    {
        private readonly RequestDelegate _next;

        public ClientContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, Certio.Web.Services.IClientContextAccessor accessor, IFirmRelationshipCacheService cacheService)
        {
            int? orgId = null;
            
            // Parse orgId from /Client/{orgId}/... routes
            var path = context.Request.Path.Value ?? string.Empty;
            var match = Regex.Match(path, @"^/Client/(?<orgId>\d+)(/|$)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (int.TryParse(match.Groups["orgId"].Value, out var parsedOrgId))
                {
                    orgId = parsedOrgId;
                }
            }
            // Also check for API routes with orgId query parameter or X-Organization-Id header
            else if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                // First try query parameter (e.g., /api/dm/threads?orgId=1)
                if (context.Request.Query.TryGetValue("orgId", out var orgIdValue) && 
                    int.TryParse(orgIdValue.FirstOrDefault(), out var parsedOrgId))
                {
                    orgId = parsedOrgId;
                }
                // Then try X-Organization-Id header (for agent actions API)
                else if (context.Request.Headers.TryGetValue("X-Organization-Id", out var headerOrgId) && 
                    int.TryParse(headerOrgId.FirstOrDefault(), out var parsedHeaderOrgId))
                {
                    orgId = parsedHeaderOrgId;
                }
            }

            // Build ClientContext if we have an orgId
            if (orgId.HasValue)
            {
                context.Items["CurrentOrganizationId"] = orgId.Value;
                // Build the ClientContext once per request
                var clientContext = await ClientContext.CreateAsync(context, db, cacheService, context.RequestAborted);
                accessor.ClientContext = clientContext;
            }

            await _next(context);
        }
    }

    // Accessor moved to Certio.Web.Services
}


