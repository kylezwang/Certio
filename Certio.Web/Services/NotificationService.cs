using Microsoft.AspNetCore.SignalR;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Web.Hubs;

namespace Certio.Web.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendUserNotificationAsync(int userId, string title, string message, string type = "Info")
        {
            await _hubContext.Clients.Group($"user_{userId}")
                .SendAsync("ReceiveNotification", new
                {
                    title,
                    message,
                    type,
                    timestamp = DateTime.UtcNow
                });
        }

        public async Task SendMatterNotificationAsync(int matterId, string title, string message, string type = "Info")
        {
            await _hubContext.Clients.Group($"matter_{matterId}")
                .SendAsync("ReceiveNotification", new
                {
                    title,
                    message,
                    type,
                    matterId,
                    timestamp = DateTime.UtcNow
                });
        }

        public async Task SendOrganizationNotificationAsync(int organizationId, string title, string message, string type = "Info")
        {
            await _hubContext.Clients.Group($"org_{organizationId}")
                .SendAsync("ReceiveNotification", new
                {
                    title,
                    message,
                    type,
                    organizationId,
                    timestamp = DateTime.UtcNow
                });
        }

        public async Task NotifyEntityChangeAsync(string entityType, int entityId, string action, int organizationId, int? userId = null)
        {
            var notification = new
            {
                entityType,
                entityId,
                action,
                userId,
                organizationId,
                timestamp = DateTime.UtcNow
            };

            // Every case below is scoped to the owning organization's group. This
            // used to fall back to Clients.All for tasks and any unrecognized
            // entity type, which meant every connected client - across every
            // tenant - received every task/entity change in the system. Matters
            // additionally fan out to their own matter group for clients already
            // subscribed to that matter.
            switch (entityType.ToLower())
            {
                case "matter":
                    await _hubContext.Clients.Group($"matter_{entityId}")
                        .SendAsync("EntityChanged", notification);
                    await _hubContext.Clients.Group($"org_{organizationId}")
                        .SendAsync("EntityChanged", notification);
                    break;

                default:
                    await _hubContext.Clients.Group($"org_{organizationId}")
                        .SendAsync("EntityChanged", notification);
                    break;
            }
        }

        public async Task NotifyAIContentPendingApprovalAsync(int organizationId, AIContentReviewDto content)
        {
            await _hubContext.Clients.Group($"org_{organizationId}")
                .SendAsync("AIContentPendingApproval", new
                {
                    content.EntityType,
                    content.EntityId,
                    Title = content.EntityTitle,
                    content.AIAgentType,
                    content.CreatedAt,
                    timestamp = DateTime.UtcNow
                });
        }
    }
}

