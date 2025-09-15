using System.ComponentModel.DataAnnotations;
using Certio.Domain.Teams;
using Certio.Domain.Users;
using Certio.Domain.Documents;
using Certio.Domain.Services;

namespace Certio.Domain.Projects;

public class Project
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
    
    [StringLength(50)]
    public string Category { get; set; } = ""; // Contract Review, LLC Formation, Real Estate, etc.
    
    [StringLength(20)]
    public string ProjectType { get; set; } = "Legal"; // Legal, Business, Compliance, etc.
    
    public int? TeamId { get; set; }
    public int? ClientId { get; set; }
    
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    
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
    
    // Navigation properties
    public virtual Team? Team { get; set; }
    public virtual User? Client { get; set; }
    public virtual ICollection<StatusItem> StatusItems { get; set; } = new List<StatusItem>();
    public virtual ICollection<ProjectAssignment> Assignments { get; set; } = new List<ProjectAssignment>();
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
}

public class ProjectAssignment
{
    public int Id { get; set; }
    
    public int ProjectId { get; set; }
    public int UserId { get; set; }
    
    [Required]
    [StringLength(20)]
    public string Role { get; set; } = "Member"; // Owner, Manager, Member, Observer
    
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAt { get; set; }
    
    // Navigation properties
    public virtual Project Project { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
