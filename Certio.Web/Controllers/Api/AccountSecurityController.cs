using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Certio.Infrastructure.Data;
using System.Linq;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/account-security")]
[Authorize]
public class AccountSecurityController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _context;

    public AccountSecurityController(
        UserManager<IdentityUser> userManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet("mfa/status")]
    public async Task<IActionResult> GetMfaStatus()
    {
        var identityUser = await _userManager.GetUserAsync(User);
        if (identityUser == null)
        {
            return Unauthorized(new { success = false, error = "User not authenticated" });
        }

        // Get our custom User entity
        var customUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == identityUser.Email);
        
        if (customUser == null)
        {
            return Unauthorized(new { success = false, error = "User not found" });
        }

        return Ok(new { success = true, enabled = customUser.Enable2FA });
    }

    [HttpPost("mfa/toggle")]
    public async Task<IActionResult> ToggleMfa([FromBody] ToggleMfaRequest request)
    {
        var identityUser = await _userManager.GetUserAsync(User);
        if (identityUser == null)
        {
            return Unauthorized(new { success = false, error = "User not authenticated" });
        }

        // Get our custom User entity
        var customUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == identityUser.Email);
        
        if (customUser == null)
        {
            return Unauthorized(new { success = false, error = "User not found" });
        }

        // Update Enable2FA field
        customUser.Enable2FA = request.Enabled;
        customUser.LastModifiedDate = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        return Ok(new { success = true, enabled = request.Enabled });
    }

    public class ToggleMfaRequest
    {
        public bool Enabled { get; set; }
    }
}

