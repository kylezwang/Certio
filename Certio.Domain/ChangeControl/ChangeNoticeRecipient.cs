using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.ChangeControl;

public class ChangeNoticeRecipient
{
    public int Id { get; set; }

    [Required]
    public int ChangeNoticeId { get; set; }

    [Required]
    [StringLength(320)]
    public string Email { get; set; } = "";

    [StringLength(200)]
    public string? DisplayName { get; set; }

    // Optional link to an existing user if the email matches a registered user
    public int? UserId { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = ChangeNoticeRecipientStatuses.Pending;

    public DateTime? RespondedAt { get; set; }

    [StringLength(2000)]
    public string? ClarificationNote { get; set; }

    // Allows invalidating old links on resend if needed
    public int TokenVersion { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ChangeNotice ChangeNotice { get; set; } = null!;
    public virtual User? User { get; set; }
}



