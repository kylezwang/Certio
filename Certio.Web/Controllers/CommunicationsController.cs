using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Domain.Matters;
using Certio.Infrastructure.Data;
using Certio.Web.ViewModels;
using Certio.Web.Services;
using Certio.Application.Interfaces;

namespace Certio.Web.Controllers
{
    [Authorize]
    public class CommunicationsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly Certio.Web.Services.IChannelManagementService _channelManagementService;
        private readonly IMatterService _matterService;
        private readonly IChatService _chatService;
        private readonly IUserPresenceService _userPresenceService;
        private readonly ILogger<CommunicationsController> _logger;

        public CommunicationsController(
            ApplicationDbContext db,
            Certio.Web.Services.IChannelManagementService channelManagementService,
            IMatterService matterService,
            IChatService chatService,
            IUserPresenceService userPresenceService,
            ILogger<CommunicationsController> logger)
        {
            _db = db;
            _channelManagementService = channelManagementService;
            _matterService = matterService;
            _chatService = chatService;
            _userPresenceService = userPresenceService;
            _logger = logger;
        }

        // GET /Client/{orgId}/Communications
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Communications")]
        public async Task<IActionResult> Index(int orgId)
        {
            ViewBag.OrganizationId = orgId;
            var org = await _db.Organizations.Where(o => o.Id == orgId).FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            
            // Set user info for JavaScript
            var customUser = HttpContext.Items["CustomUser"] as User;
            ViewBag.CurrentUserId = customUser?.Id;
            ViewBag.CurrentUserName = customUser != null ? $"{customUser.FirstName} {customUser.LastName}".Trim() : "";

            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Get current organization to check if it's a law firm
            var isLawFirm = org?.Type == OrganizationType.LawFirm;
            
            // Load team members from organization using service
            var orgTeamMembers = await _channelManagementService.GetOrganizationTeamMembersAsync(orgId);
            
            // Use UserPresenceService for real-time presence detection
            var onlineUserIds = _userPresenceService.GetOnlineUsersInOrganization(orgId);
            
            // Update online status for team members and set Activity text
            foreach (var member in orgTeamMembers)
            {
                var isOnline = onlineUserIds.Contains(member.UserId);
                member.Status = isOnline ? "online" : "offline";
                member.Activity = isOnline ? "Online" : "Offline";
            }
            
            // Sort team members: current user first, then online users, then offline, then alphabetically
            orgTeamMembers = orgTeamMembers
                .OrderByDescending(m => m.UserId == customUser.Id) // Current user first
                .ThenByDescending(m => m.Status == "online") // Then online users
                .ThenBy(m => m.Name) // Then alphabetically
                .ToList();

            List<ChannelCategory> channelCategories;

            if (isLawFirm)
            {
                channelCategories = await BuildLawFirmChannelCategoriesAsync(orgId, customUser.Id);
            }
            else
            {
                channelCategories = await BuildClientChannelCategoriesAsync(orgId, customUser.Id);
            }

            // Set active channel to the first available channel
            var firstCategory = channelCategories.FirstOrDefault();
            var activeChannel = firstCategory?.Channels.FirstOrDefault()?.Name ?? 
                               firstCategory?.Subcategories.FirstOrDefault()?.Channels.FirstOrDefault()?.Name ?? 
                               "general";

            // Load demo messages for the active channel
            var messages = await LoadDemoMessagesAsync(customUser.Id, null, activeChannel);

            var viewModel = new CommunicationsViewModel
            {
                ChannelCategories = channelCategories,
                Messages = messages,
                TeamMembers = orgTeamMembers,
                ActiveChannel = activeChannel,
                OnlineMembersCount = 0 // Will be updated by SignalR
            };

            return View("~/Views/Home/Communications.cshtml", viewModel);
        }

        // GET /Client/{orgId}/Matter/{matterId}/Communications
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Matter/{matterId:int}/Communications")]
        public async Task<IActionResult> MatterCommunications(int orgId, int matterId)
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Verify the matter exists and user has access to it
            var matterResult = await _matterService.GetMatterAsync(customUser.Id, matterId);
            if (!matterResult.Success)
            {
                _logger.LogWarning("User {UserId} attempted to access communications for unauthorized matter {MatterId}", customUser.Id, matterId);
                return RedirectToAction("Index", "Matter");
            }

