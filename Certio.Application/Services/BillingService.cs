using Certio.Application.Configuration;
using Certio.Application.Interfaces;
using Certio.Domain.Billing;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services
{
    public class BillingService : IBillingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BillingService> _logger;

        public BillingService(ApplicationDbContext context, ILogger<BillingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Time Entries

        public async Task<IEnumerable<TimeEntry>> GetTimeEntriesAsync(int orgId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.TimeEntries
                .Include(te => te.Matter)
                .Include(te => te.Client)
                .Include(te => te.Assignee)
                .Where(te => te.OrganizationId == orgId && !te.IsDeleted);

            if (startDate.HasValue)
                query = query.Where(te => te.Date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(te => te.Date <= endDate.Value);

            var entries = await query
                .OrderByDescending(te => te.Date).ThenByDescending(te => te.CreatedAt)
                .Take(QueryLimits.DefaultMaxResults)
                .ToListAsync();

            if (entries.Count == QueryLimits.DefaultMaxResults)
            {
                _logger.LogWarning(
                    "GetTimeEntriesAsync truncated results at {MaxResults} for org {OrgId} - narrow the date range or add real pagination",
                    QueryLimits.DefaultMaxResults, orgId);
            }

            return entries;
        }

        public async Task<TimeEntry?> GetTimeEntryByIdAsync(int id, int orgId)
        {
            return await _context.TimeEntries
                .Include(te => te.Matter)
                .Include(te => te.Client)
                .Include(te => te.Assignee)
                .FirstOrDefaultAsync(te => te.Id == id && te.OrganizationId == orgId && !te.IsDeleted);
        }

        public async Task<TimeEntry> CreateTimeEntryAsync(TimeEntry entry, int userId)
        {
            entry.CreatedAt = DateTime.UtcNow;
            entry.CreatedById = userId;
            entry.Amount = entry.Hours * entry.Rate;

            _context.TimeEntries.Add(entry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Time entry {Id} created by user {UserId}", entry.Id, userId);
            return entry;
        }

        public async Task<TimeEntry> UpdateTimeEntryAsync(TimeEntry entry, int userId)
        {
            entry.ModifiedAt = DateTime.UtcNow;
            entry.ModifiedById = userId;
            entry.Amount = entry.Hours * entry.Rate;

            _context.TimeEntries.Update(entry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Time entry {Id} updated by user {UserId}", entry.Id, userId);
            return entry;
        }

        public async Task<bool> DeleteTimeEntryAsync(int id, int orgId, int userId)
        {
            var entry = await _context.TimeEntries
                .FirstOrDefaultAsync(te => te.Id == id && te.OrganizationId == orgId && !te.IsDeleted);

            if (entry == null)
                return false;

            entry.IsDeleted = true;
            entry.DeletedAt = DateTime.UtcNow;
            entry.DeletedById = userId;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Time entry {Id} deleted by user {UserId}", id, userId);
            return true;
        }

        #endregion

        #region Expenses

        public async Task<IEnumerable<Expense>> GetExpensesAsync(int orgId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Expenses
                .Include(e => e.Matter)
                .Include(e => e.Client)
                .Include(e => e.Assignee)
                .Where(e => e.OrganizationId == orgId && !e.IsDeleted);

            if (startDate.HasValue)
                query = query.Where(e => e.Date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(e => e.Date <= endDate.Value);

            var expenses = await query
                .OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)
                .Take(QueryLimits.DefaultMaxResults)
                .ToListAsync();

            if (expenses.Count == QueryLimits.DefaultMaxResults)
            {
                _logger.LogWarning(
                    "GetExpensesAsync truncated results at {MaxResults} for org {OrgId} - narrow the date range or add real pagination",
                    QueryLimits.DefaultMaxResults, orgId);
            }

            return expenses;
        }

        public async Task<Expense?> GetExpenseByIdAsync(int id, int orgId)
        {
            return await _context.Expenses
                .Include(e => e.Matter)
                .Include(e => e.Client)
                .Include(e => e.Assignee)
                .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == orgId && !e.IsDeleted);
        }

        public async Task<Expense> CreateExpenseAsync(Expense expense, int userId)
        {
            expense.CreatedAt = DateTime.UtcNow;
            expense.CreatedById = userId;

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Expense {Id} created by user {UserId}", expense.Id, userId);
            return expense;
        }

        public async Task<Expense> UpdateExpenseAsync(Expense expense, int userId)
        {
            expense.ModifiedAt = DateTime.UtcNow;
            expense.ModifiedById = userId;

            _context.Expenses.Update(expense);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Expense {Id} updated by user {UserId}", expense.Id, userId);
            return expense;
        }

        public async Task<bool> DeleteExpenseAsync(int id, int orgId, int userId)
        {
            var expense = await _context.Expenses
                .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == orgId && !e.IsDeleted);

            if (expense == null)
                return false;

            expense.IsDeleted = true;
            expense.DeletedAt = DateTime.UtcNow;
            expense.DeletedById = userId;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Expense {Id} deleted by user {UserId}", id, userId);
            return true;
        }

        #endregion

        #region Invoices

        public async Task<IEnumerable<Invoice>> GetInvoicesAsync(int orgId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Invoices
                .Include(i => i.Matter)
                .Include(i => i.Client)
                .Include(i => i.LineItems)
                .Where(i => i.OrganizationId == orgId && !i.IsDeleted);

            if (startDate.HasValue)
                query = query.Where(i => i.InvoiceDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(i => i.InvoiceDate <= endDate.Value);

            var invoices = await query
                .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.CreatedAt)
                .Take(QueryLimits.DefaultMaxResults)
                .ToListAsync();

            if (invoices.Count == QueryLimits.DefaultMaxResults)
            {
                _logger.LogWarning(
                    "GetInvoicesAsync truncated results at {MaxResults} for org {OrgId} - narrow the date range or add real pagination",
                    QueryLimits.DefaultMaxResults, orgId);
            }

            return invoices;
        }

        public async Task<Invoice?> GetInvoiceByIdAsync(int id, int orgId)
        {
            return await _context.Invoices
                .Include(i => i.Matter)
                .Include(i => i.Client)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == orgId && !i.IsDeleted);
        }

        public async Task<Invoice> CreateInvoiceAsync(Invoice invoice, int userId)
        {
            if (string.IsNullOrEmpty(invoice.InvoiceNumber))
                invoice.InvoiceNumber = await GenerateInvoiceNumberAsync(invoice.OrganizationId);

            invoice.CreatedAt = DateTime.UtcNow;
            invoice.CreatedById = userId;

            // Calculate totals
            invoice.Subtotal = invoice.LineItems?.Sum(li => li.Amount) ?? 0;
            invoice.TotalAmount = invoice.Subtotal + invoice.TaxAmount;

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Invoice {Id} created by user {UserId}", invoice.Id, userId);
            return invoice;
        }

        public async Task<Invoice> UpdateInvoiceAsync(Invoice invoice, int userId)
        {
            invoice.ModifiedAt = DateTime.UtcNow;
            invoice.ModifiedById = userId;

            // Recalculate totals
            invoice.Subtotal = invoice.LineItems?.Sum(li => li.Amount) ?? 0;
            invoice.TotalAmount = invoice.Subtotal + invoice.TaxAmount;

            _context.Invoices.Update(invoice);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Invoice {Id} updated by user {UserId}", invoice.Id, userId);
            return invoice;
        }

        public async Task<bool> DeleteInvoiceAsync(int id, int orgId, int userId)
        {
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == orgId && !i.IsDeleted);

            if (invoice == null)
                return false;

            invoice.IsDeleted = true;
            invoice.DeletedAt = DateTime.UtcNow;
            invoice.DeletedById = userId;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Invoice {Id} deleted by user {UserId}", id, userId);
            return true;
        }

        public async Task<string> GenerateInvoiceNumberAsync(int orgId)
        {
            var year = DateTime.UtcNow.Year;
            var lastInvoice = await _context.Invoices
                .Where(i => i.OrganizationId == orgId && i.InvoiceNumber.StartsWith($"INV-{year}-"))
                .OrderByDescending(i => i.InvoiceNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastInvoice != null)
            {
                var parts = lastInvoice.InvoiceNumber.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"INV-{year}-{nextNumber:D3}";
        }

        #endregion

        #region Retainers

        public async Task<IEnumerable<Retainer>> GetRetainersAsync(int orgId)
        {
            return await _context.Retainers
                .Include(r => r.Matter)
                .Include(r => r.Client)
                .Include(r => r.Transactions.OrderByDescending(t => t.Date).Take(5))
                .Where(r => r.OrganizationId == orgId && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<Retainer?> GetRetainerByIdAsync(int id, int orgId)
        {
            return await _context.Retainers
                .Include(r => r.Matter)
                .Include(r => r.Client)
                .Include(r => r.Transactions.OrderByDescending(t => t.Date))
                .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId && !r.IsDeleted);
        }

        public async Task<Retainer> CreateRetainerAsync(Retainer retainer, int userId)
        {
            retainer.CreatedAt = DateTime.UtcNow;
            retainer.CreatedById = userId;
            retainer.CurrentBalance = retainer.InitialAmount;
            retainer.TotalDeposited = retainer.InitialAmount;
            if (retainer.Status == RetainerStatus.Closed)
            {
                // Allow explicitly created closed retainers
            }
            else
            {
                UpdateRetainerStatus(retainer);
            }

            _context.Retainers.Add(retainer);
            await _context.SaveChangesAsync();

            // Create initial deposit transaction
            if (retainer.InitialAmount > 0)
            {
                var transaction = new RetainerTransaction
                {
                    RetainerId = retainer.Id,
                    Type = RetainerTransactionType.Deposit,
                    Amount = retainer.InitialAmount,
                    BalanceAfter = retainer.CurrentBalance,
                    Date = DateTime.UtcNow,
                    Description = "Initial deposit",
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.RetainerTransactions.Add(transaction);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Retainer {Id} created by user {UserId}", retainer.Id, userId);
            return retainer;
        }

        public async Task<Retainer> UpdateRetainerAsync(Retainer retainer, int userId)
        {
            retainer.ModifiedAt = DateTime.UtcNow;
            retainer.ModifiedById = userId;
            UpdateRetainerStatus(retainer);

            _context.Retainers.Update(retainer);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Retainer {Id} updated by user {UserId}", retainer.Id, userId);
            return retainer;
        }

        public async Task<bool> DeleteRetainerAsync(int id, int orgId, int userId)
        {
            var retainer = await _context.Retainers
                .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId && !r.IsDeleted);

            if (retainer == null)
                return false;

            retainer.IsDeleted = true;
            retainer.DeletedAt = DateTime.UtcNow;
            retainer.DeletedById = userId;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Retainer {Id} deleted by user {UserId}", id, userId);
            return true;
        }

        public async Task<RetainerTransaction> DepositAsync(int retainerId, int orgId, decimal amount, string? description, int userId)
        {
            var retainer = await _context.Retainers
                .FirstOrDefaultAsync(r => r.Id == retainerId && r.OrganizationId == orgId && !r.IsDeleted);

            if (retainer == null)
                throw new InvalidOperationException("Retainer not found");

            retainer.CurrentBalance += amount;
            retainer.TotalDeposited += amount;
            retainer.ModifiedAt = DateTime.UtcNow;
            retainer.ModifiedById = userId;
            UpdateRetainerStatus(retainer);

            var transaction = new RetainerTransaction
            {
                RetainerId = retainerId,
                Type = RetainerTransactionType.Deposit,
                Amount = amount,
                BalanceAfter = retainer.CurrentBalance,
                Date = DateTime.UtcNow,
                Description = description ?? "Deposit",
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.RetainerTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deposit of {Amount} to retainer {RetainerId} by user {UserId}", amount, retainerId, userId);
            return transaction;
        }

        public async Task<RetainerTransaction> WithdrawAsync(int retainerId, int orgId, decimal amount, string? description, int? invoiceId, int userId)
        {
            var retainer = await _context.Retainers
                .FirstOrDefaultAsync(r => r.Id == retainerId && r.OrganizationId == orgId && !r.IsDeleted);

            if (retainer == null)
                throw new InvalidOperationException("Retainer not found");

            retainer.CurrentBalance -= amount;
            retainer.TotalWithdrawn += amount;
            retainer.ModifiedAt = DateTime.UtcNow;
            retainer.ModifiedById = userId;
            UpdateRetainerStatus(retainer);

            var transaction = new RetainerTransaction
            {
                RetainerId = retainerId,
                Type = RetainerTransactionType.Withdrawal,
                Amount = amount,
                BalanceAfter = retainer.CurrentBalance,
                Date = DateTime.UtcNow,
                Description = description ?? "Withdrawal",
                InvoiceId = invoiceId,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.RetainerTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Withdrawal of {Amount} from retainer {RetainerId} by user {UserId}", amount, retainerId, userId);
            return transaction;
        }

        private void UpdateRetainerStatus(Retainer retainer)
        {
            if (retainer.Status == RetainerStatus.Closed)
                return;

            if (retainer.CurrentBalance < 0)
                retainer.Status = RetainerStatus.Overdrawn;
            else if (retainer.CurrentBalance == 0 && retainer.TotalWithdrawn > 0)
                retainer.Status = RetainerStatus.Depleted;
            else
                retainer.Status = RetainerStatus.Active;
        }

        #endregion

        #region Overview

        public async Task<BillingOverviewDto> GetOverviewAsync(int orgId, DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            // Active events (matters with activity in date range)
            var activeEvents = await _context.Matters
                .Where(m => m.OrganizationId == orgId && !m.IsDeleted)
                .CountAsync();

            // Revenue this month (paid invoices)
            var paidInvoices = await _context.Invoices
                .Where(i => i.OrganizationId == orgId && !i.IsDeleted 
                    && i.Status == InvoiceStatus.Paid
                    && i.PaidDate >= startDate && i.PaidDate <= endDate)
                .SumAsync(i => i.PaidAmount);

            // Pending invoices count
            var pendingInvoicesCount = await _context.Invoices
                .Where(i => i.OrganizationId == orgId && !i.IsDeleted 
                    && (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.PartiallyPaid))
                .CountAsync();

            // Outstanding amount
            var outstanding = await _context.Invoices
                .Where(i => i.OrganizationId == orgId && !i.IsDeleted 
                    && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled)
                .SumAsync(i => i.TotalAmount - i.PaidAmount);

            // Total billed (all time)
            var totalBilled = await _context.Invoices
                .Where(i => i.OrganizationId == orgId && !i.IsDeleted && i.Status != InvoiceStatus.Draft)
                .SumAsync(i => i.TotalAmount);

            // Total collected (all time)
            var totalCollected = await _context.Invoices
                .Where(i => i.OrganizationId == orgId && !i.IsDeleted)
                .SumAsync(i => i.PaidAmount);

            var collectionRate = totalBilled > 0 ? (totalCollected / totalBilled) * 100 : 0;

            return new BillingOverviewDto
            {
                ActiveEvents = activeEvents,
                Revenue = paidInvoices,
                PendingInvoices = pendingInvoicesCount,
                Outstanding = outstanding,
                TotalBilled = totalBilled,
                TotalCollected = totalCollected,
                CollectionRate = Math.Round(collectionRate, 1)
            };
        }

        #endregion
    }
}
