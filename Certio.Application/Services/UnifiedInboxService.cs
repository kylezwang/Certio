using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Certio.Application.Interfaces;
using Certio.Domain.UnifiedInbox;
using Certio.Domain.Audit;
using Certio.Infrastructure.Data;

namespace Certio.Application.Services
{
    /// <summary>
    /// Service for managing the unified inbox (read-only in Phase 1).
    /// Aggregates communications from Email, DirectMessage, Chat, and external sources.
    /// </summary>
    public class UnifiedInboxService : IUnifiedInboxService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UnifiedInboxService> _logger;

        public UnifiedInboxService(
            ApplicationDbContext context,
            ILogger<UnifiedInboxService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Query Operations

        public async Task<InboxQueryResult> GetInboxAsync(
            int organizationId,
            InboxQueryOptions? options = null)
        {
            options ??= new InboxQueryOptions();

            var query = _context.InboxItems
                .Where(ii => ii.OrganizationId == organizationId && !ii.IsDeleted);

            // Apply filters
            if (!string.IsNullOrEmpty(options.Status))
                query = query.Where(ii => ii.Status == options.Status);

            if (!string.IsNullOrEmpty(options.Source))
                query = query.Where(ii => ii.Source == options.Source);

            if (options.MatterId.HasValue)
                query = query.Where(ii => ii.MatterId == options.MatterId);

            if (options.UserId.HasValue)
                query = query.Where(ii => ii.UserId == options.UserId);

            if (options.IsFlagged.HasValue)
                query = query.Where(ii => ii.IsFlagged == options.IsFlagged);

            if (options.StartDate.HasValue)
                query = query.Where(ii => ii.LastMessageAt >= options.StartDate);

            if (options.EndDate.HasValue)
                query = query.Where(ii => ii.LastMessageAt <= options.EndDate);

            if (!options.IncludeArchived)
                query = query.Where(ii => ii.Status != InboxItemStatus.Archived);

            // Get total count before pagination
            var totalCount = await query.CountAsync();
            var unreadCount = await query.Where(ii => ii.Status == InboxItemStatus.Unread).CountAsync();

            // Apply ordering
            query = options.OrderBy switch
            {
                "CreatedAt" => options.Descending ? query.OrderByDescending(ii => ii.CreatedAt) : query.OrderBy(ii => ii.CreatedAt),
                "Subject" => options.Descending ? query.OrderByDescending(ii => ii.Subject) : query.OrderBy(ii => ii.Subject),
                _ => options.Descending ? query.OrderByDescending(ii => ii.LastMessageAt) : query.OrderBy(ii => ii.LastMessageAt)
            };

            // Apply pagination
            var items = await query
                .Skip(options.Skip)
                .Take(options.Take)
                .Include(ii => ii.User)
                .ToListAsync();

            return new InboxQueryResult
            {
                Items = items,
                TotalCount = totalCount,
                UnreadCount = unreadCount,
                HasMore = options.Skip + options.Take < totalCount
            };
        }

        public async Task<InboxItem?> GetItemAsync(int itemId)
        {
            return await _context.InboxItems
                .Include(ii => ii.Messages)
                .Include(ii => ii.User)
                .FirstOrDefaultAsync(ii => ii.Id == itemId);
        }

        public async Task<InboxItem?> GetItemByExternalIdAsync(string externalId, int organizationId)
        {
            return await _context.InboxItems
                .FirstOrDefaultAsync(ii => ii.ExternalId == externalId && ii.OrganizationId == organizationId);
        }

        public async Task<InboxItem?> GetItemByThreadIdAsync(string threadId, int organizationId)
        {
            return await _context.InboxItems
                .FirstOrDefaultAsync(ii => ii.ThreadId == threadId && ii.OrganizationId == organizationId);
        }

        public async Task<List<InboxMessage>> GetMessagesAsync(int itemId, int? limit = 50)
        {
            return await _context.InboxMessages
                .Where(im => im.InboxItemId == itemId)
                .OrderByDescending(im => im.CreatedAt)
                .Take(limit ?? 50)
                .Include(im => im.SenderUser)
                .ToListAsync();
        }

        public async Task<InboxMessage?> GetMessageAsync(int messageId)
        {
            return await _context.InboxMessages
                .Include(im => im.SenderUser)
                .FirstOrDefaultAsync(im => im.Id == messageId);
        }

        public async Task<List<InboxItem>> GetMatterInboxAsync(int matterId, int? limit = 50)
        {
            return await _context.InboxItems
                .Where(ii => ii.MatterId == matterId && !ii.IsDeleted)
                .OrderByDescending(ii => ii.LastMessageAt)
                .Take(limit ?? 50)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(int organizationId, int? userId = null)
        {
            var query = _context.InboxItems
                .Where(ii => ii.OrganizationId == organizationId && 
                            !ii.IsDeleted && 
                            ii.Status == InboxItemStatus.Unread);

            if (userId.HasValue)
                query = query.Where(ii => ii.UserId == userId);

            return await query.CountAsync();
        }

        public async Task<InboxQueryResult> SearchAsync(
            int organizationId,
            string searchQuery,
            InboxQueryOptions? options = null)
        {
            options ??= new InboxQueryOptions();
            var normalizedQuery = searchQuery.ToLower();

            var query = _context.InboxItems
                .Where(ii => ii.OrganizationId == organizationId && 
                            !ii.IsDeleted &&
                            (ii.Subject.ToLower().Contains(normalizedQuery) ||
                             (ii.Preview != null && ii.Preview.ToLower().Contains(normalizedQuery)) ||
                             (ii.SenderName != null && ii.SenderName.ToLower().Contains(normalizedQuery))));

            // Apply additional filters
            if (!string.IsNullOrEmpty(options.Source))
                query = query.Where(ii => ii.Source == options.Source);

            if (options.MatterId.HasValue)
                query = query.Where(ii => ii.MatterId == options.MatterId);

            if (!options.IncludeArchived)
                query = query.Where(ii => ii.Status != InboxItemStatus.Archived);

            var totalCount = await query.CountAsync();
            var unreadCount = await query.Where(ii => ii.Status == InboxItemStatus.Unread).CountAsync();

            var items = await query
                .OrderByDescending(ii => ii.LastMessageAt)
                .Skip(options.Skip)
                .Take(options.Take)
                .ToListAsync();

            return new InboxQueryResult
            {
                Items = items,
                TotalCount = totalCount,
                UnreadCount = unreadCount,
                HasMore = options.Skip + options.Take < totalCount
            };
        }

        #endregion

        #region Status Operations

        public async Task<bool> MarkAsReadAsync(int itemId, int userId)
        {
            try
            {
                var item = await _context.InboxItems.FindAsync(itemId);
                if (item == null) return false;

                item.MarkAsRead();
                
                // Mark all messages as read
                var messages = await _context.InboxMessages
                    .Where(im => im.InboxItemId == itemId && !im.IsRead)
                    .ToListAsync();
                    
                foreach (var message in messages)
                {
                    message.IsRead = true;
                    message.ReadAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await LogAuditEventAsync(item, AuditActions.InboxItemRead, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking inbox item {ItemId} as read", itemId);
                return false;
            }
        }

        public async Task<int> MarkMultipleAsReadAsync(IEnumerable<int> itemIds, int userId)
        {
            var count = 0;
            foreach (var itemId in itemIds)
            {
                if (await MarkAsReadAsync(itemId, userId))
                    count++;
            }
            return count;
        }

        public async Task<bool> MarkAsUnreadAsync(int itemId, int userId)
        {
            try
            {
                var item = await _context.InboxItems.FindAsync(itemId);
                if (item == null) return false;

                item.Status = InboxItemStatus.Unread;
                item.ReadAt = null;
                item.UnreadCount = item.MessageCount;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking inbox item {ItemId} as unread", itemId);
                return false;
            }
        }

        public async Task<bool> ArchiveAsync(int itemId, int userId)
        {
            try
            {
                var item = await _context.InboxItems.FindAsync(itemId);
                if (item == null) return false;

                item.Archive();
                await _context.SaveChangesAsync();
                await LogAuditEventAsync(item, AuditActions.InboxItemArchived, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving inbox item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> UnarchiveAsync(int itemId, int userId)
        {
            try
            {
                var item = await _context.InboxItems.FindAsync(itemId);
                if (item == null) return false;

                item.Status = item.UnreadCount > 0 ? InboxItemStatus.Unread : InboxItemStatus.Read;
                item.ArchivedAt = null;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unarchiving inbox item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> ToggleFlagAsync(int itemId, int userId)
        {
            try
            {
                var item = await _context.InboxItems.FindAsync(itemId);
                if (item == null) return false;

                item.IsFlagged = !item.IsFlagged;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling flag on inbox item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> AddLabelsAsync(int itemId, IEnumerable<string> labels, int userId)
        {
            try
            {
                var item = await _context.InboxItems.FindAsync(itemId);
                if (item == null) return false;

                foreach (var label in labels)
                {
                    if (!item.Labels.Contains(label))
                        item.Labels.Add(label);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding labels to inbox item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> RemoveLabelsAsync(int itemId, IEnumerable<string> labels, int userId)
        {
            try
            {
                var item = await _context.InboxItems.FindAsync(itemId);
                if (item == null) return false;

                foreach (var label in labels)
                {
                    item.Labels.Remove(label);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing labels from inbox item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> SnoozeAsync(int itemId, DateTime snoozeUntil, int userId)
        {
            try
            {
                var item = await _context.InboxItems.FindAsync(itemId);
                if (item == null) return false;

                item.Snooze(snoozeUntil);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error snoozing inbox item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> LinkToMatterAsync(int itemId, int matterId, int userId)
        {
            try
            {
                var item = await _context.InboxItems.FindAsync(itemId);
                if (item == null) return false;

                var matter = await _context.Matters.FindAsync(matterId);
                if (matter == null) return false;

                item.MatterId = matterId;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking inbox item {ItemId} to matter {MatterId}", itemId, matterId);
                return false;
            }
        }

        public async Task<bool> UnlinkFromMatterAsync(int itemId, int userId)
        {
            try
            {
                var item = await _context.InboxItems.FindAsync(itemId);
                if (item == null) return false;

                item.MatterId = null;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlinking inbox item {ItemId} from matter", itemId);
                return false;
            }
        }

        #endregion

        #region Ingestion Operations

        public async Task<InboxItem> IngestItemAsync(InboxIngestionRequest request)
        {
            try
            {
                // Check for existing item by external ID or thread ID
                InboxItem? existingItem = null;
                
                if (!string.IsNullOrEmpty(request.ExternalId))
                    existingItem = await GetItemByExternalIdAsync(request.ExternalId, request.OrganizationId);
                
                if (existingItem == null && !string.IsNullOrEmpty(request.ThreadId))
                    existingItem = await GetItemByThreadIdAsync(request.ThreadId, request.OrganizationId);

                if (existingItem != null)
                {
                    // Update existing item
                    existingItem.LastMessageAt = request.ReceivedAt ?? DateTime.UtcNow;
                    existingItem.MessageCount++;
                    existingItem.UnreadCount++;
                    existingItem.Preview = request.Preview;
                    existingItem.HasAttachments = existingItem.HasAttachments || request.HasAttachments;
                    existingItem.Status = InboxItemStatus.Unread;
                    existingItem.LastSyncedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated existing inbox item {ItemId} from {Source}", existingItem.Id, request.Source);
                    
                    return existingItem;
                }

                // Create new item
                var item = new InboxItem
                {
                    OrganizationId = request.OrganizationId,
                    MatterId = request.MatterId,
                    Source = request.Source,
                    ThreadId = request.ThreadId,
                    ExternalId = request.ExternalId,
                    Subject = request.Subject,
                    Preview = request.Preview,
                    Status = InboxItemStatus.Unread,
                    SenderName = request.SenderName,
                    SenderId = request.SenderId,
                    Recipients = request.Recipients,
                    ContactId = request.ContactId,
                    UserId = request.UserId,
                    HasAttachments = request.HasAttachments,
                    CreatedAt = DateTime.UtcNow,
                    LastMessageAt = request.ReceivedAt ?? DateTime.UtcNow,
                    LastSyncedAt = DateTime.UtcNow
                };

                _context.InboxItems.Add(item);
                await _context.SaveChangesAsync();
                await LogAuditEventAsync(item, AuditActions.InboxItemCreated, null);

                _logger.LogInformation("Created new inbox item {ItemId} from {Source}", item.Id, request.Source);
                
                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting inbox item from {Source}", request.Source);
                throw;
            }
        }

        public async Task<InboxMessage> IngestMessageAsync(int itemId, InboxMessageIngestionRequest request)
        {
            try
            {
                var item = await _context.InboxItems.FindAsync(itemId);
                if (item == null)
                    throw new ArgumentException($"Inbox item {itemId} not found");

                // Check for duplicate message
                if (!string.IsNullOrEmpty(request.ExternalMessageId))
                {
                    var existingMessage = await _context.InboxMessages
                        .FirstOrDefaultAsync(im => im.ExternalMessageId == request.ExternalMessageId);
                    
                    if (existingMessage != null)
                    {
                        _logger.LogDebug("Skipping duplicate message with ExternalMessageId {ExtId}", request.ExternalMessageId);
                        return existingMessage;
                    }
                }

                var message = new InboxMessage
                {
                    InboxItemId = itemId,
                    ExternalMessageId = request.ExternalMessageId,
                    Content = request.Content,
                    ContentPlainText = request.ContentPlainText,
                    Direction = request.Direction,
                    SenderName = request.SenderName,
                    SenderEmail = request.SenderEmail,
                    SenderUserId = request.SenderUserId,
                    ToRecipients = request.ToRecipients,
                    CcRecipients = request.CcRecipients,
                    HasAttachments = request.HasAttachments,
                    AttachmentsJson = request.AttachmentsJson,
                    Importance = request.Importance,
                    HeadersJson = request.HeadersJson,
                    SentAt = request.SentAt,
                    ReceivedAt = request.ReceivedAt,
                    CreatedAt = DateTime.UtcNow
                };

                _context.InboxMessages.Add(message);

                // Update parent item
                item.MessageCount++;
                if (request.Direction == MessageDirection.Inbound)
                {
                    item.UnreadCount++;
                    item.Status = InboxItemStatus.Unread;
                }
                item.LastMessageAt = request.ReceivedAt ?? DateTime.UtcNow;
                item.Preview = GetPreview(request.ContentPlainText ?? request.Content);
                item.HasAttachments = item.HasAttachments || request.HasAttachments;

                await _context.SaveChangesAsync();

                _logger.LogDebug("Ingested message {MessageId} into inbox item {ItemId}", message.Id, itemId);
                
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting message into inbox item {ItemId}", itemId);
                throw;
            }
        }

        public async Task<InboxSyncResult> SyncFromExternalSourceAsync(
            int organizationId,
            string source,
            string? cursor = null)
        {
            // This method would be implemented to sync from external sources
            // For now, return a placeholder result
            _logger.LogInformation("Sync from {Source} for org {OrgId} requested (placeholder)", source, organizationId);
            
            return new InboxSyncResult
            {
                Success = true,
                NewItemsCount = 0,
                UpdatedItemsCount = 0,
                NewMessagesCount = 0,
                NewCursor = cursor
            };
        }

        #endregion

        #region Statistics

        public async Task<InboxStats> GetStatsAsync(int organizationId)
        {
            var items = await _context.InboxItems
                .Where(ii => ii.OrganizationId == organizationId && !ii.IsDeleted)
                .ToListAsync();

            var messageCount = await _context.InboxMessages
                .Where(im => im.InboxItem.OrganizationId == organizationId && !im.InboxItem.IsDeleted)
                .CountAsync();

            return new InboxStats
            {
                TotalItems = items.Count,
                UnreadItems = items.Count(ii => ii.Status == InboxItemStatus.Unread),
                ArchivedItems = items.Count(ii => ii.Status == InboxItemStatus.Archived),
                FlaggedItems = items.Count(ii => ii.IsFlagged),
                ItemsBySource = items.GroupBy(ii => ii.Source).ToDictionary(g => g.Key, g => g.Count()),
                ItemsByStatus = items.GroupBy(ii => ii.Status).ToDictionary(g => g.Key, g => g.Count()),
                TotalMessages = messageCount
            };
        }

        #endregion

        #region Private Helpers

        private string? GetPreview(string? content, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(content))
                return null;

            // Strip HTML tags if present
            var text = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]+>", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
        }

        private async Task LogAuditEventAsync(InboxItem item, string auditAction, int? userId)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    EntityType = "InboxItem",
                    EntityId = item.Id,
                    Action = auditAction,
                    Result = AuditResults.Success,
                    UserId = userId,
                    OrganizationId = item.OrganizationId,
                    MatterId = item.MatterId,
                    Description = $"{auditAction}: {item.Subject}",
                    Timestamp = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log audit event for inbox item {ItemId}", item.Id);
            }
        }

        #endregion
    }
}

