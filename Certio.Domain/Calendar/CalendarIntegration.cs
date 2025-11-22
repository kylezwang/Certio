using System;
using System.ComponentModel.DataAnnotations;
using Certio.Domain.Organizations;
using Certio.Domain.Users;

namespace Certio.Domain.Calendar
{
    public class CalendarIntegration
    {
        public int Id { get; set; }
        
        [Required]
        public int OrgId { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Provider { get; set; } = ""; // "Google", "Outlook"
        
        [Required]
        [StringLength(500)]
        public string AccessToken { get; set; } = "";
        
        [StringLength(500)]
        public string? RefreshToken { get; set; }
        
        public DateTime? TokenExpiresAt { get; set; }
        
        [StringLength(500)]
        public string? Scopes { get; set; }
        
        [StringLength(200)]
        public string? Email { get; set; }
        
        [StringLength(200)]
        public string? DisplayName { get; set; }
        
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastSyncedAt { get; set; }
        
        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Disconnected, Error
        
        [StringLength(500)]
        public string? LastErrorMessage { get; set; }
        
        public DateTime? LastErrorAt { get; set; }
        
        // Navigation properties
        public virtual Organization? Organization { get; set; }
        public virtual User? User { get; set; }
    }
}

