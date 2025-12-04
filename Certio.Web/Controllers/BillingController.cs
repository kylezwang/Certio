using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Application.Interfaces;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers
{
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITeamService _teamService;
        private readonly IMatterService _matterService;
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            ApplicationDbContext context,
            ITeamService teamService,
            IMatterService matterService,
            ILogger<BillingController> logger)
        {
            _context = context;
            _teamService = teamService;
            _matterService = matterService;
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

            // TODO: Implement actual time entries retrieval
            // For now, return partial view with placeholder data
            return PartialView("~/Views/Billing/_BillingTimeEntries.cshtml");
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

            // TODO: Implement actual expenses retrieval
            // For now, return partial view with placeholder data
            return PartialView("~/Views/Billing/_BillingExpenses.cshtml");
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

            // TODO: Implement actual overview data retrieval
            // For now, return partial view with stats cards
            return PartialView("~/Views/Billing/_BillingOverview.cshtml");
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

            // TODO: Implement actual trusts retrieval
            // For now, return partial view with placeholder data
            return PartialView("~/Views/Billing/_BillingTrusts.cshtml");
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

            // TODO: Implement actual invoices retrieval
            // For now, return partial view with placeholder data
            return PartialView("~/Views/Billing/_BillingInvoices.cshtml");
        }

        private (User? user, int? organizationId) GetUserContext()
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            var orgId = HttpContext.Items["OrganizationId"] as int?;
            return (customUser, orgId);
        }
    }
}

