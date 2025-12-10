using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Certio.Application.Interfaces;
using Certio.Domain.AgentActions;
using Certio.Domain.Audit;
using Certio.Infrastructure.Data;

namespace Certio.Application.Services
{
    /// <summary>
    /// Service for managing agent actions with approval workflow.
    /// Implements PENDING → APPROVED → RUNNING → DONE/FAILED lifecycle.
    /// </summary>
    public class AgentActionService : IAgentActionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AgentActionService> _logger;

        public AgentActionService(
            ApplicationDbContext context,
            ILogger<AgentActionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Proposal Operations

        public async Task<AgentActionResult> ProposeActionAsync(
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
            int? sourceMessageId = null)
        {
            try
            {
                // Idempotency check
                var existingAction = await GetActionByRunIdAsync(runId);
                if (existingAction != null)
                {
                    _logger.LogInformation("Duplicate action request with RunId {RunId}, returning existing action {ActionId}", 
                        runId, existingAction.Id);
                    return AgentActionResult.Duplicate(existingAction);
                }

                // Validate action type
                if (!AgentActionTypes.IsValid(actionType))
                {
                    return AgentActionResult.Fail($"Invalid action type: {actionType}", "INVALID_ACTION_TYPE");
                }

                // Validate payload
                var validationResult = await ValidatePayloadAsync(actionType, payload, organizationId);
                if (!validationResult.IsValid)
                {
                    return AgentActionResult.Fail(string.Join("; ", validationResult.Errors), "VALIDATION_ERROR");
                }

                // Create the action
                var action = new AgentAction
                {
                    RunId = runId,
                    CorrelationId = correlationId,
                    OrganizationId = organizationId,
                    MatterId = matterId,
                    ActionType = actionType,
                    Status = AgentActionStatus.Pending,
                    Description = description ?? GetDefaultDescription(actionType, payload),
                    ActionPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    ProposedById = proposedByUserId,
                    AIAgentType = aiAgentType,
                    SourceConversationId = sourceConversationId,
                    SourceMessageId = sourceMessageId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AgentActions.Add(action);
                await _context.SaveChangesAsync();

                // Log audit event
                await LogAuditEventAsync(action, AuditActions.AgentActionProposed, proposedByUserId);

                _logger.LogInformation("Created agent action {ActionId} of type {ActionType} with RunId {RunId}", 
                    action.Id, actionType, runId);

                return AgentActionResult.Ok(action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proposing action of type {ActionType} for org {OrgId}", actionType, organizationId);
                return AgentActionResult.Fail($"Error proposing action: {ex.Message}", "INTERNAL_ERROR");
            }
        }

        public async Task<AgentActionResult> ProposeCreateTaskAsync(
            CreateTaskPayload payload,
            int proposedByUserId,
            string runId,
            int organizationId,
            string? correlationId = null,
            string? aiAgentType = null)
        {
            return await ProposeActionAsync(
                organizationId,
                AgentActionTypes.CreateTask,
                payload,
                proposedByUserId,
                runId,
                correlationId,
                payload.MatterId,
                payload.GetSummary(),
                aiAgentType);
        }

        public async Task<AgentActionResult> ProposeAttachFileAsync(
            AttachFilePayload payload,
            int proposedByUserId,
            string runId,
            int organizationId,
            string? correlationId = null,
            string? aiAgentType = null)
        {
            return await ProposeActionAsync(
                organizationId,
                AgentActionTypes.AttachFile,
                payload,
                proposedByUserId,
                runId,
                correlationId,
                payload.MatterId,
                payload.GetSummary(),
                aiAgentType);
        }

        public async Task<AgentActionResult> ProposeAddNoteAsync(
            AddNotePayload payload,
            int proposedByUserId,
            string runId,
            int organizationId,
            string? correlationId = null,
            string? aiAgentType = null)
        {
            return await ProposeActionAsync(
                organizationId,
                AgentActionTypes.AddNote,
                payload,
                proposedByUserId,
                runId,
                correlationId,
                payload.MatterId,
                payload.GetSummary(),
                aiAgentType);
        }

        public async Task<AgentActionResult> ProposeStartTimerAsync(
            StartTimerPayload payload,
            int proposedByUserId,
            string runId,
            int organizationId,
            string? correlationId = null,
            string? aiAgentType = null)
        {
            return await ProposeActionAsync(
                organizationId,
                AgentActionTypes.StartTimer,
                payload,
                proposedByUserId,
                runId,
                correlationId,
                payload.MatterId,
                payload.GetSummary(),
                aiAgentType);
        }

        #endregion

        #region Approval Operations

        public async Task<AgentActionResult> ApproveActionAsync(int actionId, int approvedByUserId, string? notes = null)
        {
            try
            {
                var action = await GetActionAsync(actionId);
                if (action == null)
                {
                    return AgentActionResult.Fail("Action not found", "NOT_FOUND");
                }

                if (!action.CanBeApproved())
                {
                    return AgentActionResult.Fail($"Action cannot be approved in current state: {action.Status}", "INVALID_STATE");
                }

                action.Status = AgentActionStatus.Approved;
                action.ApprovedById = approvedByUserId;
                action.ApprovedAt = DateTime.UtcNow;
                action.ReviewNotes = notes;

                await _context.SaveChangesAsync();
                await LogAuditEventAsync(action, AuditActions.AgentActionApproved, approvedByUserId);

                _logger.LogInformation("Action {ActionId} approved by user {UserId}", actionId, approvedByUserId);

                return AgentActionResult.Ok(action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving action {ActionId}", actionId);
                return AgentActionResult.Fail($"Error approving action: {ex.Message}", "INTERNAL_ERROR");
            }
        }

        public async Task<AgentActionResult> RejectActionAsync(int actionId, int rejectedByUserId, string? reason = null)
        {
            try
            {
                var action = await GetActionAsync(actionId);
                if (action == null)
                {
                    return AgentActionResult.Fail("Action not found", "NOT_FOUND");
                }

                if (!action.CanBeRejected())
                {
                    return AgentActionResult.Fail($"Action cannot be rejected in current state: {action.Status}", "INVALID_STATE");
                }

                action.Status = AgentActionStatus.Rejected;
                action.RejectedById = rejectedByUserId;
                action.ReviewNotes = reason;

                await _context.SaveChangesAsync();
                await LogAuditEventAsync(action, AuditActions.AgentActionRejected, rejectedByUserId);

                _logger.LogInformation("Action {ActionId} rejected by user {UserId}", actionId, rejectedByUserId);

                return AgentActionResult.Ok(action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting action {ActionId}", actionId);
                return AgentActionResult.Fail($"Error rejecting action: {ex.Message}", "INTERNAL_ERROR");
            }
        }

        public async Task<AgentActionBulkResult> BulkApproveActionsAsync(IEnumerable<int> actionIds, int approvedByUserId, string? notes = null)
        {
            var result = new AgentActionBulkResult { Success = true };

            foreach (var actionId in actionIds)
            {
                var actionResult = await ApproveActionAsync(actionId, approvedByUserId, notes);
                result.Results.Add(actionResult);
                if (actionResult.Success)
                    result.SuccessCount++;
                else
                    result.FailureCount++;
            }

            result.Success = result.FailureCount == 0;
            return result;
        }

        public async Task<AgentActionBulkResult> BulkRejectActionsAsync(IEnumerable<int> actionIds, int rejectedByUserId, string? reason = null)
        {
            var result = new AgentActionBulkResult { Success = true };

            foreach (var actionId in actionIds)
            {
                var actionResult = await RejectActionAsync(actionId, rejectedByUserId, reason);
                result.Results.Add(actionResult);
                if (actionResult.Success)
                    result.SuccessCount++;
                else
                    result.FailureCount++;
            }

            result.Success = result.FailureCount == 0;
            return result;
        }

        #endregion

        #region Execution Operations

        public async Task<AgentActionResult> ExecuteActionAsync(int actionId)
        {
            var action = await GetActionAsync(actionId);
            if (action == null)
            {
                return AgentActionResult.Fail("Action not found", "NOT_FOUND");
            }

            if (!action.CanBeExecuted())
            {
                return AgentActionResult.Fail($"Action cannot be executed in current state: {action.Status}", "INVALID_STATE");
            }

            try
            {
                // Mark as running
                action.Status = AgentActionStatus.Running;
                action.StartedAt = DateTime.UtcNow;
                action.AttemptCount++;
                await _context.SaveChangesAsync();
                await LogAuditEventAsync(action, AuditActions.AgentActionStarted, null);

                // Execute based on action type
                var result = action.ActionType switch
                {
                    AgentActionTypes.CreateTask => await ExecuteCreateTaskAsync(action),
                    AgentActionTypes.AttachFile => await ExecuteAttachFileAsync(action),
                    AgentActionTypes.AddNote => await ExecuteAddNoteAsync(action),
                    AgentActionTypes.StartTimer => await ExecuteStartTimerAsync(action),
                    AgentActionTypes.SendTemplateMessage => await ExecuteSendTemplateMessageAsync(action),
                    _ => new ActionResultPayload { Success = false, Message = $"Unknown action type: {action.ActionType}" }
                };

                if (result.Success)
                {
                    action.Status = AgentActionStatus.Done;
                    action.CompletedAt = DateTime.UtcNow;
                    action.CreatedEntityId = result.CreatedEntityId;
                    action.CreatedEntityType = result.CreatedEntityType;
                    action.SetResult(result);
                    await _context.SaveChangesAsync();
                    await LogAuditEventAsync(action, AuditActions.AgentActionCompleted, null);

                    _logger.LogInformation("Action {ActionId} completed successfully", actionId);
                    return AgentActionResult.Ok(action);
                }
                else
                {
                    action.Status = AgentActionStatus.Failed;
                    action.FailedAt = DateTime.UtcNow;
                    action.ErrorMessage = result.Message;
                    action.SetResult(result);
                    await _context.SaveChangesAsync();
                    await LogAuditEventAsync(action, AuditActions.AgentActionFailed, null);

                    _logger.LogWarning("Action {ActionId} failed: {Error}", actionId, result.Message);
                    return AgentActionResult.Fail(result.Message ?? "Action execution failed", "EXECUTION_FAILED");
                }
            }
            catch (Exception ex)
            {
                action.Status = AgentActionStatus.Failed;
                action.FailedAt = DateTime.UtcNow;
                action.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync();
                await LogAuditEventAsync(action, AuditActions.AgentActionFailed, null);

                _logger.LogError(ex, "Error executing action {ActionId}", actionId);
                return AgentActionResult.Fail($"Error executing action: {ex.Message}", "INTERNAL_ERROR");
            }
        }

        public async Task<AgentActionBulkResult> ExecutePendingActionsAsync(int organizationId, int? limit = 10)
        {
            var result = new AgentActionBulkResult { Success = true };

            var approvedActions = await _context.AgentActions
                .Where(a => a.OrganizationId == organizationId && 
                           a.Status == AgentActionStatus.Approved &&
                           a.AttemptCount < a.MaxAttempts)
                .OrderBy(a => a.Priority)
                .ThenBy(a => a.ApprovedAt)
                .Take(limit ?? 10)
                .ToListAsync();

            foreach (var action in approvedActions)
            {
                var actionResult = await ExecuteActionAsync(action.Id);
                result.Results.Add(actionResult);
                if (actionResult.Success)
                    result.SuccessCount++;
                else
                    result.FailureCount++;
            }

            result.Success = result.FailureCount == 0;
            return result;
        }

        public async Task<AgentActionResult> RollbackActionAsync(int actionId, int rolledBackByUserId, string? reason = null)
        {
            try
            {
                var action = await GetActionAsync(actionId);
                if (action == null)
                {
                    return AgentActionResult.Fail("Action not found", "NOT_FOUND");
                }

                if (!action.CanBeRolledBack())
                {
                    return AgentActionResult.Fail($"Action cannot be rolled back: Status={action.Status}, Reversible={action.IsReversible}, AlreadyRolledBack={action.IsRolledBack}", "INVALID_STATE");
                }

                // Execute rollback based on action type
                var rollbackSuccess = action.ActionType switch
                {
                    AgentActionTypes.CreateTask => await RollbackCreateTaskAsync(action),
                    AgentActionTypes.AttachFile => await RollbackAttachFileAsync(action),
                    AgentActionTypes.AddNote => await RollbackAddNoteAsync(action),
                    AgentActionTypes.StartTimer => await RollbackStartTimerAsync(action),
                    _ => false
                };

                if (rollbackSuccess)
                {
                    action.Status = AgentActionStatus.RolledBack;
                    action.IsRolledBack = true;
                    action.RolledBackAt = DateTime.UtcNow;
                    action.RolledBackById = rolledBackByUserId;
                    action.ReviewNotes = reason;
                    await _context.SaveChangesAsync();
                    await LogAuditEventAsync(action, AuditActions.AgentActionRolledBack, rolledBackByUserId);

                    _logger.LogInformation("Action {ActionId} rolled back by user {UserId}", actionId, rolledBackByUserId);
                    return AgentActionResult.Ok(action);
                }
                else
                {
                    return AgentActionResult.Fail("Failed to rollback action", "ROLLBACK_FAILED");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rolling back action {ActionId}", actionId);
                return AgentActionResult.Fail($"Error rolling back action: {ex.Message}", "INTERNAL_ERROR");
            }
        }

        #endregion

        #region Query Operations

        public async Task<AgentAction?> GetActionAsync(int actionId)
        {
            return await _context.AgentActions
                .Include(a => a.ProposedBy)
                .Include(a => a.ApprovedBy)
                .FirstOrDefaultAsync(a => a.Id == actionId);
        }

        public async Task<AgentAction?> GetActionByRunIdAsync(string runId)
        {
            return await _context.AgentActions
                .FirstOrDefaultAsync(a => a.RunId == runId);
        }

        public async Task<List<AgentAction>> GetPendingActionsAsync(int organizationId, int? matterId = null, int? limit = 50)
        {
            var query = _context.AgentActions
                .Where(a => a.OrganizationId == organizationId && a.Status == AgentActionStatus.Pending);

            if (matterId.HasValue)
                query = query.Where(a => a.MatterId == matterId);

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit ?? 50)
                .Include(a => a.ProposedBy)
                .ToListAsync();
        }

        public async Task<List<AgentAction>> GetActionsByStatusAsync(int organizationId, string status, int? limit = 50)
        {
            return await _context.AgentActions
                .Where(a => a.OrganizationId == organizationId && a.Status == status)
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit ?? 50)
                .Include(a => a.ProposedBy)
                .Include(a => a.ApprovedBy)
                .ToListAsync();
        }

        public async Task<List<AgentAction>> GetActionHistoryAsync(int organizationId, DateTime? startDate = null, DateTime? endDate = null, int? limit = 100)
        {
            var query = _context.AgentActions
                .Where(a => a.OrganizationId == organizationId);

            if (startDate.HasValue)
                query = query.Where(a => a.CreatedAt >= startDate);

            if (endDate.HasValue)
                query = query.Where(a => a.CreatedAt <= endDate);

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit ?? 100)
                .Include(a => a.ProposedBy)
                .Include(a => a.ApprovedBy)
                .ToListAsync();
        }

        public async Task<List<AgentAction>> GetMatterActionsAsync(int matterId, string? status = null, int? limit = 50)
        {
            var query = _context.AgentActions
                .Where(a => a.MatterId == matterId);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(a => a.Status == status);

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit ?? 50)
                .Include(a => a.ProposedBy)
                .ToListAsync();
        }

        public async Task<List<AgentAction>> GetUserProposedActionsAsync(int userId, string? status = null, int? limit = 50)
        {
            var query = _context.AgentActions
                .Where(a => a.ProposedById == userId);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(a => a.Status == status);

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit ?? 50)
                .ToListAsync();
        }

        public async Task<AgentActionStats> GetActionStatsAsync(int organizationId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.AgentActions
                .Where(a => a.OrganizationId == organizationId);

            if (startDate.HasValue)
                query = query.Where(a => a.CreatedAt >= startDate);

            if (endDate.HasValue)
                query = query.Where(a => a.CreatedAt <= endDate);

            var actions = await query.ToListAsync();

            var stats = new AgentActionStats
            {
                TotalActions = actions.Count,
                PendingCount = actions.Count(a => a.Status == AgentActionStatus.Pending),
                ApprovedCount = actions.Count(a => a.Status == AgentActionStatus.Approved),
                RejectedCount = actions.Count(a => a.Status == AgentActionStatus.Rejected),
                RunningCount = actions.Count(a => a.Status == AgentActionStatus.Running),
                DoneCount = actions.Count(a => a.Status == AgentActionStatus.Done),
                FailedCount = actions.Count(a => a.Status == AgentActionStatus.Failed),
                RolledBackCount = actions.Count(a => a.Status == AgentActionStatus.RolledBack),
                ActionsByType = actions.GroupBy(a => a.ActionType).ToDictionary(g => g.Key, g => g.Count())
            };

            // Calculate average times
            var approvedActions = actions.Where(a => a.ApprovedAt.HasValue && a.CreatedAt < a.ApprovedAt).ToList();
            if (approvedActions.Any())
            {
                stats.AverageApprovalTimeMinutes = approvedActions
                    .Average(a => (a.ApprovedAt!.Value - a.CreatedAt).TotalMinutes);
            }

            var completedActions = actions.Where(a => a.CompletedAt.HasValue && a.StartedAt.HasValue).ToList();
            if (completedActions.Any())
            {
                stats.AverageExecutionTimeMinutes = completedActions
                    .Average(a => (a.CompletedAt!.Value - a.StartedAt!.Value).TotalMinutes);
            }

            return stats;
        }

        #endregion

        #region Validation

        public async Task<AgentActionValidationResult> ValidatePayloadAsync(string actionType, object payload, int organizationId)
        {
            var errors = new List<string>();

            switch (actionType)
            {
                case AgentActionTypes.CreateTask:
                    if (payload is CreateTaskPayload createTask)
                    {
                        if (string.IsNullOrWhiteSpace(createTask.Title))
                            errors.Add("Task title is required");
                        if (createTask.MatterId <= 0)
                            errors.Add("Valid matter ID is required");

                        // Validate matter exists and user has access
                        var matter = await _context.Matters.FindAsync(createTask.MatterId);
                        if (matter == null)
                            errors.Add($"Matter {createTask.MatterId} not found");
                        else if (matter.OrganizationId != organizationId)
                            errors.Add("Matter does not belong to this organization");
                    }
                    else if (payload is JsonElement je)
                    {
                        var taskPayload = JsonSerializer.Deserialize<CreateTaskPayload>(je.GetRawText());
                        if (taskPayload != null)
                            return await ValidatePayloadAsync(actionType, taskPayload, organizationId);
                    }
                    else
                    {
                        errors.Add("Invalid CreateTask payload format");
                    }
                    break;

                case AgentActionTypes.AttachFile:
                    if (payload is AttachFilePayload attachFile)
                    {
                        if (attachFile.MatterId <= 0)
                            errors.Add("Valid matter ID is required");
                        if (string.IsNullOrWhiteSpace(attachFile.TargetEntityType))
                            errors.Add("Target entity type is required");
                    }
                    else
                    {
                        errors.Add("Invalid AttachFile payload format");
                    }
                    break;

                case AgentActionTypes.AddNote:
                    if (payload is AddNotePayload addNote)
                    {
                        if (addNote.MatterId <= 0)
                            errors.Add("Valid matter ID is required");
                        if (string.IsNullOrWhiteSpace(addNote.Content))
                            errors.Add("Note content is required");
                    }
                    else
                    {
                        errors.Add("Invalid AddNote payload format");
                    }
                    break;

                case AgentActionTypes.StartTimer:
                    if (payload is StartTimerPayload startTimer)
                    {
                        if (startTimer.MatterId <= 0)
                            errors.Add("Valid matter ID is required");
                        if (string.IsNullOrWhiteSpace(startTimer.Description))
                            errors.Add("Timer description is required");
                    }
                    else
                    {
                        errors.Add("Invalid StartTimer payload format");
                    }
                    break;

                case AgentActionTypes.SendTemplateMessage:
                    // Stub for phase 2 - always return valid
                    break;

                default:
                    errors.Add($"Unknown action type: {actionType}");
                    break;
            }

            return errors.Any() 
                ? AgentActionValidationResult.Invalid(errors.ToArray()) 
                : AgentActionValidationResult.Valid();
        }

        public async Task<bool> RunIdExistsAsync(string runId)
        {
            return await _context.AgentActions.AnyAsync(a => a.RunId == runId);
        }

        #endregion

        #region Private Execution Methods

        private async Task<ActionResultPayload> ExecuteCreateTaskAsync(AgentAction action)
        {
            var payload = action.GetPayload<CreateTaskPayload>();
            if (payload == null)
                return new ActionResultPayload { Success = false, Message = "Invalid CreateTask payload" };

            try
            {
                // Capture before state
                action.BeforeState = JsonSerializer.Serialize(new { MatterTasks = new List<int>() });

                // Create the task
                var task = new Domain.Tasks.TaskItem
                {
                    OrgId = action.OrganizationId,
                    MatterId = payload.MatterId,
                    Title = payload.Title,
                    Description = payload.Description,
                    Priority = payload.Priority,
                    DueDate = payload.DueDate,
                    Location = payload.Location,
                    Status = "Pending",
                    IsAIGenerated = !string.IsNullOrEmpty(action.AIAgentType),
                    AIAgentType = action.AIAgentType,
                    SourceConversationId = action.SourceConversationId,
                    SourceMessageId = action.SourceMessageId,
                    ApprovalStatus = "Approved", // Already approved via agent action
                    ApprovedById = action.ApprovedById,
                    ApprovedAt = action.ApprovedAt,
                    CreatedById = action.ProposedById,
                    CreatedAt = DateTime.UtcNow
                };

                _context.TaskItems.Add(task);
                await _context.SaveChangesAsync();

                // Create assignments if specified
                if (payload.AssigneeIds?.Any() == true)
                {
                    foreach (var assigneeId in payload.AssigneeIds)
                    {
                        var assignment = new Domain.Tasks.TaskAssignment
                        {
                            TaskItemId = task.Id,
                            UserId = assigneeId,
                            AssignmentType = "Assignee",
                            AssignedAt = DateTime.UtcNow
                        };
                        _context.TaskAssignments.Add(assignment);
                    }
                    await _context.SaveChangesAsync();
                }

                // Capture after state
                action.AfterState = JsonSerializer.Serialize(new { TaskId = task.Id, Title = task.Title });

                return new ActionResultPayload
                {
                    Success = true,
                    Message = $"Task '{task.Title}' created successfully",
                    CreatedEntityId = task.Id,
                    CreatedEntityType = "TaskItem"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing CreateTask for action {ActionId}", action.Id);
                return new ActionResultPayload { Success = false, Message = ex.Message };
            }
        }

        private async Task<ActionResultPayload> ExecuteAttachFileAsync(AgentAction action)
        {
            var payload = action.GetPayload<AttachFilePayload>();
            if (payload == null)
                return new ActionResultPayload { Success = false, Message = "Invalid AttachFile payload" };

            // TODO: Implement file attachment logic
            // This would integrate with the document service
            
            return new ActionResultPayload
            {
                Success = true,
                Message = "File attachment placeholder - implementation pending",
                CreatedEntityType = "Document"
            };
        }

        private async Task<ActionResultPayload> ExecuteAddNoteAsync(AgentAction action)
        {
            var payload = action.GetPayload<AddNotePayload>();
            if (payload == null)
                return new ActionResultPayload { Success = false, Message = "Invalid AddNote payload" };

            try
            {
                // For now, add note as a task comment if target is Task
                if (payload.TargetEntityType == "Task")
                {
                    var comment = new Domain.Tasks.TaskItemComment
                    {
                        TaskItemId = payload.TargetEntityId,
                        UserId = action.ProposedById ?? 0,
                        Content = payload.Content,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.TaskItemComments.Add(comment);
                    await _context.SaveChangesAsync();

                    return new ActionResultPayload
                    {
                        Success = true,
                        Message = "Note added successfully",
                        CreatedEntityId = comment.Id,
                        CreatedEntityType = "TaskItemComment"
                    };
                }

                // TODO: Handle other entity types (Matter, Document, etc.)
                return new ActionResultPayload
                {
                    Success = true,
                    Message = $"Note added to {payload.TargetEntityType} (placeholder)",
                    CreatedEntityType = "Note"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing AddNote for action {ActionId}", action.Id);
                return new ActionResultPayload { Success = false, Message = ex.Message };
            }
        }

        private async Task<ActionResultPayload> ExecuteStartTimerAsync(AgentAction action)
        {
            var payload = action.GetPayload<StartTimerPayload>();
            if (payload == null)
                return new ActionResultPayload { Success = false, Message = "Invalid StartTimer payload" };

            // TODO: Implement timer logic when billing module is available
            // This would create a time entry in the billing system
            
            return new ActionResultPayload
            {
                Success = true,
                Message = "Timer started (placeholder - billing module pending)",
                CreatedEntityType = "TimeEntry"
            };
        }

        private async Task<ActionResultPayload> ExecuteSendTemplateMessageAsync(AgentAction action)
        {
            // Stub for phase 2 - sending is not implemented yet
            return new ActionResultPayload
            {
                Success = false,
                Message = "SendTemplateMessage is not available in Phase 1 (read-only inbox)"
            };
        }

        #endregion

        #region Private Rollback Methods

        private async Task<bool> RollbackCreateTaskAsync(AgentAction action)
        {
            if (!action.CreatedEntityId.HasValue)
                return false;

            var task = await _context.TaskItems.FindAsync(action.CreatedEntityId.Value);
            if (task == null)
                return false;

            // Soft delete the task
            task.IsDeleted = true;
            task.DeletedAt = DateTime.UtcNow;
            task.DeletedById = action.RolledBackById;
            
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> RollbackAttachFileAsync(AgentAction action)
        {
            // TODO: Implement file attachment rollback
            return true;
        }

        private async Task<bool> RollbackAddNoteAsync(AgentAction action)
        {
            if (!action.CreatedEntityId.HasValue)
                return false;

            if (action.CreatedEntityType == "TaskItemComment")
            {
                var comment = await _context.TaskItemComments.FindAsync(action.CreatedEntityId.Value);
                if (comment != null)
                {
                    _context.TaskItemComments.Remove(comment);
                    await _context.SaveChangesAsync();
                }
            }

            return true;
        }

        private async Task<bool> RollbackStartTimerAsync(AgentAction action)
        {
            // TODO: Implement timer rollback when billing module is available
            return true;
        }

        #endregion

        #region Private Helpers

        private string GetDefaultDescription(string actionType, object payload)
        {
            if (payload is ActionPayloadBase payloadBase)
                return payloadBase.GetSummary();

            return actionType switch
            {
                AgentActionTypes.CreateTask => "Create a new task",
                AgentActionTypes.AttachFile => "Attach a file",
                AgentActionTypes.AddNote => "Add a note",
                AgentActionTypes.StartTimer => "Start a timer",
                AgentActionTypes.SendTemplateMessage => "Send a template message",
                _ => $"Execute {actionType}"
            };
        }

        private async Task LogAuditEventAsync(AgentAction action, string auditAction, int? userId)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    EntityType = "AgentAction",
                    EntityId = action.Id,
                    Action = auditAction,
                    Result = AuditResults.Success,
                    UserId = userId,
                    OrganizationId = action.OrganizationId,
                    MatterId = action.MatterId,
                    IsAIAction = !string.IsNullOrEmpty(action.AIAgentType),
                    AIAgentType = action.AIAgentType,
                    SourceConversationId = action.SourceConversationId,
                    SourceMessageId = action.SourceMessageId,
                    Description = $"{auditAction}: {action.ActionType} - {action.Description}",
                    NewValues = JsonSerializer.Serialize(new
                    {
                        action.RunId,
                        action.ActionType,
                        action.Status,
                        action.CorrelationId
                    }),
                    Timestamp = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log audit event for action {ActionId}", action.Id);
            }
        }

        #endregion
    }
}

