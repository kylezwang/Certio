using Certio.Application.DTOs;

namespace Certio.Application.Interfaces
{
    public interface INotificationService
    {
        /// <summary>
        /// Send a real-time notification to a user
        /// </summary>
        Task SendUserNotificationAsync(int userId, string title, string message, string type = "Info");

        /// <summary>
        /// Send a real-time notification to all users in a matter
        /// </summary>
        Task SendMatterNotificationAsync(int matterId, string title, string message, string type = "Info");

        /// <summary>
        /// Send a real-time notification to all users in an organization
        /// </summary>
        Task SendOrganizationNotificationAsync(int organizationId, string title, string message, string type = "Info");

        /// <summary>
        /// Notify about entity changes (Matter, Task, etc.). Always scoped to the
        /// owning organization - never broadcast to every connected client.
        /// </summary>
        Task NotifyEntityChangeAsync(string entityType, int entityId, string action, int organizationId, int? userId = null);

        /// <summary>
        /// Notify about AI-generated content pending approval
        /// </summary>
        Task NotifyAIContentPendingApprovalAsync(int organizationId, AIContentReviewDto content);
    }
}

