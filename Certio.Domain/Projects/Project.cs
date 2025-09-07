using System.ComponentModel.DataAnnotations;

namespace Certio.Domain.Projects;

public class Project
{
    public int Id { get; set; }
    public string TenantId { get; set; } = "default";
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";
    
    [StringLength(500)]
    public string Description { get; set; } = "";
    
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Planning"; // In Progress, Review, Planning, Completed
    
    [Required]
    [StringLength(20)]
    public string Priority { get; set; } = "Medium"; // High, Medium, Low
    
    [Required]
    public DateTime DueDate { get; set; }
    
    public List<string> Assignees { get; set; } = new List<string>();
    
    public int TasksCompleted { get; set; }
    
    public int TotalTasks { get; set; }
    
    [StringLength(50)]
    public string Category { get; set; } = ""; // AI/ML, Frontend, Backend, etc.
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastModifiedDate { get; set; }
}
