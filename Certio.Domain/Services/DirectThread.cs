using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Organizations;

namespace Certio.Domain.Services;

public class DirectThread
{
    public Guid Id { get; set; }
    
    // Organization scoping - REQUIRED
    [Required]
    public int OrganizationId { get; set; }
    
    // The two participants (ordered: UserA < UserB by Id for uniqueness)
    [Required]
    public int UserAId { get; set; }
    
    [Required]
    public int UserBId { get; set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }
    
    // Soft delete fields
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }
    
    // Navigation properties
    public virtual Organization Organization { get; set; } = null!;
    public virtual User UserA { get; set; } = null!;
    public virtual User UserB { get; set; } = null!;
    public virtual User? DeletedBy { get; set; }
    public virtual ICollection<DirectMessage> Messages { get; set; } = new List<DirectMessage>();
    public virtual ICollection<DirectParticipant> Participants { get; set; } = new List<DirectParticipant>();
}