            var matterDto = matterResult.Data!;
            
            // Get the actual Matter entity from database
            var matter = await _db.Matters.FindAsync(matterId);
            if (matter == null)
            {
                _logger.LogWarning("Matter {MatterId} not found in database", matterId);
                return RedirectToAction("Index", "Matter");
            }
            
            var org = await _db.Organizations.FindAsync(orgId);
            var isLawFirm = org?.Type == OrganizationType.LawFirm;

            // Ensure matter channels exist for this matter
            // Use matter.OrganizationId (originating org) not orgId (viewing org)
            await EnsureMatterChannelsExistAsync(matterId, matter.Title, matter.OrganizationId);

            // Build channel categories for matter context
            List<ChannelCategory> channelCategories;
            
            if (isLawFirm)
            {
                channelCategories = await BuildMatterChannelCategoriesForLawFirmAsync(orgId, matterId, customUser.Id, org?.Name ?? "Law Firm", matter);
            }
            else
            {
                channelCategories = await BuildMatterChannelCategoriesForClientAsync(orgId, matterId, customUser.Id);
            }

            // Load team members
            var orgTeamMembers = await _channelManagementService.GetOrganizationTeamMembersAsync(orgId);
            
            // Use UserPresenceService for real-time presence detection
            var onlineUserIds = _userPresenceService.GetOnlineUsersInOrganization(orgId);
            
            // Update online status for team members and set Activity text
            foreach (var member in orgTeamMembers)
            {
                var isOnline = onlineUserIds.Contains(member.UserId);
                member.Status = isOnline ? "online" : "offline";
                member.Activity = isOnline ? "Online" : "Offline";
            }
            
            // Sort team members: current user first, then online users, then offline, then alphabetically
            orgTeamMembers = orgTeamMembers
                .OrderByDescending(m => m.UserId == customUser.Id) // Current user first
                .ThenByDescending(m => m.Status == "online") // Then online users
                .ThenBy(m => m.Name) // Then alphabetically
                .ToList();

            // Find the matter channel across all categories and subcategories - this should be the active channel
            string activeChannel = "matter-general";
            int? activeChannelId = null;
            
            foreach (var category in channelCategories)
            {
                // Check direct channels in category
                var directMatterChannel = category.Channels.FirstOrDefault(c => c.MatterId == matterId);
                if (directMatterChannel != null)
                {
                    activeChannel = directMatterChannel.Name;
                    activeChannelId = directMatterChannel.Id;
                    break;
                }
                
                // Check subcategories (CURRENT MATTER for firm matters, CLIENT COMMUNICATIONS for client matters)
                foreach (var subcategory in category.Subcategories)
                {
                    var matterChannel = subcategory.Channels.FirstOrDefault(c => c.MatterId == matterId);
                    if (matterChannel != null)
                    {
                        activeChannel = matterChannel.Name;
                        activeChannelId = matterChannel.Id;
                        break;
                    }
                }
                
                if (activeChannelId.HasValue) break;
            }
            
            // If no matter channel found, fallback to first available channel
            if (!activeChannelId.HasValue)
            {
                var firstCategory = channelCategories.FirstOrDefault();
                var firstChannel = firstCategory?.Channels.FirstOrDefault() ?? 
                                  firstCategory?.Subcategories.FirstOrDefault()?.Channels.FirstOrDefault();
                if (firstChannel != null)
                {
                    activeChannel = firstChannel.Name;
                    activeChannelId = firstChannel.Id;
                }
            }

            // Load demo messages for the active matter channel
            var messages = await LoadDemoMessagesAsync(customUser.Id, matterId, activeChannel);

            // Set ViewBag for the partial view
            ViewBag.CurrentUserId = customUser.Id;
            ViewBag.CurrentUserName = $"{customUser.FirstName} {customUser.LastName}".Trim();
            ViewBag.OrganizationId = orgId;
            ViewBag.MatterId = matterId;
            ViewBag.ActiveChannelId = activeChannelId;

