using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Certio.Domain.Users;
using Certio.Domain.Matters;
using Certio.Domain.Organizations;

namespace Certio.Domain.AgentActions
{
    /// <summary>
    /// Represents a proposed agent action that requires approval before execution.
    /// Implements the PENDING → APPROVED → RUNNING → DONE/FAILED workflow.
    /// </summary>
    public class AgentAction
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Unique identifier for idempotency (maps to x-run-id header)
        /// </summary>
        [Required]
        [StringLength(100)]
        public string RunId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Correlation ID for tracing related actions
        /// </summary>
        [StringLength(100)]
        public string? CorrelationId { get; set; }
        
        [Required]
        public int OrganizationId { get; set; }
        
        public int? MatterId { get; set; }
        
        /// <summary>
        /// The type of action to perform
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ActionType { get; set; } = AgentActionTypes.CreateTask;
        
        /// <summary>
        /// Current status in the approval workflow
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = AgentActionStatus.Pending;
        
        /// <summary>
        /// Human-readable description of what the action will do
        /// </summary>
        [StringLength(1000)]
        public string? Description { get; set; }
        
        /// <summary>
        /// JSON-serialized action parameters (type-specific)
        /// </summary>
        public string? ActionPayload { get; set; }
        
        /// <summary>
        /// JSON-serialized result after execution
        /// </summary>
        public string? ResultPayload { get; set; }
        
        /// <summary>
        /// Before-state snapshot for undo/rollback (JSON)
        /// </summary>
        public string? BeforeState { get; set; }
        
        /// <summary>
        /// After-state reference for audit (JSON)
        /// </summary>
        public string? AfterState { get; set; }
        
        /// <summary>
        /// Error message if action failed
        /// </summary>
        [StringLength(2000)]
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Priority for action execution queue
        /// </summary>
        public int Priority { get; set; } = 5;
        
        /// <summary>
        /// Whether this action can be undone
        /// </summary>
        public bool IsReversible { get; set; } = true;
        
        /// <summary>
        /// If true, this action was already rolled back
        /// </summary>
        public bool IsRolledBack { get; set; } = false;
        
        /// <summary>
        /// Number of execution attempts
        /// </summary>
        public int AttemptCount { get; set; } = 0;
        
        /// <summary>
        /// Maximum retry attempts before marking as failed
        /// </summary>
        public int MaxAttempts { get; set; } = 3;
        
        // Timing fields
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? FailedAt { get; set; }
        public DateTime? RolledBackAt { get; set; }
        
        // User tracking
        public int? ProposedById { get; set; }
        public int? ApprovedById { get; set; }
        public int? RejectedById { get; set; }
        public int? RolledBackById { get; set; }
        
        /// <summary>
        /// Optional notes from approver/rejector
        /// </summary>
        [StringLength(1000)]
        public string? ReviewNotes { get; set; }
        
        // AI provenance
        public int? SourceConversationId { get; set; }
        public int? SourceMessageId { get; set; }
        
        [StringLength(50)]
        public string? AIAgentType { get; set; }
        
        /// <summary>
        /// References the created entity ID after successful execution
        /// </summary>
        public int? CreatedEntityId { get; set; }
        
        [StringLength(50)]
        public string? CreatedEntityType { get; set; }
        
        // Navigation properties
        public virtual Organization? Organization { get; set; }
        public virtual Matter? Matter { get; set; }
        public virtual User? ProposedBy { get; set; }
        public virtual User? ApprovedBy { get; set; }
        public virtual User? RejectedBy { get; set; }
        public virtual User? RolledBackBy { get; set; }
        
        // Helper methods
        public bool CanBeApproved() => Status == AgentActionStatus.Pending;
        public bool CanBeRejected() => Status == AgentActionStatus.Pending;
        public bool CanBeExecuted() => Status == AgentActionStatus.Approved && AttemptCount < MaxAttempts;
        public bool CanBeRolledBack() => Status == AgentActionStatus.Done && IsReversible && !IsRolledBack;
        
        public T? GetPayload<T>() where T : class
        {
            if (string.IsNullOrEmpty(ActionPayload)) return null;
            return JsonSerializer.Deserialize<T>(ActionPayload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        
        public void SetPayload<T>(T payload) where T : class
        {
            ActionPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        
        public T? GetResult<T>() where T : class
        {
            if (string.IsNullOrEmpty(ResultPayload)) return null;
            return JsonSerializer.Deserialize<T>(ResultPayload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        
        public void SetResult<T>(T result) where T : class
        {
            ResultPayload = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
    }
    
    /// <summary>
    /// Supported agent action types
    /// </summary>
    public static class AgentActionTypes
    {
        public const string CreateTask = "CreateTask";
        public const string AttachFile = "AttachFile";
        public const string AddNote = "AddNote";
        public const string StartTimer = "StartTimer";
        public const string SendTemplateMessage = "SendTemplateMessage"; // Stub for phase 2
        
        // For validation
        public static readonly string[] All = { CreateTask, AttachFile, AddNote, StartTimer, SendTemplateMessage };
        
        public static bool IsValid(string actionType) => All.Contains(actionType);
    }
    
    /// <summary>
    /// Agent action status states in the approval workflow
    /// </summary>
    public static class AgentActionStatus
    {
        public const string Pending = "Pending";       // Awaiting approval
        public const string Approved = "Approved";     // Approved, ready for execution
        public const string Rejected = "Rejected";     // Rejected by reviewer
        public const string Running = "Running";       // Currently executing
        public const string Done = "Done";             // Successfully completed
        public const string Failed = "Failed";         // Execution failed
        public const string RolledBack = "RolledBack"; // Action was undone
        
        public static readonly string[] All = { Pending, Approved, Rejected, Running, Done, Failed, RolledBack };
        
        public static bool IsValid(string status) => All.Contains(status);
        public static bool IsTerminal(string status) => status == Done || status == Failed || status == Rejected || status == RolledBack;
    }
    
}

