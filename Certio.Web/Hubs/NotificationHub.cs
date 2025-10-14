using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace Certio.Web.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public async Task JoinUserGroup(int userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        public async Task LeaveUserGroup(int userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        public async Task JoinMatterGroup(int matterId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"matter_{matterId}");
        }

        public async Task LeaveMatterGroup(int matterId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"matter_{matterId}");
        }

        public async Task JoinOrganizationGroup(int organizationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{organizationId}");
        }

        public async Task LeaveOrganizationGroup(int organizationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"org_{organizationId}");
        }

        public override async Task OnConnectedAsync()
        {
            // Auto-join user to their own group
            var userId = Context.UserIdentifier;
            if (userId != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Cleanup is automatic for groups
            await base.OnDisconnectedAsync(exception);
        }
    }
}

