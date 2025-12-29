namespace Certio.Application.DTOs.ChangeControl;

public sealed class ChangeNoticeItemDto
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string Status { get; init; } = "";
    public string Priority { get; init; } = "";
    public string ChangeType { get; init; } = "";

    public DateTime CreatedAt { get; init; }
    public DateTime? SentAt { get; init; }

    public int RecipientCount { get; init; }
    public int PendingRecipientCount { get; init; }
    public int AcknowledgedRecipientCount { get; init; }
    public int NeedsClarificationRecipientCount { get; init; }
}


