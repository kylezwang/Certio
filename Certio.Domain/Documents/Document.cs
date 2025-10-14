using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Matters;

namespace Certio.Domain.Documents
{
    public class Document
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = "";
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Type { get; set; } = ""; // Contract, Report, Research, Presentation, Template, Legal Document
        
        [StringLength(20)]
        public string FileSize { get; set; } = ""; // e.g., "2.4 MB"
        
        [StringLength(100)]
        public string? FilePath { get; set; }
        
        [StringLength(50)]
        public string? FileExtension { get; set; }
        
        [StringLength(100)]
        public string? MimeType { get; set; }
        
        public int? MatterId { get; set; }
        public int? StatusItemId { get; set; }
        public int? CreatedById { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft"; // Draft, InReview, Approved, Rejected, Published, Archived
        
        [Required]
        [StringLength(20)]
        public string Visibility { get; set; } = "Private"; // Public, Team, Private
        
        [StringLength(20)]
        public string DocumentType { get; set; } = "General"; // Contract, Agreement, Form, Template, Legal, Business
        
        public List<string> Tags { get; set; } = new List<string>();
        
        [StringLength(50)]
        public string Icon { get; set; } = "FileText"; // FileText, File, Image, Video, Archive
        
        public bool IsTemplate { get; set; } = false;
        public bool RequiresSignature { get; set; } = false;
        public bool IsSigned { get; set; } = false;
        
        public DateTime? SignedDate { get; set; }
        public DateTime? ReviewDueDate { get; set; }
        
    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
    public int? ModifiedById { get; set; }
    public DateTime? LastModifiedDate { get; set; } // Legacy - use ModifiedAt
    
    // Soft delete fields
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }
    
    // AI generation fields
    public bool IsAIGenerated { get; set; } = false;
    
    [StringLength(50)]
    public string? AIAgentType { get; set; }
    
    public string? AIGenerationMetadata { get; set; } // JSON
    public int? SourceConversationId { get; set; }
    public int? SourceMessageId { get; set; }
    
    // Approval fields for AI-generated content
    [StringLength(20)]
    public string? ApprovalStatus { get; set; } // Pending, Approved, Rejected
    
    public int? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    
    [StringLength(500)]
    public string? ApprovalNotes { get; set; }
    
    // Computed properties for views
    public string Author => CreatedBy?.FirstName + " " + CreatedBy?.LastName ?? "Unknown";
    public DateTime Modified => LastModifiedDate ?? CreatedAt;
    public string Size => FileSize;
    
    // Navigation properties
    public virtual Matter? Matter { get; set; }
    public virtual StatusItem? StatusItem { get; set; }
    public virtual User? CreatedBy { get; set; }
    public virtual ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public virtual ICollection<DocumentReview> Reviews { get; set; } = new List<DocumentReview>();
    public virtual ICollection<DocumentComment> Comments { get; set; } = new List<DocumentComment>();
    public virtual ICollection<DocumentSignature> Signatures { get; set; } = new List<DocumentSignature>();
    }
    
    public class DocumentVersion
    {
        public int Id { get; set; }
        
        public int DocumentId { get; set; }
        public int VersionNumber { get; set; }
        
        [StringLength(500)]
        public string? ChangeDescription { get; set; }
        
        [StringLength(100)]
        public string? FilePath { get; set; }
        
        public int? CreatedById { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual Document Document { get; set; } = null!;
        public virtual User? CreatedBy { get; set; }
    }
    
    public class DocumentReview
    {
        public int Id { get; set; }
        
        public int DocumentId { get; set; }
        public int ReviewerId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, NeedsRevision
        
        [StringLength(1000)]
        public string? Comments { get; set; }
        
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual Document Document { get; set; } = null!;
        public virtual User Reviewer { get; set; } = null!;
    }
    
    public class DocumentComment
    {
        public int Id { get; set; }
        
        public int DocumentId { get; set; }
        public int UserId { get; set; }
        
        [Required]
        [StringLength(2000)]
        public string Content { get; set; } = "";
        
        public int? ParentCommentId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        
        // Navigation properties
        public virtual Document Document { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public virtual DocumentComment? ParentComment { get; set; }
        public virtual ICollection<DocumentComment> Replies { get; set; } = new List<DocumentComment>();
    }
    
    public class DocumentSignature
    {
        public int Id { get; set; }
        
        public int DocumentId { get; set; }
        public int SignerId { get; set; }
        
        [StringLength(100)]
        public string? SignatureData { get; set; } // Base64 encoded signature
        
        [StringLength(100)]
        public string? IPAddress { get; set; }
        
        [StringLength(200)]
        public string? UserAgent { get; set; }
        
        public DateTime SignedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual Document Document { get; set; } = null!;
        public virtual User Signer { get; set; } = null!;
    }
}
