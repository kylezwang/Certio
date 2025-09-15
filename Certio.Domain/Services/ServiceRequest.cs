using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Projects;

namespace Certio.Domain.Services
{
    public class ServiceRequest
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";
        
        [StringLength(1000)]
        public string? Description { get; set; }
        
        [Required]
        [StringLength(50)]
        public string ServiceType { get; set; } = ""; // LLC Formation, Contract Review, Legal Advice, etc.
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "New"; // New, InProgress, PendingReview, Completed, Cancelled
        
        [Required]
        [StringLength(20)]
        public string Priority { get; set; } = "Medium"; // High, Medium, Low, Urgent
        
        public int? ProjectId { get; set; }
        public int? ClientId { get; set; }
        public int? AssignedToId { get; set; }
        
        [StringLength(1000)]
        public string? ClientGoals { get; set; }
        
        [StringLength(1000)]
        public string? Requirements { get; set; }
        
        [StringLength(1000)]
        public string? Notes { get; set; }
        
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        
        // Navigation properties
        public virtual Project? Project { get; set; }
        public virtual User? Client { get; set; }
        public virtual User? AssignedTo { get; set; }
        public virtual ICollection<ServiceRequestMessage> Messages { get; set; } = new List<ServiceRequestMessage>();
        public virtual ICollection<ServiceRequestAttachment> Attachments { get; set; } = new List<ServiceRequestAttachment>();
    }
    
    public class ServiceRequestMessage
    {
        public int Id { get; set; }
        
        public int ServiceRequestId { get; set; }
        public int UserId { get; set; }
        
        [Required]
        [StringLength(2000)]
        public string Content { get; set; } = "";
        
        [StringLength(20)]
        public string MessageType { get; set; } = "Text"; // Text, System, AI, File
        
        public int? ParentMessageId { get; set; }
        
        public bool IsInternal { get; set; } = false; // Internal notes vs client-visible messages
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        
        // Navigation properties
        public virtual ServiceRequest ServiceRequest { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public virtual ServiceRequestMessage? ParentMessage { get; set; }
        public virtual ICollection<ServiceRequestMessage> Replies { get; set; } = new List<ServiceRequestMessage>();
    }
    
    public class ServiceRequestAttachment
    {
        public int Id { get; set; }
        
        public int ServiceRequestId { get; set; }
        public int? MessageId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string FileName { get; set; } = "";
        
        [StringLength(100)]
        public string? FilePath { get; set; }
        
        [StringLength(20)]
        public string FileSize { get; set; } = "";
        
        [StringLength(100)]
        public string? MimeType { get; set; }
        
        public int? UploadedById { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual ServiceRequest ServiceRequest { get; set; } = null!;
        public virtual ServiceRequestMessage? Message { get; set; }
        public virtual User? UploadedBy { get; set; }
    }
}
