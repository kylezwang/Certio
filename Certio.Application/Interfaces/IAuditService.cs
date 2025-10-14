using Certio.Application.DTOs;
using Certio.Domain.Audit;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service for querying audit logs and tracking changes
    /// </summary>
    public interface IAuditService
    {
        /// <summary>
        /// Get complete history for a specific entity
        /// </summary>
        Task<List<AuditLog>> GetEntityHistoryAsync(string entityType, int entityId);

        /// <summary>
        /// Get all activity for a specific user within a date range
        /// </summary>
        Task<List<AuditLog>> GetUserActivityAsync(int userId, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Get all AI-generated content within a date range, optionally filtered by agent type
        /// </summary>
        Task<List<AuditLog>> GetAIGeneratedContentAsync(DateTime? startDate = null, DateTime? endDate = null, string? agentType = null);

        /// <summary>
        /// Get all AI-generated content awaiting review/approval
        /// </summary>
        Task<List<AuditLogDTO>> GetUnreviewedAIContentAsync(int? organizationId = null);

        /// <summary>
        /// Get audit logs for a specific organization
        /// </summary>
        Task<List<AuditLog>> GetOrganizationAuditLogsAsync(int organizationId, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Get audit logs for a specific matter
        /// </summary>
        Task<List<AuditLog>> GetMatterAuditLogsAsync(int matterId, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Export audit logs to CSV for compliance
        /// </summary>
        Task<byte[]> ExportAuditLogsAsync(DateTime startDate, DateTime endDate, int? organizationId = null);

        /// <summary>
        /// Get audit summary statistics
        /// </summary>
        Task<AuditSummaryDTO> GetAuditSummaryAsync(DateTime startDate, DateTime endDate, int? organizationId = null);

        /// <summary>
        /// Track a custom audit event
        /// </summary>
        Task LogAuditEventAsync(string entityType, int entityId, string action, int? userId = null, string? description = null);
    }
}
