using Microsoft.EntityFrameworkCore;
using Certio.Domain.Services;
using Certio.Web.Data;
using Certio.Application.Services;

namespace Certio.Web.Services;

public class ChannelManagementService : IChannelManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IChatService _chatService;

    public ChannelManagementService(ApplicationDbContext context, IChatService chatService)
    {
        _context = context;
        _chatService = chatService;
    }

    public async Task<List<Conversation>> GetOrganizationChannelsAsync(int organizationId)
    {
        return await _context.Conversations
            .Where(c => c.OrganizationId == organizationId && c.IsChannel)
            .Include(c => c.Messages)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Conversation> CreateDefaultChannelAsync(int organizationId, int createdById, string channelName, string description, string channelType = "Public")
    {
        // Check if channel already exists
        var existingChannel = await _context.Conversations
            .FirstOrDefaultAsync(c => c.OrganizationId == organizationId && 
                                      c.Title == channelName && 
                                      c.IsChannel);

        if (existingChannel != null)
        {
            return existingChannel;
        }

        // Create new channel using ChatService
        var channel = await _chatService.CreateChannelAsync(
            organizationId,
            createdById,
            channelName,
            description,
            channelType,
            channelType == "Private",
            null
        );

        return channel;
    }

    public async Task EnsureDefaultChannelsExistAsync(int organizationId, int createdById)
    {
        // Define default channels
        var defaultChannels = new[]
        {
            new { Name = "general", Description = "General organization discussions and announcements", Type = "Public" },
            new { Name = "urgent-matters", Description = "Time-sensitive legal matters requiring immediate attention", Type = "Public" },
            new { Name = "client-onboarding", Description = "New client onboarding discussions and coordination", Type = "Public" }
        };

        foreach (var channelDef in defaultChannels)
        {
            await CreateDefaultChannelAsync(
                organizationId,
                createdById,
                channelDef.Name,
                channelDef.Description,
                channelDef.Type
            );
        }
    }

    public async Task<int> GetUnreadCountAsync(int conversationId, int userId)
    {
        // Get the last time user read messages in this conversation
        var lastReadMessage = await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId && m.UserId == userId && m.IsRead)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        DateTime lastReadTime = lastReadMessage?.CreatedAt ?? DateTime.MinValue;

        // Count unread messages
        var unreadCount = await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId && 
                       m.CreatedAt > lastReadTime && 
                       m.UserId != userId)
            .CountAsync();

        return unreadCount;
    }

    public async Task<List<int>> GetOnlineUserIdsAsync(int organizationId)
    {
        // Get users who have been active in the last 5 minutes
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

        var onlineUserIds = await _context.ChatMessages
            .Where(m => m.Conversation.OrganizationId == organizationId && 
                       m.CreatedAt > fiveMinutesAgo &&
                       m.UserId.HasValue)
            .Select(m => m.UserId!.Value)
            .Distinct()
            .ToListAsync();

        return onlineUserIds;
    }
}

