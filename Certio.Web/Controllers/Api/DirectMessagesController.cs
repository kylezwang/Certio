using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;
using Certio.Web.Security;
using Certio.Domain.Users;
using System.Security.Claims;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/dm")]
[Authorize(Policy = "OrgMember")]
public class DirectMessagesController : ControllerBase
{
    private readonly IDirectMessageService _directMessageService;
    private readonly ILogger<DirectMessagesController> _logger;

    public DirectMessagesController(
        IDirectMessageService directMessageService,
        ILogger<DirectMessagesController> logger)
    {
        _directMessageService = directMessageService;
        _logger = logger;
    }

    /// <summary>
    /// Create or get existing thread with another user
    /// </summary>
    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequest request, [FromQuery] int orgId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var thread = await _directMessageService.GetOrCreateThreadAsync(orgId, currentUserId, request.OtherUserId);

            return Ok(new { success = true, thread });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to create thread in org {OrgId}", orgId);
            return Unauthorized(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating thread in org {OrgId}", orgId);
            return StatusCode(500, new { success = false, error = "Failed to create thread" });
        }
    }

    /// <summary>
    /// List all direct message threads for current user
    /// </summary>
    [HttpGet("threads")]
    public async Task<IActionResult> ListThreads([FromQuery] int orgId, [FromQuery] int take = 30, [FromQuery] string? cursor = null)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var threads = await _directMessageService.ListThreadsAsync(orgId, currentUserId, take, cursor);

            return Ok(new { success = true, threads });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to list threads in org {OrgId}", orgId);
            return Unauthorized(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing threads in org {OrgId}", orgId);
            return StatusCode(500, new { success = false, error = "Failed to list threads" });
        }
    }

    /// <summary>
    /// Get messages from a thread
    /// </summary>
    [HttpGet("threads/{threadId}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid threadId,
        [FromQuery] int orgId,
        [FromQuery] int take = 50,
        [FromQuery] string? cursor = null)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var result = await _directMessageService.GetMessagesAsync(orgId, currentUserId, threadId, take, cursor);

            return Ok(new
            {
                success = true,
                messages = result.Items,
                nextCursor = result.NextCursor,
                hasMore = result.HasMore
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to read messages in thread {ThreadId}", threadId);
            return Unauthorized(new { success = false, error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Thread {ThreadId} not found", threadId);
            return NotFound(new { success = false, error = "Thread not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages from thread {ThreadId}", threadId);
            return StatusCode(500, new { success = false, error = "Failed to get messages" });
        }
    }

    /// <summary>
    /// Mark a thread as read
    /// </summary>
    [HttpPost("threads/{threadId}/read")]
    public async Task<IActionResult> MarkRead(Guid threadId, [FromBody] MarkReadRequest request, [FromQuery] int orgId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            await _directMessageService.MarkReadAsync(orgId, currentUserId, threadId, request.ReadAt);

            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to mark thread {ThreadId} as read", threadId);
            return Unauthorized(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking thread {ThreadId} as read", threadId);
            return StatusCode(500, new { success = false, error = "Failed to mark as read" });
        }
    }

    private int GetCurrentUserId()
    {
        // First try to get the custom user ID from context (set by UserSyncMiddleware)
        if (HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int userId)
        {
            return userId;
        }
        
        // Fallback to claims (for backwards compatibility)
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var claimUserId))
        {
            return claimUserId;
        }
        
        throw new UnauthorizedAccessException("User ID not found in context or claims");
    }
}

