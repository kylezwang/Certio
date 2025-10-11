using Certio.Domain.Services;

namespace Certio.Application.Services;

public interface IAIAgentService
{
    Task<ChatSummary> SummarizeConversationAsync(string conversationId, List<ChatMessage> messages);
    Task<ClientGoal> ExtractClientGoalsAsync(string conversationId, List<ChatMessage> messages);
    Task<ReplySuggestion> SuggestReplyAsync(string conversationId, List<ChatMessage> messages, string userType);
    Task<ClarityExplanation> ExplainLegalLanguageAsync(string text, string userType);
    Task<string> GenerateConversationalResponseAsync(string conversationId, List<ChatMessage> messages, string userMessage);
    Task<Dictionary<string, object>> ProcessConversationIntelligentlyAsync(string conversationId, List<ChatMessage> messages, string userType = "Client");
}

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
    Task<bool> DeleteConversationAsync(int conversationId, int organizationId);
    
    // Organization-scoped queries
    Task<List<Conversation>> GetOrganizationConversationsAsync(int organizationId, int? userId = null);
}
