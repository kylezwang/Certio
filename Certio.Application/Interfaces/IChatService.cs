using Certio.Domain.Services;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service interface for chat and communication operations
    /// Manages conversations, channels, and messages with organization isolation
    /// </summary>
    public interface IChatService
    {
        // Organization-scoped conversation management
        Task<Conversation> CreateConversationAsync(int organizationId, int userId, string title, string description, int? matterId = null);
        Task<Conversation> CreateChannelAsync(int organizationId, int userId, string title, string description, string channelType = "Group", bool isPrivateChannel = false, int? matterId = null);
        Task<ChatMessage> SendMessageAsync(int conversationId, int? userId, string userType, string content, string messageType = "Text");
        Task<ChatMessage> SendChannelMessageAsync(int conversationId, int? userId, string userType, string content, string messageType = "Text", int? channelId = null, int? replyToMessageId = null);
        Task<ChatMessage> EditMessageAsync(int messageId, string newContent, int userId);
        Task<bool> AddReactionAsync(int messageId, int userId, string emoji);
        Task<bool> RemoveReactionAsync(int messageId, int userId, string emoji);
        Task<List<ChatMessage>> GetConversationMessagesAsync(int conversationId);
        Task<List<ChatMessage>> GetChannelMessagesAsync(int conversationId, int? channelId = null);
        Task<List<ChatMessage>> GetMessagesWithRepliesAsync(int conversationId);
        Task<List<Conversation>> GetUserConversationsAsync(int userId, int organizationId);
        Task<List<Conversation>> GetUserAIConversationsAsync(int userId, int organizationId);
        Task<List<Conversation>> GetChannelsAsync(int organizationId, bool includePrivateChannels = false);
        Task<List<Conversation>> GetUserChannelsAsync(int userId, int organizationId);
        Task<Conversation?> GetConversationAsync(int conversationId, int organizationId);
        
        // AI Services
        Task<ClarityExplanation> RequestClarityAsync(int conversationId, string text, string userType);
        Task<ReplySuggestion> GetReplySuggestionsAsync(int conversationId, List<ChatMessage> messages, string userType);
        Task<ChatMessage> GenerateAIResponseAsync(int conversationId, string userMessage);
        Task<Dictionary<string, object>> GetAIInsightsAsync(int conversationId);
        Task<ChatSummary?> GetConversationSummaryAsync(int conversationId);
        Task<ClientGoal?> GetClientGoalsAsync(int conversationId);
        Task<ReplySuggestion?> GetLatestReplySuggestionAsync(int conversationId);
        
        // Conversation management
        Task<bool> RenameConversationAsync(int conversationId, int organizationId, string newTitle);
        Task<bool> DeleteConversationAsync(int conversationId, int organizationId, int userId, string? ipAddress = null, string? userAgent = null);
        
        // Permission validation
        Task<bool> CanUserAccessConversationAsync(int conversationId, int userId, int organizationId);
        
        // Organization-scoped queries
        Task<List<Conversation>> GetOrganizationConversationsAsync(int organizationId, int? userId = null);
    }
}

