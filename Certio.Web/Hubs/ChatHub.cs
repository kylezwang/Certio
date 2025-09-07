using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Certio.Application.Services;
using System.Text.Json;

namespace Certio.Web.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
    }

    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
    }

    public async Task SendMessage(string conversationId, string userId, string userType, string content, string messageType = "Text")
    {
        try
        {
            var message = await _chatService.SendMessageAsync(conversationId, userId, userType, content, messageType);
            
            // Send message to all clients in the conversation group
            await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveMessage", new
            {
                message.Id,
                message.ConversationId,
                message.UserId,
                message.UserType,
                message.Content,
                message.MessageType,
                message.IsFromAI,
                message.AIAgentType,
                message.CreatedAt,
                message.IsRead
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to send message: {ex.Message}");
        }
    }

    public async Task RequestClarity(string conversationId, string text, string userType)
    {
        try
        {
            // This would typically be handled by the AI service
            // For now, we'll just echo back a placeholder
            await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveClarity", new
            {
                OriginalText = text,
                Explanation = "AI clarity explanation would appear here",
                UserType = userType,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to process clarity request: {ex.Message}");
        }
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
