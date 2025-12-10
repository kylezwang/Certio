using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Interfaces;
using Certio.Domain.AgentActions;
using Certio.Domain.Users;
using Certio.Web.Security;
using Certio.Web.Services;

namespace Certio.Web.Controllers.Api
{
    /// <summary>
    /// API controller for agent actions with approval workflow.
    /// Implements PENDING → APPROVED → RUNNING → DONE/FAILED lifecycle.
    /// </summary>
    [ApiController]
    [Route("api/agent-actions")]
    [Authorize(Policy = "OrgMember")]
    public class AgentActionsController : ControllerBase
    {
        private readonly IAgentActionService _actionService;
        private readonly IClientContextAccessor _clientContextAccessor;
        private readonly ILogger<AgentActionsController> _logger;

        public AgentActionsController(
            IAgentActionService actionService,
            IClientContextAccessor clientContextAccessor,
            ILogger<AgentActionsController> logger)
        {
            _actionService = actionService;
            _clientContextAccessor = clientContextAccessor;
            _logger = logger;
        }

        private int OrgId => _clientContextAccessor.Current?.OrganizationId ?? 0;
        private int UserId => _clientContextAccessor.Current?.UserId ?? 0;
        
        /// <summary>
        /// Get run ID from x-run-id header (idempotency key)
        /// </summary>
        private string GetRunId() => 
            Request.Headers.TryGetValue("x-run-id", out var runId) 
                ? runId.ToString() 
                : Guid.NewGuid().ToString();

        #region Query Endpoints

        /// <summary>
        /// Get pending actions for the current organization
        /// </summary>
        [HttpGet("pending")]
        [RequireAgentPermission(Permission.ViewAgentActions)]
        public async Task<IActionResult> GetPendingActions([FromQuery] int? matterId = null, [FromQuery] int? limit = 50)
        {
            var actions = await _actionService.GetPendingActionsAsync(OrgId, matterId, limit);
            return Ok(new { actions, count = actions.Count });
        }

        /// <summary>
        /// Get action history for the current organization
        /// </summary>
        [HttpGet("history")]
        [RequireAgentPermission(Permission.ViewAgentActions)]
        public async Task<IActionResult> GetActionHistory(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? limit = 100)
        {
            var actions = await _actionService.GetActionHistoryAsync(OrgId, startDate, endDate, limit);
            return Ok(new { actions, count = actions.Count });
        }

        /// <summary>
        /// Get actions by status
        /// </summary>
        [HttpGet("by-status/{status}")]
        [RequireAgentPermission(Permission.ViewAgentActions)]
        public async Task<IActionResult> GetActionsByStatus(string status, [FromQuery] int? limit = 50)
        {
            if (!AgentActionStatus.IsValid(status))
                return BadRequest(new { error = $"Invalid status: {status}" });

            var actions = await _actionService.GetActionsByStatusAsync(OrgId, status, limit);
            return Ok(new { actions, count = actions.Count });
        }

        /// <summary>
        /// Get a specific action by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [RequireAgentPermission(Permission.ViewAgentActions)]
        public async Task<IActionResult> GetAction(int id)
        {
            var action = await _actionService.GetActionAsync(id);
            if (action == null || action.OrganizationId != OrgId)
                return NotFound();

            return Ok(action);
        }

        /// <summary>
        /// Get action by run ID (idempotency key)
        /// </summary>
        [HttpGet("by-run-id/{runId}")]
        [RequireAgentPermission(Permission.ViewAgentActions)]
        public async Task<IActionResult> GetActionByRunId(string runId)
        {
            var action = await _actionService.GetActionByRunIdAsync(runId);
            if (action == null || action.OrganizationId != OrgId)
                return NotFound();

            return Ok(action);
        }

        /// <summary>
        /// Get actions for a specific matter
        /// </summary>
        [HttpGet("matter/{matterId:int}")]
        [RequireAgentPermission(Permission.ViewAgentActions)]
        public async Task<IActionResult> GetMatterActions(int matterId, [FromQuery] string? status = null, [FromQuery] int? limit = 50)
        {
            var actions = await _actionService.GetMatterActionsAsync(matterId, status, limit);
            return Ok(new { actions, count = actions.Count });
        }

        /// <summary>
        /// Get action statistics
        /// </summary>
        [HttpGet("stats")]
        [RequireAgentPermission(Permission.ViewAgentActions)]
        public async Task<IActionResult> GetStats(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var stats = await _actionService.GetActionStatsAsync(OrgId, startDate, endDate);
            return Ok(stats);
        }

