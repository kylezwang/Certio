using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Certio.Domain.Audit;
using Certio.Domain.Users;
using Certio.Domain.Matters;
using Certio.Domain.Documents;
using Certio.Domain.Tasks;
using Certio.Domain.Organizations;
using Certio.Domain.Teams;
using Certio.Domain.Calendar;

namespace Certio.Infrastructure.Interceptors
{
    /// <summary>
    /// Interceptor that automatically:
    /// - Sets CreatedById on entity creation
    /// - Sets ModifiedById on entity updates
    /// - Sets DeletedAt and DeletedById on soft deletes
    /// - Logs all changes to AuditLog table
    /// - Captures old values vs new values in JSON format
    /// - Includes HTTP context information (IP, User-Agent)
    /// </summary>
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AsyncLocal<List<(EntityEntry Entry, string Action, string? OldValues, string? NewValues)>> _pendingAuditInfo = new();

        public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            // PHASE 1: Capture audit information BEFORE save (while states are Modified/Added/Deleted)
            _pendingAuditInfo.Value = CaptureAuditInfo(eventData.Context);
            UpdateAuditFields(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            // PHASE 1: Capture audit information BEFORE save (while states are Modified/Added/Deleted)
            _pendingAuditInfo.Value = CaptureAuditInfo(eventData.Context);
            UpdateAuditFields(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            // PHASE 2: Write audit logs AFTER save completes
            WritePendingAudits(eventData.Context);
            return base.SavedChanges(eventData, result);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            // PHASE 2: Write audit logs AFTER save completes
            await WritePendingAuditsAsync(eventData.Context, cancellationToken);
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        private void UpdateAuditFields(DbContext? context)
        {
            if (context == null) return;

            var currentUserId = GetCurrentUserId();
            var timestamp = DateTime.UtcNow;

            foreach (var entry in context.ChangeTracker.Entries())
            {
                // Handle entity creation
                if (entry.State == EntityState.Added)
                {
                    SetCreatedByFields(entry, currentUserId, timestamp);
                }
                // Handle entity update
                else if (entry.State == EntityState.Modified)
                {
                    SetModifiedByFields(entry, currentUserId, timestamp);
                    
                    // Check if this is a soft delete
                    if (HasProperty(entry, "IsDeleted") && entry.CurrentValues["IsDeleted"] is bool isDeleted && isDeleted)
                    {
                        SetDeletedByFields(entry, currentUserId, timestamp);
                    }
                }
                // Handle hard delete (convert to soft delete if entity supports it)
                else if (entry.State == EntityState.Deleted)
                {
                    if (HasProperty(entry, "IsDeleted"))
                    {
                        // Convert hard delete to soft delete
                    entry.State = EntityState.Modified;
                        entry.CurrentValues["IsDeleted"] = true;
                        SetDeletedByFields(entry, currentUserId, timestamp);
                    }
                }
            }
        }

        /// <summary>
        /// PHASE 1: Capture entity info BEFORE save (while entity states are still Modified/Added/Deleted)
        /// Stores entity references and change data, but waits until after save to get IDs
        /// </summary>
        private List<(EntityEntry Entry, string Action, string? OldValues, string? NewValues)> CaptureAuditInfo(DbContext? context)
        {
            var auditInfo = new List<(EntityEntry, string, string?, string?)>();
            
            if (context == null) 
                return auditInfo;

            foreach (var entry in context.ChangeTracker.Entries())
            {
                // Include Modified, Added, and Deleted states
                if (entry.State == EntityState.Unchanged || entry.State == EntityState.Detached)
                    continue;

                if (!ShouldAudit(entry.Entity))
                    continue;

                var action = GetAction(entry);
                var oldValues = GetOldValues(entry);
                var newValues = GetNewValues(entry);

                auditInfo.Add((entry, action, oldValues, newValues));
            }

            return auditInfo;
        }

        /// <summary>
        /// PHASE 2: Write pending audit logs AFTER the main save completes
        /// Now entities have their IDs assigned, so we can create complete audit logs
        /// </summary>
        private void WritePendingAudits(DbContext? context)
        {
            if (context == null) 
                return;

            var pendingInfo = _pendingAuditInfo.Value;
            if (pendingInfo == null || !pendingInfo.Any())
                return;

            try
            {
                var auditLogs = new List<AuditLog>();
                var httpContext = _httpContextAccessor.HttpContext;
                var currentUserId = GetCurrentUserId();
                var currentUserName = GetCurrentUserName();

                foreach (var (entry, action, oldValues, newValues) in pendingInfo)
                {
                    var entityType = entry.Entity.GetType().Name;
                    var entityId = GetEntityId(entry); // Now has the real ID after save!

                    // Skip if still no ID (shouldn't happen, but safety check)
                    if (entityId == 0) 
                        continue;

                    var auditLog = new AuditLog
                    {
                        EntityType = entityType,
                        EntityId = entityId,
                        Action = action,
                        Result = AuditResults.Success,
                        UserId = currentUserId,
                        UserName = currentUserName,
                        IPAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                        UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
                        OldValues = oldValues,
                        NewValues = newValues,
                        Description = $"{action} {entityType} with ID {entityId}",
                        Timestamp = DateTime.UtcNow,
                        SessionId = httpContext?.Session?.Id,
                        RequestUrl = httpContext?.Request?.Path.ToString(),
                        HttpMethod = httpContext?.Request?.Method,
                        OrganizationId = GetOrganizationId(entry),
                        MatterId = GetMatterId(entry)
                    };

                    // Add AI-related information if applicable
                    if (HasProperty(entry, "IsAIGenerated") && entry.CurrentValues["IsAIGenerated"] is bool isAI && isAI)
                    {
                        auditLog.IsAIAction = true;
                        auditLog.AIAgentType = entry.CurrentValues["AIAgentType"]?.ToString();
                        auditLog.SourceConversationId = GetPropertyValue<int?>(entry, "SourceConversationId");
                        auditLog.SourceMessageId = GetPropertyValue<int?>(entry, "SourceMessageId");
                    }

                    auditLogs.Add(auditLog);
                }

                if (auditLogs.Any())
                {
                    context.Set<AuditLog>().AddRange(auditLogs);
                    context.SaveChanges();
                }
            }
            finally
            {
                _pendingAuditInfo.Value = null!; // Clear after writing
            }
        }

        /// <summary>
        /// PHASE 2: Write pending audit logs AFTER the main save completes (async version)
        /// </summary>
        private async Task WritePendingAuditsAsync(DbContext? context, CancellationToken cancellationToken)
        {
            if (context == null) 
                return;

            var pendingInfo = _pendingAuditInfo.Value;
            if (pendingInfo == null || !pendingInfo.Any())
                return;

            try
            {
                var auditLogs = new List<AuditLog>();
                var httpContext = _httpContextAccessor.HttpContext;
                var currentUserId = GetCurrentUserId();
                var currentUserName = GetCurrentUserName();

                foreach (var (entry, action, oldValues, newValues) in pendingInfo)
        {
            var entityType = entry.Entity.GetType().Name;
                    var entityId = GetEntityId(entry); // Now has the real ID after save!

                    // Skip if still no ID (shouldn't happen, but safety check)
                    if (entityId == 0) 
                        continue;

                    var auditLog = new AuditLog
                    {
                        EntityType = entityType,
                        EntityId = entityId,
                        Action = action,
                        Result = AuditResults.Success,
                        UserId = currentUserId,
                        UserName = currentUserName,
                        IPAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                        UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
                        OldValues = oldValues,
                        NewValues = newValues,
                        Description = $"{action} {entityType} with ID {entityId}",
                        Timestamp = DateTime.UtcNow,
                        SessionId = httpContext?.Session?.Id,
                        RequestUrl = httpContext?.Request?.Path.ToString(),
                        HttpMethod = httpContext?.Request?.Method,
                        OrganizationId = GetOrganizationId(entry),
                        MatterId = GetMatterId(entry)
                    };

                    // Add AI-related information if applicable
                    if (HasProperty(entry, "IsAIGenerated") && entry.CurrentValues["IsAIGenerated"] is bool isAI && isAI)
                    {
                        auditLog.IsAIAction = true;
                        auditLog.AIAgentType = entry.CurrentValues["AIAgentType"]?.ToString();
                        auditLog.SourceConversationId = GetPropertyValue<int?>(entry, "SourceConversationId");
                        auditLog.SourceMessageId = GetPropertyValue<int?>(entry, "SourceMessageId");
                    }

                    auditLogs.Add(auditLog);
                }

                if (auditLogs.Any())
                {
                    context.Set<AuditLog>().AddRange(auditLogs);
                    await context.SaveChangesAsync(cancellationToken);
                }
            }
            finally
            {
                _pendingAuditInfo.Value = null!; // Clear after writing
            }
        }

        private AuditLog? CreateAuditLog(EntityEntry entry, HttpContext? httpContext)
        {
            var entityType = entry.Entity.GetType().Name;
            var entityId = GetEntityId(entry);
            
            if (entityId == 0) return null; // Skip if no ID yet

            var currentUserId = GetCurrentUserId();
            var action = GetAction(entry);

            var auditLog = new AuditLog
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Result = AuditResults.Success,
                UserId = currentUserId,
                UserName = GetCurrentUserName(),
                IPAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
                OldValues = GetOldValues(entry),
                NewValues = GetNewValues(entry),
                Description = $"{action} {entityType} with ID {entityId}",
                Timestamp = DateTime.UtcNow,
                SessionId = httpContext?.Session?.Id,
                RequestUrl = httpContext?.Request?.Path.ToString(),
                HttpMethod = httpContext?.Request?.Method,
                OrganizationId = GetOrganizationId(entry),
                MatterId = GetMatterId(entry)
            };

            // Add AI-related information if applicable
            if (HasProperty(entry, "IsAIGenerated") && entry.CurrentValues["IsAIGenerated"] is bool isAI && isAI)
            {
                auditLog.IsAIAction = true;
                auditLog.AIAgentType = entry.CurrentValues["AIAgentType"]?.ToString();
                auditLog.SourceConversationId = GetPropertyValue<int?>(entry, "SourceConversationId");
                auditLog.SourceMessageId = GetPropertyValue<int?>(entry, "SourceMessageId");
            }

            return auditLog;
        }

        private void SetCreatedByFields(EntityEntry entry, int? userId, DateTime timestamp)
        {
            if (HasProperty(entry, "CreatedById"))
                entry.CurrentValues["CreatedById"] = userId;
            
            if (HasProperty(entry, "CreatedAt"))
                entry.CurrentValues["CreatedAt"] = timestamp;
        }

        private void SetModifiedByFields(EntityEntry entry, int? userId, DateTime timestamp)
        {
            if (HasProperty(entry, "ModifiedById"))
                entry.CurrentValues["ModifiedById"] = userId;
            
            if (HasProperty(entry, "ModifiedAt"))
                entry.CurrentValues["ModifiedAt"] = timestamp;
            
            if (HasProperty(entry, "LastModifiedDate"))
                entry.CurrentValues["LastModifiedDate"] = timestamp;
        }

        private void SetDeletedByFields(EntityEntry entry, int? userId, DateTime timestamp)
        {
            if (HasProperty(entry, "DeletedById"))
                entry.CurrentValues["DeletedById"] = userId;
            
            if (HasProperty(entry, "DeletedAt"))
                entry.CurrentValues["DeletedAt"] = timestamp;
        }

        private int? GetCurrentUserId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                // First, try to get the custom user ID from HttpContext.Items (set by UserSyncMiddleware)
                if (httpContext.Items.TryGetValue("CustomUserId", out var customUserIdObj) 
                    && customUserIdObj is int customUserId)
                {
                    return customUserId;
                }

                // Fallback to claims (for backwards compatibility)
                var userIdClaim = httpContext.User.FindFirst("sub") 
                    ?? httpContext.User.FindFirst("UserId")
                    ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    return userId;
                }
            }
            return null;
        }

        private string? GetCurrentUserName()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                return httpContext.User.Identity.Name;
            }
            return null;
        }

        private bool HasProperty(EntityEntry entry, string propertyName)
        {
            return entry.Properties.Any(p => p.Metadata.Name == propertyName);
        }

        private T? GetPropertyValue<T>(EntityEntry entry, string propertyName)
        {
            if (HasProperty(entry, propertyName))
            {
                var value = entry.CurrentValues[propertyName];
                if (value is T typedValue)
                    return typedValue;
            }
            return default;
        }

        private int GetEntityId(EntityEntry entry)
        {
            var idProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
            if (idProperty?.CurrentValue is int id)
                return id;
            return 0;
        }

        private int? GetOrganizationId(EntityEntry entry)
        {
            if (HasProperty(entry, "OrganizationId"))
                return GetPropertyValue<int?>(entry, "OrganizationId");
            
            if (HasProperty(entry, "OrgId"))
                return GetPropertyValue<int?>(entry, "OrgId");
            
            return null;
        }

        private int? GetMatterId(EntityEntry entry)
        {
            return GetPropertyValue<int?>(entry, "MatterId");
        }

        private string GetAction(EntityEntry entry)
        {
            return entry.State switch
            {
                EntityState.Added => AuditActions.Create,
                EntityState.Modified when HasProperty(entry, "IsDeleted") 
                    && entry.CurrentValues["IsDeleted"] is bool isDeleted && isDeleted 
                    => AuditActions.SoftDelete,
                EntityState.Modified => AuditActions.Update,
                EntityState.Deleted => AuditActions.Delete,
                _ => "Unknown"
            };
        }

        private bool ShouldAudit(object entity)
        {
            // Don't audit AuditLog itself to avoid infinite loops
            if (entity is AuditLog)
                return false;

            // Audit these entity types
            return entity is User
                || entity is Organization
                || entity is Matter
                || entity is TaskItem
                || entity is Document
                || entity is Team
                || entity is MatterAssignment
                || entity is MatterPermission
                || entity is TaskAssignment
                || entity is UserOrganization
                || entity is CalendarEvent;
        }

        private string? GetOldValues(EntityEntry entry)
        {
            if (entry.State == EntityState.Added)
                return null;

            try
            {
                var oldValues = new Dictionary<string, object?>();
                foreach (var property in entry.Properties)
                {
                    if (property.IsModified && property.OriginalValue != null)
                    {
                        oldValues[property.Metadata.Name] = property.OriginalValue;
                    }
                }

                return oldValues.Any() ? JsonSerializer.Serialize(oldValues) : null;
            }
            catch
            {
                return null;
            }
        }

        private string? GetNewValues(EntityEntry entry)
        {
            if (entry.State == EntityState.Deleted)
                return null;

            try
            {
                var newValues = new Dictionary<string, object?>();
                foreach (var property in entry.Properties)
                {
                    if ((entry.State == EntityState.Added || property.IsModified) 
                        && property.CurrentValue != null)
                    {
                        newValues[property.Metadata.Name] = property.CurrentValue;
                    }
                }

                return newValues.Any() ? JsonSerializer.Serialize(newValues) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
