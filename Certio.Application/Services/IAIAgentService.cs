using Certio.Domain.Services;

namespace Certio.Application.Services;

/// <summary>
/// AI Agent service for intelligent conversation processing
/// </summary>
public interface IAIAgentService
{
    Task<ChatSummary> SummarizeConversationAsync(string conversationId, List<ChatMessage> messages);
    Task<ClientGoal> ExtractClientGoalsAsync(string conversationId, List<ChatMessage> messages);
    Task<ReplySuggestion> SuggestReplyAsync(string conversationId, List<ChatMessage> messages, string userType);
    Task<ClarityExplanation> ExplainLegalLanguageAsync(string text, string userType);
    Task<string> GenerateConversationalResponseAsync(string conversationId, List<ChatMessage> messages, string userMessage);
    Task<Dictionary<string, object>> ProcessConversationIntelligentlyAsync(string conversationId, List<ChatMessage> messages, string userType = "Client");
}
