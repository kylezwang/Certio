using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.AIAgents
{
    public class AIAgent
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = ""; // ChatSummarizer, ClientGoalExtractor, etc.
        
        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; } = "";
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        [Required]
        [StringLength(20)]
        public string AgentType { get; set; } = ""; // ChatSummarizer, ClientGoalExtractor, ReplySuggester, ClarityAgent, DocFiller, etc.
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Maintenance, Error
        
        [StringLength(1000)]
        public string? Configuration { get; set; } // JSON configuration
        
        [StringLength(1000)]
        public string? PromptTemplate { get; set; }
        
        public bool IsEnabled { get; set; } = true;
        public bool RequiresReview { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        public DateTime? LastExecutedAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<AIAgentExecution> Executions { get; set; } = new List<AIAgentExecution>();
    }
    
    public class AIAgentExecution
    {
        public int Id { get; set; }
        
        public int AIAgentId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Running"; // Running, Completed, Failed, Cancelled
        
        public string? InputData { get; set; } // JSON input
        public string? OutputData { get; set; } // JSON output
        public string? ErrorMessage { get; set; }
        
        // Simplified: Generic foreign key approach
        public int? RelatedEntityId { get; set; } // Generic foreign key
        public string? RelatedEntityType { get; set; } // "Project", "Document", "Conversation", etc.
        
        public int? TriggeredById { get; set; }
        
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        
        // Navigation properties
        public virtual AIAgent AIAgent { get; set; } = null!;
        public virtual User? TriggeredBy { get; set; }
    }
}
