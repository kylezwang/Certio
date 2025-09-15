using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Projects;

namespace Certio.Domain.Services;

public class ChatMessage
{
    public int Id { get; set; }
    
    public string ConversationId { get; set; } = "";
    public string? UserId { get; set; }
    
    [Required]
    public string Content { get; set; } = "";
    
    [StringLength(50)]
    public string Sender { get; set; } = ""; // User name or "AI Assistant"
    
    [StringLength(20)]
    public string MessageType { get; set; } = "Text"; // Text, System, AI, File, Action
    
    [StringLength(20)]
    public string SenderType { get; set; } = "User"; // User, AI, System
    
    public bool IsFromUser { get; set; } = true;
    public bool IsInternal { get; set; } = false; // Internal notes vs client-visible messages
    
    // Additional properties for AI Agent Service compatibility
    [StringLength(20)]
    public string UserType { get; set; } = "Client"; // Client, Certio, Admin, etc.
    
    public bool IsFromAI { get; set; } = false;
    
    [StringLength(50)]
    public string? AIAgentType { get; set; } // ChatSummarizer, ClientGoalExtractor, etc.
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
    
    public int? ParentMessageId { get; set; }
    public int? ReplyToMessageId { get; set; }
    
    public string? Metadata { get; set; } // JSON string for additional data
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDate { get; set; }
    
    // Navigation properties
    public virtual Conversation Conversation { get; set; } = null!;
    public virtual User? User { get; set; }
    public virtual ChatMessage? ParentMessage { get; set; }
    public virtual ChatMessage? ReplyToMessage { get; set; }
    public virtual ICollection<ChatMessage> Replies { get; set; } = new List<ChatMessage>();
    public virtual ICollection<ChatMessage> ChildMessages { get; set; } = new List<ChatMessage>();
}


public class ConversationParticipant
{
    public int Id { get; set; }
    
    public int ConversationId { get; set; }
    public int UserId { get; set; }
    
    [StringLength(20)]
    public string Role { get; set; } = "Participant"; // Owner, Moderator, Participant, Observer
    
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual Conversation Conversation { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
