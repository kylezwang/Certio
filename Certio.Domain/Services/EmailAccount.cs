using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Services;

public class EmailAccount
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    [StringLength(20)]
    public string Provider { get; set; } = ""; // Gmail, Outlook
    
    [Required]
    [StringLength(200)]
    public string EmailAddress { get; set; } = "";
    
    [Required]
    public string AccessToken { get; set; } = ""; // Encrypted
    
    public string? RefreshToken { get; set; } // Encrypted, nullable
    
    public DateTime? TokenExpiresAt { get; set; }
    
    [StringLength(200)]
    public string? WebhookSubscriptionId { get; set; } // Provider's webhook subscription ID
    
    public bool IsActive { get; set; } = true;
    
    public DateTime? LastSyncAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ICollection<EmailMessage> EmailMessages { get; set; } = new List<EmailMessage>();
}

