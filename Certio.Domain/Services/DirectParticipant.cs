using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Services;

public class DirectParticipant
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid ThreadId { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    // User-specific thread state
    public DateTime? LastReadAt { get; set; }
    public DateTime? MutedUntil { get; set; }
    public bool Pinned { get; set; } = false;
    public bool Archived { get; set; } = false;
    
    // Soft delete fields
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual DirectThread Thread { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

