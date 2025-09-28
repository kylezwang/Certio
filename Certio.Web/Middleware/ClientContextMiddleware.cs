using System.Text.RegularExpressions;
using Certio.Web.Data;
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

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, Certio.Web.Services.IClientContextAccessor accessor)
        {
            // Parse orgId from /Client/{orgId}/... routes
            var path = context.Request.Path.Value ?? string.Empty;
            var match = Regex.Match(path, @"^/Client/(?<orgId>\d+)(/|$)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (int.TryParse(match.Groups["orgId"].Value, out var orgId))
                {
                    context.Items["CurrentOrganizationId"] = orgId;
                    // Build the ClientContext once per request
                    var clientContext = await ClientContext.CreateAsync(context, db, context.RequestAborted);
                    accessor.ClientContext = clientContext;
                }
            }

            await _next(context);
        }
    }

    // Accessor moved to Certio.Web.Services
}


