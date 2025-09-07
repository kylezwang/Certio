using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Services;
using System.Security.Claims;
using Certio.Domain.Services;

namespace Certio.Web.Controllers;

[Authorize]
public class ChatController : Controller
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var conversations = await _chatService.GetUserConversationsAsync(userId ?? "");
        return View(conversations);
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var conversations = await _chatService.GetUserConversationsAsync(userId ?? "");
            return Json(conversations);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> Conversation(string id)
    {
        var messages = await _chatService.GetConversationMessagesAsync(id);
        return View(messages);
    }

    [HttpPost]
    public async Task<IActionResult> CreateConversation(string title, string description)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "test-user-123";
        var safeDescription = string.IsNullOrEmpty(description) ? "No description provided" : description;
        var conversation = await _chatService.CreateConversationAsync(title, safeDescription, userId);
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage(string conversationId, string content, string messageType = "Text")
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "test-user-123";
        var userType = User.FindFirstValue("UserType") ?? "Client";
        
        await _chatService.SendMessageAsync(conversationId, userId, userType, content, messageType);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> RequestClarity([FromBody] ClarityRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "test-user-123";
        var userType = User.FindFirstValue("UserType") ?? "Client";
        
        var clarity = await _chatService.RequestClarityAsync(request.ConversationId, request.Text, userType);
        return Json(clarity);
    }

    [HttpGet]
    public async Task<IActionResult> GetMessages(string id)
    {
        var messages = await _chatService.GetConversationMessagesAsync(id);
        return Json(messages);
    }

    [HttpPost]
    public async Task<IActionResult> GetSuggestions([FromBody] SuggestionRequest request)
    {
        var messages = await _chatService.GetConversationMessagesAsync(request.ConversationId);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "test-user-123";
        var userType = User.FindFirstValue("UserType") ?? "Client";
        
        var suggestions = await _chatService.GetReplySuggestionsAsync(request.ConversationId, messages, userType);
        return Json(suggestions);
    }

    [HttpPost]
    public async Task<IActionResult> GenerateAIResponse([FromBody] AIResponseRequest request)
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

    [HttpGet]
    public async Task<IActionResult> GetAIInsights(string conversationId)
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

    [HttpGet]
    public async Task<IActionResult> GetConversationSummary(string conversationId)
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

    [HttpGet]
    public async Task<IActionResult> GetClientGoals(string conversationId)
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

    [HttpGet]
    public async Task<IActionResult> GetLatestReplySuggestion(string conversationId)
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

    [HttpPost]
    public async Task<IActionResult> RenameConversation([FromBody] RenameConversationRequest request)
    {
        try
        {
            var success = await _chatService.RenameConversationAsync(request.ConversationId, request.NewTitle);
            return Json(new { success = success, error = success ? null : "Failed to rename conversation" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteConversation([FromBody] DeleteConversationRequest request)
    {
        try
        {
            var success = await _chatService.DeleteConversationAsync(request.ConversationId);
            return Json(new { success = success, error = success ? null : "Failed to delete conversation" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }
}

public class ClarityRequest
{
    public string ConversationId { get; set; } = "";
    public string Text { get; set; } = "";
}

public class SuggestionRequest
{
    public string ConversationId { get; set; } = "";
}

public class AIResponseRequest
{
    public string ConversationId { get; set; } = "";
    public string UserMessage { get; set; } = "";
}

public class RenameConversationRequest
{
    public string ConversationId { get; set; } = "";
    public string NewTitle { get; set; } = "";
}

public class DeleteConversationRequest
{
    public string ConversationId { get; set; } = "";
}
