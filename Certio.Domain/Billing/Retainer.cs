using Certio.Domain.Audit;
using Certio.Domain.Matters;
using Certio.Domain.Organizations;
using Certio.Domain.Users;

namespace Certio.Domain.Billing
{
    public class Retainer : AuditableEntity
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int? MatterId { get; set; }
        public int? ClientId { get; set; }
        
        public decimal InitialAmount { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal TotalDeposited { get; set; }
        public decimal TotalWithdrawn { get; set; }
        
        public RetainerStatus Status { get; set; } = RetainerStatus.Active;
        
        public string? Notes { get; set; }
        
        // Navigation properties
        public virtual Organization? Organization { get; set; }
        public virtual Matter? Matter { get; set; }
        public virtual Organization? Client { get; set; }
        public virtual User? CreatedBy { get; set; }
        public virtual User? ModifiedBy { get; set; }
        public virtual User? DeletedBy { get; set; }
        
        // Transactions
        public virtual ICollection<RetainerTransaction> Transactions { get; set; } = new List<RetainerTransaction>();
        
        // Computed properties
        public decimal UsagePercentage => InitialAmount > 0 ? ((TotalWithdrawn / InitialAmount) * 100) : 0;
        public bool IsOverdrawn => CurrentBalance < 0;
        public bool IsDepleted => CurrentBalance == 0 && TotalWithdrawn > 0;
    }
    
    public class RetainerTransaction
    {
        public int Id { get; set; }
        public int RetainerId { get; set; }
        
        public RetainerTransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }
        
        // Link to invoice if withdrawal is for an invoice
        public int? InvoiceId { get; set; }
        
        // Audit
        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public virtual Retainer? Retainer { get; set; }
        public virtual Invoice? Invoice { get; set; }
        public virtual User? CreatedBy { get; set; }
    }
    
    public enum RetainerStatus
    {
        Active,
        Depleted,
        Overdrawn,
        Closed
    }
    
    public enum RetainerTransactionType
    {
        Deposit,
        Withdrawal,
        Refund,
        Adjustment
    }
}
