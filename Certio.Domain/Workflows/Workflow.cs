using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Workflows
{
    public class Workflow
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = "";
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        [Required]
        [StringLength(20)]
        public string WorkflowType { get; set; } = ""; // MatterCreation, DocumentReview, etc.
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Draft
        
        public string? Configuration { get; set; } // JSON workflow configuration
        
        public bool IsSystem { get; set; } = false; // System workflows vs custom
        public bool IsEnabled { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        
        // Navigation properties
        public virtual ICollection<WorkflowInstance> Instances { get; set; } = new List<WorkflowInstance>();
    }
    
    public class WorkflowInstance
    {
        public int Id { get; set; }
        
        public int WorkflowId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Running"; // Running, Completed, Failed, Cancelled, Paused
        
        // Simplified: Generic foreign key approach
        public int? RelatedEntityId { get; set; } // Generic foreign key
        public string? RelatedEntityType { get; set; } // "Matter", "Document", etc.
        
        public int? StartedById { get; set; }
        
        public string? ContextData { get; set; } // JSON context data
        
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        
        // Navigation properties
        public virtual Workflow Workflow { get; set; } = null!;
        public virtual User? StartedBy { get; set; }
    }
}
