using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Audit
{
    public class AuditLog
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string EntityType { get; set; } = ""; // User, Matter, Document, etc.
        
        public int EntityId { get; set; }
        
        [Required]
        [StringLength(30)]
        public string Action { get; set; } = ""; // Create, Update, Delete, View, AUTH_FAILURE, etc.
        
        [StringLength(20)]
        public string? Result { get; set; } // SUCCESS, FAILURE
        
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
        
        // AI-related audit fields
        public bool IsAIAction { get; set; } = false;
        
        [StringLength(50)]
        public string? AIAgentType { get; set; }
        
        public string? AIContext { get; set; } // JSON containing AI execution context
        
        // Provenance fields
        public int? SourceConversationId { get; set; }
        public int? SourceMessageId { get; set; }
        
        // Additional context
        public int? OrganizationId { get; set; }
        public int? MatterId { get; set; }
        
        [StringLength(100)]
        public string? SessionId { get; set; }
        
        [StringLength(500)]
        public string? RequestUrl { get; set; }
        
        [StringLength(20)]
        public string? HttpMethod { get; set; }
        
        public int? ResponseCode { get; set; }
        
        public long? DurationMs { get; set; } // Request duration in milliseconds
        
        // Navigation properties
        public virtual User? User { get; set; }
    }
    
    /// <summary>
    /// Audit action types
    /// </summary>
    public static class AuditActions
    {
        public const string Create = "Create";
        public const string Update = "Update";
        public const string Delete = "Delete";
        public const string SoftDelete = "SoftDelete";
        public const string Restore = "Restore";
        public const string View = "View";
        public const string Export = "Export";
        public const string Import = "Import";
        public const string Login = "Login";
        public const string Logout = "Logout";
        public const string LoginFailed = "LoginFailed";
        public const string PasswordChange = "PasswordChange";
        public const string PasswordReset = "PasswordReset";
        public const string PermissionGranted = "PermissionGranted";
        public const string PermissionRevoked = "PermissionRevoked";
        public const string AIGenerated = "AIGenerated";
        public const string AIApproved = "AIApproved";
        public const string AIRejected = "AIRejected";
        public const string DocumentSigned = "DocumentSigned";
        public const string DocumentShared = "DocumentShared";
    }
    
    /// <summary>
    /// Audit result types
    /// </summary>
    public static class AuditResults
    {
        public const string Success = "SUCCESS";
        public const string Failure = "FAILURE";
        public const string PartialSuccess = "PARTIAL_SUCCESS";
    }
}
