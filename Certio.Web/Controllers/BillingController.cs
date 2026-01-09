using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Application.Interfaces;
using Certio.Domain.Users;
using Certio.Domain.Billing;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers
{
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITeamService _teamService;
        private readonly IMatterService _matterService;
        private readonly IBillingService _billingService;
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            ApplicationDbContext context,
            ITeamService teamService,
            IMatterService matterService,
            IBillingService billingService,
            ILogger<BillingController> logger)
        {
            _context = context;
            _teamService = teamService;
            _matterService = matterService;
            _billingService = billingService;
            _logger = logger;
        }

        // GET: /Client/{orgId}/Billing
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Billing")]
        public async Task<IActionResult> Index(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Set ViewBag for layout and client-side
            ViewBag.OrganizationId = orgId;
            ViewBag.CurrentUserId = user.Id;
            ViewBag.CurrentUserName = $"{user.FirstName} {user.LastName}";
            ViewBag.CurrentUserInitials = $"{user.FirstName[0]}{user.LastName[0]}".ToUpper();
            ViewBag.CurrentUserEmail = user.Email ?? "";

            // Get organization name
            var org = await _context.Organizations
                .Where(o => o.Id == orgId)
                .FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            ViewBag.OrganizationType = org?.Type ?? Certio.Domain.Organizations.OrganizationType.Client;
            ViewBag.OrganizationEntity = org; // For custom terminology

            return View();
        }

        // GET: /Client/{orgId}/Billing/TimeEntries
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Billing/TimeEntries")]
        public async Task<IActionResult> GetTimeEntries(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var entries = await _billingService.GetTimeEntriesAsync(orgId);
            return PartialView("~/Views/Billing/_BillingTimeEntries.cshtml", entries);
        }

        // GET: /Client/{orgId}/Billing/Expenses
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Billing/Expenses")]
        public async Task<IActionResult> GetExpenses(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var expenses = await _billingService.GetExpensesAsync(orgId);
            return PartialView("~/Views/Billing/_BillingExpenses.cshtml", expenses);
        }

        // GET: /Client/{orgId}/Billing/Overview
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Billing/Overview")]
        public async Task<IActionResult> GetOverview(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var overview = await _billingService.GetOverviewAsync(orgId);
            return PartialView("~/Views/Billing/_BillingOverview.cshtml", overview);
        }

        // GET: /Client/{orgId}/Billing/Trusts
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Billing/Trusts")]
        public async Task<IActionResult> GetTrusts(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var retainers = await _billingService.GetRetainersAsync(orgId);
            return PartialView("~/Views/Billing/_BillingTrusts.cshtml", retainers);
        }

        // GET: /Client/{orgId}/Billing/Invoices
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Billing/Invoices")]
        public async Task<IActionResult> GetInvoices(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var invoices = await _billingService.GetInvoicesAsync(orgId);
            return PartialView("~/Views/Billing/_BillingInvoices.cshtml", invoices);
        }

        #region Time Entry API Endpoints

        // POST: /api/billing/{orgId}/time-entries
        [Authorize(Policy = "OrgMember")]
        [HttpPost("/api/billing/{orgId:int}/time-entries")]
        public async Task<IActionResult> CreateTimeEntry(int orgId, [FromBody] TimeEntryDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            try
            {
                var entry = new TimeEntry
                {
                    OrganizationId = orgId,
                    MatterId = dto.MatterId,
                    ClientId = dto.ClientId,
                    AssigneeId = dto.AssigneeId ?? user.Id,
                    Date = dto.Date ?? DateTime.UtcNow,
                    Description = dto.Description,
                    Hours = dto.Hours,
                    Rate = dto.Rate,
                    IsBillable = dto.IsBillable,
                    Status = dto.Status ?? TimeEntryStatus.NeedsReview
                };

                var result = await _billingService.CreateTimeEntryAsync(entry, user.Id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating time entry");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // PUT: /api/billing/{orgId}/time-entries/{id}
        [Authorize(Policy = "OrgMember")]
        [HttpPut("/api/billing/{orgId:int}/time-entries/{id:int}")]
        public async Task<IActionResult> UpdateTimeEntry(int orgId, int id, [FromBody] TimeEntryDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            try
            {
                var entry = await _billingService.GetTimeEntryByIdAsync(id, orgId);
                if (entry == null) return NotFound(new { success = false, error = "Time entry not found" });

                entry.MatterId = dto.MatterId;
                entry.ClientId = dto.ClientId;
                entry.AssigneeId = dto.AssigneeId ?? entry.AssigneeId;
                entry.Date = dto.Date ?? entry.Date;
                entry.Description = dto.Description;
                entry.Hours = dto.Hours;
                entry.Rate = dto.Rate;
                entry.IsBillable = dto.IsBillable;
                entry.Status = dto.Status ?? entry.Status;

                var result = await _billingService.UpdateTimeEntryAsync(entry, user.Id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating time entry {Id}", id);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // DELETE: /api/billing/{orgId}/time-entries/{id}
        [Authorize(Policy = "OrgMember")]
        [HttpDelete("/api/billing/{orgId:int}/time-entries/{id:int}")]
        public async Task<IActionResult> DeleteTimeEntry(int orgId, int id)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var success = await _billingService.DeleteTimeEntryAsync(id, orgId, user.Id);
            if (!success) return NotFound(new { success = false, error = "Time entry not found" });

            return Ok(new { success = true });
        }

        // GET: /api/billing/{orgId}/time-entries
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/api/billing/{orgId:int}/time-entries")]
        public async Task<IActionResult> GetTimeEntries(int orgId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var entries = await _billingService.GetTimeEntriesAsync(orgId, startDate, endDate);
            return Ok(new { success = true, data = entries });
        }

        #endregion

        #region Expense API Endpoints

        // POST: /api/billing/{orgId}/expenses
        [Authorize(Policy = "OrgMember")]
        [HttpPost("/api/billing/{orgId:int}/expenses")]
        public async Task<IActionResult> CreateExpense(int orgId, [FromBody] ExpenseDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            try
            {
                var expense = new Expense
                {
                    OrganizationId = orgId,
                    MatterId = dto.MatterId,
                    ClientId = dto.ClientId,
                    AssigneeId = dto.AssigneeId ?? user.Id,
                    Date = dto.Date ?? DateTime.UtcNow,
                    Description = dto.Description,
                    Category = dto.Category,
                    Amount = dto.Amount,
                    IsBillable = dto.IsBillable,
                    Status = dto.Status ?? ExpenseStatus.Unbilled
                };

                var result = await _billingService.CreateExpenseAsync(expense, user.Id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating expense");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // PUT: /api/billing/{orgId}/expenses/{id}
        [Authorize(Policy = "OrgMember")]
        [HttpPut("/api/billing/{orgId:int}/expenses/{id:int}")]
        public async Task<IActionResult> UpdateExpense(int orgId, int id, [FromBody] ExpenseDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            try
            {
                var expense = await _billingService.GetExpenseByIdAsync(id, orgId);
                if (expense == null) return NotFound(new { success = false, error = "Expense not found" });

                expense.MatterId = dto.MatterId;
                expense.ClientId = dto.ClientId;
                expense.AssigneeId = dto.AssigneeId ?? expense.AssigneeId;
                expense.Date = dto.Date ?? expense.Date;
                expense.Description = dto.Description;
                expense.Category = dto.Category;
                expense.Amount = dto.Amount;
                expense.IsBillable = dto.IsBillable;
                expense.Status = dto.Status ?? expense.Status;

                var result = await _billingService.UpdateExpenseAsync(expense, user.Id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating expense {Id}", id);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // DELETE: /api/billing/{orgId}/expenses/{id}
        [Authorize(Policy = "OrgMember")]
        [HttpDelete("/api/billing/{orgId:int}/expenses/{id:int}")]
        public async Task<IActionResult> DeleteExpense(int orgId, int id)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var success = await _billingService.DeleteExpenseAsync(id, orgId, user.Id);
            if (!success) return NotFound(new { success = false, error = "Expense not found" });

            return Ok(new { success = true });
        }

        // GET: /api/billing/{orgId}/expenses
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/api/billing/{orgId:int}/expenses")]
        public async Task<IActionResult> GetExpensesApi(int orgId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var expenses = await _billingService.GetExpensesAsync(orgId, startDate, endDate);
            return Ok(new { success = true, data = expenses });
        }

        #endregion

        #region Invoice API Endpoints

        // POST: /api/billing/{orgId}/invoices
        [Authorize(Policy = "OrgMember")]
        [HttpPost("/api/billing/{orgId:int}/invoices")]
        public async Task<IActionResult> CreateInvoice(int orgId, [FromBody] InvoiceDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            try
            {
                var invoice = new Invoice
                {
                    OrganizationId = orgId,
                    MatterId = dto.MatterId,
                    ClientId = dto.ClientId,
                    InvoiceDate = dto.InvoiceDate ?? DateTime.UtcNow,
                    DueDate = dto.DueDate ?? DateTime.UtcNow.AddDays(30),
                    Notes = dto.Notes,
                    Terms = dto.Terms,
                    TaxAmount = dto.TaxAmount,
                    Status = dto.Status ?? InvoiceStatus.Draft
                };

                var result = await _billingService.CreateInvoiceAsync(invoice, user.Id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // PUT: /api/billing/{orgId}/invoices/{id}
        [Authorize(Policy = "OrgMember")]
        [HttpPut("/api/billing/{orgId:int}/invoices/{id:int}")]
        public async Task<IActionResult> UpdateInvoice(int orgId, int id, [FromBody] InvoiceDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            try
            {
                var invoice = await _billingService.GetInvoiceByIdAsync(id, orgId);
                if (invoice == null) return NotFound(new { success = false, error = "Invoice not found" });

                invoice.MatterId = dto.MatterId;
                invoice.ClientId = dto.ClientId;
                invoice.InvoiceDate = dto.InvoiceDate ?? invoice.InvoiceDate;
                invoice.DueDate = dto.DueDate ?? invoice.DueDate;
                invoice.Notes = dto.Notes;
                invoice.Terms = dto.Terms;
                invoice.TaxAmount = dto.TaxAmount;
                invoice.Status = dto.Status ?? invoice.Status;
                invoice.PaidAmount = dto.PaidAmount ?? invoice.PaidAmount;
                invoice.PaidDate = dto.PaidDate;
                invoice.PaymentMethod = dto.PaymentMethod;
                invoice.PaymentReference = dto.PaymentReference;

                var result = await _billingService.UpdateInvoiceAsync(invoice, user.Id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating invoice {Id}", id);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // DELETE: /api/billing/{orgId}/invoices/{id}
        [Authorize(Policy = "OrgMember")]
        [HttpDelete("/api/billing/{orgId:int}/invoices/{id:int}")]
        public async Task<IActionResult> DeleteInvoice(int orgId, int id)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var success = await _billingService.DeleteInvoiceAsync(id, orgId, user.Id);
            if (!success) return NotFound(new { success = false, error = "Invoice not found" });

            return Ok(new { success = true });
        }

        // GET: /api/billing/{orgId}/invoices
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/api/billing/{orgId:int}/invoices")]
        public async Task<IActionResult> GetInvoicesApi(int orgId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var invoices = await _billingService.GetInvoicesAsync(orgId, startDate, endDate);
            return Ok(new { success = true, data = invoices });
        }

        #endregion

        #region Retainer API Endpoints

        // POST: /api/billing/{orgId}/retainers
        [Authorize(Policy = "OrgMember")]
        [HttpPost("/api/billing/{orgId:int}/retainers")]
        public async Task<IActionResult> CreateRetainer(int orgId, [FromBody] RetainerDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            try
            {
                var retainer = new Retainer
                {
                    OrganizationId = orgId,
                    MatterId = dto.MatterId,
                    ClientId = dto.ClientId,
                    InitialAmount = dto.InitialAmount,
                    Notes = dto.Notes,
                    Status = dto.Status ?? RetainerStatus.Active
                };

                var result = await _billingService.CreateRetainerAsync(retainer, user.Id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating retainer");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // PUT: /api/billing/{orgId}/retainers/{id}
        [Authorize(Policy = "OrgMember")]
        [HttpPut("/api/billing/{orgId:int}/retainers/{id:int}")]
        public async Task<IActionResult> UpdateRetainer(int orgId, int id, [FromBody] RetainerDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            try
            {
                var retainer = await _billingService.GetRetainerByIdAsync(id, orgId);
                if (retainer == null) return NotFound(new { success = false, error = "Retainer not found" });

                retainer.MatterId = dto.MatterId;
                retainer.ClientId = dto.ClientId;
                retainer.Notes = dto.Notes;
                retainer.Status = dto.Status ?? retainer.Status;

                var result = await _billingService.UpdateRetainerAsync(retainer, user.Id);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating retainer {Id}", id);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // POST: /api/billing/{orgId}/retainers/{id}/deposit
        [Authorize(Policy = "OrgMember")]
        [HttpPost("/api/billing/{orgId:int}/retainers/{id:int}/deposit")]
        public async Task<IActionResult> Deposit(int orgId, int id, [FromBody] RetainerTransactionDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            try
            {
                var transaction = await _billingService.DepositAsync(id, orgId, dto.Amount, dto.Description, user.Id);
                return Ok(new { success = true, data = transaction });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error depositing to retainer {Id}", id);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // POST: /api/billing/{orgId}/retainers/{id}/withdraw
        [Authorize(Policy = "OrgMember")]
        [HttpPost("/api/billing/{orgId:int}/retainers/{id:int}/withdraw")]
        public async Task<IActionResult> Withdraw(int orgId, int id, [FromBody] RetainerTransactionDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            try
            {
                var transaction = await _billingService.WithdrawAsync(id, orgId, dto.Amount, dto.Description, dto.InvoiceId, user.Id);
                return Ok(new { success = true, data = transaction });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error withdrawing from retainer {Id}", id);
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // DELETE: /api/billing/{orgId}/retainers/{id}
        [Authorize(Policy = "OrgMember")]
        [HttpDelete("/api/billing/{orgId:int}/retainers/{id:int}")]
        public async Task<IActionResult> DeleteRetainer(int orgId, int id)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var success = await _billingService.DeleteRetainerAsync(id, orgId, user.Id);
            if (!success) return NotFound(new { success = false, error = "Retainer not found" });

            return Ok(new { success = true });
        }

        // GET: /api/billing/{orgId}/retainers
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/api/billing/{orgId:int}/retainers")]
        public async Task<IActionResult> GetRetainersApi(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var retainers = await _billingService.GetRetainersAsync(orgId);
            return Ok(new { success = true, data = retainers });
        }

        #endregion

        #region Overview API

        // GET: /api/billing/{orgId}/overview
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/api/billing/{orgId:int}/overview")]
        public async Task<IActionResult> GetOverviewApi(int orgId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var overview = await _billingService.GetOverviewAsync(orgId, startDate, endDate);
            return Ok(new { success = true, data = overview });
        }

        #endregion

        #region Supporting Data API

        // GET: /api/billing/{orgId}/recent-activity
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/api/billing/{orgId:int}/recent-activity")]
        public async Task<IActionResult> GetRecentActivity(int orgId, [FromQuery] int take = 25)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            take = Math.Clamp(take, 1, 100);

            // Pull a small batch from each table and merge in memory.
            var timeEntries = await _context.TimeEntries
                .Where(te => te.OrganizationId == orgId && !te.IsDeleted)
                .OrderByDescending(te => te.Date)
                .ThenByDescending(te => te.CreatedAt)
                .Take(take)
                .Select(te => new
                {
                    Type = "time-entry",
                    te.Id,
                    Date = te.Date,
                    MatterId = te.MatterId,
                    MatterTitle = te.Matter != null ? te.Matter.Title : null,
                    ClientId = te.ClientId,
                    ClientName = te.Client != null ? te.Client.Name : null,
                    AssigneeId = (int?)te.AssigneeId,
                    AssigneeName = te.Assignee != null ? (te.Assignee.FirstName + " " + te.Assignee.LastName) : null,
                    AssigneeInitials = te.Assignee != null ? (te.Assignee.FirstName.Substring(0, 1) + te.Assignee.LastName.Substring(0, 1)).ToUpper() : null,
                    Description = te.Description,
                    Amount = (decimal?)te.Amount,
                    Hours = (decimal?)te.Hours,
                    Rate = (decimal?)te.Rate,
                    IsBillable = (bool?)te.IsBillable,
                    Status = (int?)te.Status,
                    StatusText = te.Status.ToString(),
                    StatusClass = te.Status == TimeEntryStatus.NeedsReview ? "needs-review"
                        : te.Status == TimeEntryStatus.Approved ? "approved"
                        : te.Status == TimeEntryStatus.Billed ? "billed"
                        : te.Status == TimeEntryStatus.Paid ? "paid"
                        : "needs-review",
                    Category = (string?)null,
                    InvoiceDate = (DateTime?)null,
                    DueDate = (DateTime?)null,
                    TaxAmount = (decimal?)null,
                    Notes = (string?)null,
                    Terms = (string?)null,
                    InitialAmount = (decimal?)null
                })
                .ToListAsync();

            var expenses = await _context.Expenses
                .Where(e => e.OrganizationId == orgId && !e.IsDeleted)
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.CreatedAt)
                .Take(take)
                .Select(e => new
                {
                    Type = "expense",
                    e.Id,
                    Date = e.Date,
                    MatterId = e.MatterId,
                    MatterTitle = e.Matter != null ? e.Matter.Title : null,
                    ClientId = e.ClientId,
                    ClientName = e.Client != null ? e.Client.Name : null,
                    AssigneeId = (int?)e.AssigneeId,
                    AssigneeName = e.Assignee != null ? (e.Assignee.FirstName + " " + e.Assignee.LastName) : null,
                    AssigneeInitials = e.Assignee != null ? (e.Assignee.FirstName.Substring(0, 1) + e.Assignee.LastName.Substring(0, 1)).ToUpper() : null,
                    Description = e.Description,
                    Amount = (decimal?)e.Amount,
                    Hours = (decimal?)null,
                    Rate = (decimal?)null,
                    IsBillable = (bool?)e.IsBillable,
                    Status = (int?)e.Status,
                    StatusText = e.Status.ToString(),
                    StatusClass = e.Status == ExpenseStatus.Unbilled ? "unbilled"
                        : e.Status == ExpenseStatus.Billed ? "billed"
                        : e.Status == ExpenseStatus.Paid ? "paid"
                        : "unbilled",
                    Category = e.Category,
                    InvoiceDate = (DateTime?)null,
                    DueDate = (DateTime?)null,
                    TaxAmount = (decimal?)null,
                    Notes = (string?)null,
                    Terms = (string?)null,
                    InitialAmount = (decimal?)null
                })
                .ToListAsync();

            var invoices = await _context.Invoices
                .Where(i => i.OrganizationId == orgId && !i.IsDeleted)
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.CreatedAt)
                .Take(take)
                .Select(i => new
                {
                    Type = "invoice",
                    i.Id,
                    Date = i.InvoiceDate,
                    MatterId = i.MatterId,
                    MatterTitle = i.Matter != null ? i.Matter.Title : null,
                    ClientId = i.ClientId,
                    ClientName = i.Client != null ? i.Client.Name : null,
                    AssigneeId = (int?)null,
                    AssigneeName = (string?)null,
                    AssigneeInitials = (string?)null,
                    Description = i.InvoiceNumber,
                    Amount = (decimal?)i.TotalAmount,
                    Hours = (decimal?)null,
                    Rate = (decimal?)null,
                    IsBillable = (bool?)null,
                    Status = (int?)i.Status,
                    StatusText = i.Status.ToString(),
                    StatusClass = i.Status == InvoiceStatus.Draft ? "draft"
                        : i.Status == InvoiceStatus.Pending ? "pending"
                        : i.Status == InvoiceStatus.PartiallyPaid ? "pending"
                        : i.Status == InvoiceStatus.Paid ? "paid"
                        : i.Status == InvoiceStatus.Cancelled ? "cancelled"
                        : "pending",
                    Category = (string?)null,
                    InvoiceDate = (DateTime?)i.InvoiceDate,
                    DueDate = (DateTime?)i.DueDate,
                    TaxAmount = (decimal?)i.TaxAmount,
                    Notes = i.Notes,
                    Terms = i.Terms,
                    InitialAmount = (decimal?)null
                })
                .ToListAsync();

            var retainers = await _context.Retainers
                .Where(r => r.OrganizationId == orgId && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .Take(take)
                .Select(r => new
                {
                    Type = "retainer",
                    r.Id,
                    Date = r.CreatedAt,
                    MatterId = r.MatterId,
                    MatterTitle = r.Matter != null ? r.Matter.Title : null,
                    ClientId = r.ClientId,
                    ClientName = r.Client != null ? r.Client.Name : null,
                    AssigneeId = (int?)null,
                    AssigneeName = (string?)null,
                    AssigneeInitials = (string?)null,
                    Description = r.Notes,
                    Amount = (decimal?)r.InitialAmount,
                    Hours = (decimal?)null,
                    Rate = (decimal?)null,
                    IsBillable = (bool?)null,
                    Status = (int?)r.Status,
                    StatusText = r.Status.ToString(),
                    StatusClass = r.Status == RetainerStatus.Active ? "active"
                        : r.Status == RetainerStatus.Depleted ? "depleted"
                        : r.Status == RetainerStatus.Overdrawn ? "overdrawn"
                        : r.Status == RetainerStatus.Closed ? "depleted"
                        : "active",
                    Category = (string?)null,
                    InvoiceDate = (DateTime?)null,
                    DueDate = (DateTime?)null,
                    TaxAmount = (decimal?)null,
                    Notes = r.Notes,
                    Terms = (string?)null,
                    InitialAmount = (decimal?)r.InitialAmount
                })
                .ToListAsync();

            var merged = timeEntries
                .Concat(expenses)
                .Concat(invoices)
                .Concat(retainers)
                .OrderByDescending(x => x.Date)
                .Take(take)
                .Select(x => new
                {
                    type = x.Type,
                    id = x.Id,
                    date = x.Date.ToString("yyyy-MM-dd"),
                    dateDisplay = x.Date.ToString("MMM dd, yyyy"),
                    typeDisplay = x.Type == "time-entry" ? "Time Entry"
                        : x.Type == "expense" ? "Expense"
                        : x.Type == "invoice" ? "Invoice"
                        : x.Type == "retainer" ? "Retainer"
                        : x.Type,
                    clientId = x.ClientId,
                    clientName = x.ClientName,
                    matterId = x.MatterId,
                    matterTitle = x.MatterTitle,
                    assigneeId = x.AssigneeId,
                    assigneeName = x.AssigneeName,
                    assigneeInitials = x.AssigneeInitials,
                    description = x.Description,
                    amount = x.Amount,
                    amountDisplay = x.Amount.HasValue ? x.Amount.Value.ToString("C0") : "—",
                    hours = x.Hours,
                    rate = x.Rate,
                    isBillable = x.IsBillable,
                    status = x.Status,
                    statusText = x.StatusText,
                    statusClass = x.StatusClass,
                    category = x.Category,
                    invoiceDate = x.InvoiceDate?.ToString("yyyy-MM-dd"),
                    dueDate = x.DueDate?.ToString("yyyy-MM-dd"),
                    taxAmount = x.TaxAmount,
                    notes = x.Notes,
                    terms = x.Terms,
                    initialAmount = x.InitialAmount
                })
                .ToList();

            return Ok(new { success = true, data = merged });
        }

        // GET: /api/billing/{orgId}/expense-categories
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/api/billing/{orgId:int}/expense-categories")]
        public IActionResult GetExpenseCategories(int orgId)
        {
            return Ok(new { success = true, data = ExpenseCategories.All });
        }

        // GET: /api/billing/{orgId}/matters
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/api/billing/{orgId:int}/matters")]
        public async Task<IActionResult> GetMattersForBilling(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var matters = await _context.Matters
                .Where(m => m.OrganizationId == orgId && !m.IsDeleted)
                .Select(m => new
                {
                    m.Id,
                    m.Title,

                    // Matter.Client is a USER (not an Organization).
                    ClientUserId = m.ClientId,
                    ClientUserName = m.Client != null ? (m.Client.FirstName + " " + m.Client.LastName) : null,

                    // Billing.ClientId expects an Organization, so infer the client's primary Organization from the client user.
                    ClientOrganizationId = m.Client != null
                        ? (m.Client.UserOrganizations
                              .Where(uo => uo.IsActive && uo.IsPrimary)
                              .Select(uo => (int?)uo.OrganizationId)
                              .FirstOrDefault()
                           ?? m.Client.UserOrganizations
                              .Where(uo => uo.IsActive)
                              .Select(uo => (int?)uo.OrganizationId)
                              .FirstOrDefault())
                        : null,
                    ClientOrganizationName = m.Client != null
                        ? (m.Client.UserOrganizations
                              .Where(uo => uo.IsActive && uo.IsPrimary)
                              .Select(uo => uo.Organization.Name)
                              .FirstOrDefault()
                           ?? m.Client.UserOrganizations
                              .Where(uo => uo.IsActive)
                              .Select(uo => uo.Organization.Name)
                              .FirstOrDefault())
                        : null
                })
                .ToListAsync();

            return Ok(new { success = true, data = matters });
        }

        // GET: /api/billing/{orgId}/clients
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/api/billing/{orgId:int}/clients")]
        public async Task<IActionResult> GetClientsForBilling(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var now = DateTime.UtcNow;

            // Primary source: organization relationships (LawFirmClient / EventPlannerClient etc.)
            var clients = await _context.OrganizationRelationships
                .Where(rel =>
                    rel.SourceOrganizationId == orgId &&
                    rel.IsActive &&
                    !rel.IsDeleted &&
                    (!rel.ExpiresAt.HasValue || rel.ExpiresAt.Value > now) &&
                    Certio.Domain.Organizations.RelationshipTypes.ServiceProviderClientTypes.Contains(rel.RelationshipType))
                .Select(rel => new
                {
                    rel.TargetOrganizationId,
                    Name = rel.TargetOrganization.Name
                })
                .Distinct()
                .OrderBy(x => x.Name)
                .ToListAsync();

            if (clients.Count == 0)
            {
                // Fallback: include the current organization as the only client option.
                var self = await _context.Organizations
                    .Where(o => o.Id == orgId)
                    .Select(o => new { o.Id, o.Name })
                    .FirstOrDefaultAsync();

                if (self == null)
                {
                    return Ok(new { success = true, data = Array.Empty<object>() });
                }

                return Ok(new
                {
                    success = true,
                    data = new[]
                    {
                        new
                        {
                            Id = self.Id,
                            Name = self.Name,
                            Initials = !string.IsNullOrWhiteSpace(self.Name) ? self.Name.Substring(0, 1).ToUpperInvariant() : "C"
                        }
                    }
                });
            }

            return Ok(new
            {
                success = true,
                data = clients.Select(c => new
                {
                    Id = c.TargetOrganizationId,
                    c.Name,
                    Initials = !string.IsNullOrWhiteSpace(c.Name) ? c.Name.Substring(0, 1).ToUpperInvariant() : "C"
                })
            });
        }

        // GET: /api/billing/{orgId}/assignees
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/api/billing/{orgId:int}/assignees")]
        public async Task<IActionResult> GetAssigneesForBilling(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null) return Unauthorized();

            var members = await _context.UserOrganizations
                .Include(uo => uo.User)
                .Where(uo => uo.OrganizationId == orgId)
                .Select(uo => new { 
                    uo.User!.Id, 
                    Name = uo.User.FirstName + " " + uo.User.LastName,
                    Initials = uo.User.FirstName.Substring(0, 1) + uo.User.LastName.Substring(0, 1),
                    Email = uo.User.Email
                })
                .ToListAsync();

            return Ok(new { success = true, data = members });
        }

        #endregion

        private (User? user, int? organizationId) GetUserContext()
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            var orgId = HttpContext.Items["OrganizationId"] as int?;
            return (customUser, orgId);
        }
    }

    #region DTOs

    public class TimeEntryDto
    {
        public int? MatterId { get; set; }
        public int? ClientId { get; set; }
        public int? AssigneeId { get; set; }
        public DateTime? Date { get; set; }
        public string? Description { get; set; }
        public decimal Hours { get; set; }
        public decimal Rate { get; set; }
        public bool IsBillable { get; set; } = true;
        public TimeEntryStatus? Status { get; set; }
    }

    public class ExpenseDto
    {
        public int? MatterId { get; set; }
        public int? ClientId { get; set; }
        public int? AssigneeId { get; set; }
        public DateTime? Date { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public decimal Amount { get; set; }
        public bool IsBillable { get; set; } = true;
        public ExpenseStatus? Status { get; set; }
    }

    public class InvoiceDto
    {
        public int? MatterId { get; set; }
        public int? ClientId { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Notes { get; set; }
        public string? Terms { get; set; }
        public decimal TaxAmount { get; set; }
        public InvoiceStatus? Status { get; set; }
        public decimal? PaidAmount { get; set; }
        public DateTime? PaidDate { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
    }

    public class RetainerDto
    {
        public int? MatterId { get; set; }
        public int? ClientId { get; set; }
        public decimal InitialAmount { get; set; }
        public string? Notes { get; set; }
        public RetainerStatus? Status { get; set; }
    }

    public class RetainerTransactionDto
    {
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public int? InvoiceId { get; set; }
    }

    #endregion
}

