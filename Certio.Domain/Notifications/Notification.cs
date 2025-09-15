using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Projects;
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
        public string Category { get; set; } = ""; // Project, Document, Service, System, AI
        
        public int? ProjectId { get; set; }
        public int? DocumentId { get; set; }
        public int? ServiceRequestId { get; set; }
        public int? StatusItemId { get; set; }
        public int? ConversationId { get; set; }
        
        [StringLength(100)]
        public string? ActionUrl { get; set; }
        
        [StringLength(50)]
        public string? ActionText { get; set; }
        
        public bool IsRead { get; set; } = false;
        public bool IsArchived { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
        public DateTime? ArchivedAt { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Project? Project { get; set; }
        public virtual Document? Document { get; set; }
        public virtual ServiceRequest? ServiceRequest { get; set; }
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
        public string Category { get; set; } = ""; // Project, Document, Service, System, AI
        
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
