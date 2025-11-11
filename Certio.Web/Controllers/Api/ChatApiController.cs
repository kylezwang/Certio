using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Services;
using Certio.Application.Interfaces;
using System.Security.Claims;
using Certio.Domain.Services;
using Certio.Web.Security;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/chat")]
[Authorize(Policy = "OrgMember")] // PHASE 3: Added proper authorization
[RequirePermission(Permission.ViewMessages)] // PHASE 3: Require message viewing permission
public class ChatApiController : Controller
{
    private readonly IChatService _chatService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ChatApiController> _logger;

    public ChatApiController(IChatService chatService, ApplicationDbContext context, ILogger<ChatApiController> logger)
    {
        _chatService = chatService;
        _context = context;
        _logger = logger;
    }

    [HttpGet("channel/{channelId}/messages")]
    public async Task<IActionResult> GetChannelMessages(int channelId)
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            // Get conversation to determine organization
            var conversation = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == channelId);
            
            if (conversation == null)
            {
                return NotFound(new { success = false, error = "Channel not found." });
            }

            // Security: Verify user has access to this channel
            var hasAccess = await _chatService.CanUserAccessConversationAsync(channelId, userId, conversation.OrganizationId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to access channel {ChannelId} without permission in org {OrgId}", 
                    userId, channelId, conversation.OrganizationId);
                return Forbid();
            }

            var messages = await _chatService.GetChannelMessagesAsync(channelId);
            
            // Transform to match expected format
            var formattedMessages = messages.Select(m => {
                var fullName = "";
                if (m.User != null && !string.IsNullOrEmpty(m.User.FirstName) && !string.IsNullOrEmpty(m.User.LastName))
                {
                    fullName = $"{m.User.FirstName} {m.User.LastName}".Trim();
                }
                else if (m.User != null && !string.IsNullOrEmpty(m.User.FirstName))
                {
                    fullName = m.User.FirstName;
                }
                else if (m.User != null && !string.IsNullOrEmpty(m.User.LastName))
                {
                    fullName = m.User.LastName;
                }
                else if (!string.IsNullOrEmpty(m.Sender))
                {
                    fullName = m.Sender; // Fallback to Sender field
                }
                else
                {
                    fullName = "Unknown User";
                }

                return new
                {
                    m.Id,
                    m.ConversationId,
                    m.UserId,
                    User = fullName,
                    SenderName = fullName,
                    m.Content,
                    m.CreatedAt,
                    m.IsEdited,
                    m.EditedAt,
                    m.Reactions,
                    m.IsFromAI,
                    m.MessageType,
                    m.ReplyToMessageId,
                    Time = m.CreatedAt.ToString("h:mm tt")
                };
            }).ToList();

            return Json(formattedMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving channel messages for channel {ChannelId}", channelId);
            return Json(new { success = false, error = "An error occurred while retrieving messages." });
        }
    }
}
