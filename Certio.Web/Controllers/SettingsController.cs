using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers
{
    [Authorize(Policy = "OrgMember")]
    public class SettingsController : Controller
    {
        private readonly IOrganizationService _organizationService;
        private readonly ApplicationDbContext _context;

        public SettingsController(
            IOrganizationService organizationService, 
            ApplicationDbContext context)
        {
            _organizationService = organizationService;
            _context = context;
        }

        [HttpGet("/Client/{orgId:int}/Settings")]
        public async Task<IActionResult> Index(int orgId, CancellationToken ct)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Get organization info to determine the type
            var orgResult = await _organizationService.GetOrganizationBasicInfoAsync(orgId, customUser.Id);
            
            ViewBag.OrganizationId = orgId;
            ViewBag.OrganizationName = orgResult.Success ? orgResult.Data!.Name : "Organization";
            ViewBag.OrganizationType = orgResult.Success ? orgResult.Data!.Type : Certio.Domain.Organizations.OrganizationType.Client;

            // Get full organization details from database context
            var orgDetailsResult = await _organizationService.GetOrganizationAsync(orgId, customUser.Id);
            ViewBag.OrganizationDetails = orgDetailsResult.Success ? orgDetailsResult.Data : null;

            // Get organization entity to access Settings for AI tier
            var orgEntity = await _context.Organizations.FindAsync(new object[] { orgId }, ct);
            ViewBag.AIModelTier = orgEntity?.GetAIModelTier() ?? Certio.Domain.Organizations.AIModelTier.Auto;

            return View();
        }

        public IActionResult AccountSettings()
        {
            return View();
        }

        public IActionResult Account()
        {
            return View();
        }

        public IActionResult Security()
        {
            return View();
        }

        public IActionResult Organization()
        {
            return View();
        }

        public IActionResult Notifications()
        {
            return View();
        }

        [HttpPost("/Client/{orgId:int}/Settings/UpdateAIModelTier")]
        public async Task<IActionResult> UpdateAIModelTier(int orgId, [FromBody] UpdateAIModelTierRequest request)
        {
            try
            {
                var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
                if (customUser == null)
                {
                    return Json(new { success = false, error = "User not authenticated" });
                }

                // Validate tier value
                if (!Enum.TryParse<Certio.Domain.Organizations.AIModelTier>(request.Tier, true, out var tier))
                {
                    return Json(new { success = false, error = "Invalid tier value" });
                }

                // Get organization
                var org = await _context.Organizations.FindAsync(new object[] { orgId });
                if (org == null)
                {
                    return Json(new { success = false, error = "Organization not found" });
                }

                // Update tier
                org.SetAIModelTier(tier);
                org.ModifiedAt = DateTime.UtcNow;
                org.ModifiedById = customUser.Id;

                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        public IActionResult Matters()
        {
            return View();
        }

        public IActionResult Documents()
        {
            return View();
        }

        public IActionResult Integrations()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Billing()
        {
            return View();
        }

        public IActionResult AI()
        {
            return View();
        }
    }

    public class UpdateAIModelTierRequest
    {
        public string Tier { get; set; } = "";
    }
}
