using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Matters;
using Certio.Domain.Documents;
using Certio.Domain.Services;

namespace Certio.Domain.Notifications
{
    public class Notification
    {
        public int Id { get; set; }
        
        public int UserId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";
        
        [Required]
        public string Message { get; set; } = "";
        
        [Required]
        [StringLength(20)]
        public string Type { get; set; } = ""; // Info, Warning, Error, Success, Action
        
        [Required]
        [StringLength(20)]
        public string Category { get; set; } = ""; // Matter, Document, Service, System, AI
        
    public int? MatterId { get; set; }
    public Guid? DocumentId { get; set; }
    public int? StatusItemId { get; set; }
    public int? ConversationId { get; set; }
    
    [StringLength(100)]
    public string? ActionUrl { get; set; }
    
    [StringLength(50)]
    public string? ActionText { get; set; }
    
    public bool IsRead { get; set; } = false;
    public bool IsArchived { get; set; } = false;
    
    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    
    // Soft delete fields (notifications can be soft-deleted for cleanup)
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Matter? Matter { get; set; }
    public virtual Document? Document { get; set; }
    public virtual StatusItem? StatusItem { get; set; }
    public virtual Conversation? Conversation { get; set; }
    }
    
    public class NotificationTemplate
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = "";
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";
        
        [Required]
        public string Message { get; set; } = "";
        
        [Required]
        [StringLength(20)]
        public string Type { get; set; } = ""; // Info, Warning, Error, Success, Action
        
        [Required]
        [StringLength(20)]
        public string Category { get; set; } = ""; // Matter, Document, Service, System, AI
        
        [StringLength(100)]
        public string? ActionUrl { get; set; }
        
        [StringLength(50)]
        public string? ActionText { get; set; }
        
        public bool IsActive { get; set; } = true;
        public bool IsSystem { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
    }
}
