using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Audit
{
    public class AuditLog
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string EntityType { get; set; } = ""; // User, Project, Document, etc.
        
        public int EntityId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Action { get; set; } = ""; // Create, Update, Delete, View, etc.
        
        public int? UserId { get; set; }
        
        [StringLength(100)]
        public string? UserName { get; set; }
        
        [StringLength(100)]
        public string? IPAddress { get; set; }
        
        [StringLength(200)]
        public string? UserAgent { get; set; }
        
        public string? OldValues { get; set; } // JSON of old values
        public string? NewValues { get; set; } // JSON of new values
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual User? User { get; set; }
    }
}
