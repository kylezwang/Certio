using Microsoft.EntityFrameworkCore;
using System.Text;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Audit;
using Certio.Domain.Matters;
using Certio.Domain.Tasks;
using Certio.Domain.Documents;
using Certio.Infrastructure.Data;

namespace Certio.Application.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<AuditLog>> GetEntityHistoryAsync(string entityType, int entityId)
        {
            return await _context.AuditLogs
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetUserActivityAsync(int userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.AuditLogs.Where(a => a.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            return await query
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetAIGeneratedContentAsync(DateTime? startDate = null, DateTime? endDate = null, string? agentType = null)
        {
            var query = _context.AuditLogs.Where(a => a.IsAIAction);

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            if (!string.IsNullOrEmpty(agentType))
                query = query.Where(a => a.AIAgentType == agentType);

            return await query
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLogDTO>> GetUnreviewedAIContentAsync(int? organizationId = null)
        {
            // Get AI-generated entities that need review
            var matterQuery = _context.Matters
                .Where(m => m.IsAIGenerated && m.ApprovalStatus == "Pending");
            
            if (organizationId.HasValue)
                matterQuery = matterQuery.Where(m => m.OrganizationId == organizationId.Value);

            var matters = await matterQuery
                .Select(m => new AuditLogDTO
                {
                    EntityType = "Matter",
                    EntityId = m.Id,
                    EntityTitle = m.Title,
                    IsAIAction = true,
                    AIAgentType = m.AIAgentType,
                    SourceConversationId = m.SourceConversationId,
                    SourceMessageId = m.SourceMessageId,
                    ApprovalStatus = m.ApprovalStatus,
                    NeedsReview = true,
                    Timestamp = m.CreatedAt
                })
                .ToListAsync();

            var taskQuery = _context.TaskItems
                .Where(t => t.IsAIGenerated && t.ApprovalStatus == "Pending");
            
            if (organizationId.HasValue)
                taskQuery = taskQuery.Where(t => t.OrgId == organizationId.Value);

            var tasks = await taskQuery
                .Select(t => new AuditLogDTO
                {
                    EntityType = "TaskItem",
                    EntityId = t.Id,
                    EntityTitle = t.Title,
                    IsAIAction = true,
                    AIAgentType = t.AIAgentType,
                    SourceConversationId = t.SourceConversationId,
                    SourceMessageId = t.SourceMessageId,
                    ApprovalStatus = t.ApprovalStatus,
                    NeedsReview = true,
                    Timestamp = t.CreatedAt
                })
                .ToListAsync();

            var result = new List<AuditLogDTO>();
            result.AddRange(matters);
            result.AddRange(tasks);

            return result.OrderByDescending(a => a.Timestamp).ToList();
        }

        public async Task<List<AuditLog>> GetOrganizationAuditLogsAsync(int organizationId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.AuditLogs.Where(a => a.OrganizationId == organizationId);

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            return await query
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetMatterAuditLogsAsync(int matterId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.AuditLogs.Where(a => a.MatterId == matterId);

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            return await query
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<byte[]> ExportAuditLogsAsync(DateTime startDate, DateTime endDate, int? organizationId = null)
        {
            var query = _context.AuditLogs
                .Where(a => a.Timestamp >= startDate && a.Timestamp <= endDate);

            if (organizationId.HasValue)
                query = query.Where(a => a.OrganizationId == organizationId.Value);

            var logs = await query
                .OrderBy(a => a.Timestamp)
                .ToListAsync();

            var csv = new StringBuilder();
            
            // Header
            csv.AppendLine("Timestamp,EntityType,EntityId,Action,UserId,UserName,IPAddress,Description,IsAIAction,AIAgentType,Result");

            // Data rows
            foreach (var log in logs)
            {
                csv.AppendLine($"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\"," +
                    $"\"{log.EntityType}\"," +
                    $"\"{log.EntityId}\"," +
                    $"\"{log.Action}\"," +
                    $"\"{log.UserId}\"," +
                    $"\"{log.UserName}\"," +
                    $"\"{log.IPAddress}\"," +
                    $"\"{log.Description}\"," +
                    $"\"{log.IsAIAction}\"," +
                    $"\"{log.AIAgentType}\"," +
                    $"\"{log.Result}\"");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<AuditSummaryDTO> GetAuditSummaryAsync(DateTime startDate, DateTime endDate, int? organizationId = null)
        {
            var query = _context.AuditLogs
                .Where(a => a.Timestamp >= startDate && a.Timestamp <= endDate);

            if (organizationId.HasValue)
                query = query.Where(a => a.OrganizationId == organizationId.Value);

            var logs = await query.ToListAsync();

            var summary = new AuditSummaryDTO
            {
                TotalActions = logs.Count,
                TotalUsers = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId).Distinct().Count(),
                CreatedCount = logs.Count(l => l.Action == AuditActions.Create),
                UpdatedCount = logs.Count(l => l.Action == AuditActions.Update),
                DeletedCount = logs.Count(l => l.Action == AuditActions.Delete || l.Action == AuditActions.SoftDelete),
                AIGeneratedCount = logs.Count(l => l.IsAIAction),
                AIApprovedCount = logs.Count(l => l.Action == AuditActions.AIApproved),
                AIPendingCount = logs.Count(l => l.IsAIAction && l.Action == AuditActions.Create),
                ActionsByType = logs.GroupBy(l => l.Action)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ActionsByEntity = logs.GroupBy(l => l.EntityType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TopUsers = logs.Where(l => l.UserId.HasValue && l.UserName != null)
                    .GroupBy(l => new { l.UserId, l.UserName })
                    .Select(g => new TopUserActivityDTO
                    {
                        UserId = g.Key.UserId!.Value,
                        UserName = g.Key.UserName!,
                        ActionCount = g.Count()
                    })
                    .OrderByDescending(u => u.ActionCount)
                    .Take(10)
                    .ToList()
            };

            return summary;
        }

        public async Task LogAuditEventAsync(string entityType, int entityId, string action, int? userId = null, string? description = null)
        {
            var auditLog = new AuditLog
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                UserId = userId,
                Description = description,
                Result = AuditResults.Success,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
    }
}
