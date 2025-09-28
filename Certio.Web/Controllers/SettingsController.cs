using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Certio.Web.Controllers
{
    [Authorize(Policy = "OrgMember")]
    public class SettingsController : Controller
    {
        public IActionResult Index()
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
