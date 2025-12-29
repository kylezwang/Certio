namespace Certio.Application.DTOs.ChangeControl;

public sealed class ChangeNoticeDetailDto
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string Status { get; init; } = "";
    public string Priority { get; init; } = "";
    public string ChangeType { get; init; } = "";
    public DateTime? AcknowledgementDueDate { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? SentAt { get; init; }
    public int SendCount { get; init; }

    public int RecipientCount { get; init; }
    public int PendingRecipientCount { get; init; }
    public int AcknowledgedRecipientCount { get; init; }
    public int NeedsClarificationRecipientCount { get; init; }

    public List<ChangeNoticeRecipientDto> Recipients { get; init; } = new();
}

public sealed class ChangeNoticeRecipientDto
{
    public int Id { get; init; }
    public string Email { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime? RespondedAt { get; init; }
    public string? ClarificationNote { get; init; }
}

