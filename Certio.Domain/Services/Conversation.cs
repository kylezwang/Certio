namespace Certio.Domain.Services;

public class Conversation
{
    public int Id { get; set; }
    public string TenantId { get; set; } = "default";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "Active"; // Active, Closed, Archived
    public string? ClientId { get; set; }
    public string? CertioId { get; set; }
    public string? LawyerId { get; set; }
    public string? BusinessId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }
    public string? AI_Summary { get; set; }
    public string? Client_Goals { get; set; }
    public string? Priority { get; set; } = "Medium"; // Low, Medium, High, Urgent
    public string? Category { get; set; } = "General"; // Legal, Business, Technical, etc.
}
