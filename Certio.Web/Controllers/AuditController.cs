using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;

namespace Certio.Web.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly IAuditService _auditService;
        private readonly ILogger<AuditController> _logger;

        public AuditController(IAuditService auditService, ILogger<AuditController> logger)
        {
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>
        /// Get audit history for a specific entity
        /// </summary>
        [HttpGet("entity/{entityType}/{entityId}")]
        public async Task<ActionResult<List<AuditLogDTO>>> GetEntityHistory(string entityType, int entityId)
        {
            try
            {
                var history = await _auditService.GetEntityHistoryAsync(entityType, entityId);
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entity history for {EntityType}/{EntityId}", entityType, entityId);
                return StatusCode(500, "An error occurred while retrieving audit history");
            }
        }

        /// <summary>
        /// Get user activity logs
        /// </summary>
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<List<AuditLogDTO>>> GetUserActivity(
            int userId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var activity = await _auditService.GetUserActivityAsync(userId, startDate, endDate);
                return Ok(activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user activity for user {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving user activity");
            }
        }

        /// <summary>
        /// Get AI-generated content logs
        /// </summary>
        [HttpGet("ai-generated")]
        public async Task<ActionResult<List<AuditLogDTO>>> GetAIGeneratedContent(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? agentType = null)
        {
            try
            {
                var content = await _auditService.GetAIGeneratedContentAsync(startDate, endDate, agentType);
                return Ok(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AI-generated content");
                return StatusCode(500, "An error occurred while retrieving AI-generated content");
            }
        }

        /// <summary>
        /// Get unreviewed AI-generated content requiring approval
        /// </summary>
        [HttpGet("ai-pending-review")]
        public async Task<ActionResult<List<AIContentReviewDto>>> GetUnreviewedAIContent(
            [FromQuery] int? organizationId = null)
        {
            try
            {
                var content = await _auditService.GetUnreviewedAIContentAsync(organizationId);
                return Ok(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unreviewed AI content");
                return StatusCode(500, "An error occurred while retrieving unreviewed AI content");
            }
        }

        /// <summary>
        /// Export audit logs for compliance
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportAuditLogs(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] string format = "csv")
        {
            try
            {
                var data = await _auditService.ExportAuditLogsAsync(startDate, endDate, null);
                var contentType = format.ToLower() == "csv" ? "text/csv" : "application/octet-stream";
                var fileName = $"audit-logs-{startDate:yyyy-MM-dd}-to-{endDate:yyyy-MM-dd}.{format}";
                
                return File(data, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit logs");
                return StatusCode(500, "An error occurred while exporting audit logs");
            }
        }

        /// <summary>
        /// Get audit statistics
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<AuditSummaryDTO>> GetAuditStats(
            [FromQuery] int? organizationId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var stats = await _auditService.GetAuditSummaryAsync(startDate ?? DateTime.UtcNow.AddMonths(-1), endDate ?? DateTime.UtcNow, organizationId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit statistics");
                return StatusCode(500, "An error occurred while retrieving audit statistics");
            }
        }
    }
}

