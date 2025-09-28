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
    Task<Conversation> CreateConversationAsync(int organizationId, int userId, string title, string description, int? matterId = null, int? serviceRequestId = null);
    Task<ChatMessage> SendMessageAsync(int conversationId, int? userId, string userType, string content, string messageType = "Text");
    Task<List<ChatMessage>> GetConversationMessagesAsync(int conversationId);
    Task<List<Conversation>> GetUserConversationsAsync(int userId, int organizationId);
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
