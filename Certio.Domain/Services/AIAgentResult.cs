using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Projects;
using Certio.Domain.Documents;

namespace Certio.Domain.Services;

public class AIAgentResult
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string AgentType { get; set; } = ""; // ChatSummarizer, ClientGoalExtractor, ReplySuggester, etc.
    
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Processing"; // Processing, Completed, Failed, RequiresReview
    
    [Required]
    public string Content { get; set; } = "";
    
    [StringLength(20)]
    public string Confidence { get; set; } = "Medium"; // Low, Medium, High
    
    public string? Metadata { get; set; } // JSON string for flexible data
    
    public int? ConversationId { get; set; }
    public int? ProjectId { get; set; }
    public int? DocumentId { get; set; }
    public int? ServiceRequestId { get; set; }
    
    public bool RequiresReview { get; set; } = false;
    public bool IsApproved { get; set; } = false;
    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Conversation? Conversation { get; set; }
    public virtual Project? Project { get; set; }
    public virtual Document? Document { get; set; }
    public virtual ServiceRequest? ServiceRequest { get; set; }
    public virtual User? ReviewedBy { get; set; }
}

public class ChatSummary
{
    public int Id { get; set; }
    
    public int ConversationId { get; set; }
    
    [Required]
    public string Summary { get; set; } = "";
    
    public string? KeyPoints { get; set; } // JSON array as string
    
    [StringLength(20)]
    public string Sentiment { get; set; } = "Neutral"; // Positive, Negative, Neutral
    
    [StringLength(20)]
    public string Urgency { get; set; } = "Medium"; // Low, Medium, High, Urgent
    
    public string? SuggestedActions { get; set; } // JSON array as string
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Conversation Conversation { get; set; } = null!;
}

public class ClientGoal
{
    public int Id { get; set; }
    
    public int? ConversationId { get; set; }
    public int? ProjectId { get; set; }
    public int? ServiceRequestId { get; set; }
    
    [Required]
    public string PrimaryGoal { get; set; } = "";
    
    public string? SecondaryGoals { get; set; } // JSON array as string
    
    [StringLength(100)]
    public string? BusinessType { get; set; }
    
    [StringLength(100)]
    public string? LegalArea { get; set; }
    
    [StringLength(50)]
    public string? Timeline { get; set; }
    
    [StringLength(50)]
    public string? Budget { get; set; }
    
    public string? RequiredDocuments { get; set; } // JSON array as string
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Conversation? Conversation { get; set; }
    public virtual Project? Project { get; set; }
    public virtual ServiceRequest? ServiceRequest { get; set; }
}

public class ReplySuggestion
{
    public int Id { get; set; }
    
    public int? ConversationId { get; set; }
    public int? ServiceRequestId { get; set; }
    
    [Required]
    public string SuggestedReply { get; set; } = "";
    
    [StringLength(20)]
    public string Tone { get; set; } = "Professional"; // Professional, Friendly, Formal, Casual
    
    [StringLength(50)]
    public string? Purpose { get; set; } // Clarification, Information, Action, etc.
    
    public string? KeyPoints { get; set; } // JSON array as string
    
    public bool RequiresLegalReview { get; set; } = false;
    public bool IsUsed { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Conversation? Conversation { get; set; }
    public virtual ServiceRequest? ServiceRequest { get; set; }
}

public class ClarityExplanation
{
    public int Id { get; set; }
    
    public int? DocumentId { get; set; }
    public int? ConversationId { get; set; }
    
    [Required]
    public string OriginalText { get; set; } = "";
    
    [Required]
    public string SimplifiedExplanation { get; set; } = "";
    
    public string? KeyTerms { get; set; } // JSON array as string
    
    public string? Implications { get; set; } // JSON array as string
    
    [StringLength(20)]
    public string RiskLevel { get; set; } = "Low"; // Low, Medium, High
    
    public string? RecommendedActions { get; set; } // JSON array as string
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Document? Document { get; set; }
    public virtual Conversation? Conversation { get; set; }
}
