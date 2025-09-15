using Microsoft.AspNetCore.Identity;
using Certio.Web.Services;

namespace Certio.Web.Middleware
{
    /// <summary>
    /// Middleware to automatically sync Identity users with custom User records
    /// Runs after authentication to ensure custom User records exist
    /// </summary>
    public class UserSyncMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UserSyncMiddleware> _logger;

        public UserSyncMiddleware(RequestDelegate next, ILogger<UserSyncMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IUserSyncService userSyncService, UserManager<IdentityUser> userManager)
        {
            // Only process authenticated users
            if (context.User.Identity?.IsAuthenticated == true)
            {
                try
                {
                    var identityUserId = userManager.GetUserId(context.User);
                    if (!string.IsNullOrEmpty(identityUserId))
                    {
                        // Ensure custom user exists
                        var customUser = await userSyncService.EnsureCustomUserExistsAsync(identityUserId);
                        
                        if (customUser != null)
                        {
                            // Backfill defaults for legacy users
                            customUser = await userSyncService.EnsureDefaultsAsync(customUser);

                            // Store custom user ID in context for easy access
                            context.Items["CustomUserId"] = customUser.Id;
                            context.Items["CustomUser"] = customUser;
                            
                            _logger.LogDebug("Synced user {IdentityUserId} with custom user {CustomUserId}", 
                                identityUserId, customUser.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to sync user {IdentityUserId}", identityUserId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in UserSyncMiddleware for user {UserId}", 
                        userManager.GetUserId(context.User));
                }
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Extension methods for registering the UserSyncMiddleware
    /// </summary>
    public static class UserSyncMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserSync(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UserSyncMiddleware>();
        }
    }
}
