using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Certio.Application.Services;
using Certio.Web.Services;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Certio.Domain.Services;
using Certio.Domain.Users;

namespace Certio.Web.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ApplicationDbContext _context;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IUserPresenceService _userPresenceService;
    
    // Track typing users
    private static readonly Dictionary<string, HashSet<string>> _typingUsers = new();

    public ChatHub(IChatService chatService, ApplicationDbContext context, IClientContextAccessor clientContextAccessor, IUserPresenceService userPresenceService)
    {
        _chatService = chatService;
        _context = context;
        _clientContextAccessor = clientContextAccessor;
        _userPresenceService = userPresenceService;
    }

    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        
        // Update user online status
        await UpdateUserOnlineStatus(true);
        
        // Notify others in the conversation that user joined
        await Clients.Group($"conversation_{conversationId}").SendAsync("UserJoined", new
        {
            UserId = GetCurrentUserId(),
            UserName = GetCurrentUserName(),
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        
        // Notify others in the conversation that user left
        await Clients.Group($"conversation_{conversationId}").SendAsync("UserLeft", new
        {
            UserId = GetCurrentUserId(),
            UserName = GetCurrentUserName(),
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task JoinChannel(string channelId)
    {
        var conversationId = int.Parse(channelId);
        var organizationId = GetCurrentOrganizationId();
        
        if (!organizationId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Organization context not found");
            return;
        }
        
        // Verify user has access to this channel
        var conversation = await _chatService.GetConversationAsync(conversationId, organizationId.Value);
        if (conversation == null || !conversation.IsChannel)
        {
            await Clients.Caller.SendAsync("Error", "Channel not found or access denied");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"channel_{channelId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{channelId}");
        
        // Update user online status
        await UpdateUserOnlineStatus(true);
        
        // Notify others in the channel that user joined
        await Clients.Group($"channel_{channelId}").SendAsync("UserJoinedChannel", new
        {
            UserId = GetCurrentUserId(),
            UserName = GetCurrentUserName(),
            ChannelId = channelId,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task LeaveChannel(string channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"channel_{channelId}");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{channelId}");
        
        // Notify others in the channel that user left
        await Clients.Group($"channel_{channelId}").SendAsync("UserLeftChannel", new
        {
            UserId = GetCurrentUserId(),
            UserName = GetCurrentUserName(),
            ChannelId = channelId,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task SendMessage(string conversationId, string userId, string userType, string content, string messageType = "Text")
    {
        try
        {
            var message = await _chatService.SendMessageAsync(int.Parse(conversationId), int.Parse(userId), userType, content, messageType);
            
            // Send message to all clients in the conversation group
            await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveMessage", new
            {
                message.Id,
                message.ConversationId,
                message.UserId,
                message.UserType,
                message.Content,
                message.MessageType,
                message.IsFromAI,
                message.AIAgentType,
                message.CreatedAt,
                message.IsRead,
                message.IsChannelMessage,
                message.ChannelId,
                message.ReplyToMessageId,
                message.IsEdited,
                message.EditedAt,
                message.Reactions
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to send message: {ex.Message}");
        }
    }

    public async Task SendChannelMessage(string channelId, string userId, string userType, string content, string messageType = "Text", string? replyToMessageId = null, string? userName = null)
    {
        try
        {
            
            if (string.IsNullOrEmpty(userId) || userId == "null" || userId == "undefined")
            {
                await Clients.Caller.SendAsync("Error", "User ID is required");
                return;
            }
            
            if (!int.TryParse(channelId, out var conversationId))
            {
                await Clients.Caller.SendAsync("Error", "Invalid channel ID");
                return;
            }
            
            if (!int.TryParse(userId, out var userIdInt))
            {
                await Clients.Caller.SendAsync("Error", "Invalid user ID");
                return;
            }
            
            var organizationId = GetCurrentOrganizationId();
            
            if (!organizationId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "Organization context not found");
                return;
            }
            
            // Verify user has access to this channel
            var conversation = await _chatService.GetConversationAsync(conversationId, organizationId.Value);
            
            if (conversation == null || !conversation.IsChannel)
            {
                await Clients.Caller.SendAsync("Error", "Channel not found or access denied");
                return;
            }

            var message = await _chatService.SendChannelMessageAsync(
                conversationId, 
                userIdInt, 
                userType, 
                content, 
                messageType, 
                conversationId, // Use conversationId as channelId
                !string.IsNullOrEmpty(replyToMessageId) ? int.Parse(replyToMessageId) : null
            );
            
            // Get sender name - try multiple sources
            var senderName = "Unknown User";
            
            // First try the userName parameter passed from JavaScript
            if (!string.IsNullOrEmpty(userName))
            {
                senderName = userName;
            }
            else
            {
                // Try to get from current user context
                var currentUserName = GetCurrentUserName();
                
                if (!string.IsNullOrEmpty(currentUserName))
                {
                    senderName = currentUserName;
                }
                else
                {
                    // Fallback: get from database using userId
                    var dbUser = await _context.Users.FindAsync(userIdInt);
                    if (dbUser != null)
                    {
                        var fullName = $"{dbUser.FirstName} {dbUser.LastName}".Trim();
                        
                        if (!string.IsNullOrEmpty(fullName))
                        {
                            senderName = fullName;
                        }
                    }
                }
            }
            
            await Clients.Group($"channel_{channelId}").SendAsync("ReceiveChannelMessage", new
            {
                message.Id,
                message.ConversationId,
                message.UserId,
                message.UserType,
                message.Content,
                message.MessageType,
                message.IsFromAI,
                message.AIAgentType,
                message.CreatedAt,
                message.IsRead,
                message.IsChannelMessage,
                message.ChannelId,
                message.ReplyToMessageId,
                message.IsEdited,
                message.EditedAt,
                message.Reactions,
                SenderName = senderName
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to send channel message: {ex.Message}");
        }
    }

    public async Task StartTyping(string conversationId)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();
        
        if (!userId.HasValue)
        {
            return;
        }
        
        var userIdString = userId.Value.ToString();
        
        if (!_typingUsers.ContainsKey(conversationId))
        {
            _typingUsers[conversationId] = new HashSet<string>();
        }
        
        _typingUsers[conversationId].Add(userIdString);
        
        // Notify others in the conversation that user is typing
        await Clients.GroupExcept($"conversation_{conversationId}", Context.ConnectionId).SendAsync("UserTyping", new
        {
            UserId = userId.Value,
            UserName = userName,
            ConversationId = conversationId,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task StopTyping(string conversationId)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();
        
        if (!userId.HasValue)
        {
            return;
        }
        
        var userIdString = userId.Value.ToString();
        
        if (_typingUsers.ContainsKey(conversationId))
        {
            _typingUsers[conversationId].Remove(userIdString);
        }
        
        // Notify others in the conversation that user stopped typing
        await Clients.GroupExcept($"conversation_{conversationId}", Context.ConnectionId).SendAsync("UserStoppedTyping", new
        {
            UserId = userId.Value,
            UserName = userName,
            ConversationId = conversationId,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task GetOnlineUsers(string conversationId)
    {
        var organizationId = GetCurrentOrganizationId();
        if (!organizationId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Organization context not found");
            return;
        }

        var onlineUserIds = _userPresenceService.GetOnlineUsersInOrganization(organizationId.Value);
        
        // Get user details from database
        var users = await _context.Users
            .Where(u => onlineUserIds.Contains(u.Id))
            .Select(u => new
            {
                UserId = u.Id,
                UserName = $"{u.FirstName} {u.LastName}".Trim(),
                LastSeen = DateTime.UtcNow
            })
            .ToListAsync();

        await Clients.Caller.SendAsync("OnlineUsers", new
        {
            ConversationId = conversationId,
            Users = users,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task AddReaction(string messageId, string emoji)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }
            
            var success = await _chatService.AddReactionAsync(int.Parse(messageId), userId.Value, emoji);
            
            if (success)
            {
                // Get the message to find which conversation/channel it belongs to
                var message = await _context.ChatMessages
                    .FirstOrDefaultAsync(m => m.Id == int.Parse(messageId));
                
                if (message != null)
                {
                    // Notify all clients in the conversation about the reaction
                    await Clients.Group($"conversation_{message.ConversationId}").SendAsync("ReactionAdded", new
                    {
                        MessageId = messageId,
                        UserId = userId.Value,
                        UserName = GetCurrentUserName(),
                        Emoji = emoji,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to add reaction");
            }
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to add reaction: {ex.Message}");
        }
    }

    public async Task RemoveReaction(string messageId, string emoji)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }
            
            var success = await _chatService.RemoveReactionAsync(int.Parse(messageId), userId.Value, emoji);
            
            if (success)
            {
                // Get the message to find which conversation/channel it belongs to
                var message = await _context.ChatMessages
                    .FirstOrDefaultAsync(m => m.Id == int.Parse(messageId));
                
                if (message != null)
                {
                    // Notify all clients in the conversation about the reaction removal
                    await Clients.Group($"conversation_{message.ConversationId}").SendAsync("ReactionRemoved", new
                    {
                        MessageId = messageId,
                        UserId = userId.Value,
                        UserName = GetCurrentUserName(),
                        Emoji = emoji,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to remove reaction");
            }
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to remove reaction: {ex.Message}");
        }
    }

    public async Task EditMessage(string messageId, string newContent)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }
            
            var message = await _chatService.EditMessageAsync(int.Parse(messageId), newContent, userId.Value);
            
            // Notify all clients in the conversation about the edit
            await Clients.Group($"conversation_{message.ConversationId}").SendAsync("MessageEdited", new
            {
                message.Id,
                message.Content,
                message.IsEdited,
                message.EditedAt,
                EditedBy = GetCurrentUserName(),
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to edit message: {ex.Message}");
        }
    }

    public async Task ReplyToMessage(string conversationId, string userId, string userType, string content, string replyToMessageId, string messageType = "Text")
    {
        try
        {
            var message = await _chatService.SendChannelMessageAsync(
                int.Parse(conversationId), 
                int.Parse(userId), 
                userType, 
                content, 
                messageType, 
                int.Parse(conversationId), // Use conversationId as channelId
                int.Parse(replyToMessageId)
            );
            
            // Send reply to all clients in the conversation group
            await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveReply", new
            {
                message.Id,
                message.ConversationId,
                message.UserId,
                message.UserType,
                message.Content,
                message.MessageType,
                message.IsFromAI,
                message.AIAgentType,
                message.CreatedAt,
                message.IsRead,
                message.IsChannelMessage,
                message.ChannelId,
                message.ReplyToMessageId,
                message.IsEdited,
                message.EditedAt,
                message.Reactions,
                SenderName = GetCurrentUserName()
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to send reply: {ex.Message}");
        }
    }

    public async Task RequestClarity(string conversationId, string text, string userType)
    {
        try
        {
            var clarity = await _chatService.RequestClarityAsync(int.Parse(conversationId), text, userType);
            
            // Send clarity explanation to all clients in the conversation group
            await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveClarity", new
            {
                OriginalText = text,
                Explanation = clarity.SimplifiedExplanation,
                UserType = userType,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to process clarity request: {ex.Message}");
        }
    }

    public async Task GenerateAIResponse(string conversationId, string userMessage)
    {
        try
        {
            var aiMessage = await _chatService.GenerateAIResponseAsync(int.Parse(conversationId), userMessage);
            
            // Send AI response to all clients in the conversation group
            await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveAIResponse", new
            {
                aiMessage.Id,
                aiMessage.ConversationId,
                aiMessage.UserId,
                aiMessage.UserType,
                aiMessage.Content,
                aiMessage.MessageType,
                aiMessage.IsFromAI,
                aiMessage.AIAgentType,
                aiMessage.CreatedAt,
                aiMessage.IsRead,
                aiMessage.IsChannelMessage,
                aiMessage.ChannelId,
                aiMessage.ReplyToMessageId,
                aiMessage.IsEdited,
                aiMessage.EditedAt,
                aiMessage.Reactions,
                SenderName = "Certio AI"
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to generate AI response: {ex.Message}");
        }
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        
        // Update user online status
        await UpdateUserOnlineStatus(true);
        
        // Notify organization about user coming online
        var organizationId = GetCurrentOrganizationId();
        if (organizationId.HasValue)
        {
            await Clients.Group($"org_{organizationId}").SendAsync("UserOnline", new
            {
                UserId = GetCurrentUserId(),
                UserName = GetCurrentUserName(),
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Update user offline status
        await UpdateUserOnlineStatus(false);
        
        // Notify organization about user going offline
        var organizationId = GetCurrentOrganizationId();
        if (organizationId.HasValue)
        {
            await Clients.Group($"org_{organizationId}").SendAsync("UserOffline", new
            {
                UserId = GetCurrentUserId(),
                UserName = GetCurrentUserName(),
                Timestamp = DateTime.UtcNow
            });
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    // Helper methods
    private async Task UpdateUserOnlineStatus(bool isOnline)
    {
        var userId = GetCurrentUserId();
        var organizationId = GetCurrentOrganizationId();
        
        if (userId.HasValue && organizationId.HasValue)
        {
            if (isOnline)
            {
                _userPresenceService.SetUserOnline(userId.Value, Context.ConnectionId, organizationId.Value);
            }
            else
            {
                _userPresenceService.SetUserOffline(Context.ConnectionId);
            }
        }
    }

    private int? GetCurrentUserId()
    {
        var user = Context.User;
        
        if (user?.Identity?.IsAuthenticated == true && 
            user.HasClaim("UserId", ""))
        {
            var userIdClaim = user.FindFirst("UserId")?.Value;
            
            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
        }
        
        return null;
    }

    private async Task<string?> GetCurrentUserNameAsync()
    {
        var user = Context.User;
        var userName = user?.Identity?.Name ?? user?.FindFirst("name")?.Value;
        
        if (!string.IsNullOrEmpty(userName))
        {
            return userName;
        }
        
        // Fallback: get from database using user ID
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            var dbUser = await _context.Users.FindAsync(userId.Value);
            if (dbUser != null)
            {
                return $"{dbUser.FirstName} {dbUser.LastName}".Trim();
            }
        }
        
        return null;
    }

    private string? GetCurrentUserName()
    {
        var user = Context.User;
        var userName = user?.Identity?.Name ?? user?.FindFirst("name")?.Value;
        
        if (!string.IsNullOrEmpty(userName))
        {
            return userName;
        }
        
        // Fallback: get from database using user ID (synchronous)
        var userId = GetCurrentUserId();
        
        if (userId.HasValue)
        {
            var dbUser = _context.Users.Find(userId.Value);
            if (dbUser != null)
            {
                var fullName = $"{dbUser.FirstName} {dbUser.LastName}".Trim();
                return fullName;
            }
        }
        
        return null;
    }

    private int? GetCurrentOrganizationId()
    {
        // Try to get from query string first (for SignalR connections)
        if (Context.GetHttpContext()?.Request.Query.TryGetValue("orgId", out var orgIdValue) == true)
        {
            if (int.TryParse(orgIdValue, out var orgId))
            {
                return orgId;
            }
        }
        
        // Try to get from client context
        var clientContext = _clientContextAccessor.ClientContext;
        if (clientContext?.OrganizationId.HasValue == true)
        {
            return clientContext.OrganizationId;
        }
        
        // Fallback to user claims
        var user = Context.User;
        if (user?.HasClaim("OrganizationId", "") == true)
        {
            var orgIdClaim = user.FindFirst("OrganizationId")?.Value;
            if (int.TryParse(orgIdClaim, out var orgId))
            {
                return orgId;
            }
        }
        
        return null;
    }
}
