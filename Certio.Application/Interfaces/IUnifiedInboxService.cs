using Certio.Domain.UnifiedInbox;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service for managing the unified inbox (read-only in.
    /// Aggregates communications from Email, DirectMessage, Chat, and external sources.
    /// </summary>
    public interface IUnifiedInboxService
    {
        #region Query Operations
        
        /// <summary>
        /// Get inbox items for an organization
        /// </summary>
        Task<InboxQueryResult> GetInboxAsync(
            int organizationId,
            InboxQueryOptions? options = null);
        
        /// <summary>
        /// Get a specific inbox item by ID
        /// </summary>
        Task<InboxItem?> GetItemAsync(int itemId);
        
        /// <summary>
        /// Get inbox item by external ID
        /// </summary>
        Task<InboxItem?> GetItemByExternalIdAsync(string externalId, int organizationId);
        
        /// <summary>
        /// Get inbox item by thread ID
        /// </summary>
        Task<InboxItem?> GetItemByThreadIdAsync(string threadId, int organizationId);
        
        /// <summary>
        /// Get messages for an inbox item
        /// </summary>
        Task<List<InboxMessage>> GetMessagesAsync(int itemId, int? limit = 50);
        
        /// <summary>
        /// Get a specific message by ID
        /// </summary>
        Task<InboxMessage?> GetMessageAsync(int messageId);
        
        /// <summary>
        /// Get inbox items for a specific matter
        /// </summary>
        Task<List<InboxItem>> GetMatterInboxAsync(int matterId, int? limit = 50);
        
        /// <summary>
        /// Get unread count for an organization
        /// </summary>
        Task<int> GetUnreadCountAsync(int organizationId, int? userId = null);
        
        /// <summary>
        /// Search inbox items
        /// </summary>
        Task<InboxQueryResult> SearchAsync(
            int organizationId,
            string query,
            InboxQueryOptions? options = null);
        
        #endregion
        
        #region Status Operations (Read-only actions)
        
        /// <summary>
        /// Mark an inbox item as read
        /// </summary>
        Task<bool> MarkAsReadAsync(int itemId, int userId);
        
        /// <summary>
        /// Mark multiple items as read
        /// </summary>
        Task<int> MarkMultipleAsReadAsync(IEnumerable<int> itemIds, int userId);
        
        /// <summary>
        /// Mark an inbox item as unread
        /// </summary>
        Task<bool> MarkAsUnreadAsync(int itemId, int userId);
        
        /// <summary>
        /// Archive an inbox item
        /// </summary>
        Task<bool> ArchiveAsync(int itemId, int userId);
        
        /// <summary>
        /// Unarchive an inbox item
        /// </summary>
        Task<bool> UnarchiveAsync(int itemId, int userId);
        
        /// <summary>
        /// Toggle star/flag on an inbox item
        /// </summary>
        Task<bool> ToggleFlagAsync(int itemId, int userId);
        
        /// <summary>
        /// Add labels to an inbox item
        /// </summary>
        Task<bool> AddLabelsAsync(int itemId, IEnumerable<string> labels, int userId);
        
        /// <summary>
        /// Remove labels from an inbox item
        /// </summary>
        Task<bool> RemoveLabelsAsync(int itemId, IEnumerable<string> labels, int userId);
        
        /// <summary>
        /// Snooze an inbox item until a specified time
        /// </summary>
        Task<bool> SnoozeAsync(int itemId, DateTime snoozeUntil, int userId);
        
        /// <summary>
        /// Link an inbox item to a matter
        /// </summary>
        Task<bool> LinkToMatterAsync(int itemId, int matterId, int userId);
        
        /// <summary>
        /// Unlink an inbox item from a matter
        /// </summary>
        Task<bool> UnlinkFromMatterAsync(int itemId, int userId);
        
        #endregion
        
        #region Ingestion Operations
        
        /// <summary>
        /// Ingest an item from an external source (Email, DM, etc.)
        /// </summary>
        Task<InboxItem> IngestItemAsync(InboxIngestionRequest request);
        
        /// <summary>
        /// Ingest a message into an existing inbox item
        /// </summary>
        Task<InboxMessage> IngestMessageAsync(int itemId, InboxMessageIngestionRequest request);
        
        /// <summary>
        /// Sync inbox items from external sources
        /// </summary>
        Task<InboxSyncResult> SyncFromExternalSourceAsync(
            int organizationId,
            string source,
            string? cursor = null);
        
        #endregion
        
        #region Statistics
        
        /// <summary>
        /// Get inbox statistics for an organization
        /// </summary>
        Task<InboxStats> GetStatsAsync(int organizationId);
        
        #endregion
    }
    
    /// <summary>
    /// Query options for inbox retrieval
    /// </summary>
    public class InboxQueryOptions
    {
        public string? Status { get; set; }
        public string? Source { get; set; }
        public int? MatterId { get; set; }
        public int? UserId { get; set; }
        public bool? IsFlagged { get; set; }
        public List<string>? Labels { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
        public string OrderBy { get; set; } = "LastMessageAt";
        public bool Descending { get; set; } = true;
        public bool IncludeArchived { get; set; } = false;
    }
    
    /// <summary>
    /// Result of an inbox query
    /// </summary>
    public class InboxQueryResult
    {
        public List<InboxItem> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public bool HasMore { get; set; }
    }
    
    /// <summary>
    /// Request for ingesting an inbox item
    /// </summary>
    public class InboxIngestionRequest
    {
        public int OrganizationId { get; set; }
        public int? MatterId { get; set; }
        public string Source { get; set; } = "Email";
        public string? ThreadId { get; set; }
        public string? ExternalId { get; set; }
        public string Subject { get; set; } = "";
        public string? Preview { get; set; }
        public string? SenderName { get; set; }
        public string? SenderId { get; set; }
        public string? Recipients { get; set; }
        public int? ContactId { get; set; }
        public int? UserId { get; set; }
        public bool HasAttachments { get; set; }
        public DateTime? ReceivedAt { get; set; }
    }
    
    /// <summary>
    /// Request for ingesting a message into an inbox item
    /// </summary>
    public class InboxMessageIngestionRequest
    {
        public string? ExternalMessageId { get; set; }
        public string? Content { get; set; }
        public string? ContentPlainText { get; set; }
        public string Direction { get; set; } = "Inbound";
        public string? SenderName { get; set; }
        public string? SenderEmail { get; set; }
        public int? SenderUserId { get; set; }
        public string? ToRecipients { get; set; }
        public string? CcRecipients { get; set; }
        public bool HasAttachments { get; set; }
        public string? AttachmentsJson { get; set; }
        public string Importance { get; set; } = "Normal";
        public string? HeadersJson { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? ReceivedAt { get; set; }
    }
    
    /// <summary>
    /// Result of an inbox sync operation
    /// </summary>
    public class InboxSyncResult
    {
        public bool Success { get; set; }
        public int NewItemsCount { get; set; }
        public int UpdatedItemsCount { get; set; }
        public int NewMessagesCount { get; set; }
        public string? NewCursor { get; set; }
        public string? ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Inbox statistics
    /// </summary>
    public class InboxStats
    {
        public int TotalItems { get; set; }
        public int UnreadItems { get; set; }
        public int ArchivedItems { get; set; }
        public int FlaggedItems { get; set; }
        public Dictionary<string, int> ItemsBySource { get; set; } = new();
        public Dictionary<string, int> ItemsByStatus { get; set; } = new();
        public int TotalMessages { get; set; }
    }
}

