namespace Certio.Domain.Services;

public class AIAgentResult
{
    public string AgentType { get; set; } = "";
    public string Content { get; set; } = "";
    public string Confidence { get; set; } = "Medium"; // Low, Medium, High
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public bool RequiresReview { get; set; } = false;
}

public class ChatSummary
{
    public string Summary { get; set; } = "";
    public List<string> KeyPoints { get; set; } = new();
    public string Sentiment { get; set; } = "Neutral"; // Positive, Negative, Neutral
    public string Urgency { get; set; } = "Medium"; // Low, Medium, High, Urgent
    public List<string> SuggestedActions { get; set; } = new();
}

public class ClientGoal
{
    public string PrimaryGoal { get; set; } = "";
    public List<string> SecondaryGoals { get; set; } = new();
    public string BusinessType { get; set; } = "";
    public string LegalArea { get; set; } = "";
    public string Timeline { get; set; } = "";
    public string Budget { get; set; } = "";
    public List<string> RequiredDocuments { get; set; } = new();
}

public class ReplySuggestion
{
    public string SuggestedReply { get; set; } = "";
    public string Tone { get; set; } = "Professional"; // Professional, Friendly, Formal, Casual
    public string Purpose { get; set; } = ""; // Clarification, Information, Action, etc.
    public List<string> KeyPoints { get; set; } = new();
    public bool RequiresLegalReview { get; set; } = false;
}

public class ClarityExplanation
{
    public string OriginalText { get; set; } = "";
    public string SimplifiedExplanation { get; set; } = "";
    public List<string> KeyTerms { get; set; } = new();
    public List<string> Implications { get; set; } = new();
    public string RiskLevel { get; set; } = "Low"; // Low, Medium, High
    public List<string> RecommendedActions { get; set; } = new();
}
