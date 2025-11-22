using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Certio.Infrastructure.Data;
using Certio.Web.Services;
using System.Linq;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/account-security")]
[Authorize]
public class AccountSecurityController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ITrustedDeviceService _trustedDeviceService;

    public AccountSecurityController(
        UserManager<IdentityUser> userManager,
        ApplicationDbContext context,
        SignInManager<IdentityUser> signInManager,
        ITrustedDeviceService trustedDeviceService)
    {
        _userManager = userManager;
        _context = context;
        _signInManager = signInManager;
        _trustedDeviceService = trustedDeviceService;
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

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var identityUser = await _userManager.GetUserAsync(User);
        if (identityUser == null)
        {
            return Unauthorized(new { success = false, error = "User not authenticated" });
        }

        var customUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == identityUser.Email);

        if (customUser == null)
        {
            return Unauthorized(new { success = false, error = "User not found" });
        }

        var sessions = await _trustedDeviceService.GetDevicesAsync(customUser.Id);
        var currentCookie = _trustedDeviceService.GetDeviceCookie(HttpContext);
        Guid? currentDeviceId = null;
        if (!string.IsNullOrWhiteSpace(currentCookie))
        {
            var parts = currentCookie.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && Guid.TryParse(parts[0], out var parsed))
            {
                currentDeviceId = parsed;
            }
        }

        var responseSessions = sessions.Select(session => new
        {
            id = session.Id,
            deviceName = session.DeviceName,
            deviceType = session.DeviceType,
            operatingSystem = session.OperatingSystem,
            browser = session.Browser,
            location = session.LastKnownLocation ?? "Unknown location",
            ipAddress = session.IpAddress,
            isTrusted = session.IsTrusted,
            isCurrent = currentDeviceId.HasValue && session.Id == currentDeviceId.Value,
            createdAtUtc = session.CreatedAt,
            lastSeenAtUtc = session.LastSeenAt
        }).ToList();

        return Ok(new { success = true, sessions = responseSessions });
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId)
    {
        var identityUser = await _userManager.GetUserAsync(User);
        if (identityUser == null)
        {
            return Unauthorized(new { success = false, error = "User not authenticated" });
        }

        var customUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == identityUser.Email);

        if (customUser == null)
        {
            return Unauthorized(new { success = false, error = "User not found" });
        }

        var currentCookie = _trustedDeviceService.GetDeviceCookie(HttpContext);
        var isCurrent = !string.IsNullOrWhiteSpace(currentCookie) &&
                        currentCookie.StartsWith(sessionId.ToString("N"), StringComparison.OrdinalIgnoreCase);

        var success = await _trustedDeviceService.RevokeDeviceAsync(customUser.Id, sessionId, HttpContext);
        if (!success)
        {
            return NotFound(new { success = false, error = "Session not found" });
        }

        if (isCurrent)
        {
            await _signInManager.SignOutAsync();
            Response.Cookies.Delete("CertioAuth");
            Response.Cookies.Delete(".AspNetCore.Identity.Application");
            Response.Cookies.Delete(".AspNetCore.Antiforgery");
            Response.Cookies.Delete(TrustedDeviceService.DeviceCookieName);

            try
            {
                HttpContext.Session.Clear();
            }
            catch (InvalidOperationException)
            {
                // Session not configured
            }
        }

        return Ok(new { success = true, current = isCurrent, redirectUrl = isCurrent ? Url.Action("Index", "Home") : null });
    }

    [HttpPost("sessions/signout-all")]
    public async Task<IActionResult> SignOutAllSessions()
    {
        var identityUser = await _userManager.GetUserAsync(User);
        if (identityUser == null)
        {
            return Unauthorized(new { success = false, error = "User not authenticated" });
        }

        var customUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == identityUser.Email);

        if (customUser == null)
        {
            return Unauthorized(new { success = false, error = "User not found" });
        }

        await _trustedDeviceService.RevokeAllDevicesAsync(customUser.Id, HttpContext);

        await _signInManager.SignOutAsync();
        Response.Cookies.Delete("CertioAuth");
        Response.Cookies.Delete(".AspNetCore.Identity.Application");
        Response.Cookies.Delete(".AspNetCore.Antiforgery");
        Response.Cookies.Delete(TrustedDeviceService.DeviceCookieName);

        try
        {
            HttpContext.Session.Clear();
        }
        catch (InvalidOperationException)
        {
            // Session not configured
        }

        return Ok(new { success = true, redirectUrl = Url.Action("Index", "Home") });
    }

}

