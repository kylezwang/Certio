using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Domain.Matters;

namespace Certio.Domain.UnifiedInbox
{
    /// <summary>
    /// Represents a unified inbox item aggregating communications from various sources.
    /// Phase 1: Read-only ingestion and display. Phase 2: Sending capabilities.
    /// </summary>
    public class InboxItem
    {
        public int Id { get; set; }
        
        [Required]
        public int OrganizationId { get; set; }
        
        /// <summary>
        /// Optional link to a specific matter
        /// </summary>
        public int? MatterId { get; set; }
        
        /// <summary>
        /// Source system: Email, DirectMessage, Chat, External, Calendar
        /// </summary>
        [Required]
        [StringLength(30)]
        public string Source { get; set; } = InboxSources.Email;
        
        /// <summary>
        /// Thread/conversation identifier for grouping related messages
        /// </summary>
        [StringLength(200)]
        public string? ThreadId { get; set; }
        
        /// <summary>
        /// External reference ID from source system
        /// </summary>
        [StringLength(200)]
        public string? ExternalId { get; set; }
        
        /// <summary>
        /// Conversation subject/title
        /// </summary>
        [Required]
        [StringLength(500)]
        public string Subject { get; set; } = "";
        
        /// <summary>
        /// Preview of the latest message content
        /// </summary>
        [StringLength(500)]
        public string? Preview { get; set; }
        
        /// <summary>
        /// Item status: Unread, Read, Archived, Starred
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = InboxItemStatus.Unread;
        
        /// <summary>
        /// Priority: High, Normal, Low
        /// </summary>
        [StringLength(20)]
        public string Priority { get; set; } = "Normal";
        
        /// <summary>
        /// Whether this item has been flagged for follow-up
        /// </summary>
        public bool IsFlagged { get; set; } = false;
        
        /// <summary>
        /// Labels/tags for categorization
        /// </summary>
        public List<string> Labels { get; set; } = new();
        
        /// <summary>
        /// Number of messages in the thread
        /// </summary>
        public int MessageCount { get; set; } = 1;
        
        /// <summary>
        /// Number of unread messages in the thread
        /// </summary>
        public int UnreadCount { get; set; } = 1;
        
        /// <summary>
        /// Whether there are attachments in the thread
        /// </summary>
        public bool HasAttachments { get; set; } = false;
        
        /// <summary>
        /// Sender information (display name)
        /// </summary>
        [StringLength(200)]
        public string? SenderName { get; set; }
        
        /// <summary>
        /// Sender identifier (email, user ID, etc.)
        /// </summary>
        [StringLength(200)]
        public string? SenderId { get; set; }
        
        /// <summary>
        /// Recipients display (comma-separated)
        /// </summary>
        [StringLength(500)]
        public string? Recipients { get; set; }
        
        /// <summary>
        /// Associated contact ID if sender is a known contact
        /// </summary>
        public int? ContactId { get; set; }
        
        /// <summary>
        /// Associated user ID if sender is a system user
        /// </summary>
        public int? UserId { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public DateTime? SnoozedUntil { get; set; }
        
        // Soft delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        
        // Sync tracking
        public DateTime? LastSyncedAt { get; set; }
        
        [StringLength(100)]
        public string? SyncCursor { get; set; }
        
        // Navigation properties
        public virtual Organization? Organization { get; set; }
        public virtual Matter? Matter { get; set; }
        public virtual User? User { get; set; }
        public virtual ICollection<InboxMessage> Messages { get; set; } = new List<InboxMessage>();
        
        // Helper methods
        public void MarkAsRead()
        {
            if (Status == InboxItemStatus.Unread)
            {
                Status = InboxItemStatus.Read;
                ReadAt = DateTime.UtcNow;
                UnreadCount = 0;
            }
        }
        
        public void Archive()
        {
            Status = InboxItemStatus.Archived;
            ArchivedAt = DateTime.UtcNow;
        }
        
        public void Snooze(DateTime until)
        {
            Status = InboxItemStatus.Snoozed;
            SnoozedUntil = until;
        }
    }
    
    /// <summary>
    /// Individual message within an inbox thread
    /// </summary>
    public class InboxMessage
    {
        public int Id { get; set; }
        
        [Required]
        public int InboxItemId { get; set; }
        
        /// <summary>
        /// External message ID from source system
        /// </summary>
        [StringLength(200)]
        public string? ExternalMessageId { get; set; }
        
        /// <summary>
        /// Message content (HTML or plain text)
        /// </summary>
        public string? Content { get; set; }
        
        /// <summary>
        /// Plain text version of content
        /// </summary>
        public string? ContentPlainText { get; set; }
        
        /// <summary>
        /// Direction: Inbound, Outbound
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Direction { get; set; } = MessageDirection.Inbound;
        
        /// <summary>
        /// Whether this message has been read
        /// </summary>
        public bool IsRead { get; set; } = false;
        
        /// <summary>
        /// Sender information
        /// </summary>
        [StringLength(200)]
        public string? SenderName { get; set; }
        
        [StringLength(200)]
        public string? SenderEmail { get; set; }
        
        public int? SenderUserId { get; set; }
        
        /// <summary>
        /// Recipients (To field)
        /// </summary>
        [StringLength(1000)]
        public string? ToRecipients { get; set; }
        
        /// <summary>
        /// CC recipients
        /// </summary>
        [StringLength(1000)]
        public string? CcRecipients { get; set; }
        
        /// <summary>
        /// BCC recipients (only for outbound)
        /// </summary>
        [StringLength(1000)]
        public string? BccRecipients { get; set; }
        
        /// <summary>
        /// Whether this message has attachments
        /// </summary>
        public bool HasAttachments { get; set; } = false;
        
        /// <summary>
        /// JSON array of attachment metadata
        /// </summary>
        public string? AttachmentsJson { get; set; }
        
        /// <summary>
        /// Importance: High, Normal, Low
        /// </summary>
        [StringLength(20)]
        public string Importance { get; set; } = "Normal";
        
        /// <summary>
        /// Message headers (JSON) for email
        /// </summary>
        public string? HeadersJson { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When the message was sent (from source system)
        /// </summary>
        public DateTime? SentAt { get; set; }
        
        /// <summary>
        /// When the message was received
        /// </summary>
        public DateTime? ReceivedAt { get; set; }
        
        public DateTime? ReadAt { get; set; }
        
        // Navigation properties
        public virtual InboxItem InboxItem { get; set; } = null!;
        public virtual User? SenderUser { get; set; }
    }
    
    /// <summary>
    /// Inbox sources for unified communications
    /// </summary>
    public static class InboxSources
    {
        public const string Email = "Email";
        public const string DirectMessage = "DirectMessage";
        public const string Chat = "Chat";
        public const string External = "External";
        public const string Calendar = "Calendar";
        
        public static readonly string[] All = { Email, DirectMessage, Chat, External, Calendar };
        
        public static bool IsValid(string source) => All.Contains(source);
    }
    
    /// <summary>
    /// Inbox item status states
    /// </summary>
    public static class InboxItemStatus
    {
        public const string Unread = "Unread";
        public const string Read = "Read";
        public const string Archived = "Archived";
        public const string Starred = "Starred";
        public const string Snoozed = "Snoozed";
        
        public static readonly string[] All = { Unread, Read, Archived, Starred, Snoozed };
        
        public static bool IsValid(string status) => All.Contains(status);
    }
    
    /// <summary>
    /// Message direction
    /// </summary>
    public static class MessageDirection
    {
        public const string Inbound = "Inbound";
        public const string Outbound = "Outbound";
    }
}

