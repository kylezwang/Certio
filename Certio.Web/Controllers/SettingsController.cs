using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Certio.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers
{
    [Authorize(Policy = "OrgMember")]
    public class SettingsController : Controller
    {
        private readonly IOrganizationService _organizationService;
        private readonly ApplicationDbContext _context;
        private readonly IAIUsageService _aiUsageService;

        public SettingsController(
            IOrganizationService organizationService, 
            ApplicationDbContext context,
            IAIUsageService aiUsageService)
        {
            _organizationService = organizationService;
            _context = context;
            _aiUsageService = aiUsageService;
        }

        [HttpGet("/Client/{orgId:int}/Settings")]
        public async Task<IActionResult> Index(int orgId, CancellationToken ct)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.CurrentUserId = customUser.Id;
            ViewBag.CurrentUserName = $"{customUser.FirstName} {customUser.LastName}".Trim();
            ViewBag.UserTimeZone = string.IsNullOrWhiteSpace(customUser.TimeZone) ? "America/New_York" : customUser.TimeZone;

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

        /// <summary>
        /// Gets AI usage chart data for the current user
        /// </summary>
        [HttpGet("/Client/{orgId:int}/Settings/AIUsage")]
        public async Task<IActionResult> GetAIUsage(int orgId, [FromQuery] int days = 30, CancellationToken ct = default)
        {
            try
            {
                var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
                if (customUser == null)
                {
                    return Json(new { success = false, error = "User not authenticated" });
                }

                var chartData = await _aiUsageService.GetUsageChartDataAsync(customUser.Id, orgId, days, ct);
                
                return Json(new { 
                    success = true, 
                    data = new {
                        dataPoints = chartData.DataPoints.Select(dp => new {
                            date = dp.Date.ToString("yyyy-MM-dd"),
                            cost = dp.Cost,
                            calls = dp.Calls,
                            tokens = dp.Tokens
                        }),
                        totalCost = chartData.TotalCost,
                        totalCalls = chartData.TotalCalls,
                        totalTokens = chartData.TotalTokens,
                        costByModel = chartData.CostByModel,
                        startDate = chartData.StartDate.ToString("yyyy-MM-dd"),
                        endDate = chartData.EndDate.ToString("yyyy-MM-dd")
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Gets organization-wide AI usage statistics
        /// </summary>
        [HttpGet("/Client/{orgId:int}/Settings/AIUsage/Organization")]
        public async Task<IActionResult> GetOrganizationAIUsage(int orgId, [FromQuery] int days = 30, CancellationToken ct = default)
        {
            try
            {
                var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
                if (customUser == null)
                {
                    return Json(new { success = false, error = "User not authenticated" });
                }

                var stats = await _aiUsageService.GetOrganizationUsageStatsAsync(orgId, days, ct);
                
                return Json(new { 
                    success = true, 
                    data = new {
                        totalCost = stats.TotalCost,
                        totalCalls = stats.TotalCalls,
                        totalTokens = stats.TotalTokens,
                        averageDailyCost = stats.AverageDailyCost,
                        uniqueUsers = stats.UniqueUsers,
                        startDate = stats.StartDate.ToString("yyyy-MM-dd"),
                        endDate = stats.EndDate.ToString("yyyy-MM-dd")
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }

    public class UpdateAIModelTierRequest
    {
        public string Tier { get; set; } = "";
    }
}
