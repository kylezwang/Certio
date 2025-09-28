using System.ComponentModel.DataAnnotations;
using Certio.Domain.Teams;
using Certio.Domain.Users;
using Certio.Domain.Documents;
using Certio.Domain.Services;

namespace Certio.Domain.Matters;

public class Matter
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";
    
    [StringLength(1000)]
    public string Description { get; set; } = "";
    
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Planning"; // Planning, InProgress, Review, Completed, OnHold, Cancelled
    
    [Required]
    [StringLength(20)]
    public string Priority { get; set; } = "Medium"; // High, Medium, Low
    
    [StringLength(100)]
    public string PracticeArea { get; set; } = ""; // Contract Review, LLC Formation, Real Estate, etc.
    
    [StringLength(100)]
    public string MatterType { get; set; } = "Legal"; // Legal, Business, Compliance, etc.
    
    [StringLength(20)]
    public string AccessLevel { get; set; } = ""; // Everyone, Specific
    
    [Required]
    public int OrganizationId { get; set; }
    
    public int? TeamId { get; set; }
    public int? ClientId { get; set; }
    
    
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    
    // New date fields
    public DateTime? PendingDate { get; set; }
    public DateTime? StatuteOfLimitationsDate { get; set; }
    public bool StatuteOfLimitationsSatisfied { get; set; } = false;
    
    [StringLength(1000)]
    public string? ClientGoals { get; set; }
    
    [StringLength(1000)]
    public string? LegalRequirements { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDate { get; set; }
    
    // Computed properties for views
    public int TasksCompleted => StatusItems.Count(si => si.Status == "Completed");
    public int TotalTasks => StatusItems.Count;
    public string Assignees => string.Join(", ", Assignments.Select(a => a.User.FirstName + " " + a.User.LastName));
    
    // Helper properties for firm assignments
    public User? OriginatingAttorney => Assignments.FirstOrDefault(a => a.AssignmentType == "OriginatingAttorney")?.User;
    public User? ResponsibleAttorney => Assignments.FirstOrDefault(a => a.AssignmentType == "ResponsibleAttorney")?.User;
    public User? ResponsibleStaff => Assignments.FirstOrDefault(a => a.AssignmentType == "ResponsibleStaff")?.User;
    
    // Navigation properties
    public virtual Team? Team { get; set; }
    public virtual User? Client { get; set; }
    public virtual ICollection<StatusItem> StatusItems { get; set; } = new List<StatusItem>();
    public virtual ICollection<MatterAssignment> Assignments { get; set; } = new List<MatterAssignment>();
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
    public virtual ICollection<MatterPermission> Permissions { get; set; } = new List<MatterPermission>();
}

public class MatterAssignment
{
    public int Id { get; set; }
    
    public int MatterId { get; set; }
    public int UserId { get; set; }
    
    [Required]
    [StringLength(20)]
    public string AssignmentType { get; set; } = "RelevantContact"; // OriginatingAttorney, ResponsibleAttorney, ResponsibleStaff, RelevantContact
    
    [Required]
    [StringLength(100)]
    public string Role { get; set; } = ""; // Free-text involvement description
    
    public bool IsNotifyRecipient { get; set; } = true;
    
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAt { get; set; }
    
    // Navigation properties
    public virtual Matter Matter { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public class MatterPermission
{
    public int Id { get; set; }
    
    public int MatterId { get; set; }
    public int UserId { get; set; }
    
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public int? GrantedById { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int? RevokedById { get; set; }
    
    // Navigation properties
    public virtual Matter Matter { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual User? GrantedBy { get; set; }
    public virtual User? RevokedBy { get; set; }
}
