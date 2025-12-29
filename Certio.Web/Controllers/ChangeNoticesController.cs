using Certio.Application.DTOs.ChangeControl;
using Certio.Application.Interfaces;
using Certio.Domain.Users;
using Certio.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace Certio.Web.Controllers;

[Authorize(Policy = "OrgMember")]
[Route("Client/{orgId:int}/Matter/{matterId:int}/ChangeNotices")]
public sealed class ChangeNoticesController : Controller
{
    private readonly IChangeNoticeService _changeNoticeService;
    private readonly ILogger<ChangeNoticesController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _env;

    public ChangeNoticesController(
        IChangeNoticeService changeNoticeService,
        ILogger<ChangeNoticesController> logger,
        IConfiguration configuration,
        IHostEnvironment env)
    {
        _changeNoticeService = changeNoticeService;
        _logger = logger;
        _configuration = configuration;
        _env = env;
    }

    [HttpGet("Summary")]
    public async Task<IActionResult> Summary(int orgId, int matterId, CancellationToken ct)
    {
        var dto = await _changeNoticeService.GetSummaryAsync(orgId, matterId, ct);
        return Json(dto);
    }

    [HttpGet("{changeNoticeId:int}")]
    public async Task<IActionResult> Get(int orgId, int matterId, int changeNoticeId, CancellationToken ct)
    {
        var dto = await _changeNoticeService.GetByIdAsync(orgId, matterId, changeNoticeId, ct);
        if (dto == null)
        {
            return NotFound();
        }
        return Json(dto);
    }

    [HttpPut("{changeNoticeId:int}")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.EditMatters)]
    public async Task<IActionResult> Update(int orgId, int matterId, int changeNoticeId, [FromForm] CreateChangeNoticeRequest request, CancellationToken ct)
    {
        var user = HttpContext.Items["CustomUser"] as User;
        if (user == null)
        {
            return Unauthorized();
        }

        HttpContext.Items["CurrentOrganizationId"] = orgId;

        var dto = await _changeNoticeService.UpdateDraftAsync(orgId, matterId, changeNoticeId, user.Id, request, ct);
        return Json(dto);
    }

    [HttpDelete("{changeNoticeId:int}")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.EditMatters)]
    public async Task<IActionResult> Delete(int orgId, int matterId, int changeNoticeId, CancellationToken ct)
    {
        var user = HttpContext.Items["CustomUser"] as User;
        if (user == null)
        {
            return Unauthorized();
        }

        HttpContext.Items["CurrentOrganizationId"] = orgId;

        await _changeNoticeService.DeleteAsync(orgId, matterId, changeNoticeId, ct);
        return Json(new { success = true });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.EditMatters)]
    public async Task<IActionResult> Create(int orgId, int matterId, [FromForm] CreateChangeNoticeRequest request, CancellationToken ct)
    {
        var user = HttpContext.Items["CustomUser"] as User;
        if (user == null)
        {
            return Unauthorized();
        }

        HttpContext.Items["CurrentOrganizationId"] = orgId;

        var dto = await _changeNoticeService.CreateDraftAsync(orgId, matterId, user.Id, request, ct);
        return Json(dto);
    }

    [HttpPost("{changeNoticeId:int}/Send")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.EditMatters)]
    public async Task<IActionResult> Send(int orgId, int matterId, int changeNoticeId, CancellationToken ct)
    {
        var user = HttpContext.Items["CustomUser"] as User;
        if (user == null)
        {
            return Unauthorized();
        }

        HttpContext.Items["CurrentOrganizationId"] = orgId;

        var configuredBaseUrl = _configuration["App:PublicBaseUrl"];
        // Environment-aware:
        // - Development: use the current request host (localhost is fine)
        // - Production: prefer App:PublicBaseUrl (fallback to request host if unset)
        var requestBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var baseUrl = _env.IsDevelopment()
            ? requestBaseUrl
            : (!string.IsNullOrWhiteSpace(configuredBaseUrl) ? configuredBaseUrl.Trim() : requestBaseUrl);

        var dto = await _changeNoticeService.SendAsync(orgId, matterId, changeNoticeId, baseUrl, user.Id, ct);
        return Json(dto);
    }
}


