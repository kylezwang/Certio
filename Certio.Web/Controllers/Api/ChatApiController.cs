using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Services;
using Certio.Application.Interfaces;
using System.Security.Claims;
using Certio.Domain.Services;
using Certio.Web.Security;
using Certio.Domain.Users;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/chat")]
[Authorize(Policy = "OrgMember")] // PHASE 3: Added proper authorization
[RequirePermission(Permission.ViewMessages)] // PHASE 3: Require message viewing permission
public class ChatApiController : Controller
{
    private readonly IChatService _chatService;

    public ChatApiController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpGet("channel/{channelId}/messages")]
    public async Task<IActionResult> GetChannelMessages(int channelId)
    {
        try
        {
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
            return Json(new { success = false, error = ex.Message });
        }
    }
}
