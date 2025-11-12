using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/account-security")]
[Authorize]
public class AccountSecurityController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;

    public AccountSecurityController(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("mfa/status")]
    public async Task<IActionResult> GetMfaStatus()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { success = false, error = "User not authenticated" });
        }

        var enabled = await _userManager.GetTwoFactorEnabledAsync(user);
        return Ok(new { success = true, enabled });
    }

    [HttpPost("mfa/toggle")]
    public async Task<IActionResult> ToggleMfa([FromBody] ToggleMfaRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { success = false, error = "User not authenticated" });
        }

        var result = await _userManager.SetTwoFactorEnabledAsync(user, request.Enabled);
        if (!result.Succeeded)
        {
            var error = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new { success = false, error });
        }

        return Ok(new { success = true, enabled = request.Enabled });
    }

    public class ToggleMfaRequest
    {
        public bool Enabled { get; set; }
    }
}

