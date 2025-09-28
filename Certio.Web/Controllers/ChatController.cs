using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Services;
using System.Security.Claims;
using Certio.Domain.Services;

namespace Certio.Web.Controllers;

[Authorize(Policy = "OrgMember")]
[Route("Client/{orgId}/Chat")]
public class ChatController : Controller
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
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
            var success = await _chatService.DeleteConversationAsync(request.ConversationId, orgId);
            return Json(new { success = success, error = success ? null : "Failed to delete conversation" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
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
