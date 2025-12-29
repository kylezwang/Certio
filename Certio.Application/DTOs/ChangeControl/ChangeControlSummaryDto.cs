namespace Certio.Application.DTOs.ChangeControl;

public sealed class ChangeControlSummaryDto
{
    public int OrganizationId { get; init; }
    public int MatterId { get; init; }

    public int DraftCount { get; init; }
    public int PendingCount { get; init; }
    public int NeedsClarificationCount { get; init; }
    public int AcknowledgedCount { get; init; }

    public List<ChangeNoticeItemDto> Notices { get; init; } = new();
}


