using System;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Certio.Web.Controllers;

[Authorize(Policy = "OrgMember")]
[ApiController]
[Route("api/documents/{documentId:guid}/embed")]
public class DocumentEmbedController : ControllerBase
{
    private readonly IDocumentEmbedService _embedService;

    public DocumentEmbedController(IDocumentEmbedService embedService)
    {
        _embedService = embedService;
    }

    [HttpGet]
    public async Task<IActionResult> GetEmbedUrl([FromRoute] Guid documentId, [FromQuery] Guid orgId, CancellationToken cancellationToken)
    {
        if (orgId == Guid.Empty)
        {
            return BadRequest("orgId is required");
        }

        var embedUrl = await _embedService.CreateEmbedUrlAsync(documentId, orgId, Guid.Empty, cancellationToken);
        return Ok(new { embedUrl });
    }
}

