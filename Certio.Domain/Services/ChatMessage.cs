namespace Certio.Domain.Services;

public class ChatMessage
{
    public int Id { get; set; }
    public string TenantId { get; set; } = "default";
    public string ConversationId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string UserType { get; set; } = ""; // Client, SeedJura, Lawyer, Business
    public string Content { get; set; } = "";
    public string MessageType { get; set; } = "Text"; // Text, AI_Summary, AI_Goal, AI_Reply, AI_Clarity
    public bool IsFromAI { get; set; } = false;
    public string? AIAgentType { get; set; } // ChatSummarizer, ClientGoalExtractor, ReplySuggester, ClarityAgent
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
    public string? Metadata { get; set; } // JSON for additional data
}
