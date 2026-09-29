using Certio.Infrastructure.Data;
using Certio.Domain.Services;
using Certio.Domain.Users;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Certio.Web.Services;

public interface IBriefingMessageService
{
    Task SendDailyBriefingAsync(int userId, int organizationId);
    Task SendNotableSuggestionsAsync(int userId, int organizationId);
    bool HasReceivedBriefingToday(int userId);
    Task<object> GenerateBriefingDataAsync(int userId, int organizationId);
    Task<object> GenerateSuggestionsDataAsync(int userId, int organizationId);
}

public class BriefingMessageService : IBriefingMessageService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BriefingMessageService> _logger;
    private const string BRIEFING_CHANNEL_NAME = "daily-briefing";

    public BriefingMessageService(
        ApplicationDbContext context,
        ILogger<BriefingMessageService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public bool HasReceivedBriefingToday(int userId)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var cacheKey = $"briefing_sent:{userId}:{today:yyyyMMdd}";
            
            // Simple in-memory check using static dictionary
            lock (_briefingLock)
            {
                return _briefingSentToday.ContainsKey(cacheKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking daily briefing status for user {UserId}", userId);
            return true; // Assume briefing sent to avoid spam on errors
        }
    }

    private static readonly Dictionary<string, DateTime> _briefingSentToday = new();
    private static readonly object _briefingLock = new();

    private void MarkBriefingSentToday(int userId)
    {
        var today = DateTime.UtcNow.Date;
        var cacheKey = $"briefing_sent:{userId}:{today:yyyyMMdd}";
        
        lock (_briefingLock)
        {
            _briefingSentToday[cacheKey] = DateTime.UtcNow;
            
            // Clean up old entries (older than 2 days)
            var cutoff = DateTime.UtcNow.AddDays(-2);
            var keysToRemove = _briefingSentToday
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in keysToRemove)
            {
                _briefingSentToday.Remove(key);
            }
        }
    }

    public async Task<object> GenerateBriefingDataAsync(int userId, int organizationId)
    {
        try
        {
            if (HasReceivedBriefingToday(userId))
            {
                return new { alreadySent = true };
            }

            var stats = await GatherDailyStatsAsync(userId, organizationId);
            var user = await _context.Users.FindAsync(userId);
            
            if (user == null)
            {
                return new { error = "User not found" };
            }

            var greeting = GetGreeting();
            var firstName = user.FirstName ?? user.Email.Split('@')[0];

            MarkBriefingSentToday(userId);

            return new
            {
                greeting = $"{greeting}, {firstName}!",
                date = DateTime.Now.ToString("dddd, MMMM d"),
                stats = new
                {
                    pendingTasks = stats.PendingTasks,
                    dueToday = stats.DueToday,
                    activeMatters = stats.ActiveMatters,
                    unreadMessages = stats.UnreadMessages,
                    recentDocuments = stats.RecentDocuments
                },
                message = stats.DueToday > 0 
                    ? $"You have **{stats.DueToday} task{(stats.DueToday > 1 ? "s" : "")}** due today - let's tackle them!"
                    : "No tasks due today - great time to get ahead!"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating briefing data for user {UserId}", userId);
            throw;
        }
    }

    public async Task<object> GenerateSuggestionsDataAsync(int userId, int organizationId)
    {
        try
        {
            var suggestions = await GatherNotableSuggestionsAsync(userId, organizationId);
            
            return new
            {
                count = suggestions.Count,
                items = suggestions.Select(s => new
                {
                    type = s.Type,
                    priority = s.Priority,
                    title = s.Title,
                    description = s.Description,
                    actionUrl = s.ActionUrl,
                    icon = GetSuggestionIcon(s.Priority)
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating suggestions data for user {UserId}", userId);
            throw;
        }
    }

    private string GetSuggestionIcon(string priority)
    {
        return priority switch
        {
            "high" => "",
            "medium" => "",
            _ => ""
        };
    }

    public async Task SendDailyBriefingAsync(int userId, int organizationId)
    {
        try
        {
            if (HasReceivedBriefingToday(userId))
            {
                _logger.LogInformation("User {UserId} already received daily briefing today", userId);
                return;
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return;
            }

            // Find or create briefing conversation/channel
            var conversation = await GetOrCreateBriefingChannelAsync(organizationId);
            if (conversation == null)
            {
                _logger.LogWarning("Could not create briefing channel for org {OrgId}", organizationId);
                return;
            }

            // Get today's statistics
            var stats = await GatherDailyStatsAsync(userId, organizationId);

            // Create main briefing message
            var briefingContent = GenerateBriefingContent(user, stats);
            var mainMessage = new ChatMessage
            {
                ConversationId = conversation.Id,
                UserId = null, // System message
                Content = briefingContent,
                Sender = "Certio AI Assistant",
                MessageType = "AI",
                SenderType = "AI",
                AIAgentType = "DailyBriefing",
                IsFromAI = true,
                IsFromUser = false,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId,
                IsAIGenerated = true,
                Metadata = JsonSerializer.Serialize(new
                {
                    briefingDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    stats
                })
            };

            _context.ChatMessages.Add(mainMessage);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sent daily briefing to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending daily briefing to user {UserId}", userId);
        }
    }

    public async Task SendNotableSuggestionsAsync(int userId, int organizationId)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            var conversation = await GetOrCreateBriefingChannelAsync(organizationId);
            if (conversation == null) return;

            // Find the most recent briefing message to thread under
            var recentBriefing = await _context.ChatMessages
                .Where(m => m.ConversationId == conversation.Id &&
                           m.AIAgentType == "DailyBriefing" &&
                           !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            var suggestions = await GatherNotableSuggestionsAsync(userId, organizationId);
            var suggestionsContent = GenerateSuggestionsContent(user, suggestions);

            var suggestionMessage = new ChatMessage
            {
                ConversationId = conversation.Id,
                UserId = null,
                Content = suggestionsContent,
                Sender = "Certio AI Assistant",
                MessageType = "AI",
                SenderType = "AI",
                AIAgentType = "NotableSuggestions",
                IsFromAI = true,
                IsFromUser = false,
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddSeconds(1), // Slightly after briefing
                CreatedById = userId,
                IsAIGenerated = true,
                ParentMessageId = recentBriefing?.Id, // Thread under briefing
                ReplyToMessageId = recentBriefing?.Id,
                Metadata = JsonSerializer.Serialize(new
                {
                    suggestionDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    suggestionCount = suggestions.Count
                })
            };

            _context.ChatMessages.Add(suggestionMessage);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sent notable suggestions to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notable suggestions to user {UserId}", userId);
        }
    }

    private async Task<Conversation?> GetOrCreateBriefingChannelAsync(int organizationId)
    {
        try
        {
            // Look for existing briefing conversation
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.OrganizationId == organizationId &&
                                         c.Title == BRIEFING_CHANNEL_NAME &&
                                         !c.IsDeleted);

            if (conversation != null)
                return conversation;

            // Get first admin user as creator (required field)
            var adminUser = await _context.Users
                .Include(u => u.UserOrganizations)
                .Where(u => u.UserOrganizations.Any(uo => uo.OrganizationId == organizationId && uo.IsActive))
                .FirstOrDefaultAsync();
            
            if (adminUser == null)
            {
                _logger.LogWarning("No users found in org {OrgId} for briefing channel creation", organizationId);
                return null;
            }

            // Create new briefing conversation
            conversation = new Conversation
            {
                OrganizationId = organizationId,
                CreatedById = adminUser.Id,
                Title = BRIEFING_CHANNEL_NAME,
                Description = "Daily briefings and AI-powered suggestions from Certio",
                ConversationType = "System",
                Status = "Active",
                IsChannel = true,
                ChannelType = "Public",
                CreatedAt = DateTime.UtcNow
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating briefing channel for org {OrgId}", organizationId);
            return null;
        }
    }

    private async Task<DailyStats> GatherDailyStatsAsync(int userId, int organizationId)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var sevenDaysAgo = today.AddDays(-7);

        var stats = new DailyStats
        {
            PendingTasks = await _context.TaskItems
                .CountAsync(t => t.OrgId == organizationId &&
                                t.TaskAssignments.Any(a => a.UserId == userId) &&
                                t.Status != "Completed" &&
                                !t.IsDeleted),
                                
            DueToday = await _context.TaskItems
                .CountAsync(t => t.OrgId == organizationId &&
                                t.TaskAssignments.Any(a => a.UserId == userId) &&
                                t.DueDate.HasValue &&
                                t.DueDate.Value.Date == today &&
                                t.Status != "Completed" &&
                                !t.IsDeleted),
                                
            UnreadMessages = await _context.ChatMessages
                .CountAsync(m => m.Conversation.OrganizationId == organizationId &&
                                m.UserId != userId &&
                                !m.IsRead &&
                                !m.IsDeleted),
                                
            ActiveMatters = await _context.Matters
                .CountAsync(m => m.OrganizationId == organizationId &&
                                (m.Assignments.Any(a => a.UserId == userId) || m.CreatedById == userId) &&
                                m.Status != "Closed" &&
                                !m.IsDeleted),
                                
            RecentDocuments = await _context.Documents
                .CountAsync(d => d.CreatedAt >= sevenDaysAgo)
        };

        return stats;
    }

    private async Task<List<NotableSuggestion>> GatherNotableSuggestionsAsync(int userId, int organizationId)
    {
        var suggestions = new List<NotableSuggestion>();
        var now = DateTime.UtcNow;

        // Overdue tasks
        var overdueTasks = await _context.TaskItems
            .Where(t => t.OrgId == organizationId &&
                       t.TaskAssignments.Any(a => a.UserId == userId) &&
                       t.DueDate.HasValue &&
                       t.DueDate.Value < now &&
                       t.Status != "Completed" &&
                       !t.IsDeleted)
            .Take(3)
            .Select(t => new { t.Title, t.DueDate, MatterId = (int?)t.MatterId })
            .ToListAsync();

        foreach (var task in overdueTasks)
        {
            suggestions.Add(new NotableSuggestion
            {
                Type = "overdue_task",
                Priority = "high",
                Title = $"Task overdue: {task.Title}",
                Description = $"Due {task.DueDate:MMM d}",
                ActionUrl = task.MatterId.HasValue ? $"/Matter/Details/{task.MatterId}?tab=tasks" : null
            });
        }

        // Matters without recent activity
        var staleMatters = await _context.Matters
            .Where(m => m.OrganizationId == organizationId &&
                       (m.Assignments.Any(a => a.UserId == userId) || m.CreatedById == userId) &&
                       m.Status == "Active" &&
                       m.ModifiedAt < now.AddDays(-7) &&
                       !m.IsDeleted)
            .Take(2)
            .Select(m => new { m.Title, m.Id, m.ModifiedAt })
            .ToListAsync();

        foreach (var matter in staleMatters)
        {
            suggestions.Add(new NotableSuggestion
            {
                Type = "stale_matter",
                Priority = "medium",
                Title = $"No recent activity: {matter.Title}",
                Description = $"Last updated {matter.ModifiedAt:MMM d}",
                ActionUrl = $"/Matter/Details/{matter.Id}"
            });
        }

        // Unread important messages
        var unreadCount = await _context.ChatMessages
            .CountAsync(m => m.Conversation.OrganizationId == organizationId &&
                            m.UserId != userId &&
                            !m.IsRead &&
                            !m.IsDeleted);

        if (unreadCount > 10)
        {
            suggestions.Add(new NotableSuggestion
            {
                Type = "unread_messages",
                Priority = "medium",
                Title = $"{unreadCount} unread messages",
                Description = "Consider catching up on conversations",
                ActionUrl = $"/Client/{organizationId}/Communications"
            });
        }

        return suggestions.OrderByDescending(s => s.Priority).Take(5).ToList();
    }

    private string GenerateBriefingContent(User user, DailyStats stats)
    {
        var greeting = GetGreeting();
        var firstName = (user.FirstName ?? user.Email.Split('@')[0]);

        return $@"# {greeting}, {firstName}!

Here's your daily briefing for **{DateTime.Now:dddd, MMMM d}**:

## Your Dashboard
• **{stats.PendingTasks}** pending tasks ({stats.DueToday} due today)
• **{stats.ActiveMatters}** active events
• **{stats.UnreadMessages}** unread messages
• **{stats.RecentDocuments}** new documents (last 7 days)

{(stats.DueToday > 0 ? $" You have **{stats.DueToday} task{(stats.DueToday > 1 ? "s" : "")}** due today - let's tackle them!" : " No tasks due today - great time to get ahead!")}

 Click below for Notable Suggestions to optimize your workflow.";
    }

    private string GenerateSuggestionsContent(User user, List<NotableSuggestion> suggestions)
    {
        if (!suggestions.Any())
        {
            return " Everything looks good! No urgent suggestions at this time.";
        }

        var content = $"## Notable Suggestions\n\nBased on your recent activity, here are {suggestions.Count} actionable insight{(suggestions.Count > 1 ? "s" : "")}:\n\n";

        foreach (var suggestion in suggestions)
        {
            var icon = suggestion.Priority == "high" ? "" : suggestion.Priority == "medium" ? "" : "";
            content += $"{icon} **{suggestion.Title}**\n";
            content += $"   {suggestion.Description}\n";
            if (!string.IsNullOrEmpty(suggestion.ActionUrl))
            {
                content += $"   [View →]({suggestion.ActionUrl})\n";
            }
            content += "\n";
        }

        return content;
    }

    private string GetGreeting()
    {
        var hour = DateTime.Now.Hour;
        if (hour < 12) return "Good morning";
        if (hour < 18) return "Good afternoon";
        return "Good evening";
    }

    private class DailyStats
    {
        public int PendingTasks { get; set; }
        public int DueToday { get; set; }
        public int UnreadMessages { get; set; }
        public int ActiveMatters { get; set; }
        public int RecentDocuments { get; set; }
    }

    private class NotableSuggestion
    {
        public string Type { get; set; } = "";
        public string Priority { get; set; } = "low";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string? ActionUrl { get; set; }
    }
}

