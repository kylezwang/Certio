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
using Azure;
using Azure.AI.DocumentIntelligence;

namespace Certio.Web.Controllers;

[Authorize(Policy = "OrgMember")]
[Route("Client/{orgId}/Chat")]
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly IOrganizationContextService _orgContextService;
    private readonly ILogger<ChatController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly DocumentIntelligenceClient? _documentIntelligenceClient;

    public ChatController(
        IChatService chatService,
        IOrganizationContextService orgContextService,
        ILogger<ChatController> logger,
        ApplicationDbContext context,
        DocumentIntelligenceClient? documentIntelligenceClient = null)
    {
        _chatService = chatService;
        _orgContextService = orgContextService;
        _logger = logger;
        _context = context;
        _documentIntelligenceClient = documentIntelligenceClient;
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
            _logger.LogError(ex, "Error creating conversation in org {OrgId}", orgId);
            return Json(new { success = false, error = "An error occurred while creating the conversation." });
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
            _logger.LogError(ex, "Error getting AI conversations for user in org {OrgId}", orgId);
            return Json(new { success = false, error = "An error occurred while retrieving conversations." });
        }
    }

    [HttpGet("Conversation/{id}")]
    public async Task<IActionResult> Conversation(int orgId, int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Security: Verify user has access to this conversation
            var hasAccess = await _chatService.CanUserAccessConversationAsync(id, userId, orgId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to access unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId, id, orgId);
                return Forbid();
            }
            
        var messages = await _chatService.GetConversationMessagesAsync(id);
        return View(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading conversation {ConversationId}", id);
            return StatusCode(500, "An error occurred while loading the conversation.");
        }
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
            _logger.LogError(ex, "Error creating conversation in org {OrgId}", orgId);
            return Json(new { success = false, error = "An error occurred while creating the conversation." });
        }
    }

    [HttpPost("SendMessage")]
    public async Task<IActionResult> SendMessage(int orgId, int conversationId, string content, string messageType = "Text", string? attachmentFileName = null, long? attachmentFileSize = null)
    {
        try
    {
        var userId = GetCurrentUserId();
        var userType = GetCurrentUserType();
            
            // Security: Verify user has access to this conversation
            var hasAccess = await _chatService.CanUserAccessConversationAsync(conversationId, userId, orgId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to send message to unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId, conversationId, orgId);
                return Json(new { success = false, error = "Access denied." });
            }
        
        // Prepare metadata with attachment info if present
        string? metadata = null;
        if (!string.IsNullOrEmpty(attachmentFileName) && attachmentFileSize.HasValue)
        {
            var metadataObj = new
            {
                attachmentFileName,
                attachmentFileSize = attachmentFileSize.Value
            };
            metadata = System.Text.Json.JsonSerializer.Serialize(metadataObj);
        }
        
        await _chatService.SendMessageAsync(conversationId, userId, userType, content, messageType, metadata);
        return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to conversation {ConversationId}", conversationId);
            return Json(new { success = false, error = "An error occurred while sending the message." });
        }
    }

    /// <summary>
    /// Uploads a file attachment and extracts text content using Azure Document Intelligence.
    /// Returns the extracted text that can be included in the AI conversation context.
    /// </summary>
    [HttpPost("UploadAttachment")]
    public async Task<IActionResult> UploadAttachment(int orgId, IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, error = "No file provided." });
            }

            // Validate file size (max 10MB)
            if (file.Length > 10 * 1024 * 1024)
            {
                return Json(new { success = false, error = "File size exceeds 10MB limit." });
            }

            // Validate file type
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt", ".png", ".jpg", ".jpeg", ".xlsx", ".xls", ".pptx", ".ppt" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return Json(new { success = false, error = $"File type '{extension}' is not supported." });
            }

            _logger.LogInformation("Processing chat attachment: {FileName} ({Size} bytes)", file.FileName, file.Length);

            string extractedContent;

            // For text files, read directly
            if (extension == ".txt")
            {
                using var reader = new StreamReader(file.OpenReadStream());
                extractedContent = await reader.ReadToEndAsync();
            }
            else if (_documentIntelligenceClient != null)
            {
                // Use Azure Document Intelligence for PDFs, images, Office docs
                extractedContent = await ExtractContentWithDocumentIntelligenceAsync(file);
            }
            else
            {
                _logger.LogWarning("Azure Document Intelligence not configured, returning file metadata only");
                extractedContent = $"[Attached file: {file.FileName} ({file.Length / 1024.0:F1} KB) - Content extraction not available]";
            }

            // Truncate if too long (keep first 50K characters for context)
            const int maxContentLength = 50000;
            if (extractedContent.Length > maxContentLength)
            {
                extractedContent = extractedContent.Substring(0, maxContentLength) + "\n\n[Content truncated due to length...]";
            }

            _logger.LogInformation("Extracted {Length} characters from attachment {FileName}", extractedContent.Length, file.FileName);

            return Json(new { 
                success = true, 
                fileName = file.FileName,
                fileSize = file.Length,
                extractedContent = extractedContent,
                contentLength = extractedContent.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing attachment {FileName}", file?.FileName);
            return Json(new { success = false, error = "An error occurred while processing the attachment." });
        }
    }

    private async Task<string> ExtractContentWithDocumentIntelligenceAsync(IFormFile file)
    {
        try
        {
            using var stream = file.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            var operation = await _documentIntelligenceClient!.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-read",
                BinaryData.FromBytes(fileBytes));

            var result = operation.Value;

            // Extract all text content
            var textBuilder = new System.Text.StringBuilder();
            
            if (result.Content != null)
            {
                textBuilder.AppendLine(result.Content);
            }
            else if (result.Pages != null)
            {
                foreach (var page in result.Pages)
                {
                    if (page.Lines != null)
                    {
                        foreach (var line in page.Lines)
                        {
                            textBuilder.AppendLine(line.Content);
                        }
                    }
                }
            }

            var extractedText = textBuilder.ToString().Trim();
            
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return $"[Document '{file.FileName}' processed but no text content was extracted. This may be a scanned image or empty document.]";
            }

            return extractedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting content with Document Intelligence for {FileName}", file.FileName);
            return $"[Error extracting content from '{file.FileName}': {ex.Message}]";
        }
    }

    [HttpPost("RequestClarity")]
    public async Task<IActionResult> RequestClarity(int orgId, [FromBody] ClarityRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
        var userType = GetCurrentUserType();
            
            // Security: Verify user has access to this conversation
            var hasAccess = await _chatService.CanUserAccessConversationAsync(request.ConversationId, userId, orgId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to request clarity for unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId, request.ConversationId, orgId);
                return Json(new { success = false, error = "Access denied." });
            }
        
        var clarity = await _chatService.RequestClarityAsync(request.ConversationId, request.Text, userType);
        return Json(clarity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting clarity for conversation {ConversationId}", request.ConversationId);
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    [HttpGet("GetMessages/{id}")]
    public async Task<IActionResult> GetMessages(int orgId, int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            _logger.LogInformation("🔍 DEBUG GetMessages: User {UserId} requesting messages for Conversation {ConvId} in Org {OrgId}", 
                userId, id, orgId);
            
            // Security: Verify user has access to this conversation
            var hasAccess = await _chatService.CanUserAccessConversationAsync(id, userId, orgId);
            
            _logger.LogInformation("🔐 DEBUG GetMessages: Access check result for User {UserId} Conversation {ConvId} Org {OrgId}: {HasAccess}", 
                userId, id, orgId, hasAccess);
            
            if (!hasAccess)
            {
                _logger.LogWarning("⛔ SECURITY: User {UserId} DENIED access to conversation {ConversationId} in org {OrgId}", 
                    userId, id, orgId);
                _logger.LogWarning("🔍 DEBUG: User may be in a client org trying to access law firm conversation, or conversation doesn't exist");
                return Json(new { success = false, error = "Access denied." });
            }
            
            _logger.LogInformation("✅ DEBUG GetMessages: Access granted, fetching messages...");
            var messages = await _chatService.GetConversationMessagesAsync(id);
            _logger.LogInformation("✅ DEBUG GetMessages: Retrieved {Count} messages for conversation {ConvId}", messages.Count, id);
            
            return Json(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ERROR retrieving messages for conversation {ConversationId} in org {OrgId}", id, orgId);
            return Json(new { success = false, error = "An error occurred while retrieving messages." });
        }
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

            // Security: Verify user has access to this channel (uses CanUserAccessConversationAsync which handles channels properly)
            var canAccess = await _chatService.CanUserAccessConversationAsync(channelId, userId, orgId);
            if (!canAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to mark channel {ChannelId} as read without permission in org {OrgId}", 
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

            // Security: Verify user has access to this channel (uses CanUserAccessConversationAsync which handles channels properly)
            var canAccess = await _chatService.CanUserAccessConversationAsync(channelId, customUser.Id, orgId);
            if (!canAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to access channel {ChannelId} without permission in org {OrgId}", 
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
        try
        {
            var userId = GetCurrentUserId();
        var userType = GetCurrentUserType();
        
            // Security: Verify user has access to this conversation
            var hasAccess = await _chatService.CanUserAccessConversationAsync(request.ConversationId, userId, orgId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to get suggestions for unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId, request.ConversationId, orgId);
                return Json(new { success = false, error = "Access denied." });
            }
            
            var messages = await _chatService.GetConversationMessagesAsync(request.ConversationId);
        var suggestions = await _chatService.GetReplySuggestionsAsync(request.ConversationId, messages, userType);
        return Json(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suggestions for conversation {ConversationId}", request.ConversationId);
            return Json(new { success = false, error = "An error occurred while generating suggestions." });
        }
    }

    [HttpPost("GenerateAIResponse")]
    public async Task<IActionResult> GenerateAIResponse(int orgId, [FromBody] AIResponseRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Security: Verify user has access to this conversation
            var hasAccess = await _chatService.CanUserAccessConversationAsync(request.ConversationId, userId, orgId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to generate AI response for unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId, request.ConversationId, orgId);
                return Json(new { success = false, error = "Access denied." });
            }
            
            var aiMessage = await _chatService.GenerateAIResponseAsync(request.ConversationId, request.UserMessage);
            return Json(new { success = true, message = aiMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI response for conversation {ConversationId}", request.ConversationId);
            return Json(new { success = false, error = "An error occurred while generating the AI response." });
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
            var userId = GetCurrentUserId();
            
            // Security: Verify user has access to this conversation before streaming
            var hasAccess = await _chatService.CanUserAccessConversationAsync(request.ConversationId, userId, orgId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to stream AI response for unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId, request.ConversationId, orgId);
                var errorData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { content = "Access denied.", done = true, error = true })}\n\n";
                await Response.WriteAsync(errorData);
                await Response.Body.FlushAsync();
                return;
            }
            
            // Build the message with attachment context if provided
            var messageWithContext = request.UserMessage;
            if (!string.IsNullOrEmpty(request.AttachmentContent))
            {
                messageWithContext = $"[USER ATTACHED FILE: {request.AttachmentFileName ?? "document"}]\n\n--- ATTACHED FILE CONTENT ---\n{request.AttachmentContent}\n--- END OF ATTACHED FILE ---\n\nUser's message: {request.UserMessage}";
                _logger.LogInformation("Including attachment content ({Length} chars) in AI context for conversation {ConversationId}", 
                    request.AttachmentContent.Length, request.ConversationId);
            }
            
            await foreach (var chunk in _chatService.GenerateAIResponseStreamAsync(request.ConversationId, messageWithContext))
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

    [HttpPost("GenerateDashboardCardStream")]
    public async Task GenerateDashboardCardStream(int orgId, [FromBody] DashboardCardRequest request)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            var userId = GetCurrentUserId();
            var userType = GetCurrentUserType();
            await foreach (var chunk in _chatService.GenerateDashboardCardStreamAsync(
                request.UserMessage, 
                userType, 
                orgId, 
                userId,
                request.CardType))
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
            _logger.LogError(ex, "Error streaming dashboard card response");
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
            var userId = GetCurrentUserId();
            
            // Security: Verify user has access to this conversation
            var hasAccess = await _chatService.CanUserAccessConversationAsync(conversationId, userId, orgId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to get AI insights for unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId, conversationId, orgId);
                return Json(new { success = false, error = "Access denied." });
            }
            
            var insights = await _chatService.GetAIInsightsAsync(conversationId);
            return Json(new { success = true, insights = insights });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI insights for conversation {ConversationId}", conversationId);
            return Json(new { success = false, error = "An error occurred while retrieving insights." });
        }
    }

    [HttpGet("GetConversationSummary/{conversationId}")]
    public async Task<IActionResult> GetConversationSummary(int orgId, int conversationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Security: Verify user has access to this conversation
            var hasAccess = await _chatService.CanUserAccessConversationAsync(conversationId, userId, orgId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to get conversation summary for unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId, conversationId, orgId);
                return Json(new { success = false, error = "Access denied." });
            }
            
            var summary = await _chatService.GetConversationSummaryAsync(conversationId);
            return Json(new { success = true, summary = summary });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation summary for conversation {ConversationId}", conversationId);
            return Json(new { success = false, error = "An error occurred while retrieving the summary." });
        }
    }

    [HttpGet("GetClientGoals/{conversationId}")]
    public async Task<IActionResult> GetClientGoals(int orgId, int conversationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Security: Verify user has access to this conversation
            var hasAccess = await _chatService.CanUserAccessConversationAsync(conversationId, userId, orgId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to get client goals for unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId, conversationId, orgId);
                return Json(new { success = false, error = "Access denied." });
            }
            
            var goals = await _chatService.GetClientGoalsAsync(conversationId);
            return Json(new { success = true, goals = goals });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client goals for conversation {ConversationId}", conversationId);
            return Json(new { success = false, error = "An error occurred while retrieving goals." });
        }
    }

    [HttpGet("GetLatestReplySuggestion/{conversationId}")]
    public async Task<IActionResult> GetLatestReplySuggestion(int orgId, int conversationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Security: Verify user has access to this conversation
            var hasAccess = await _chatService.CanUserAccessConversationAsync(conversationId, userId, orgId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to get reply suggestion for unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId, conversationId, orgId);
                return Json(new { success = false, error = "Access denied." });
            }
            
            var suggestion = await _chatService.GetLatestReplySuggestionAsync(conversationId);
            return Json(new { success = true, suggestion = suggestion });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reply suggestion for conversation {ConversationId}", conversationId);
            return Json(new { success = false, error = "An error occurred while retrieving the suggestion." });
        }
    }

    [HttpPost("RenameConversation")]
    public async Task<IActionResult> RenameConversation(int orgId, [FromBody] RenameConversationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Security: Verify user has access to this conversation
            var hasAccess = await _chatService.CanUserAccessConversationAsync(request.ConversationId, userId, orgId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to rename unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId, request.ConversationId, orgId);
                return Json(new { success = false, error = "Access denied." });
            }
            
            var success = await _chatService.RenameConversationAsync(request.ConversationId, orgId, request.NewTitle);
            return Json(new { success = success, error = success ? null : "Failed to rename conversation" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming conversation {ConversationId}", request.ConversationId);
            return Json(new { success = false, error = "An error occurred while renaming the conversation." });
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
    public string? AttachmentContent { get; set; }
    public string? AttachmentFileName { get; set; }
}

public class DashboardCardRequest
{
    public string UserMessage { get; set; } = "";
    public string CardType { get; set; } = "";
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
