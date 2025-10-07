using System.ComponentModel.DataAnnotations;
using Certio.Domain.Matters;

namespace Certio.Domain.Tasks
{
    public class SubTaskItem
    {
        public int Id { get; set; }

        [Required]
        public int TaskId { get; set; }

        [Required]
        public int MatterId { get; set; }

        [Required]
        public int OrgId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedAt { get; set; }
        public DateTime? DueDate { get; set; }

        public bool IsCompleted { get; set; } = false;
        
        public DateTime? CompletedAt { get; set; }

        // Navigation properties
        public virtual TaskItem Task { get; set; } = null!;
        public virtual Matter Matter { get; set; } = null!;
        public virtual ICollection<SubTaskAssignment> Assignments { get; set; } = new List<SubTaskAssignment>();
    }

    public class SubTaskAssignment
    {
        public int Id { get; set; }
        
        [Required]
        public int SubTaskItemId { get; set; }
        
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
        public virtual SubTaskItem SubTaskItem { get; set; } = null!;
        public virtual Certio.Domain.Users.User User { get; set; } = null!;
    }
}
