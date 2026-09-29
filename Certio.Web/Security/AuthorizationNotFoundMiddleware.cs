using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Certio.Web.Security
{
    // Converts authorization failures on client routes to 404 to prevent IDOR disclosure
    public sealed class AuthorizationNotFoundMiddleware : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

        public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            
            if (!authorizeResult.Succeeded)
            {
                if (path.StartsWith("/Client/", StringComparison.OrdinalIgnoreCase))
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<AuthorizationNotFoundMiddleware>>();
                    logger.LogError("AuthorizationNotFoundMiddleware: Authorization FAILED for path: {Path}, Returning 404", path);
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
            }

            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}


