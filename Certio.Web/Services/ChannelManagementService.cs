using Microsoft.EntityFrameworkCore;
using Certio.Domain.Services;
using Certio.Infrastructure.Data;
using Certio.Application.Services;
using Certio.Application.Interfaces;
using Certio.Web.ViewModels;
using Microsoft.Extensions.Logging;
using Certio.Domain.Organizations;
using System;
using System.Collections.Generic;
using System.Linq;

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

    public async Task<List<CommunicationsTeamMember>> GetOrganizationTeamMembersAsync(int organizationId, int currentUserId)
    {
        // Get direct organization members
        var directMembers = await _context.UserOrganizations
            .Where(uo => uo.OrganizationId == organizationId &&
                         uo.IsActive &&
                         uo.User != null &&
                         uo.User.IsActive &&
                         !uo.User.IsDeleted)
            .Select(uo => new CommunicationsTeamMember
            {
                UserId = uo.UserId,
                Name = $"{uo.User!.FirstName} {uo.User.LastName}".Trim(),
                Role = uo.Role ?? "Member",
                Status = "offline", // Will be updated by SignalR
                Avatar = string.Concat(
                        string.IsNullOrEmpty(uo.User!.FirstName) ? "?" : uo.User.FirstName.Substring(0, 1),
                        string.IsNullOrEmpty(uo.User.LastName) ? string.Empty : uo.User.LastName.Substring(0, 1))
                    .ToUpper(),
                Activity = "Offline",
                RoleIcon = "",
                RoleColor = "",
                OrganizationId = organizationId,
                OrganizationName = uo.Organization != null ? uo.Organization.Name : null,
                IsExternalContacts = uo.Organization != null && uo.Organization.Name.EndsWith("'s External Contacts", StringComparison.OrdinalIgnoreCase),
                Color = uo.User.Color ?? "#3d1019"
            })
            .ToListAsync();

        // Determine accessible client organizations via relationships the user participates in
        var relationshipCandidates = await _context.OrganizationRelationships
            .Include(or => or.TargetOrganization)
            .Where(or => or.SourceOrganizationId == organizationId &&
                         or.IsActive &&
                         !or.IsDeleted &&
                         or.RelationshipType == RelationshipTypes.LawFirmClient &&
                         (!or.ExpiresAt.HasValue || or.ExpiresAt.Value > DateTime.UtcNow))
            .ToListAsync();

        var accessibleClientOrgIds = new HashSet<int>();

        if (relationshipCandidates.Any())
        {
            var relationshipIdList = relationshipCandidates.Select(rc => rc.Id).ToList();
            var targetOrgIdList = relationshipCandidates.Select(rc => rc.TargetOrganizationId).ToList();

            var assignedRelationshipIds = await _context.OrganizationRelationshipAssignedUsers
                .Where(a => a.UserId == currentUserId && relationshipIdList.Contains(a.RelationshipId))
                .Select(a => a.RelationshipId)
                .ToListAsync();

            var directMembershipOrgIds = await _context.UserOrganizations
                .Where(uo => uo.UserId == currentUserId &&
                             uo.IsActive &&
                             targetOrgIdList.Contains(uo.OrganizationId))
                .Select(uo => uo.OrganizationId)
                .ToListAsync();

            // Check which client organizations are confirmed
            // A client is confirmed if:
            // 1. The owner has joined (has membership), OR
            // 2. There are registered users (any active UserOrganizations entries)
            var clientOrgUsers = await _context.UserOrganizations
                .Where(uo => uo.IsActive && targetOrgIdList.Contains(uo.OrganizationId))
                .Select(uo => new { uo.OrganizationId, uo.UserId })
                .ToListAsync();

            var confirmedOrgIds = new HashSet<int>();
            foreach (var candidate in relationshipCandidates)
            {
                var targetOrg = candidate.TargetOrganization;
                if (targetOrg == null) continue;

                var orgUserIds = clientOrgUsers
                    .Where(u => u.OrganizationId == targetOrg.Id)
                    .Select(u => u.UserId)
                    .ToList();

                var hasRegisteredUsers = orgUserIds.Any();
                var ownerHasMembership = orgUserIds.Contains(targetOrg.OwnerId);
                var isConfirmed = ownerHasMembership || hasRegisteredUsers;

                if (isConfirmed)
                {
                    confirmedOrgIds.Add(targetOrg.Id);
                }
            }

            var assignedSet = assignedRelationshipIds.ToHashSet();
            var directSet = directMembershipOrgIds.ToHashSet();

            foreach (var candidate in relationshipCandidates)
            {
                var targetOrg = candidate.TargetOrganization;
                if (targetOrg == null) continue;

                // If confirmed, include ALL users from this organization
                // Otherwise, use the existing assignment/permission logic
                if (confirmedOrgIds.Contains(targetOrg.Id))
                {
                    accessibleClientOrgIds.Add(candidate.TargetOrganizationId);
                }
                else if (assignedSet.Contains(candidate.Id) ||
                         directSet.Contains(candidate.TargetOrganizationId) ||
                         (targetOrg.OwnerId == currentUserId))
                {
                    accessibleClientOrgIds.Add(candidate.TargetOrganizationId);
                }
            }
        }

        var relationshipMembers = new List<CommunicationsTeamMember>();

        if (accessibleClientOrgIds.Count > 0)
        {
            relationshipMembers = await _context.UserOrganizations
                .Where(uo => accessibleClientOrgIds.Contains(uo.OrganizationId) &&
                             uo.IsActive &&
                             uo.User != null &&
                             uo.User.IsActive &&
                             !uo.User.IsDeleted)
                .Select(uo => new CommunicationsTeamMember
                {
                    UserId = uo.UserId,
                    Name = $"{uo.User!.FirstName} {uo.User.LastName}".Trim(),
                    Role = uo.Role ?? "Member",
                    Status = "offline", // Will be updated by SignalR
                    Avatar = string.Concat(
                            string.IsNullOrEmpty(uo.User!.FirstName) ? "?" : uo.User.FirstName.Substring(0, 1),
                            string.IsNullOrEmpty(uo.User.LastName) ? string.Empty : uo.User.LastName.Substring(0, 1))
                        .ToUpper(),
                    Activity = "Offline",
                    RoleIcon = "",
                    RoleColor = "",
                    OrganizationId = uo.OrganizationId,
                    OrganizationName = uo.Organization != null ? uo.Organization.Name : null,
                    IsExternalContacts = uo.Organization != null && uo.Organization.Name.EndsWith("'s External Contacts", StringComparison.OrdinalIgnoreCase),
                    Color = uo.User.Color ?? "#3d1019"
                })
                .ToListAsync();
        }

        var allMembers = directMembers
            .Concat(relationshipMembers)
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

    public async Task<Dictionary<int, List<Conversation>>> GetClientOrganizationChannelsForLawFirmAsync(int lawFirmOrgId, int userId)
    {
        var relationships = await _context.OrganizationRelationships
            .Where(or => or.SourceOrganizationId == lawFirmOrgId &&
                        or.IsActive &&
                        !or.IsDeleted &&
                        or.RelationshipType == RelationshipTypes.LawFirmClient &&
                        (!or.ExpiresAt.HasValue || or.ExpiresAt.Value > DateTime.UtcNow))
            .Select(or => new
            {
                or.Id,
                or.TargetOrganizationId,
                TargetOwnerId = (int?)or.TargetOrganization!.OwnerId
            })
            .ToListAsync();

        if (!relationships.Any())
        {
            return new Dictionary<int, List<Conversation>>();
        }

        var relationshipIds = relationships.Select(rel => rel.Id).ToList();
        var targetOrgIds = relationships.Select(rel => rel.TargetOrganizationId).ToList();

        var assignedRelationshipIds = await _context.OrganizationRelationshipAssignedUsers
            .Where(a => a.UserId == userId && relationshipIds.Contains(a.RelationshipId))
            .Select(a => a.RelationshipId)
            .ToListAsync();
        var assignedSet = assignedRelationshipIds.ToHashSet();

        var directMembershipOrgIds = await _context.UserOrganizations
            .Where(uo => uo.UserId == userId &&
                         uo.IsActive &&
                         targetOrgIds.Contains(uo.OrganizationId))
            .Select(uo => uo.OrganizationId)
            .ToListAsync();
        var directSet = directMembershipOrgIds.ToHashSet();

        var accessibleOrgIds = relationships
            .Where(rel => assignedSet.Contains(rel.Id) ||
                          directSet.Contains(rel.TargetOrganizationId) ||
                          (rel.TargetOwnerId.HasValue && rel.TargetOwnerId.Value == userId))
            .Select(rel => rel.TargetOrganizationId)
            .Distinct()
            .ToList();

        if (!accessibleOrgIds.Any())
        {
            return new Dictionary<int, List<Conversation>>();
        }

        var channels = await _context.Conversations
            .Where(c => accessibleOrgIds.Contains(c.OrganizationId) && c.IsChannel)
            .Include(c => c.Matter)
            .Include(c => c.Messages)
            .ToListAsync();

        var grouped = channels
            .GroupBy(c => c.OrganizationId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => c.MatterId.HasValue ? 1 : 0)
                      .ThenBy(c => c.Title)
                      .ToList());

        return grouped;
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

