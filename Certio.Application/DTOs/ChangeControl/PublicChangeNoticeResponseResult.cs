namespace Certio.Application.DTOs.ChangeControl;

public sealed class PublicChangeNoticeResponseResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";

    public int? ChangeNoticeId { get; init; }
    public int? RecipientId { get; init; }
    public string? Action { get; init; }
    public string? NewRecipientStatus { get; init; }
    public string? NewNoticeStatus { get; init; }
}


