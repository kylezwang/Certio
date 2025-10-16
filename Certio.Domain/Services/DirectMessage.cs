using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Services;

public class DirectMessage
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid ThreadId { get; set; }
    
    [Required]
    public int SenderId { get; set; }
    
    public string? Body { get; set; }
    
    [StringLength(20)]
    public string MessageType { get; set; } = "Text"; // Text, File, System
    
    public string? Metadata { get; set; } // JSON string for additional data (attachments, etc.)
    
    // Message lifecycle
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
    
    // Soft delete fields
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }
    
    // Navigation properties
    public virtual DirectThread Thread { get; set; } = null!;
    public virtual User Sender { get; set; } = null!;
    public virtual User? DeletedBy { get; set; }
}

