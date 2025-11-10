using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Services;
using Certio.Application.Interfaces;
using Certio.Web.Security;
using Certio.Web.Services;
using System.Security.Claims;
using Certio.Domain.Services;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers;

[Authorize(Policy = "OrgMember")]
[Route("Client/{orgId}/Chat")]
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly IOrganizationContextService _orgContextService;
    private readonly ILogger<ChatController> _logger;
    private readonly ApplicationDbContext _context;

    public ChatController(
        IChatService chatService,
        IOrganizationContextService orgContextService,
        ILogger<ChatController> logger,
        ApplicationDbContext context)
    {
        _chatService = chatService;
        _orgContextService = orgContextService;
        _logger = logger;
        _context = context;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(int orgId)
    {
        var userId = GetCurrentUserId();
        var conversations = await _chatService.GetUserConversationsAsync(userId, orgId);
        return View(conversations);
    }

    [HttpGet("GetConversations")]
    public async Task<IActionResult> GetConversations(int orgId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var conversations = await _chatService.GetUserConversationsAsync(userId, orgId);
            return Json(conversations);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    // Returns only conversations that include AI-generated messages, for lightweight global panel usage
    [HttpGet("GetAIConversations")]
    public async Task<IActionResult> GetAIConversations(int orgId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var conversations = await _chatService.GetUserAIConversationsAsync(userId, orgId);
            return Json(conversations);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("Conversation/{id}")]
    public async Task<IActionResult> Conversation(int orgId, int id)
    {
        var messages = await _chatService.GetConversationMessagesAsync(id);
        return View(messages);
    }

    [HttpPost("CreateConversation")]
    public async Task<IActionResult> CreateConversation(int orgId, string title, string description)
    {
        try
        {
            var userId = GetCurrentUserId();
            var safeDescription = string.IsNullOrEmpty(description) ? "No description provided" : description;
            var conversation = await _chatService.CreateConversationAsync(orgId, userId, title, safeDescription);
            
            // Return JSON for AJAX requests
            if (Request.Headers["Content-Type"].ToString().Contains("application/x-www-form-urlencoded"))
            {
                return Json(new { success = true, conversationId = conversation.Id, title = conversation.Title });
            }
            
            // Redirect for form submissions
            return RedirectToAction("Index", new { orgId });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("SendMessage")]
    public async Task<IActionResult> SendMessage(int orgId, int conversationId, string content, string messageType = "Text")
    {
        var userId = GetCurrentUserId();
        var userType = GetCurrentUserType();
        
        await _chatService.SendMessageAsync(conversationId, userId, userType, content, messageType);
        return Json(new { success = true });
    }

    [HttpPost("RequestClarity")]
    public async Task<IActionResult> RequestClarity(int orgId, [FromBody] ClarityRequest request)
    {
        var userType = GetCurrentUserType();
        
        var clarity = await _chatService.RequestClarityAsync(request.ConversationId, request.Text, userType);
        return Json(clarity);
    }

    [HttpGet("GetMessages/{id}")]
    public async Task<IActionResult> GetMessages(int orgId, int id)
    {
        var messages = await _chatService.GetConversationMessagesAsync(id);
        return Json(messages);
    }

    [HttpPost("channel/{channelId}/read")]
    [Authorize(Policy = "OrgMember")]
    public async Task<IActionResult> MarkChannelAsRead(int orgId, int channelId)
    {
        try
        {
            // Input validation
            if (!InputValidator.IsValidId(channelId) || !InputValidator.IsValidId(orgId))
            {
                _logger.LogWarning("Invalid channel or org ID in MarkChannelAsRead: channel={ChannelId}, org={OrgId}", channelId, orgId);
                return Json(new { success = false, error = "Invalid parameters" });
            }

            // Get current user
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for MarkChannelAsRead");
                return Json(new { success = false, error = "User not authenticated" });
            }

            var userId = customUser.Id;

            // Validate user has access to the organization
            var canAccess = await _orgContextService.ValidateUserInOrganizationAsync(userId, orgId);
            if (!canAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to mark channel {ChannelId} as read in unauthorized org {OrgId}", 
                    userId, channelId, orgId);
                return Json(new { success = false, error = "Access denied" });
            }

            // Mark all unread messages in this channel as read for this user
            // The unread count logic works by finding the last message the user sent that is marked as read,
            // then counting messages from other users after that time.
            // So we create a "read receipt" by marking the latest message the user sent as read,
            // or if the user hasn't sent any messages, we create a system read receipt message.
            
            var latestUserMessage = await _context.ChatMessages
                .Where(m => m.ConversationId == channelId && m.UserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestUserMessage != null)
            {
                // Mark the user's latest message as read (this acts as a read receipt)
                latestUserMessage.IsRead = true;
            }
            else
            {
                // If user hasn't sent any messages, create a system read receipt message
                // This ensures the unread count logic works correctly
                var readReceipt = new ChatMessage
                {
                    ConversationId = channelId,
                    UserId = userId,
                    Content = "", // Empty content for read receipt
                    Sender = "System",
                    MessageType = "System",
                    SenderType = "System",
                    IsFromUser = false,
                    IsRead = true, // Mark as read immediately
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.ChatMessages.Add(readReceipt);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} marked channel {ChannelId} as read", userId, channelId);

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking channel {ChannelId} as read", channelId);
            return Json(new { success = false, error = "An error occurred" });
        }
    }

    [HttpGet("channel/{channelId}/messages")]
    [Authorize(Policy = "OrgMember")]
    public async Task<IActionResult> GetChannelMessages(int orgId, int channelId)
    {
        try
        {
            // Input validation
            if (!InputValidator.IsValidId(channelId) || !InputValidator.IsValidId(orgId))
            {
                _logger.LogWarning("Invalid channel or org ID in GetChannelMessages: channel={ChannelId}, org={OrgId}", channelId, orgId);
                return Json(new { success = false, error = "Invalid parameters" });
            }

            // Get current user
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for GetChannelMessages");
                return Json(new { success = false, error = "User not authenticated" });
            }

            // Validate user has access to the organization
            var canAccess = await _orgContextService.ValidateUserInOrganizationAsync(customUser.Id, orgId);
            if (!canAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to access channel {ChannelId} in unauthorized org {OrgId}", 
                    customUser.Id, channelId, orgId);
                return Json(new { success = false, error = "Access denied" });
            }

            var messages = await _chatService.GetChannelMessagesAsync(channelId);
            
            // Transform to match expected format
            var formattedMessages = messages.Select(m => new
            {
                m.Id,
                m.ConversationId,
                m.UserId,
                User = m.User?.FirstName + " " + m.User?.LastName,
                SenderName = m.User?.FirstName + " " + m.User?.LastName,
                m.Content,
                m.CreatedAt,
                m.IsEdited,
                m.EditedAt,
                m.Reactions,
                m.IsFromAI,
                m.MessageType,
                m.ReplyToMessageId,
                Time = m.CreatedAt.ToString("h:mm tt")
            }).ToList();

            return Json(formattedMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving channel messages for channel {ChannelId}", channelId);
            return Json(new { success = false, error = "An error occurred" });
        }
    }

    [HttpPost("GetSuggestions")]
    public async Task<IActionResult> GetSuggestions(int orgId, [FromBody] SuggestionRequest request)
    {
        var messages = await _chatService.GetConversationMessagesAsync(request.ConversationId);
        var userType = GetCurrentUserType();
        
        var suggestions = await _chatService.GetReplySuggestionsAsync(request.ConversationId, messages, userType);
        return Json(suggestions);
    }

    [HttpPost("GenerateAIResponse")]
    public async Task<IActionResult> GenerateAIResponse(int orgId, [FromBody] AIResponseRequest request)
    {
        try
        {
            var aiMessage = await _chatService.GenerateAIResponseAsync(request.ConversationId, request.UserMessage);
            return Json(new { success = true, message = aiMessage });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("GenerateAIResponseStream")]
    public async Task GenerateAIResponseStream(int orgId, [FromBody] AIResponseRequest request)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var chunk in _chatService.GenerateAIResponseStreamAsync(request.ConversationId, request.UserMessage))
            {
                var data = $"data: {System.Text.Json.JsonSerializer.Serialize(new { content = chunk, done = false })}\n\n";
                await Response.WriteAsync(data);
                await Response.Body.FlushAsync();
            }
            
            // Send completion signal
            var doneData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { content = "", done = true })}\n\n";
            await Response.WriteAsync(doneData);
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming AI response for conversation {ConversationId}", request.ConversationId);
            var errorData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { content = "An error occurred while generating the response.", done = true, error = true })}\n\n";
            await Response.WriteAsync(errorData);
            await Response.Body.FlushAsync();
        }
    }

    [HttpGet("GetAIInsights/{conversationId}")]
    public async Task<IActionResult> GetAIInsights(int orgId, int conversationId)
    {
        try
        {
            var insights = await _chatService.GetAIInsightsAsync(conversationId);
            return Json(new { success = true, insights = insights });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("GetConversationSummary/{conversationId}")]
    public async Task<IActionResult> GetConversationSummary(int orgId, int conversationId)
    {
        try
        {
            var summary = await _chatService.GetConversationSummaryAsync(conversationId);
            return Json(new { success = true, summary = summary });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("GetClientGoals/{conversationId}")]
    public async Task<IActionResult> GetClientGoals(int orgId, int conversationId)
    {
        try
        {
            var goals = await _chatService.GetClientGoalsAsync(conversationId);
            return Json(new { success = true, goals = goals });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("GetLatestReplySuggestion/{conversationId}")]
    public async Task<IActionResult> GetLatestReplySuggestion(int orgId, int conversationId)
    {
        try
        {
            var suggestion = await _chatService.GetLatestReplySuggestionAsync(conversationId);
            return Json(new { success = true, suggestion = suggestion });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("RenameConversation")]
    public async Task<IActionResult> RenameConversation(int orgId, [FromBody] RenameConversationRequest request)
    {
        try
        {
            var success = await _chatService.RenameConversationAsync(request.ConversationId, orgId, request.NewTitle);
            return Json(new { success = success, error = success ? null : "Failed to rename conversation" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("DeleteConversation")]
    public async Task<IActionResult> DeleteConversation(int orgId, [FromBody] DeleteConversationRequest request)
    {
        try
        {
            // Input validation
            if (!InputValidator.IsValidId(request.ConversationId) || !InputValidator.IsValidId(orgId))
            {
                _logger.LogWarning("Invalid conversation or org ID in DeleteConversation: conv={ConvId}, org={OrgId}", request.ConversationId, orgId);
                return Json(new { success = false, error = "Invalid parameters" });
            }

            // Get current user
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for DeleteConversation");
                return Json(new { success = false, error = "User not authenticated" });
            }

            // Validate user has access to this conversation
            var canAccess = await _chatService.CanUserAccessConversationAsync(request.ConversationId, customUser.Id, orgId);
            if (!canAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to delete unauthorized conversation {ConvId}", 
                    customUser.Id, request.ConversationId);
                return Json(new { success = false, error = "Access denied" });
            }

            // Delete conversation (audit logging handled in service)
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            var success = await _chatService.DeleteConversationAsync(request.ConversationId, orgId, customUser.Id, ipAddress, userAgent);

            if (success)
            {
                _logger.LogInformation("User {UserId} deleted conversation {ConvId} in org {OrgId}", 
                    customUser.Id, request.ConversationId, orgId);
            }

            return Json(new { success = success, error = success ? null : "Failed to delete conversation" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation {ConvId}", request.ConversationId);
            return Json(new { success = false, error = "An error occurred" });
        }
    }

    // Helper methods
    private int GetCurrentUserId()
    {
        // First try to get the custom user ID from context (set by UserSyncMiddleware)
        if (HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int userId)
        {
            return userId;
        }
        
        // Fallback to claims (for backwards compatibility)
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out int claimUserId))
        {
            return claimUserId;
        }
        
        throw new UnauthorizedAccessException("Invalid user ID");
    }

    private string GetCurrentUserType()
    {
        return User.FindFirstValue("UserType") ?? "Client";
    }
}

public class ClarityRequest
{
    public int ConversationId { get; set; }
    public string Text { get; set; } = "";
}

public class SuggestionRequest
{
    public int ConversationId { get; set; }
}

public class AIResponseRequest
{
    public int ConversationId { get; set; }
    public string UserMessage { get; set; } = "";
}

public class RenameConversationRequest
{
    public int ConversationId { get; set; }
    public string NewTitle { get; set; } = "";
}

public class DeleteConversationRequest
{
    public int ConversationId { get; set; }
}
