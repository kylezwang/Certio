using Certio.Domain.Billing;

namespace Certio.Application.Interfaces
{
    public interface IBillingService
    {
        // Time Entries
        Task<IEnumerable<TimeEntry>> GetTimeEntriesAsync(int orgId, DateTime? startDate = null, DateTime? endDate = null);
        Task<TimeEntry?> GetTimeEntryByIdAsync(int id, int orgId);
        Task<TimeEntry> CreateTimeEntryAsync(TimeEntry entry, int userId);
        Task<TimeEntry> UpdateTimeEntryAsync(TimeEntry entry, int userId);
        Task<bool> DeleteTimeEntryAsync(int id, int orgId, int userId);
        
        // Expenses
        Task<IEnumerable<Expense>> GetExpensesAsync(int orgId, DateTime? startDate = null, DateTime? endDate = null);
        Task<Expense?> GetExpenseByIdAsync(int id, int orgId);
        Task<Expense> CreateExpenseAsync(Expense expense, int userId);
        Task<Expense> UpdateExpenseAsync(Expense expense, int userId);
        Task<bool> DeleteExpenseAsync(int id, int orgId, int userId);
        
        // Invoices
        Task<IEnumerable<Invoice>> GetInvoicesAsync(int orgId, DateTime? startDate = null, DateTime? endDate = null);
        Task<Invoice?> GetInvoiceByIdAsync(int id, int orgId);
        Task<Invoice> CreateInvoiceAsync(Invoice invoice, int userId);
        Task<Invoice> UpdateInvoiceAsync(Invoice invoice, int userId);
        Task<bool> DeleteInvoiceAsync(int id, int orgId, int userId);
        Task<string> GenerateInvoiceNumberAsync(int orgId);
        
        // Retainers
        Task<IEnumerable<Retainer>> GetRetainersAsync(int orgId);
        Task<Retainer?> GetRetainerByIdAsync(int id, int orgId);
        Task<Retainer> CreateRetainerAsync(Retainer retainer, int userId);
        Task<Retainer> UpdateRetainerAsync(Retainer retainer, int userId);
        Task<bool> DeleteRetainerAsync(int id, int orgId, int userId);
        Task<RetainerTransaction> DepositAsync(int retainerId, int orgId, decimal amount, string? description, int userId);
        Task<RetainerTransaction> WithdrawAsync(int retainerId, int orgId, decimal amount, string? description, int? invoiceId, int userId);
        
        // Overview/Statistics
        Task<BillingOverviewDto> GetOverviewAsync(int orgId, DateTime? startDate = null, DateTime? endDate = null);
    }
    
    public class BillingOverviewDto
    {
        public int ActiveEvents { get; set; }
        public decimal Revenue { get; set; }
        public int PendingInvoices { get; set; }
        public decimal Outstanding { get; set; }
        public decimal CollectionRate { get; set; }
        public decimal TotalBilled { get; set; }
        public decimal TotalCollected { get; set; }
    }
}
