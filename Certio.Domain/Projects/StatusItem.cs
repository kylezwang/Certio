using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Documents;

namespace Certio.Domain.Projects
{
    public class StatusItem
    {
        public int Id { get; set; }
        
        public int ProjectId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";
        
        [StringLength(1000)]
        public string? Description { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Blocked, Cancelled
        
        [Required]
        [StringLength(20)]
        public string Priority { get; set; } = "Medium"; // High, Medium, Low
        
        [StringLength(20)]
        public string Category { get; set; } = "General"; // Analysis, Closing, Documentation, Review, etc.
        
        public int? ParentStatusItemId { get; set; }
        
        public int Order { get; set; } = 0;
        
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        
        // Navigation properties
        public virtual Project Project { get; set; } = null!;
        public virtual StatusItem? ParentStatusItem { get; set; }
        public virtual ICollection<StatusItem> SubStatusItems { get; set; } = new List<StatusItem>();
        public virtual ICollection<StatusItemDependency> Dependencies { get; set; } = new List<StatusItemDependency>();
        public virtual ICollection<StatusItemDependency> DependentItems { get; set; } = new List<StatusItemDependency>();
        public virtual ICollection<StatusItemAssignment> Assignments { get; set; } = new List<StatusItemAssignment>();
        public virtual ICollection<StatusItemComment> Comments { get; set; } = new List<StatusItemComment>();
        public virtual ICollection<Document> RelatedDocuments { get; set; } = new List<Document>();
    }
    
    public class StatusItemDependency
    {
        public int Id { get; set; }
        
        public int StatusItemId { get; set; }
        public int DependsOnStatusItemId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string DependencyType { get; set; } = "FinishToStart"; // FinishToStart, StartToStart, FinishToFinish, StartToFinish
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual StatusItem StatusItem { get; set; } = null!;
        public virtual StatusItem DependsOnStatusItem { get; set; } = null!;
    }
    
    public class StatusItemAssignment
    {
        public int Id { get; set; }
        
        public int StatusItemId { get; set; }
        public int UserId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string AssignmentType { get; set; } = "Assignee"; // Assignee, Reviewer, Observer
        
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        
        // Navigation properties
        public virtual StatusItem StatusItem { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
    
    public class StatusItemComment
    {
        public int Id { get; set; }
        
        public int StatusItemId { get; set; }
        public int UserId { get; set; }
        
        [Required]
        [StringLength(2000)]
        public string Content { get; set; } = "";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        
        // Navigation properties
        public virtual StatusItem StatusItem { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
