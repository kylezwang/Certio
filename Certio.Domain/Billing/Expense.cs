using Certio.Domain.Audit;
using Certio.Domain.Matters;
using Certio.Domain.Organizations;
using Certio.Domain.Users;

namespace Certio.Domain.Billing
{
    public class Expense : AuditableEntity
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int? MatterId { get; set; }
        public int? ClientId { get; set; }
        public int AssigneeId { get; set; }
        
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public decimal Amount { get; set; }
        public bool IsBillable { get; set; } = true;
        
        public ExpenseStatus Status { get; set; } = ExpenseStatus.Unbilled;
        
        // Receipt/attachment info
        public string? ReceiptFileName { get; set; }
        public string? ReceiptFilePath { get; set; }
        
        // Navigation properties
        public virtual Organization? Organization { get; set; }
        public virtual Matter? Matter { get; set; }
        public virtual Organization? Client { get; set; }
        public virtual User? Assignee { get; set; }
        public virtual User? CreatedBy { get; set; }
        public virtual User? ModifiedBy { get; set; }
        public virtual User? DeletedBy { get; set; }
    }
    
    public enum ExpenseStatus
    {
        Unbilled,
        Billed,
        Paid
    }
    
    public static class ExpenseCategories
    {
        public const string VenueRental = "Venue Rental";
        public const string Catering = "Catering";
        public const string FloralsDecor = "Florals & Decor";
        public const string Entertainment = "Entertainment";
        public const string Photography = "Photography";
        public const string Rentals = "Rentals";
        public const string Transportation = "Transportation";
        public const string Supplies = "Supplies";
        public const string Marketing = "Marketing";
        public const string Staffing = "Staffing";
        public const string Permits = "Permits & Insurance";
        public const string VendorFees = "Vendor Fees";
        public const string Travel = "Travel";
        public const string Other = "Other";
        
        public static string[] All => new[]
        {
            VenueRental, Catering, FloralsDecor, Entertainment, Photography,
            Rentals, Transportation, Supplies, Marketing, Staffing,
            Permits, VendorFees, Travel, Other
        };
    }
}
