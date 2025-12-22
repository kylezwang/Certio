using System.ComponentModel.DataAnnotations;

namespace Certio.Domain.AgentActions
{
    /// <summary>
    /// Base class for all action payloads
    /// </summary>
    public abstract class ActionPayloadBase
    {
        /// <summary>
        /// Human-readable summary of what this action will do
        /// </summary>
        public abstract string GetSummary();
        
        /// <summary>
        /// Redacted version for audit logging (sensitive data removed)
        /// </summary>
        public virtual string GetRedactedSummary() => GetSummary();
    }
    
    /// <summary>
    /// Payload for CreateTask action
    /// </summary>
    public class CreateTaskPayload : ActionPayloadBase
    {
        [Required]
        public int MatterId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";
        
        [StringLength(2000)]
        public string? Description { get; set; }
        
        [StringLength(20)]
        public string Priority { get; set; } = "Medium"; // High, Medium, Low, Critical
        
        /// <summary>
        /// Task status: Pending, In Progress, Review, Completed, On Hold, Cancelled
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Pending";
        
        public DateTime? DueDate { get; set; }
        
        public List<int>? AssigneeIds { get; set; }
        
        [StringLength(200)]
        public string? Location { get; set; }
        
        public override string GetSummary() =>
            $"Create task '{Title}' for matter {MatterId}" +
            (DueDate.HasValue ? $" due {DueDate:MMM dd, yyyy}" : "") +
            (AssigneeIds?.Any() == true ? $" assigned to {AssigneeIds.Count} user(s)" : "");
    }
    
    /// <summary>
    /// Payload for AttachFile action
    /// </summary>
    public class AttachFilePayload : ActionPayloadBase
    {
        [Required]
        public int MatterId { get; set; }
        
        /// <summary>
        /// Source document ID to attach (if existing document)
        /// </summary>
        public int? SourceDocumentId { get; set; }
        
        /// <summary>
        /// Target entity type: Matter, Task, Communication
        /// </summary>
        [Required]
        [StringLength(50)]
        public string TargetEntityType { get; set; } = "Matter";
        
        /// <summary>
        /// Target entity ID
        /// </summary>
        [Required]
        public int TargetEntityId { get; set; }
        
        /// <summary>
        /// File name for new uploads
        /// </summary>
        [StringLength(255)]
        public string? FileName { get; set; }
        
        /// <summary>
        /// Temporary file path for staged uploads
        /// </summary>
        [StringLength(500)]
        public string? StagedFilePath { get; set; }
        
        /// <summary>
        /// Optional folder path within the matter
        /// </summary>
        [StringLength(500)]
        public string? FolderPath { get; set; }
        
        public override string GetSummary() =>
            SourceDocumentId.HasValue
                ? $"Attach document {SourceDocumentId} to {TargetEntityType} {TargetEntityId}"
                : $"Attach file '{FileName}' to {TargetEntityType} {TargetEntityId}";
                
        public override string GetRedactedSummary() =>
            $"Attach file to {TargetEntityType} {TargetEntityId}";
    }
    
    /// <summary>
    /// Payload for AddNote action
    /// </summary>
    public class AddNotePayload : ActionPayloadBase
    {
        [Required]
        public int MatterId { get; set; }
        
        /// <summary>
        /// Target entity type: Matter, Task, Document, Contact
        /// </summary>
        [Required]
        [StringLength(50)]
        public string TargetEntityType { get; set; } = "Matter";
        
        /// <summary>
        /// Target entity ID
        /// </summary>
        [Required]
        public int TargetEntityId { get; set; }
        
        /// <summary>
        /// Note content (can contain markdown)
        /// </summary>
        [Required]
        [StringLength(5000)]
        public string Content { get; set; } = "";
        
        /// <summary>
        /// Whether the note is internal only (not visible to clients)
        /// </summary>
        public bool IsInternal { get; set; } = true;
        
        /// <summary>
        /// Optional category/type for the note
        /// </summary>
        [StringLength(50)]
        public string? NoteType { get; set; }
        
        public override string GetSummary() =>
            $"Add {(IsInternal ? "internal" : "public")} note to {TargetEntityType} {TargetEntityId}: {Content.Substring(0, Math.Min(50, Content.Length))}...";
            
        public override string GetRedactedSummary() =>
            $"Add {(IsInternal ? "internal" : "public")} note to {TargetEntityType} {TargetEntityId}";
    }
    
    /// <summary>
    /// Payload for StartTimer action
    /// </summary>
    public class StartTimerPayload : ActionPayloadBase
    {
        [Required]
        public int MatterId { get; set; }
        
        /// <summary>
        /// Task ID if timer is associated with a task
        /// </summary>
        public int? TaskId { get; set; }
        
        /// <summary>
        /// Description of the time entry
        /// </summary>
        [Required]
        [StringLength(500)]
        public string Description { get; set; } = "";
        
        /// <summary>
        /// Billing code for the time entry
        /// </summary>
        [StringLength(50)]
        public string? BillingCode { get; set; }
        
        /// <summary>
        /// Whether this time is billable
        /// </summary>
        public bool IsBillable { get; set; } = true;
        
        /// <summary>
        /// User ID for whom to start the timer (defaults to current user)
        /// </summary>
        public int? UserId { get; set; }
        
        public override string GetSummary() =>
            $"Start timer for matter {MatterId}: {Description}" +
            (TaskId.HasValue ? $" (task {TaskId})" : "") +
            (IsBillable ? " [billable]" : " [non-billable]");
    }
    
    /// <summary>
    /// Payload for SendTemplateMessage action (stub for phase 2)
    /// </summary>
    public class SendTemplateMessagePayload : ActionPayloadBase
    {
        [Required]
        public int MatterId { get; set; }
        
        /// <summary>
        /// Template ID to use
        /// </summary>
        [Required]
        public int TemplateId { get; set; }
        
        /// <summary>
        /// Recipient contact IDs
        /// </summary>
        [Required]
        public List<int> RecipientIds { get; set; } = new();
        
        /// <summary>
        /// Template variable substitutions
        /// </summary>
        public Dictionary<string, string>? TemplateVariables { get; set; }
        
        /// <summary>
        /// Channel: Email, SMS, InApp
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Channel { get; set; } = "Email";
        
        /// <summary>
        /// Subject line override (for email)
        /// </summary>
        [StringLength(200)]
        public string? SubjectOverride { get; set; }
        
        public override string GetSummary() =>
            $"Send template {TemplateId} via {Channel} to {RecipientIds.Count} recipient(s)";
            
        public override string GetRedactedSummary() =>
            $"Send template message via {Channel}";
    }
    
    /// <summary>
    /// Result payload for executed actions
    /// </summary>
    public class ActionResultPayload
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? CreatedEntityId { get; set; }
        public string? CreatedEntityType { get; set; }
        public Dictionary<string, object>? AdditionalData { get; set; }
    }
}