        #endregion

        #region Propose Endpoints

        /// <summary>
        /// Propose a new agent action
        /// </summary>
        [HttpPost("propose")]
        [RequireAgentPermission(Permission.ProposeAgentActions)]
        public async Task<IActionResult> ProposeAction([FromBody] ProposeActionRequest request)
        {
            if (!AgentActionTypes.IsValid(request.ActionType))
                return BadRequest(new { error = $"Invalid action type: {request.ActionType}" });

            var runId = GetRunId();
            var correlationId = Request.Headers.TryGetValue("x-correlation-id", out var corrId) ? corrId.ToString() : null;

            var result = await _actionService.ProposeActionAsync(
                OrgId,
                request.ActionType,
                request.Payload,
                UserId,
                runId,
                correlationId,
                request.MatterId,
                request.Description,
                request.AIAgentType,
                request.SourceConversationId,
                request.SourceMessageId);

            if (!result.Success)
                return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

            return result.IsDuplicate 
                ? Ok(new { action = result.Action, duplicate = true })
                : CreatedAtAction(nameof(GetAction), new { id = result.Action!.Id }, result.Action);
        }

        /// <summary>
        /// Propose a CreateTask action
        /// </summary>
        [HttpPost("propose/create-task")]
        [RequireAgentPermission(Permission.ProposeAgentActions)]
        public async Task<IActionResult> ProposeCreateTask([FromBody] CreateTaskPayload payload)
        {
            var runId = GetRunId();
            var correlationId = Request.Headers.TryGetValue("x-correlation-id", out var corrId) ? corrId.ToString() : null;

            var result = await _actionService.ProposeCreateTaskAsync(payload, UserId, runId, OrgId, correlationId);

            if (!result.Success)
                return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

            return result.IsDuplicate 
                ? Ok(new { action = result.Action, duplicate = true })
                : CreatedAtAction(nameof(GetAction), new { id = result.Action!.Id }, result.Action);
        }

        /// <summary>
        /// Propose an AttachFile action
        /// </summary>
        [HttpPost("propose/attach-file")]
        [RequireAgentPermission(Permission.ProposeAgentActions)]
        public async Task<IActionResult> ProposeAttachFile([FromBody] AttachFilePayload payload)
        {
            var runId = GetRunId();
            var correlationId = Request.Headers.TryGetValue("x-correlation-id", out var corrId) ? corrId.ToString() : null;

            var result = await _actionService.ProposeAttachFileAsync(payload, UserId, runId, OrgId, correlationId);

            if (!result.Success)
                return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

            return result.IsDuplicate 
                ? Ok(new { action = result.Action, duplicate = true })
                : CreatedAtAction(nameof(GetAction), new { id = result.Action!.Id }, result.Action);
        }

        /// <summary>
        /// Propose an AddNote action
        /// </summary>
        [HttpPost("propose/add-note")]
        [RequireAgentPermission(Permission.ProposeAgentActions)]
        public async Task<IActionResult> ProposeAddNote([FromBody] AddNotePayload payload)
        {
            var runId = GetRunId();
            var correlationId = Request.Headers.TryGetValue("x-correlation-id", out var corrId) ? corrId.ToString() : null;

            var result = await _actionService.ProposeAddNoteAsync(payload, UserId, runId, OrgId, correlationId);

            if (!result.Success)
                return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

            return result.IsDuplicate 
                ? Ok(new { action = result.Action, duplicate = true })
                : CreatedAtAction(nameof(GetAction), new { id = result.Action!.Id }, result.Action);
        }

        /// <summary>
        /// Propose a StartTimer action
        /// </summary>
        [HttpPost("propose/start-timer")]
        [RequireAgentPermission(Permission.ProposeAgentActions)]
        public async Task<IActionResult> ProposeStartTimer([FromBody] StartTimerPayload payload)
        {
            var runId = GetRunId();
            var correlationId = Request.Headers.TryGetValue("x-correlation-id", out var corrId) ? corrId.ToString() : null;

            var result = await _actionService.ProposeStartTimerAsync(payload, UserId, runId, OrgId, correlationId);

            if (!result.Success)
                return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

            return result.IsDuplicate 
                ? Ok(new { action = result.Action, duplicate = true })
                : CreatedAtAction(nameof(GetAction), new { id = result.Action!.Id }, result.Action);
        }

        #endregion

        #region Approval Endpoints

