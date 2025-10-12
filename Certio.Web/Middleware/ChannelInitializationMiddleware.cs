using Certio.Infrastructure.Data;
using Certio.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Middleware;

public class ChannelInitializationMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly HashSet<int> _initializedOrganizations = new();
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public ChannelInitializationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, IChannelManagementService channelService)
    {
        // Check if we have an organization context
        if (context.Items.TryGetValue("CurrentOrganizationId", out var orgObj) && orgObj is int orgId)
        {
            // Check if this organization has been initialized
            if (!_initializedOrganizations.Contains(orgId))
            {
                await _semaphore.WaitAsync();
                try
                {
                    // Double-check inside the lock
                    if (!_initializedOrganizations.Contains(orgId))
                    {
                        // Get a user from the organization to use as creator
                        var orgUser = await db.UserOrganizations
                            .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                            .OrderBy(uo => uo.JoinedAt)
                            .FirstOrDefaultAsync();

                        if (orgUser != null)
                        {
                            await channelService.EnsureDefaultChannelsExistAsync(orgId, orgUser.UserId);
                            _initializedOrganizations.Add(orgId);
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        await _next(context);
    }
}

public static class ChannelInitializationMiddlewareExtensions
{
    public static IApplicationBuilder UseChannelInitialization(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ChannelInitializationMiddleware>();
    }
}

