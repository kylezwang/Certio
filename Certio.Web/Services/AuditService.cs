using Certio.Domain.Audit;
using Certio.Web.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Certio.Web.Services
{
    /// <summary>
    /// Implementation of audit logging service
    /// All security-relevant operations should be logged here
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IServiceProvider serviceProvider, ILogger<AuditService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task LogOperationAsync(
            int userId, 
            int organizationId, 
            string action, 
            string entityType, 
            int entityId, 
            string? ipAddress = null,
            string? userAgent = null,
            string? details = null)
        {
            try
            {
                // Create a new scope with its own DbContext to avoid conflicts
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Timestamp = DateTime.UtcNow,
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    Description = details,
                    Result = "SUCCESS"
                };

                context.AuditLogs.Add(auditLog);
                await context.SaveChangesAsync();

                _logger.LogInformation(
                    "Audit: User {UserId} performed {Action} on {EntityType} {EntityId} in org {OrgId}",
                    userId, action, entityType, entityId, organizationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit event for user {UserId}, action {Action}", userId, action);
                // Don't throw - audit logging failure shouldn't break the application
            }
        }

        public async Task LogAuthorizationFailureAsync(
            int userId, 
            int organizationId, 
            string entityType, 
            int entityId, 
            string reason,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                // Create a new scope with its own DbContext to avoid conflicts
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "AUTH_FAILURE",
                    EntityType = entityType,
                    EntityId = entityId,
                    Timestamp = DateTime.UtcNow,
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    Description = $"FAILURE: {reason}",
                    Result = "FAILURE"
                };

                context.AuditLogs.Add(auditLog);
                await context.SaveChangesAsync();

                _logger.LogWarning(
                    "SECURITY: Authorization failure - User {UserId} attempted to access {EntityType} {EntityId} in org {OrgId}. Reason: {Reason}",
                    userId, entityType, entityId, organizationId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log authorization failure for user {UserId}", userId);
            }
        }

        public async Task LogCreateAsync(
            int userId, 
            int organizationId, 
            string entityType, 
            int entityId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            await LogOperationAsync(userId, organizationId, "CREATE", entityType, entityId, ipAddress, userAgent);
        }

        public async Task LogUpdateAsync(
            int userId, 
            int organizationId, 
            string entityType, 
            int entityId,
            string? changes = null,
            string? ipAddress = null,
            string? userAgent = null)
        {
            await LogOperationAsync(userId, organizationId, "UPDATE", entityType, entityId, ipAddress, userAgent, changes);
        }

        public async Task LogDeleteAsync(
            int userId, 
            int organizationId, 
            string entityType, 
            int entityId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            await LogOperationAsync(userId, organizationId, "DELETE", entityType, entityId, ipAddress, userAgent);
        }
    }
}

