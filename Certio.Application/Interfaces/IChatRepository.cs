using Certio.Domain.Services;

namespace Certio.Application.Interfaces;

public interface IChatRepository
{
    Task<Conversation> CreateConversationAsync(Conversation conversation);
    Task<ChatMessage> SendMessageAsync(ChatMessage message);
    Task<List<ChatMessage>> GetConversationMessagesAsync(string conversationId);
    Task<List<Conversation>> GetUserConversationsAsync(string userId);
    Task UpdateConversationAsync(Conversation conversation);
}
