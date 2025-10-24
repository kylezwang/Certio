using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Application.Interfaces;

namespace Certio.Web.Controllers
{
    [Authorize(Policy = "OrgMember")]
    public class SettingsController : Controller
    {
        private readonly IOrganizationService _organizationService;

        public SettingsController(IOrganizationService organizationService)
        {
            _organizationService = organizationService;
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
}
