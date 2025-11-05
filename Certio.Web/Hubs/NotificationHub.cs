using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Certio.Application.Interfaces;

namespace Certio.Web.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly IPermissionService _permissionService;

        public NotificationHub(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        public async Task JoinUserGroup(int userId)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue || currentUserId.Value != userId)
            {
                await Clients.Caller.SendAsync("Error", "Unauthorized to join requested user group");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        public async Task LeaveUserGroup(int userId)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue || currentUserId.Value != userId)
            {
                // Silently ignore to avoid information disclosure
                return;
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        public async Task JoinMatterGroup(int matterId)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "Authentication required");
                return;
            }

            var canAccessMatter = await _permissionService.CanAccessMatterAsync(currentUserId.Value, matterId);
            if (!canAccessMatter)
            {
                await Clients.Caller.SendAsync("Error", "Unauthorized to subscribe to this matter");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"matter_{matterId}");
        }

        public async Task LeaveMatterGroup(int matterId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"matter_{matterId}");
        }

        public async Task JoinOrganizationGroup(int organizationId)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "Authentication required");
                return;
            }

            var isMember = await _permissionService.IsOrganizationMemberAsync(currentUserId.Value, organizationId);
            if (!isMember)
            {
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(currentUserId.Value, organizationId);
                if (!hasFirmAccess)
                {
                    await Clients.Caller.SendAsync("Error", "Unauthorized to subscribe to this organization");
                    return;
                }
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{organizationId}");
        }

        public async Task LeaveOrganizationGroup(int organizationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"org_{organizationId}");
        }

        public override async Task OnConnectedAsync()
        {
            // Auto-join user to their own group
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId.Value}");
            }
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Cleanup is automatic for groups
            await base.OnDisconnectedAsync(exception);
        }

        private int? GetCurrentUserId()
        {
            var httpContext = Context.GetHttpContext();
            if (httpContext?.Items.TryGetValue("CustomUserId", out var customUserId) == true && customUserId is int userId)
            {
                return userId;
            }

            var principal = Context.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? principal.FindFirst("UserId")?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }
    }
}

