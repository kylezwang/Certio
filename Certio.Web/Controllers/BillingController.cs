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

        private (User? user, int? organizationId) GetUserContext()
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            var orgId = HttpContext.Items["OrganizationId"] as int?;
            return (customUser, orgId);
        }
    }
}

