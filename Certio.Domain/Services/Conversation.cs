using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Matters;
using Certio.Domain.Organizations;

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
    public string ConversationType { get; set; } = "General"; // General, Matter, Service, Support
    
    // Channel support fields
    [StringLength(20)]
    public string ChannelType { get; set; } = "Direct"; // Direct, Group, Public, Private
    public bool IsChannel { get; set; } = false;
    [StringLength(500)]
    public string? ChannelDescription { get; set; }
    public bool IsPrivateChannel { get; set; } = false;
    
    // Organization scoping - REQUIRED
    [Required]
    public int OrganizationId { get; set; }
    
    // User who created the conversation - REQUIRED
    [Required]
    public int CreatedById { get; set; }
    
    // Optional matter association
    public int? MatterId { get; set; }
    
    public bool IsPrivate { get; set; } = false;
    public bool IsArchived { get; set; } = false;
    
    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
    public int? ModifiedById { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    
    // Soft delete fields
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }
    
    // Navigation properties
    public virtual Organization Organization { get; set; } = null!;
    public virtual User CreatedBy { get; set; } = null!;
    public virtual Matter? Matter { get; set; }
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    public virtual ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
}
