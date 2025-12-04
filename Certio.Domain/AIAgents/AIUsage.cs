using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Organizations;

namespace Certio.Domain.AIAgents;

/// <summary>
/// Tracks individual AI API usage events for cost tracking and analytics
/// </summary>
public class AIUsage
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    public int OrganizationId { get; set; }
    
    /// <summary>
    /// The AI model used (e.g., gpt-4o-mini, gpt-4o, gpt-5)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string ModelName { get; set; } = "";
    
    /// <summary>
    /// The model tier selected (Auto, Basic, Advanced, Premium)
    /// </summary>
    [StringLength(20)]
    public string ModelTier { get; set; } = "Auto";
    
    /// <summary>
    /// Number of input tokens consumed
    /// </summary>
    public int InputTokens { get; set; }
    
    /// <summary>
    /// Number of output tokens generated
    /// </summary>
    public int OutputTokens { get; set; }
    
    /// <summary>
    /// Total tokens (input + output)
    /// </summary>
    public int TotalTokens { get; set; }
    
    /// <summary>
    /// Estimated cost in USD for this API call
    /// </summary>
    public decimal EstimatedCost { get; set; }
    
    /// <summary>
    /// The type of request (chat, dashboard-card, briefing, suggestion, etc.)
    /// </summary>
    [StringLength(50)]
    public string RequestType { get; set; } = "chat";
    
    /// <summary>
    /// Optional conversation ID for chat-based requests
    /// </summary>
    [StringLength(100)]
    public string? ConversationId { get; set; }
    
    /// <summary>
    /// Complexity score calculated for the request (0.0 - 1.0)
    /// </summary>
    public decimal? ComplexityScore { get; set; }
    
    /// <summary>
    /// Response time in milliseconds
    /// </summary>
    public int? ResponseTimeMs { get; set; }
    
    /// <summary>
    /// Whether this was a streaming response
    /// </summary>
    public bool IsStreaming { get; set; } = false;
    
    /// <summary>
    /// Whether the response was successful
    /// </summary>
    public bool IsSuccessful { get; set; } = true;
    
    /// <summary>
    /// Error message if the request failed
    /// </summary>
    [StringLength(500)]
    public string? ErrorMessage { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual User? User { get; set; }
    public virtual Organization? Organization { get; set; }
}

/// <summary>
/// Daily aggregated AI usage statistics per user/organization
/// </summary>
public class AIUsageDaily
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    public int OrganizationId { get; set; }
    
    /// <summary>
    /// The date (UTC) for this aggregate
    /// </summary>
    [Required]
    public DateTime Date { get; set; }
    
    /// <summary>
    /// Total API calls for the day
    /// </summary>
    public int TotalCalls { get; set; }
    
    /// <summary>
    /// Total input tokens for the day
    /// </summary>
    public int TotalInputTokens { get; set; }
    
    /// <summary>
    /// Total output tokens for the day
    /// </summary>
    public int TotalOutputTokens { get; set; }
    
    /// <summary>
    /// Total tokens for the day
    /// </summary>
    public int TotalTokens { get; set; }
    
    /// <summary>
    /// Total estimated cost for the day in USD
    /// </summary>
    public decimal TotalCost { get; set; }
    
    /// <summary>
    /// Calls by model: JSON {"gpt-4o-mini": 10, "gpt-4o": 5, "gpt-5": 1}
    /// </summary>
    public string? CallsByModel { get; set; }
    
    /// <summary>
    /// Cost by model: JSON {"gpt-4o-mini": 0.001, "gpt-4o": 0.05, "gpt-5": 0.10}
    /// </summary>
    public string? CostByModel { get; set; }
    
    /// <summary>
    /// Average complexity score for the day
    /// </summary>
    public decimal? AverageComplexityScore { get; set; }
    
    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public int? AverageResponseTimeMs { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual User? User { get; set; }
    public virtual Organization? Organization { get; set; }
}


