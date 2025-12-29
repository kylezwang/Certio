using Certio.Application.Interfaces;
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Certio.Web.Controllers;

[AllowAnonymous]
public sealed class PublicChangeNoticeController : Controller
{
    private readonly IChangeNoticeService _changeNoticeService;

    public PublicChangeNoticeController(IChangeNoticeService changeNoticeService)
    {
        _changeNoticeService = changeNoticeService;
    }

    [HttpGet("/public/change-notice/respond")]
    public async Task<IActionResult> Respond([FromQuery(Name = "t")] string token, CancellationToken ct)
    {
        var result = await _changeNoticeService.ProcessPublicResponseAsync(token, ct);
        return View("Respond", result);
    }

    [HttpPost("/public/change-notice/respond")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RespondClarification(string t, [FromForm] string clarificationNote, CancellationToken ct)
    {
        // Always process the response first (idempotent for our purposes)
        var initial = await _changeNoticeService.ProcessPublicResponseAsync(t, ct);
        if (!initial.Success)
        {
            return View("Respond", initial);
        }

        // Only accept clarification note for clarify action
        if (!string.Equals((initial.Action ?? "").Trim(), "clarify", StringComparison.OrdinalIgnoreCase))
        {
            return View("Respond", initial);
        }

        if (string.IsNullOrWhiteSpace(clarificationNote))
        {
            return View("Respond", new Certio.Application.DTOs.ChangeControl.PublicChangeNoticeResponseResult
            {
                Success = initial.Success,
                Message = "Please enter what you need clarified before submitting.",
                ChangeNoticeId = initial.ChangeNoticeId,
                RecipientId = initial.RecipientId,
                RecipientEmail = initial.RecipientEmail,
                HasNotalAccount = initial.HasNotalAccount,
                Action = initial.Action,
                NewRecipientStatus = initial.NewRecipientStatus,
                NewNoticeStatus = initial.NewNoticeStatus,
                ClarificationNoteSaved = false,
                ClarificationNoteMessage = "Not sent — please type what you need clarified, then press send."
            });
        }

        var saved = await _changeNoticeService.SavePublicClarificationNoteAsync(t, clarificationNote.Trim(), ct);
        return View("Respond", saved);
    }
}


