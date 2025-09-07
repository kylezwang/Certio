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
    Task<Conversation> CreateConversationAsync(string title, string description, string clientId, string? seedJuraId = null, string? lawyerId = null, string? businessId = null);
    Task<ChatMessage> SendMessageAsync(string conversationId, string userId, string userType, string content, string messageType = "Text");
    Task<List<ChatMessage>> GetConversationMessagesAsync(string conversationId);
    Task<List<Conversation>> GetUserConversationsAsync(string userId);
    Task<ClarityExplanation> RequestClarityAsync(string conversationId, string text, string userType);
    Task<ReplySuggestion> GetReplySuggestionsAsync(string conversationId, List<ChatMessage> messages, string userType);
    Task<ChatMessage> GenerateAIResponseAsync(string conversationId, string userMessage);
    Task<Dictionary<string, object>> GetAIInsightsAsync(string conversationId);
    Task<ChatSummary?> GetConversationSummaryAsync(string conversationId);
    Task<ClientGoal?> GetClientGoalsAsync(string conversationId);
    Task<ReplySuggestion?> GetLatestReplySuggestionAsync(string conversationId);
    Task<bool> RenameConversationAsync(string conversationId, string newTitle);
    Task<bool> DeleteConversationAsync(string conversationId);
}