            var viewModel = new CommunicationsViewModel
            {
                ChannelCategories = channelCategories,
                Messages = messages,
                TeamMembers = orgTeamMembers,
                ActiveChannel = activeChannel,
                OnlineMembersCount = orgTeamMembers.Count(m => m.Status == "online")
            };

            return View("~/Views/Matter/_MatterCommunications.cshtml", viewModel);
        }

        // GET /Client/{orgId}/Communications/GetChannelsJson
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Communications/GetChannelsJson")]
        public async Task<IActionResult> GetChannelsJson(int orgId)
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            if (customUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId);
            var isLawFirm = org?.Type == OrganizationType.LawFirm;
            
            List<ChannelCategory> channelCategories;
            if (isLawFirm)
            {
                channelCategories = await BuildLawFirmChannelCategoriesAsync(orgId, customUser.Id);
            }
            else
            {
                channelCategories = await BuildClientChannelCategoriesAsync(orgId, customUser.Id);
            }

            // Get team members with online status
            var orgTeamMembers = await _channelManagementService.GetOrganizationTeamMembersAsync(orgId);
            var onlineUserIds = _userPresenceService.GetOnlineUsersInOrganization(orgId);
            
            // Update online status for team members and set Activity text
            foreach (var member in orgTeamMembers)
            {
                var isOnline = onlineUserIds.Contains(member.UserId);
                member.Status = isOnline ? "online" : "offline";
                member.Activity = isOnline ? "Online" : "Offline";
            }
            
            // Sort team members: current user first, then online users, then offline, then alphabetically
            orgTeamMembers = orgTeamMembers
                .OrderByDescending(m => m.UserId == customUser.Id) // Current user first
                .ThenByDescending(m => m.Status == "online") // Then online users
                .ThenBy(m => m.Name) // Then alphabetically
                .ToList();

            return Json(new { 
                success = true, 
                channelCategories = channelCategories,
                teamMembers = orgTeamMembers,
                organizationId = orgId,
                organizationName = org?.Name ?? "Organization"
            });
        }

        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Communications/GetChannelMembers")]
        public async Task<IActionResult> GetChannelMembers(int orgId, int channelId)
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            if (customUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                // Get conversation/channel
                var conversation = await _db.Conversations
                    .Include(c => c.Participants)
                        .ThenInclude(cp => cp.User)
                    .Include(c => c.Matter)
                        .ThenInclude(m => m.Assignments)
                            .ThenInclude(a => a.User)
                    .Include(c => c.Matter)
                        .ThenInclude(m => m.Permissions)
                            .ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(c => c.Id == channelId && c.OrganizationId == orgId);

                if (conversation == null)
                {
                    return Json(new { success = false, message = "Channel not found" });
                }

                // Get online users
                var onlineUserIds = _userPresenceService.GetOnlineUsersInOrganization(orgId);
                var members = new List<object>();

                // For matter channels, show users assigned to the matter
                if (conversation.MatterId.HasValue && conversation.Matter != null)
                {
                    var matter = conversation.Matter;
                    var userIds = new HashSet<int>();

                    // Add users from matter assignments (not removed)
                    var assignedUsers = matter.Assignments
                        .Where(a => a.RemovedAt == null && a.User != null && a.User.IsActive)
                        .Select(a => a.User)
                        .ToList();
                    
                    foreach (var user in assignedUsers)
                    {
                        if (userIds.Add(user.Id))
                        {
                            var initials = $"{user.FirstName?.FirstOrDefault() ?? '?'}{user.LastName?.FirstOrDefault() ?? '?'}";
                            var isOnline = onlineUserIds.Contains(user.Id);

                            members.Add(new
                            {
                                userId = user.Id,
                                name = $"{user.FirstName} {user.LastName}".Trim(),
                                avatar = initials,
                                status = isOnline ? "online" : "offline"
                            });
                        }
                    }

                    // Add users with specific permissions (if AccessLevel is "Specific")
                    if (matter.AccessLevel == "Specific")
                    {
                        var permissionedUsers = matter.Permissions
                            .Where(p => p.RevokedAt == null && p.User != null && p.User.IsActive)
                            .Select(p => p.User)
                            .ToList();
                        
                        foreach (var user in permissionedUsers)
                        {
                            if (userIds.Add(user.Id))
                            {
                                var initials = $"{user.FirstName?.FirstOrDefault() ?? '?'}{user.LastName?.FirstOrDefault() ?? '?'}";
                                var isOnline = onlineUserIds.Contains(user.Id);

                                members.Add(new
                                {
                                    userId = user.Id,
                                    name = $"{user.FirstName} {user.LastName}".Trim(),
                                    avatar = initials,
                                    status = isOnline ? "online" : "offline"
                                });
                            }
                        }
                    }
                }
                else
                {
                    // For non-matter channels, show conversation participants
                    foreach (var participant in conversation.Participants.Where(p => p.User != null && p.User.IsActive))
                    {
                        var user = participant.User;
                        var initials = $"{user.FirstName?.FirstOrDefault() ?? '?'}{user.LastName?.FirstOrDefault() ?? '?'}";
                        var isOnline = onlineUserIds.Contains(user.Id);

                        members.Add(new
                        {
                            userId = user.Id,
                            name = $"{user.FirstName} {user.LastName}".Trim(),
                            avatar = initials,
                            status = isOnline ? "online" : "offline"
                        });
                    }
                }

                return Json(new { success = true, members = members });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting channel members for channel {ChannelId}", channelId);
                return Json(new { success = false, message = "Failed to get channel members" });
            }
        }

        #region Private Helper Methods

        private async Task<List<ChannelCategory>> BuildLawFirmChannelCategoriesAsync(int orgId, int userId)
        {
            var channelCategories = new List<ChannelCategory>();
            var org = await _db.Organizations.FindAsync(orgId);

            // 1. Get law firm's own channels (general, urgent-matters, client-onboarding)
            var firmChannels = await _channelManagementService.GetOrganizationChannelsAsync(orgId);
            var firmChannelsWithUnread = new List<Channel>();
            
            foreach (var channel in firmChannels)
            {
                var unreadCount = await _channelManagementService.GetUnreadCountAsync(channel.Id, userId);
                firmChannelsWithUnread.Add(new Channel
                {
                    Id = channel.Id,
                    Name = channel.Title,
                    Unread = unreadCount,
                    Type = ChannelType.Text,
                    IsPrivate = channel.IsPrivateChannel,
                    MatterId = channel.MatterId,
                    MatterTitle = channel.Matter?.Title
                });
            }

            // Separate firm's general channels from matter channels
            var firmGeneralChannels = firmChannelsWithUnread.Where(c => !c.MatterId.HasValue && !c.IsPrivate).ToList();
            var firmPrivateChannels = firmChannelsWithUnread.Where(c => c.IsPrivate && !c.MatterId.HasValue).ToList();
            var firmMatterChannels = firmChannelsWithUnread.Where(c => c.MatterId.HasValue).ToList();

            // Add law firm organization category with Firm Matters as a subcategory
            var firmSubcategories = new List<ChannelSubcategory>();
            
            // Add Firm Matters as a subcategory under the law firm
            if (firmMatterChannels.Any())
            {
                firmSubcategories.Add(new ChannelSubcategory
                {
                    Name = "FIRM MATTERS",
                    Channels = firmMatterChannels
                });
            }

            channelCategories.Add(new ChannelCategory
            {
                Name = org?.Name?.ToUpperInvariant() ?? "LAW FIRM",
                Channels = firmGeneralChannels,
                Subcategories = firmSubcategories
            });

            // 2. Get all client organizations and their channels
            var clientOrgChannelsDict = await _channelManagementService.GetClientOrganizationChannelsForLawFirmAsync(orgId);
            
            if (clientOrgChannelsDict.Any())
            {
                var clientSubcategories = new List<ChannelSubcategory>();

                foreach (var kvp in clientOrgChannelsDict)
                {
                    var clientOrgId = kvp.Key;
                    var clientChannels = kvp.Value;

                    var clientOrg = await _db.Organizations.FindAsync(clientOrgId);
                    if (clientOrg == null) continue;

                    var clientChannelsWithUnread = new List<Channel>();
                    foreach (var channel in clientChannels)
                    {
                        var unreadCount = await _channelManagementService.GetUnreadCountAsync(channel.Id, userId);
                        clientChannelsWithUnread.Add(new Channel
                        {
                            Id = channel.Id,
                            Name = channel.Title,
                            Unread = unreadCount,
                            Type = ChannelType.Text,
                            IsPrivate = channel.IsPrivateChannel,
                            MatterId = channel.MatterId,
                            MatterTitle = channel.Matter?.Title
                        });
                    }

                    clientSubcategories.Add(new ChannelSubcategory
                    {
                        Name = clientOrg.Name,
                        OrganizationId = clientOrgId,
                        Channels = clientChannelsWithUnread
                    });
                }

                channelCategories.Add(new ChannelCategory
                {
                    Name = "CLIENT COMMUNICATIONS",
                    Channels = new List<Channel>(),
                    Subcategories = clientSubcategories
                });
            }

            // Add private channels if any exist
            if (firmPrivateChannels.Any())
            {
                channelCategories.Add(new ChannelCategory
                {
                    Name = "LEGAL TEAM",
                    Channels = firmPrivateChannels
                });
            }

            // Add voice channels category (placeholder for future)
            channelCategories.Add(new ChannelCategory
            {
                Name = "VOICE CHANNELS",
                Channels = new List<Channel>
                {
                    new Channel { Id = -1, Name = "Client Consultations", Unread = 0, Type = ChannelType.Voice, IsPrivate = false, Users = new List<string>() },
                    new Channel { Id = -2, Name = "Team Meetings", Unread = 0, Type = ChannelType.Voice, IsPrivate = true, Users = new List<string>() }
                }
            });

            return channelCategories;
        }

        private async Task<List<ChannelCategory>> BuildClientChannelCategoriesAsync(int orgId, int userId)
        {
            var channels = await _channelManagementService.GetOrganizationChannelsAsync(orgId);
            
            // Get unread counts for each channel
            var channelsWithUnread = new List<Channel>();
            foreach (var channel in channels)
            {
                var unreadCount = await _channelManagementService.GetUnreadCountAsync(channel.Id, userId);
                channelsWithUnread.Add(new Channel
                {
                    Id = channel.Id,
                    Name = channel.Title,
                    Unread = unreadCount,
                    Type = ChannelType.Text,
                    IsPrivate = channel.IsPrivateChannel,
                    MatterId = channel.MatterId,
                    MatterTitle = channel.Matter?.Title
                });
            }

            // Organize channels into categories
            var publicChannels = channelsWithUnread.Where(c => !c.IsPrivate).ToList();
            var privateChannels = channelsWithUnread.Where(c => c.IsPrivate).ToList();

            var channelCategories = new List<ChannelCategory>
            {
                new ChannelCategory
                {
                    Name = "CLIENT COMMUNICATIONS",
                    Channels = publicChannels
                }
            };

            // Add private channels if any exist
            if (privateChannels.Any())
            {
                channelCategories.Add(new ChannelCategory
                {
                    Name = "LEGAL TEAM",
                    Channels = privateChannels
                });
            }

            // Add voice channels category (placeholder for future)
            channelCategories.Add(new ChannelCategory
            {
                Name = "VOICE CHANNELS",
                Channels = new List<Channel>
                {
                    new Channel { Id = -1, Name = "Client Consultations", Unread = 0, Type = ChannelType.Voice, IsPrivate = false, Users = new List<string>() },
                    new Channel { Id = -2, Name = "Team Meetings", Unread = 0, Type = ChannelType.Voice, IsPrivate = true, Users = new List<string>() }
                }
            });

            return channelCategories;
        }

        private async Task<List<ChannelCategory>> BuildMatterChannelCategoriesForLawFirmAsync(int orgId, int matterId, int userId, string firmName, Matter matter)
        {
            var channelCategories = new List<ChannelCategory>();
            
            // Determine if this is a firm matter (matter belongs to the firm) or a client matter
            var isFirmMatter = matter.OrganizationId == orgId;
            var matterOrgId = matter.OrganizationId;

            // Get the matter's channel
            var matterOrgChannels = await _channelManagementService.GetOrganizationChannelsAsync(matterOrgId);
            var matterChannel = matterOrgChannels.FirstOrDefault(c => c.MatterId == matterId);
            
            Channel? matterChannelWithUnread = null;
            if (matterChannel != null)
            {
                var unreadCount = await _channelManagementService.GetUnreadCountAsync(matterChannel.Id, userId);
                matterChannelWithUnread = new Channel
                {
                    Id = matterChannel.Id,
                    Name = matterChannel.Title,
                    Unread = unreadCount,
                    Type = ChannelType.Text,
                    IsPrivate = matterChannel.IsPrivateChannel,
                    MatterId = matterChannel.MatterId,
                    MatterTitle = matterChannel.Matter?.Title
                };
            }

            if (!isFirmMatter)
            {
                // CLIENT MATTER: Show CLIENT COMMUNICATIONS section
                var clientOrg = await _db.Organizations.FindAsync(matterOrgId);
                
                if (clientOrg != null && matterChannelWithUnread != null)
                {
                    // Add Client Communications category with the matter's organization as subcategory
                    channelCategories.Add(new ChannelCategory
                    {
                        Name = "CLIENT COMMUNICATIONS",
                        Channels = new List<Channel>(),
                        Subcategories = new List<ChannelSubcategory>
                        {
                            new ChannelSubcategory
                            {
                                Name = clientOrg.Name,
                                OrganizationId = matterOrgId,
                                Channels = new List<Channel> { matterChannelWithUnread }
                            }
                        }
                    });
                }
            }

            // Get law firm's non-matter channels (general, urgent-matters, client-onboarding)
            var allFirmChannels = await _channelManagementService.GetOrganizationChannelsAsync(orgId);
            var firmGeneralChannels = new List<Channel>();
            
            foreach (var channel in allFirmChannels.Where(c => !c.MatterId.HasValue && !c.IsPrivateChannel))
            {
                var unreadCount = await _channelManagementService.GetUnreadCountAsync(channel.Id, userId);
                firmGeneralChannels.Add(new Channel
                {
                    Id = channel.Id,
                    Name = channel.Title,
                    Unread = unreadCount,
                    Type = ChannelType.Text,
                    IsPrivate = channel.IsPrivateChannel,
                    MatterId = channel.MatterId,
                    MatterTitle = channel.Matter?.Title
                });
            }

            // Build law firm category
            var firmCategory = new ChannelCategory
            {
                Name = firmName?.ToUpperInvariant() ?? "LAW FIRM",
                Channels = firmGeneralChannels,
                Subcategories = new List<ChannelSubcategory>()
            };

            // FIRM MATTER: Add current matter channel under "CURRENT MATTER" subcategory
            if (isFirmMatter && matterChannelWithUnread != null)
            {
                firmCategory.Subcategories.Add(new ChannelSubcategory
                {
                    Name = "CURRENT MATTER",
                    Channels = new List<Channel> { matterChannelWithUnread }
                });
            }

            channelCategories.Add(firmCategory);

            return channelCategories;
        }

        private async Task<List<ChannelCategory>> BuildMatterChannelCategoriesForClientAsync(int orgId, int matterId, int userId)
        {
            var allChannels = await _channelManagementService.GetOrganizationChannelsAsync(orgId);
            
            // Get matter-specific channels
            var matterChannels = allChannels.Where(c => c.MatterId == matterId).ToList();
            var matterChannelsWithUnread = new List<Channel>();

            foreach (var channel in matterChannels)
            {
                var unreadCount = await _channelManagementService.GetUnreadCountAsync(channel.Id, userId);
                matterChannelsWithUnread.Add(new Channel
                {
                    Id = channel.Id,
                    Name = channel.Title,
                    Unread = unreadCount,
                    Type = ChannelType.Text,
                    IsPrivate = channel.IsPrivateChannel,
                    MatterId = channel.MatterId,
                    MatterTitle = channel.Matter?.Title
                });
            }

            var channelCategories = new List<ChannelCategory>
            {
                new ChannelCategory
                {
                    Name = "CLIENT COMMUNICATIONS",
                    Channels = matterChannelsWithUnread
                }
            };

            return channelCategories;
        }

        private async Task EnsureMatterChannelsExistAsync(int matterId, string matterTitle, int organizationId)
        {
            // Get current user for channel creation
            var customUser = HttpContext.Items["CustomUser"] as User;
            if (customUser == null)
            {
                _logger.LogWarning("Cannot create matter channels: user not found");
                return;
            }

            // Check if matter channel already exists using the same logic as Matter/Create
            var existingChannels = await _db.Conversations
                .Where(c => c.MatterId == matterId && c.IsChannel)
                .ToListAsync();

            // If channel already exists, don't create another one
            if (existingChannels.Any())
            {
                _logger.LogDebug("Matter channel already exists for matter {MatterId}", matterId);
                return;
            }

            // Use ChannelManagementService.CreateMatterChannelAsync which follows the Matter/Create pattern
            // This creates a channel with kebab-case name based on the matter title
            try
            {
                var channel = await _channelManagementService.CreateMatterChannelAsync(
                    matterId,
                    organizationId,
                    customUser.Id
                );

                if (channel != null)
                {
                    _logger.LogInformation("Created matter channel '{ChannelName}' for matter {MatterId}: {MatterTitle}", 
                        channel.Title, matterId, matterTitle);
                }
                else
                {
                    _logger.LogWarning("Failed to create matter channel for matter {MatterId}: {MatterTitle}", 
                        matterId, matterTitle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating matter channel for matter {MatterId}: {MatterTitle}", 
                    matterId, matterTitle);
            }
        }

        private Task<List<Message>> LoadDemoMessagesAsync(int userId, int? matterId = null, string? channelName = null)
        {
            // In production, these would come from database filtered by channel/matter
            var messages = new List<Message>();
            
            // Known firm/organization channels (not matter-specific)
            var knownGeneralChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "general", 
                "urgent-matters", 
                "client-onboarding" 
            };
            
            // If matterId is provided and channelName is not a known general channel, show matter-specific messages
            if (matterId.HasValue && !string.IsNullOrEmpty(channelName) && !knownGeneralChannels.Contains(channelName))
            {
                // Matter-specific messages for the matter channel
                messages.Add(new Message
                {
                    Id = 1,
                    UserId = userId,
                    User = "Kyle Wang",
                    Avatar = "KW",
                    Time = "4:42 PM",
                    CreatedAt = DateTime.Now.AddMinutes(-30),
                    Content = "Starting the conversation for the SLA II Matter Details Communications module here",
                    Reactions = new List<Reaction>()
                });
            }
            else if (!string.IsNullOrEmpty(channelName))
            {
                // General channel messages
                messages.Add(new Message
                {
                    Id = 1,
                    UserId = userId,
                    User = "System",
                    Avatar = "SY",
                    Time = DateTime.Now.ToString("h:mm tt"),
                    CreatedAt = DateTime.Now,
                    Content = $"Welcome to #{channelName}! This is the general discussion channel.",
                    Reactions = new List<Reaction>()
                });
            }
            else
            {
                // Fallback message when no channel is specified
                messages.Add(new Message
                {
                    Id = 1,
                    UserId = userId,
                    User = "Current User",
                    Avatar = "U",
                    Time = DateTime.Now.ToString("h:mm tt"),
                    CreatedAt = DateTime.Now.AddHours(-1),
                    Content = matterId.HasValue 
                        ? "Matter-specific communications will appear here." 
                        : "Communications will appear here.",
                    Reactions = new List<Reaction>()
                });
            }

            return Task.FromResult(messages);
        }

        #endregion
        
        #region API Endpoints for Communications Sidebar
        
        // GET /api/communications/channels/{channelId}/messages
        [Authorize]
        [HttpGet("/api/communications/channels/{channelId}/messages")]
        public async Task<IActionResult> GetChannelMessages(string channelId)
        {
            try
            {
                var customUser = HttpContext.Items["CustomUser"] as User;
                if (customUser == null)
                {
                    return Unauthorized();
                }

                // Parse channel ID to extract organization and channel info
                // Format: "org_{orgId}_channel_{channelName}" or "matter_{matterId}_channel_{channelName}"
                var messages = new List<object>();
                
                if (channelId.StartsWith("org_"))
                {
                    // Organization channel
                    var parts = channelId.Split('_');
                    if (parts.Length >= 4)
                    {
                        var channelName = string.Join("_", parts.Skip(3));
                        messages = await LoadDemoMessagesForChannel(customUser.Id, null, channelName);
                    }
                }
                else if (channelId.StartsWith("matter_"))
                {
                    // Matter channel
                    var parts = channelId.Split('_');
                    if (parts.Length >= 4 && int.TryParse(parts[1], out int matterId))
                    {
                        var channelName = string.Join("_", parts.Skip(3));
                        messages = await LoadDemoMessagesForChannel(customUser.Id, matterId, channelName);
                    }
                }
                
                return Json(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading messages for channel {ChannelId}", channelId);
                return Json(new List<object>());
            }
        }
        
        // POST /api/communications/channels/{channelId}/messages
        [Authorize]
        [HttpPost("/api/communications/channels/{channelId}/messages")]
        public async Task<IActionResult> SendMessage(string channelId, [FromBody] SendMessageRequest request)
        {
            try
            {
                var customUser = HttpContext.Items["CustomUser"] as User;
                if (customUser == null)
                {
                    return Unauthorized();
                }

                // In production, save to database via ChatService
                // For now, just return success with the message
                var message = new
                {
                    userId = customUser.Id.ToString(),
                    userName = $"{customUser.FirstName} {customUser.LastName}".Trim(),
                    userAvatar = $"{customUser.FirstName?.FirstOrDefault()}{customUser.LastName?.FirstOrDefault()}",
                    content = request.Content,
                    createdAt = DateTime.UtcNow
                };
                
                return Json(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to channel {ChannelId}", channelId);
                return StatusCode(500, new { error = "Failed to send message" });
            }
        }
        
        private async Task<List<object>> LoadDemoMessagesForChannel(int userId, int? matterId, string channelName)
        {
            var messages = new List<object>();
            
            // Add some demo messages based on channel type
            if (matterId.HasValue)
            {
                // Matter-specific messages
                messages.Add(new
                {
                    userId = userId.ToString(),
                    userName = "Kyle Wang",
                    userAvatar = "KW",
                    content = $"Starting discussion for this matter in #{channelName}",
                    createdAt = DateTime.UtcNow.AddHours(-2)
                });
                messages.Add(new
                {
                    userId = (userId + 1).ToString(),
                    userName = "Sarah Chen",
                    userAvatar = "SC",
                    content = "Thanks for setting this up! I've reviewed the initial documents.",
                    createdAt = DateTime.UtcNow.AddHours(-1)
                });
            }
            else
            {
                // Organization channel messages
                messages.Add(new
                {
                    userId = "system",
                    userName = "System",
                    userAvatar = "SY",
                    content = $"Welcome to #{channelName}!",
                    createdAt = DateTime.UtcNow.AddDays(-1)
                });
                messages.Add(new
                {
                    userId = userId.ToString(),
                    userName = "Current User",
                    userAvatar = "CU",
                    content = "Looking forward to collaborating here!",
                    createdAt = DateTime.UtcNow.AddHours(-3)
                });
            }
            
            return await Task.FromResult(messages);
        }
        
        #endregion
    }
    
    public class SendMessageRequest
    {
        public string Content { get; set; } = string.Empty;
        public string? ChannelId { get; set; }
    }
}

