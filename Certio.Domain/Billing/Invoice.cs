using Certio.Domain.Audit;
using Certio.Domain.Matters;
using Certio.Domain.Organizations;
using Certio.Domain.Users;

namespace Certio.Domain.Billing
{
    public class Invoice : AuditableEntity
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int? MatterId { get; set; }
        public int? ClientId { get; set; }
        
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceDue => TotalAmount - PaidAmount;
        
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
        
        public string? Notes { get; set; }
        public string? Terms { get; set; }
        
        // Payment info
        public DateTime? PaidDate { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
        
        // Navigation properties
        public virtual Organization? Organization { get; set; }
        public virtual Matter? Matter { get; set; }
        public virtual Organization? Client { get; set; }
        public virtual User? CreatedBy { get; set; }
        public virtual User? ModifiedBy { get; set; }
        public virtual User? DeletedBy { get; set; }
        
        // Line items
        public virtual ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    }
    
    public class InvoiceLineItem
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        
        public int? TimeEntryId { get; set; }
        public int? ExpenseId { get; set; }
        
        // Navigation properties
        public virtual Invoice? Invoice { get; set; }
        public virtual TimeEntry? TimeEntry { get; set; }
        public virtual Expense? Expense { get; set; }
    }
    
    public enum InvoiceStatus
    {
        Draft,
        Pending,
        Paid,
        Overdue,
        PartiallyPaid,
        Cancelled,
        Refunded
    }
}
