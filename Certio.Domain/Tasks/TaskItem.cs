using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Documents;
using Certio.Domain.Matters;

namespace Certio.Domain.Tasks
{
    public class TaskItem
    {
        public int Id { get; set; }
        
        [Required]
        public int OrgId { get; set; }
        
        [Required]
        public int MatterId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";
        
        [StringLength(2000)]
        public string? Description { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Review, Completed
        
        [Required]
        [StringLength(20)]
        public string Priority { get; set; } = "Medium"; // High, Medium, Low, Critical
        
        [StringLength(200)]
        public string? Location { get; set; }
        
        public int Order { get; set; } = 0;
        
        // Audit fields
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedById { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public int? ModifiedById { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; } // Legacy - use ModifiedAt
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        
        // Soft delete fields
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedById { get; set; }
        
        // AI generation fields
        public bool IsAIGenerated { get; set; } = false;
        
        [StringLength(50)]
        public string? AIAgentType { get; set; }
        
        public string? AIGenerationMetadata { get; set; } // JSON
        public int? SourceConversationId { get; set; }
        public int? SourceMessageId { get; set; }
        
        // Approval fields for AI-generated content
        [StringLength(20)]
        public string? ApprovalStatus { get; set; } // Pending, Approved, Rejected
        
        public int? ApprovedById { get; set; }
        public DateTime? ApprovedAt { get; set; }
        
        [StringLength(500)]
        public string? ApprovalNotes { get; set; }
        
        // Parent-Child relationship for SubTasks
        public int? ParentTaskItemId { get; set; }
        
        // Navigation properties
        public virtual Certio.Domain.Organizations.Organization? Organization { get; set; }
        public virtual Matter Matter { get; set; } = null!;
        public virtual TaskItem? ParentTaskItem { get; set; }
        public virtual ICollection<TaskItem> SubTaskItems { get; set; } = new List<TaskItem>();
        public virtual ICollection<SubTaskItem> SubTasks { get; set; } = new List<SubTaskItem>();
        public virtual ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();
        public virtual ICollection<TaskItemComment> Comments { get; set; } = new List<TaskItemComment>();
        public virtual ICollection<Document> RelatedDocuments { get; set; } = new List<Document>();
        public virtual ICollection<TaskItemDependency> Dependencies { get; set; } = new List<TaskItemDependency>();
        public virtual ICollection<TaskItemDependency> DependentItems { get; set; } = new List<TaskItemDependency>();
    }
    
    public class TaskAssignment
    {
        public int Id { get; set; }
        
        [Required]
        public int TaskItemId { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string AssignmentType { get; set; } = "Assignee"; // Assignee, Reviewer, Observer, Contributor
        
        [StringLength(100)]
        public string? Role { get; set; } // Free-text involvement description
        
        public bool IsNotifyRecipient { get; set; } = true;
        
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public DateTime? RemovedAt { get; set; }
        
        // Navigation properties
        public virtual TaskItem TaskItem { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
    
    public class TaskItemComment
    {
        public int Id { get; set; }
        
        [Required]
        public int TaskItemId { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        [StringLength(2000)]
        public string Content { get; set; } = "";
        
        public int? ParentCommentId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedAt { get; set; }
        
        // Navigation properties
        public virtual TaskItem TaskItem { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public virtual TaskItemComment? ParentComment { get; set; }
        public virtual ICollection<TaskItemComment> Replies { get; set; } = new List<TaskItemComment>();
        public virtual ICollection<TaskCommentMention> Mentions { get; set; } = new List<TaskCommentMention>();
        public virtual ICollection<TaskCommentReaction> Reactions { get; set; } = new List<TaskCommentReaction>();
    }
    
    public class TaskItemDependency
    {
        public int Id { get; set; }
        
        [Required]
        public int TaskItemId { get; set; }
        
        [Required]
        public int DependsOnTaskItemId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string DependencyType { get; set; } = "FinishToStart"; // FinishToStart, StartToStart, FinishToFinish, StartToFinish
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual TaskItem TaskItem { get; set; } = null!;
        public virtual TaskItem DependsOnTaskItem { get; set; } = null!;
    }
}



