using System.ComponentModel.DataAnnotations;

namespace Certio.Application.DTOs.ChangeControl;

public sealed class CreateChangeNoticeRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; init; } = "";

    [StringLength(2000)]
    public string? Description { get; init; }

    [StringLength(50)]
    public string ChangeType { get; init; } = "General";

    [StringLength(20)]
    public string Priority { get; init; } = "Medium";

    public DateTime? AcknowledgementDueDate { get; init; }

    /// <summary>
    /// One email per line or comma-separated. Will be parsed/normalized server-side.
    /// </summary>
    [Required]
    public string RecipientEmails { get; init; } = "";
}


