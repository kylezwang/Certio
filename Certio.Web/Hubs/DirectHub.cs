using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;
using Certio.Web.Services;
using System.Security.Claims;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Hubs;

[Authorize]
public class DirectHub : Hub
{
    private readonly IDirectMessageService _directMessageService;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly ILogger<DirectHub> _logger;

    public DirectHub(
        IDirectMessageService directMessageService,
        IClientContextAccessor clientContextAccessor,
        ILogger<DirectHub> logger)
    {
        _directMessageService = directMessageService;
        _clientContextAccessor = clientContextAccessor;
        _logger = logger;
    }

    public async Task JoinThread(string threadId)
    {
        if (!Guid.TryParse(threadId, out var threadGuid))
        {
            await Clients.Caller.SendAsync("Error", "Invalid thread ID");
            return;
        }

        var userId = GetCurrentUserId();
        var orgId = GetCurrentOrganizationId();

        if (!userId.HasValue || !orgId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Authentication required");
            return;
        }

        // Verify membership before joining
        var isParticipant = await _directMessageService.IsParticipantAsync(orgId.Value, userId.Value, threadGuid, Context.ConnectionAborted);
        if (!isParticipant)
        {
            await Clients.Caller.SendAsync("Error", "Not a participant in this thread");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"dm:{threadId}");
        
        _logger.LogInformation("User {UserId} joined DM thread {ThreadId}", userId.Value, threadId);

        // Notify user joined
        await Clients.Group($"dm:{threadId}").SendAsync("UserJoined", new
        {
            UserId = userId.Value,
            ThreadId = threadId,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task LeaveThread(string threadId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dm:{threadId}");
        
        _logger.LogInformation("User {UserId} left DM thread {ThreadId}", GetCurrentUserId(), threadId);
    }

    public async Task SendMessage(string threadId, string? body, string messageType = "Text")
    {
        if (!Guid.TryParse(threadId, out var threadGuid))
        {
            await Clients.Caller.SendAsync("Error", "Invalid thread ID");
            return;
        }

        var userId = GetCurrentUserId();
        var orgId = GetCurrentOrganizationId();

        if (!userId.HasValue || !orgId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Authentication required");
            return;
        }

        try
        {
            var dto = new NewMessageDto(body, messageType);
            var message = await _directMessageService.SendAsync(orgId.Value, userId.Value, threadGuid, dto, Context.ConnectionAborted);

            // MessageDto from service should already include sender color and external contact status
            // If not set, use defaults
            var senderColor = message.SenderColor ?? "#3d1019";
            var isExternalContacts = message.IsExternalContacts ?? false;

            // Broadcast to all participants in the thread
            await Clients.Group($"dm:{threadId}").SendAsync("ReceiveDirectMessage", new
            {
                message.Id,
                message.ThreadId,
                message.SenderId,
                message.SenderName,
                message.Body,
                message.MessageType,
                message.CreatedAt,
                message.EditedAt,
                message.IsDeleted,
                senderColor = senderColor,
                isExternalContacts = isExternalContacts,
                emailSubject = message.EmailSubject
            });

            _logger.LogInformation("User {UserId} sent message {MessageId} in thread {ThreadId}", 
                userId.Value, message.Id, threadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message in thread {ThreadId}", threadId);
            await Clients.Caller.SendAsync("Error", $"Failed to send message: {ex.Message}");
        }
    }

    public async Task Typing(string threadId, bool isTyping)
    {
        if (!Guid.TryParse(threadId, out var threadGuid))
        {
            return;
        }

        var userId = GetCurrentUserId();
        var orgId = GetCurrentOrganizationId();

        if (!userId.HasValue || !orgId.HasValue)
        {
            return;
        }

        try
        {
            await _directMessageService.SetTypingAsync(orgId.Value, userId.Value, threadGuid, isTyping, Context.ConnectionAborted);

            // Broadcast typing status to other participants only
            await Clients.OthersInGroup($"dm:{threadId}").SendAsync("UserTyping", new
            {
                UserId = userId.Value,
                ThreadId = threadId,
                IsTyping = isTyping,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting typing status in thread {ThreadId}", threadId);
        }
    }

    public async Task MarkRead(string threadId, DateTime readAt)
    {
        if (!Guid.TryParse(threadId, out var threadGuid))
        {
            await Clients.Caller.SendAsync("Error", "Invalid thread ID");
            return;
        }

        var userId = GetCurrentUserId();
        var orgId = GetCurrentOrganizationId();

        if (!userId.HasValue || !orgId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Authentication required");
            return;
        }

        try
        {
            await _directMessageService.MarkReadAsync(orgId.Value, userId.Value, threadGuid, readAt, Context.ConnectionAborted);

            // Broadcast read receipt to other participants
            await Clients.OthersInGroup($"dm:{threadId}").SendAsync("ReadReceipt", new
            {
                UserId = userId.Value,
                ThreadId = threadId,
                ReadAt = readAt
            });

            _logger.LogInformation("User {UserId} marked thread {ThreadId} as read", userId.Value, threadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking thread {ThreadId} as read", threadId);
            await Clients.Caller.SendAsync("Error", $"Failed to mark as read: {ex.Message}");
        }
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} connected to DirectHub", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DirectHub OnConnectedAsync");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} disconnected from DirectHub", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DirectHub OnDisconnectedAsync");
        }
        await base.OnDisconnectedAsync(exception);
    }

    // Helper methods
    private int? GetCurrentUserId()
    {
        // First try to get from HttpContext.Items (set by UserSyncMiddleware)
        var httpContext = Context.GetHttpContext();
        if (httpContext?.Items.TryGetValue("CustomUserId", out var customUserId) == true && customUserId is int userId)
        {
            return userId;
        }

        // Fallback to claims
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var claimUserId))
        {
            return claimUserId;
        }

        return null;
    }

    private int? GetCurrentOrganizationId()
    {
        // Try to get from query string first (passed during connection)
        var orgIdStr = Context.GetHttpContext()?.Request.Query["orgId"].FirstOrDefault();
        if (!string.IsNullOrEmpty(orgIdStr) && int.TryParse(orgIdStr, out var orgId))
        {
            return orgId;
        }

        // Fallback to context accessor
        if (_clientContextAccessor.Current?.OrganizationId.HasValue == true)
        {
            return _clientContextAccessor.Current.OrganizationId.Value;
        }

        return null;
    }
}

