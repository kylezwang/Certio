using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Projects;

namespace Certio.Domain.Services;

public class Conversation
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [StringLength(20)]
    public string Status { get; set; } = "Active"; // Active, Archived, Closed
    
    [StringLength(20)]
    public string ConversationType { get; set; } = "General"; // General, Project, Service, Support
    
    public int? ProjectId { get; set; }
    public int? ServiceRequestId { get; set; }
    public int? CreatedById { get; set; }
    
    public bool IsPrivate { get; set; } = false;
    public bool IsArchived { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    
    // Legacy fields for backward compatibility
    public string TenantId { get; set; } = "default";
    public string? ClientId { get; set; }
    public string? CertioId { get; set; }
    public string? LawyerId { get; set; }
    public string? BusinessId { get; set; }
    public string? AI_Summary { get; set; }
    public string? Client_Goals { get; set; }
    public string? Priority { get; set; } = "Medium"; // Low, Medium, High, Urgent
    public string? Category { get; set; } = "General"; // Legal, Business, Technical, etc.
    
    // Navigation properties
    public virtual Project? Project { get; set; }
    public virtual ServiceRequest? ServiceRequest { get; set; }
    public virtual User? CreatedBy { get; set; }
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    public virtual ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
}
