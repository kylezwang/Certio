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

        public async Task NotifyEntityChangeAsync(string entityType, int entityId, string action, int? userId = null)
        {
            var notification = new
            {
                entityType,
                entityId,
                action,
                userId,
                timestamp = DateTime.UtcNow
            };

            // Notify based on entity type
            switch (entityType.ToLower())
            {
                case "matter":
                    await _hubContext.Clients.Group($"matter_{entityId}")
                        .SendAsync("EntityChanged", notification);
                    break;

                case "taskitem":
                case "task":
                    // Notify users following this task
                    await _hubContext.Clients.All
                        .SendAsync("EntityChanged", notification);
                    break;

                default:
                    await _hubContext.Clients.All
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

