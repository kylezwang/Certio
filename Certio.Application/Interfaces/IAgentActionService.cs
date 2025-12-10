using Certio.Domain.AgentActions;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service for managing agent actions with approval workflow.
    /// Implements PENDING → APPROVED → RUNNING → DONE/FAILED lifecycle.
    /// </summary>
    public interface IAgentActionService
    {
        #region Proposal Operations
        
        /// <summary>
        /// Propose a new agent action (goes to PENDING state)
        /// </summary>
        /// <param name="organizationId">Organization context</param>
        /// <param name="actionType">Type of action (CreateTask, AttachFile, etc.)</param>
        /// <param name="payload">Type-specific action payload</param>
        /// <param name="proposedByUserId">User proposing the action</param>
        /// <param name="runId">Idempotency key (x-run-id header)</param>
        /// <param name="correlationId">Optional correlation for tracing</param>
        /// <param name="matterId">Optional matter context</param>
        /// <param name="description">Human-readable description</param>
        /// <param name="aiAgentType">AI agent type if proposed by AI</param>
        /// <param name="sourceConversationId">Source conversation if from chat</param>
        /// <param name="sourceMessageId">Source message if from chat</param>
        Task<AgentActionResult> ProposeActionAsync(
            int organizationId,
            string actionType,
            object payload,
            int proposedByUserId,
            string runId,
            string? correlationId = null,
            int? matterId = null,
            string? description = null,
            string? aiAgentType = null,
            int? sourceConversationId = null,
            int? sourceMessageId = null);
        
        /// <summary>
        /// Propose a CreateTask action
        /// </summary>
        Task<AgentActionResult> ProposeCreateTaskAsync(
            CreateTaskPayload payload,
            int proposedByUserId,
            string runId,
            int organizationId,
            string? correlationId = null,
            string? aiAgentType = null);
        
        /// <summary>
        /// Propose an AttachFile action
        /// </summary>
        Task<AgentActionResult> ProposeAttachFileAsync(
            AttachFilePayload payload,
            int proposedByUserId,
            string runId,
            int organizationId,
            string? correlationId = null,
            string? aiAgentType = null);
        
        /// <summary>
        /// Propose an AddNote action
        /// </summary>
        Task<AgentActionResult> ProposeAddNoteAsync(
            AddNotePayload payload,
            int proposedByUserId,
            string runId,
            int organizationId,
            string? correlationId = null,
            string? aiAgentType = null);
        
        /// <summary>
        /// Propose a StartTimer action
        /// </summary>
        Task<AgentActionResult> ProposeStartTimerAsync(
            StartTimerPayload payload,
            int proposedByUserId,
            string runId,
            int organizationId,
            string? correlationId = null,
            string? aiAgentType = null);
        
        #endregion
        
        #region Approval Operations
        
        /// <summary>
        /// Approve a pending action (moves to APPROVED state)
        /// </summary>
        Task<AgentActionResult> ApproveActionAsync(int actionId, int approvedByUserId, string? notes = null);
        
        /// <summary>
        /// Reject a pending action (moves to REJECTED state)
        /// </summary>
        Task<AgentActionResult> RejectActionAsync(int actionId, int rejectedByUserId, string? reason = null);
        
        /// <summary>
        /// Bulk approve multiple pending actions
        /// </summary>
        Task<AgentActionBulkResult> BulkApproveActionsAsync(IEnumerable<int> actionIds, int approvedByUserId, string? notes = null);
        
        /// <summary>
        /// Bulk reject multiple pending actions
        /// </summary>
        Task<AgentActionBulkResult> BulkRejectActionsAsync(IEnumerable<int> actionIds, int rejectedByUserId, string? reason = null);
        
        #endregion
        
        #region Execution Operations
        
        /// <summary>
        /// Execute an approved action (APPROVED → RUNNING → DONE/FAILED)
        /// </summary>
        Task<AgentActionResult> ExecuteActionAsync(int actionId);
        
        /// <summary>
        /// Execute all approved actions for an organization (batch processing)
        /// </summary>
        Task<AgentActionBulkResult> ExecutePendingActionsAsync(int organizationId, int? limit = 10);
        
        /// <summary>
        /// Rollback a completed action (if reversible)
        /// </summary>
        Task<AgentActionResult> RollbackActionAsync(int actionId, int rolledBackByUserId, string? reason = null);
        
        #endregion
        
        #region Query Operations
        
        /// <summary>
        /// Get an action by ID
        /// </summary>
        Task<AgentAction?> GetActionAsync(int actionId);
        
        /// <summary>
        /// Get an action by run ID (idempotency key)
        /// </summary>
        Task<AgentAction?> GetActionByRunIdAsync(string runId);
        
        /// <summary>
        /// Get pending actions for an organization
        /// </summary>
        Task<List<AgentAction>> GetPendingActionsAsync(int organizationId, int? matterId = null, int? limit = 50);
        
        /// <summary>
        /// Get actions by status
        /// </summary>
        Task<List<AgentAction>> GetActionsByStatusAsync(int organizationId, string status, int? limit = 50);
        
        /// <summary>
        /// Get action history for an organization
        /// </summary>
        Task<List<AgentAction>> GetActionHistoryAsync(int organizationId, DateTime? startDate = null, DateTime? endDate = null, int? limit = 100);
        
        /// <summary>
        /// Get actions for a specific matter
        /// </summary>
        Task<List<AgentAction>> GetMatterActionsAsync(int matterId, string? status = null, int? limit = 50);
        
        /// <summary>
        /// Get actions proposed by a specific user
        /// </summary>
        Task<List<AgentAction>> GetUserProposedActionsAsync(int userId, string? status = null, int? limit = 50);
        
        /// <summary>
        /// Get action statistics for an organization
        /// </summary>
        Task<AgentActionStats> GetActionStatsAsync(int organizationId, DateTime? startDate = null, DateTime? endDate = null);
        
        #endregion
        
        #region Validation
        
        /// <summary>
        /// Validate an action payload before proposal
        /// </summary>
        Task<AgentActionValidationResult> ValidatePayloadAsync(string actionType, object payload, int organizationId);
        
        /// <summary>
        /// Check if a run ID already exists (idempotency check)
        /// </summary>
        Task<bool> RunIdExistsAsync(string runId);
        
        #endregion
    }
    
    /// <summary>
    /// Result of an agent action operation
    /// </summary>
    public class AgentActionResult
    {
        public bool Success { get; set; }
        public AgentAction? Action { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
        public bool IsDuplicate { get; set; }
        
        public static AgentActionResult Ok(AgentAction action) => 
            new() { Success = true, Action = action };
        
        public static AgentActionResult Fail(string message, string? code = null) => 
            new() { Success = false, ErrorMessage = message, ErrorCode = code };
        
        public static AgentActionResult Duplicate(AgentAction existingAction) => 
            new() { Success = true, Action = existingAction, IsDuplicate = true };
    }
    
    /// <summary>
    /// Result of a bulk agent action operation
    /// </summary>
    public class AgentActionBulkResult
    {
        public bool Success { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<AgentActionResult> Results { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Result of action payload validation
    /// </summary>
    public class AgentActionValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        
        public static AgentActionValidationResult Valid() => 
            new() { IsValid = true };
        
        public static AgentActionValidationResult Invalid(params string[] errors) => 
            new() { IsValid = false, Errors = errors.ToList() };
    }
    
    /// <summary>
    /// Statistics about agent actions
    /// </summary>
    public class AgentActionStats
    {
        public int TotalActions { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int RunningCount { get; set; }
        public int DoneCount { get; set; }
        public int FailedCount { get; set; }
        public int RolledBackCount { get; set; }
        public Dictionary<string, int> ActionsByType { get; set; } = new();
        public double AverageApprovalTimeMinutes { get; set; }
        public double AverageExecutionTimeMinutes { get; set; }
    }
}

