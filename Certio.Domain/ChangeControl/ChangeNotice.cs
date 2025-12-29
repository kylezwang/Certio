using System.ComponentModel.DataAnnotations;
using Certio.Domain.Matters;
using Certio.Domain.Organizations;
using Certio.Domain.Users;

namespace Certio.Domain.ChangeControl;

public static class ChangeNoticeStatuses
{
    public const string Draft = "Draft";
    public const string Sent = "Sent";
    public const string PartiallyAcknowledged = "PartiallyAcknowledged";
    public const string Acknowledged = "Acknowledged";
    public const string NeedsClarification = "NeedsClarification";
}

public static class ChangeNoticeRecipientStatuses
{
    public const string Pending = "Pending";
    public const string Acknowledged = "Acknowledged";
    public const string NeedsClarification = "NeedsClarification";
}

public class ChangeNotice
{
    public int Id { get; set; }

    [Required]
    public int MatterId { get; set; }

    [Required]
    public int OrganizationId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(50)]
    public string ChangeType { get; set; } = "General";

    [Required]
    [StringLength(20)]
    public string Priority { get; set; } = "Medium";

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = ChangeNoticeStatuses.Draft;

    public DateTime? AcknowledgementDueDate { get; set; }

    public DateTime? SentAt { get; set; }
    public DateTime? LastResentAt { get; set; }
    public int SendCount { get; set; } = 0;

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public int? ModifiedById { get; set; }

    // Soft delete fields
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    // Navigation properties
    public virtual Matter Matter { get; set; } = null!;
    public virtual Organization? Organization { get; set; }
    public virtual User? CreatedBy { get; set; }
    public virtual User? ModifiedBy { get; set; }
    public virtual User? DeletedBy { get; set; }

    public virtual ICollection<ChangeNoticeRecipient> Recipients { get; set; } = new List<ChangeNoticeRecipient>();
}



