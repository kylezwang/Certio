using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Services;

public class EmailMessage
{
    public Guid Id { get; set; }
    
    [Required]
    public int EmailAccountId { get; set; }
    
    public Guid? DirectMessageId { get; set; } // Links to DirectMessage if converted
    
    [Required]
    [StringLength(200)]
    public string ExternalEmailId { get; set; } = ""; // Provider's email ID
    
    [StringLength(200)]
    public string? ThreadId { get; set; } // Provider's thread ID
    
    [StringLength(500)]
    public string? Subject { get; set; }
    
    [Required]
    [StringLength(200)]
    public string FromEmail { get; set; } = "";
    
    [StringLength(200)]
    public string? FromName { get; set; }
    
    public string? ToEmails { get; set; } // JSON array of email addresses
    
    public string? CcEmails { get; set; } // JSON array of email addresses
    
    public string? BccEmails { get; set; } // JSON array of email addresses
    
    public string? Body { get; set; } // HTML content
    
    public string? BodyText { get; set; } // Plain text version
    
    public bool IsRead { get; set; } = false;
    
    public DateTime ReceivedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual EmailAccount EmailAccount { get; set; } = null!;
    public virtual DirectMessage? DirectMessage { get; set; }
}

