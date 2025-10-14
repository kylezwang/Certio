namespace Certio.Domain.Audit
{
    /// <summary>
    /// Base interface for all auditable entities
    /// </summary>
    public interface IAuditable
    {
        DateTime CreatedAt { get; set; }
        int? CreatedById { get; set; }
        DateTime? ModifiedAt { get; set; }
        int? ModifiedById { get; set; }
    }

    /// <summary>
    /// Interface for entities that support soft delete
    /// </summary>
    public interface ISoftDeletable
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
        int? DeletedById { get; set; }
    }

    /// <summary>
    /// Interface for AI-generated content
    /// </summary>
    public interface IAIGenerated
    {
        bool IsAIGenerated { get; set; }
        string? AIAgentType { get; set; }
        string? AIGenerationMetadata { get; set; }
        int? SourceConversationId { get; set; }
        int? SourceMessageId { get; set; }
    }

    /// <summary>
    /// Interface for entities requiring approval
    /// </summary>
    public interface IApprovable
    {
        string? ApprovalStatus { get; set; } // Pending, Approved, Rejected
        int? ApprovedById { get; set; }
        DateTime? ApprovedAt { get; set; }
        string? ApprovalNotes { get; set; }
    }

    /// <summary>
    /// Base abstract class for auditable entities
    /// </summary>
    public abstract class AuditableEntity : IAuditable, ISoftDeletable
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedById { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public int? ModifiedById { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedById { get; set; }
    }

    /// <summary>
    /// Base class for AI-generated auditable entities
    /// </summary>
    public abstract class AIAuditableEntity : AuditableEntity, IAIGenerated, IApprovable
    {
        public bool IsAIGenerated { get; set; } = false;
        public string? AIAgentType { get; set; }
        public string? AIGenerationMetadata { get; set; }
        public int? SourceConversationId { get; set; }
        public int? SourceMessageId { get; set; }
        public string? ApprovalStatus { get; set; } // Pending, Approved, Rejected
        public int? ApprovedById { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovalNotes { get; set; }
    }
}