        /// <summary>
        /// Approve a pending action
        /// </summary>
        [HttpPost("{id:int}/approve")]
        [RequireAgentPermission(Permission.ApproveAgentActions)]
        public async Task<IActionResult> ApproveAction(int id, [FromBody] ApprovalRequest? request = null)
        {
            var result = await _actionService.ApproveActionAsync(id, UserId, request?.Notes);

            if (!result.Success)
            {
                return result.ErrorCode == "NOT_FOUND" 
                    ? NotFound(new { error = result.ErrorMessage })
                    : BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });
            }

            return Ok(result.Action);
        }

        /// <summary>
        /// Reject a pending action
        /// </summary>
        [HttpPost("{id:int}/reject")]
        [RequireAgentPermission(Permission.ApproveAgentActions)]
        public async Task<IActionResult> RejectAction(int id, [FromBody] ApprovalRequest? request = null)
        {
            var result = await _actionService.RejectActionAsync(id, UserId, request?.Notes);

            if (!result.Success)
            {
                return result.ErrorCode == "NOT_FOUND" 
                    ? NotFound(new { error = result.ErrorMessage })
                    : BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });
            }

            return Ok(result.Action);
        }

        /// <summary>
        /// Bulk approve multiple pending actions
        /// </summary>
        [HttpPost("bulk-approve")]
        [RequireAgentPermission(Permission.ApproveAgentActions)]
        public async Task<IActionResult> BulkApprove([FromBody] BulkApprovalRequest request)
        {
            var result = await _actionService.BulkApproveActionsAsync(request.ActionIds, UserId, request.Notes);
            return Ok(new { 
                success = result.Success, 
                successCount = result.SuccessCount, 
                failureCount = result.FailureCount 
            });
        }

        /// <summary>
        /// Bulk reject multiple pending actions
        /// </summary>
        [HttpPost("bulk-reject")]
        [RequireAgentPermission(Permission.ApproveAgentActions)]
        public async Task<IActionResult> BulkReject([FromBody] BulkApprovalRequest request)
        {
            var result = await _actionService.BulkRejectActionsAsync(request.ActionIds, UserId, request.Notes);
            return Ok(new { 
                success = result.Success, 
                successCount = result.SuccessCount, 
                failureCount = result.FailureCount 
            });
        }

        #endregion

        #region Execution Endpoints

        /// <summary>
        /// Execute an approved action
        /// </summary>
        [HttpPost("{id:int}/execute")]
        [RequireAgentPermission(Permission.ApproveAgentActions)]
        public async Task<IActionResult> ExecuteAction(int id)
        {
            var result = await _actionService.ExecuteActionAsync(id);

            if (!result.Success)
            {
                return result.ErrorCode == "NOT_FOUND" 
                    ? NotFound(new { error = result.ErrorMessage })
                    : BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });
            }

            return Ok(result.Action);
        }

        /// <summary>
        /// Execute all approved actions (batch processing)
        /// </summary>
        [HttpPost("execute-pending")]
        [RequireAgentPermission(Permission.ApproveAgentActions)]
        public async Task<IActionResult> ExecutePendingActions([FromQuery] int? limit = 10)
        {
            var result = await _actionService.ExecutePendingActionsAsync(OrgId, limit);
            return Ok(new { 
                success = result.Success, 
                successCount = result.SuccessCount, 
                failureCount = result.FailureCount 
            });
        }

        /// <summary>
        /// Rollback a completed action
        /// </summary>
        [HttpPost("{id:int}/rollback")]
        [RequireAgentPermission(Permission.RollbackAgentActions)]
        public async Task<IActionResult> RollbackAction(int id, [FromBody] ApprovalRequest? request = null)
        {
            var result = await _actionService.RollbackActionAsync(id, UserId, request?.Notes);

            if (!result.Success)
            {
                return result.ErrorCode == "NOT_FOUND" 
                    ? NotFound(new { error = result.ErrorMessage })
                    : BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });
            }

            return Ok(result.Action);
        }

        #endregion
    }

    #region Request DTOs

    public class ProposeActionRequest
    {
        public string ActionType { get; set; } = "";
        public object Payload { get; set; } = new();
        public int? MatterId { get; set; }
        public string? Description { get; set; }
        public string? AIAgentType { get; set; }
        public int? SourceConversationId { get; set; }
        public int? SourceMessageId { get; set; }
    }

    public class ApprovalRequest
    {
        public string? Notes { get; set; }
    }

    public class BulkApprovalRequest
    {
        public List<int> ActionIds { get; set; } = new();
        public string? Notes { get; set; }
    }

    #endregion
}

