namespace Certio.Application.DTOs
{
    public class AuditLogDTO
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = "";
        public int EntityId { get; set; }
        public string Action { get; set; } = "";
        public string? Result { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string? IPAddress { get; set; }
        public string? Description { get; set; }
        public DateTime Timestamp { get; set; }
        
        // AI-related fields
        public bool IsAIAction { get; set; }
        public string? AIAgentType { get; set; }
        public int? SourceConversationId { get; set; }
        public int? SourceMessageId { get; set; }
        
        // Entity details for display
        public string? EntityTitle { get; set; }
        public string? ApprovalStatus { get; set; }
        public bool NeedsReview { get; set; }
    }

    public class AuditSummaryDTO
    {
        public int TotalActions { get; set; }
        public int TotalUsers { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int DeletedCount { get; set; }
        public int AIGeneratedCount { get; set; }
        public int AIApprovedCount { get; set; }
        public int AIPendingCount { get; set; }
        public Dictionary<string, int> ActionsByType { get; set; } = new();
        public Dictionary<string, int> ActionsByEntity { get; set; } = new();
        public List<TopUserActivityDTO> TopUsers { get; set; } = new();
    }

    public class TopUserActivityDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public int ActionCount { get; set; }
    }

    public class AIContentReviewDto
    {
        public string EntityType { get; set; } = "";
        public int EntityId { get; set; }
        public string EntityTitle { get; set; } = "";
        public string? AIAgentType { get; set; }
        public int? SourceConversationId { get; set; }
        public int? SourceMessageId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

