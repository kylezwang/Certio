using Certio.Domain.Audit;
using Certio.Domain.Matters;
using Certio.Domain.Organizations;
using Certio.Domain.Users;

namespace Certio.Domain.Billing
{
    public class TimeEntry : AuditableEntity
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int? MatterId { get; set; }
        public int? ClientId { get; set; }
        public int AssigneeId { get; set; }
        
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public decimal Hours { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        
        public TimeEntryStatus Status { get; set; } = TimeEntryStatus.NeedsReview;
        public bool IsBillable { get; set; } = true;
        
        // Navigation properties
        public virtual Organization? Organization { get; set; }
        public virtual Matter? Matter { get; set; }
        public virtual Organization? Client { get; set; }
        public virtual User? Assignee { get; set; }
        public virtual User? CreatedBy { get; set; }
        public virtual User? ModifiedBy { get; set; }
        public virtual User? DeletedBy { get; set; }
    }
    
    public enum TimeEntryStatus
    {
        NeedsReview,
        Approved,
        Billed,
        Paid
    }
}
