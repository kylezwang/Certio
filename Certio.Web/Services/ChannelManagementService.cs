using Microsoft.EntityFrameworkCore;
using Certio.Domain.Services;
using Certio.Infrastructure.Data;
using Certio.Application.Services;
using Certio.Application.Interfaces;
using Certio.Web.ViewModels;
using Microsoft.Extensions.Logging;
using Certio.Domain.Organizations;

namespace Certio.Web.Services;

public class ChannelManagementService : Certio.Web.Services.IChannelManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IChatService _chatService;
    private readonly ILogger<ChannelManagementService> _logger;

    public ChannelManagementService(ApplicationDbContext context, IChatService chatService, ILogger<ChannelManagementService> logger)
    {
        _context = context;
        _chatService = chatService;
        _logger = logger;
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
        // Check if this is a law firm organization - only law firms should have default channels
        var organization = await _context.Organizations.FindAsync(organizationId);
        if (organization?.Type != Domain.Organizations.OrganizationType.LawFirm)
        {
            _logger.LogInformation("Skipping default channel creation for non-law-firm organization {OrganizationId} of type {OrganizationType}", organizationId, organization?.Type);
            return;
        }

        // Define default channels for law firms only
        var defaultChannels = new[]
        {
            new { Name = "general", Description = "General organization discussions and announcements", Type = "Public" },
            new { Name = "urgent-matters", Description = "Time-sensitive legal matters requiring immediate attention", Type = "Public" },
            new { Name = "client-onboarding", Description = "New client onboarding discussions and coordination", Type = "Public" }
        };

        _logger.LogInformation("Creating default channels for law firm organization {OrganizationId}", organizationId);

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

    public async Task<List<CommunicationsTeamMember>> GetOrganizationTeamMembersAsync(int organizationId)
    {
        // Get direct organization members
        var directMembers = await _context.UserOrganizations
            .Where(uo => uo.OrganizationId == organizationId && 
                         uo.IsActive && 
                         uo.User != null && 
                         uo.User.IsActive && 
                         !uo.User.IsDeleted)
            .Include(uo => uo.User)
            .Select(uo => new CommunicationsTeamMember
            {
                UserId = uo.UserId,
                Name = $"{uo.User.FirstName} {uo.User.LastName}",
                Role = uo.Role ?? "Member",
                Status = "offline", // Will be updated by SignalR
                Avatar = $"{uo.User.FirstName.Substring(0, 1)}{uo.User.LastName.Substring(0, 1)}",
                Activity = "Available",
                RoleIcon = "", // Remove role icons
                RoleColor = "" // Remove role colors
            })
            .ToListAsync();

        // Get organization relationship members (firm-based access)
        var relationshipMembers = await _context.OrganizationRelationships
            .Where(or => (or.SourceOrganizationId == organizationId || or.TargetOrganizationId == organizationId) && 
                         or.IsActive && 
                         or.RelationshipType == "LawFirmClient")
            .Include(or => or.SourceOrganization)
                .ThenInclude(so => so.UserOrganizations)
                    .ThenInclude(uo => uo.User)
            .Include(or => or.TargetOrganization)
                .ThenInclude(to => to.UserOrganizations)
                    .ThenInclude(uo => uo.User)
            .SelectMany(or => or.SourceOrganization.UserOrganizations.Concat(or.TargetOrganization.UserOrganizations))
            .Where(uo => uo.IsActive && 
                         uo.User != null && 
                         uo.User.IsActive && 
                         !uo.User.IsDeleted)
            .Select(uo => new CommunicationsTeamMember
            {
                UserId = uo.UserId,
                Name = $"{uo.User.FirstName} {uo.User.LastName}",
                Role = uo.Role ?? "Member",
                Status = "offline", // Will be updated by SignalR
                Avatar = $"{uo.User.FirstName.Substring(0, 1)}{uo.User.LastName.Substring(0, 1)}",
                Activity = "Available",
                RoleIcon = "", // Remove role icons
                RoleColor = "" // Remove role colors
            })
            .ToListAsync();

        // Combine and deduplicate by UserId
        var allMembers = directMembers.Concat(relationshipMembers)
            .GroupBy(m => m.UserId)
            .Select(g => g.First())
            .ToList();

        return allMembers;
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

    public async Task<Conversation?> CreateMatterChannelAsync(int matterId, int organizationId, int createdById)
    {
        try
        {
            var matter = await _context.Matters.FindAsync(matterId);
            if (matter == null)
            {
                _logger.LogWarning("Matter {MatterId} not found", matterId);
                return null;
            }

            var channelName = ConvertToKebabCase(matter.Title);
            var description = $"Discussion channel for matter: {matter.Title}";

            _logger.LogInformation("Creating channel for matter {MatterId} with title '{MatterTitle}' -> channel name '{ChannelName}'", matterId, matter.Title, channelName);

            // Check if channel already exists for this matter
            var existingChannel = await _context.Conversations
                .FirstOrDefaultAsync(c => c.OrganizationId == organizationId && 
                                          c.MatterId == matterId && 
                                          c.IsChannel);

            if (existingChannel != null)
            {
                _logger.LogInformation("Channel already exists for matter {MatterId}: {ChannelId}", matterId, existingChannel.Id);
                return existingChannel;
            }

            // Create new matter channel using ChatService
            _logger.LogInformation("Calling ChatService.CreateChannelAsync for matter {MatterId}", matterId);
            var channel = await _chatService.CreateChannelAsync(
                organizationId,
                createdById,
                channelName,
                description,
                "Public",
                false,
                matterId
            );

            _logger.LogInformation("Successfully created channel {ChannelId} for matter {MatterId}", channel.Id, matterId);
            return channel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating channel for matter {MatterId}", matterId);
            return null;
        }
    }

    public async Task<List<Conversation>> GetMatterChannelsForOrganizationAsync(int organizationId)
    {
        return await _context.Conversations
            .Where(c => c.OrganizationId == organizationId && 
                       c.IsChannel && 
                       c.MatterId.HasValue)
            .Include(c => c.Matter)
            .Include(c => c.Messages)
            .OrderBy(c => c.Title)
            .ToListAsync();
    }

    public async Task<Dictionary<int, List<Conversation>>> GetClientOrganizationChannelsForLawFirmAsync(int lawFirmOrgId)
    {
        // Get all valid client relationships for this law firm
        var relationships = await _context.OrganizationRelationships
            .Where(or => or.SourceOrganizationId == lawFirmOrgId &&
                        or.IsActive &&
                        !or.IsDeleted &&
                        or.RelationshipType == "LawFirmClient" &&
                        (!or.ExpiresAt.HasValue || or.ExpiresAt.Value > DateTime.UtcNow))
            .Include(or => or.TargetOrganization)
            .ToListAsync();

        var result = new Dictionary<int, List<Conversation>>();

        foreach (var relationship in relationships)
        {
            var clientOrgId = relationship.TargetOrganizationId;

            // Get all channels for this client organization (both general and matter-specific)
            var channels = await _context.Conversations
                .Where(c => c.OrganizationId == clientOrgId && c.IsChannel)
                .Include(c => c.Matter)
                .Include(c => c.Messages)
                .OrderBy(c => c.MatterId.HasValue ? 1 : 0) // General channels first, then matter channels
                .ThenBy(c => c.Title)
                .ToListAsync();

            result[clientOrgId] = channels;
        }

        return result;
    }

    private static string ConvertToKebabCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "untitled";
        }

        // Convert to lowercase
        var result = input.ToLowerInvariant();

        // Replace spaces and special characters with hyphens
        result = System.Text.RegularExpressions.Regex.Replace(result, @"[^a-z0-9]+", "-");

        // Remove leading/trailing hyphens
        result = result.Trim('-');

        // Replace multiple consecutive hyphens with a single hyphen
        result = System.Text.RegularExpressions.Regex.Replace(result, @"-+", "-");

        return string.IsNullOrEmpty(result) ? "untitled" : result;
    }
}

