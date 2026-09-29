using Microsoft.EntityFrameworkCore;
using Certio.Domain.Services;
using Certio.Infrastructure.Data;
using Certio.Application.Services;
using Certio.Application.Interfaces;
using Certio.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Certio.Web.Services;

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _context;
    private readonly IAIAgentService _aiAgentService;
    private readonly AIBackgroundService _aiBackgroundService;
    private readonly ICacheService _cacheService;
    private readonly IAuditService _auditService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ApplicationDbContext context, 
        IAIAgentService aiAgentService, 
        AIBackgroundService aiBackgroundService, 
        ICacheService cacheService,
        IAuditService auditService,
        IPermissionService permissionService,
        ILogger<ChatService> logger)
    {
        _context = context;
        _aiAgentService = aiAgentService;
        _aiBackgroundService = aiBackgroundService;
        _cacheService = cacheService;
        _auditService = auditService;
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<Conversation> CreateConversationAsync(int organizationId, int userId, string title, string description, int? matterId = null)
    {
        // Validate user has access to organization (direct membership or firm-based access)
        var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, organizationId);
        var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, organizationId);
        
        if (!isOrgMember && !hasFirmAccess)
        {
            _logger.LogWarning("User {UserId} attempted to create conversation in unauthorized org {OrgId}", userId, organizationId);
            throw new UnauthorizedOperationException(userId, "create", "Conversation", "Not a member of organization");
        }

        // If linked to a matter, validate matter access
        if (matterId.HasValue)
        {
            var canAccessMatter = await _permissionService.CanAccessMatterAsync(userId, matterId.Value);
            if (!canAccessMatter)
            {
                _logger.LogWarning("User {UserId} attempted to create conversation linked to unauthorized matter {MatterId}", userId, matterId.Value);
                throw new UnauthorizedOperationException(userId, "create", "Conversation", "No access to linked matter");
            }
        }

        var conversation = new Conversation
        {
            OrganizationId = organizationId,
            CreatedById = userId,
            Title = title,
            Description = description,
            MatterId = matterId,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        // Audit log
        // Audit logging now handled automatically by AuditInterceptor

        // Invalidate conversation cache for this user/organization
        await _cacheService.InvalidateUserConversationsCacheAsync(userId, organizationId);

        _logger.LogInformation("User {UserId} created conversation {ConvId} in org {OrgId}", userId, conversation.Id, organizationId);

        return conversation;
    }

    private async Task<string> BuildDashboardCardContextAsync(int organizationId)
    {
        var today = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;

        var overdueTasksRaw = await _context.TaskItems
            .AsNoTracking()
            .Include(t => t.Matter)
            .Include(t => t.TaskAssignments)
                .ThenInclude(a => a.User)
            .Where(t => t.OrgId == organizationId && !t.IsDeleted && t.DueDate.HasValue && t.DueDate.Value.Date < today && t.Status != "Completed")
            .OrderBy(t => t.DueDate)
            .Take(12)
            .ToListAsync();

        var overdueTasks = overdueTasksRaw.Select(t =>
        {
            var dueDate = t.DueDate?.Date;
            var daysOverdue = dueDate.HasValue ? (today - dueDate.Value).Days : (int?)null;
            var assignees = t.TaskAssignments
                .Where(a => a.User != null && a.AssignmentType == "Assignee")
                .Select(a => new DashboardAssignee(
                    $"{a.User!.FirstName} {a.User.LastName}".Trim(),
                    string.IsNullOrWhiteSpace(a.Role) ? a.AssignmentType : a.Role))
                .ToList();

            return new DashboardTaskSummary(
                t.Id,
                t.Title,
                t.Matter?.Title ?? "Unspecified Event",
                t.Priority,
                t.Status,
                dueDate,
                daysOverdue,
                assignees);
        }).ToList();

        var upcomingEventsRaw = await _context.CalendarEvents
            .AsNoTracking()
            .Include(e => e.Matter)
            .Where(e => e.OrgId == organizationId && !e.IsDeleted && e.StartDateTime >= now && e.StartDateTime <= now.AddDays(7))
            .OrderBy(e => e.StartDateTime)
            .Take(10)
            .ToListAsync();

        var upcomingEvents = upcomingEventsRaw.Select(e => new DashboardEventSummary(
            e.Id,
            e.Title,
            e.EventType,
            e.StartDateTime,
            e.EndDateTime,
            e.Matter?.Title,
            e.Location)).ToList();

        var mattersRaw = await _context.Matters
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && !m.IsDeleted && m.Status != "Completed")
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.Status,
                m.PracticeArea,
                LastActivity = m.ModifiedAt ?? m.LastModifiedDate ?? m.CreatedAt
            })
            .ToListAsync();

        var staleEvents = mattersRaw
            .Select(m =>
            {
                var lastActivity = m.LastActivity; // Already non-nullable due to ?? m.CreatedAt fallback
                var daysInactive = (now - lastActivity).TotalDays;
                return new DashboardStaleEventSummary(
                    m.Id,
                    m.Title,
                    m.Status,
                    m.PracticeArea,
                    lastActivity,
                    Math.Round(daysInactive, 1));
            })
            .OrderByDescending(m => m.DaysSinceActivity)
            .Take(5)
            .ToList();

        var recentActivityRaw = await _context.TaskItems
            .AsNoTracking()
            .Include(t => t.Matter)
            .Where(t => t.OrgId == organizationId && !t.IsDeleted)
            .OrderByDescending(t => t.ModifiedAt ?? t.LastModifiedAt ?? t.CreatedAt)
            .Take(8)
            .ToListAsync();

        var recentActivity = recentActivityRaw.Select(t =>
        {
            var activityDate = t.ModifiedAt ?? t.LastModifiedAt ?? t.CreatedAt;
            var activityType = t.Status == "Completed" ? "Task completed" : "Task updated";
            return new DashboardActivitySummary(
                t.Id,
                t.Title,
                t.Matter?.Title ?? "Unspecified Event",
                activityType,
                activityDate);
        }).ToList();

        var stats = new DashboardStats(
            overdueTasks.Count,
            upcomingEvents.Count(e => e.EventType.Equals("Call", StringComparison.OrdinalIgnoreCase) || e.EventType.Equals("Meeting", StringComparison.OrdinalIgnoreCase)),
            staleEvents.Count(m => m.DaysSinceActivity >= 7));

        var context = new DashboardCardContext(
            organizationId,
            DateTime.UtcNow,
            stats,
            overdueTasks,
            upcomingEvents,
            staleEvents,
            recentActivity);

        return JsonSerializer.Serialize(context, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    public async Task<Conversation> CreateChannelAsync(int organizationId, int userId, string title, string description, string channelType = "Group", bool isPrivateChannel = false, int? matterId = null)
    {
        // Validate user has access to organization (direct membership or firm-based access)
        var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, organizationId);
        var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, organizationId);
        
        if (!isOrgMember && !hasFirmAccess)
        {
            _logger.LogWarning("User {UserId} attempted to create channel in unauthorized org {OrgId}", userId, organizationId);
            throw new UnauthorizedOperationException(userId, "create", "Channel", "Not a member of organization");
        }

        // If linked to a matter, validate matter access
        if (matterId.HasValue)
        {
            var canAccessMatter = await _permissionService.CanAccessMatterAsync(userId, matterId.Value);
            if (!canAccessMatter)
            {
                _logger.LogWarning("User {UserId} attempted to create channel linked to unauthorized matter {MatterId}", userId, matterId.Value);
                throw new UnauthorizedOperationException(userId, "create", "Channel", "No access to linked matter");
            }
        }

        var conversation = new Conversation
        {
            OrganizationId = organizationId,
            CreatedById = userId,
            Title = title,
            Description = description,
            MatterId = matterId,
            ChannelType = channelType,
            IsChannel = true,
            IsPrivateChannel = isPrivateChannel,
            ChannelDescription = description,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        // Audit log
        // Audit logging now handled automatically by AuditInterceptor

        _logger.LogInformation("User {UserId} created channel {ChannelId} in org {OrgId}", userId, conversation.Id, organizationId);

        return conversation;
    }

    public async Task<ChatMessage> SendMessageAsync(int conversationId, int? userId, string userType, string content, string messageType = "Text", string? metadata = null)
    {
        // Defense-in-depth: Verify conversation exists and user has access
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        
        if (conversation == null)
        {
            _logger.LogWarning("SECURITY: Attempted to send message to non-existent conversation {ConversationId}", conversationId);
            throw new ArgumentException("Conversation not found");
        }

        if (userId.HasValue)
        {
            var hasAccess = await CanUserAccessConversationAsync(conversationId, userId.Value, conversation.OrganizationId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to send message to unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId.Value, conversationId, conversation.OrganizationId);
                throw new UnauthorizedOperationException(userId.Value, "send a message to", "Conversation", "Access denied");
            }
        }

        var message = new ChatMessage
        {
            ConversationId = conversationId,
            UserId = userId,
            UserType = userType,
            Content = content,
            MessageType = messageType,
            IsFromAI = false,
            CreatedAt = DateTime.UtcNow,
            Metadata = metadata // Store attachment metadata
        };

        _context.ChatMessages.Add(message);

        // Update conversation last message time
            conversation.LastMessageAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Only process AI agents if the message is meaningful and not a simple greeting
        if (ShouldProcessAIAgents(content))
        {
            _ = _aiBackgroundService.ProcessAIAgentsAsync(conversationId.ToString());
        }

        return message;
    }

    public async Task<ChatMessage> SendChannelMessageAsync(int conversationId, int? userId, string userType, string content, string messageType = "Text", int? channelId = null, int? replyToMessageId = null)
    {
        // Defense-in-depth: Verify conversation exists and user has access
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        
        if (conversation == null)
        {
            _logger.LogWarning("SECURITY: Attempted to send channel message to non-existent conversation {ConversationId}", conversationId);
            throw new ArgumentException("Channel not found");
        }

        if (userId.HasValue)
        {
            var hasAccess = await CanUserAccessConversationAsync(conversationId, userId.Value, conversation.OrganizationId);
            if (!hasAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to send channel message to unauthorized conversation {ConversationId} in org {OrgId}", 
                    userId.Value, conversationId, conversation.OrganizationId);
                throw new UnauthorizedOperationException(userId.Value, "send a message to", "Channel", "Access denied");
            }
        }

        var message = new ChatMessage
        {
            ConversationId = conversationId,
            UserId = userId,
            UserType = userType,
            Content = content,
            MessageType = messageType,
            IsFromAI = false,
            IsChannelMessage = true,
            ChannelId = channelId,
            ReplyToMessageId = replyToMessageId,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);

        // Update conversation last message time
            conversation.LastMessageAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        
        // Invalidate cache for this channel (Discord/Slack style)
        await _cacheService.InvalidateChannelCacheAsync(conversationId);

        return message;
    }

    public async Task<ChatMessage> EditMessageAsync(int messageId, string newContent, int userId)
    {
        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId);

        if (message == null)
        {
            throw new ArgumentException("Message not found or user not authorized to edit");
        }

        message.Content = newContent;
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;
        message.LastModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return message;
    }

    public async Task<bool> AddReactionAsync(int messageId, int userId, string emoji)
    {
        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null) return false;

        // Parse existing reactions or create new dictionary
        var reactions = new Dictionary<string, List<int>>();
        if (!string.IsNullOrEmpty(message.Reactions))
        {
            try
            {
                reactions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<int>>>(message.Reactions) ?? new Dictionary<string, List<int>>();
            }
            catch
            {
                reactions = new Dictionary<string, List<int>>();
            }
        }

        // Add or update reaction
        if (!reactions.ContainsKey(emoji))
        {
            reactions[emoji] = new List<int>();
        }

        if (!reactions[emoji].Contains(userId))
        {
            reactions[emoji].Add(userId);
        }

        // Serialize back to JSON
        message.Reactions = System.Text.Json.JsonSerializer.Serialize(reactions);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RemoveReactionAsync(int messageId, int userId, string emoji)
    {
        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null) return false;

        // Parse existing reactions
        var reactions = new Dictionary<string, List<int>>();
        if (!string.IsNullOrEmpty(message.Reactions))
        {
            try
            {
                reactions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<int>>>(message.Reactions) ?? new Dictionary<string, List<int>>();
            }
            catch
            {
                return false;
            }
        }

        // Remove reaction
        if (reactions.ContainsKey(emoji))
        {
            reactions[emoji].Remove(userId);
            if (reactions[emoji].Count == 0)
            {
                reactions.Remove(emoji);
            }
        }

        // Serialize back to JSON
        message.Reactions = System.Text.Json.JsonSerializer.Serialize(reactions);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<ChatMessage>> GetConversationMessagesAsync(int conversationId, int? limit = null)
    {
        // Cache key includes limit so UI can request a small window while AI can request full history.
        var cacheKey = limit.HasValue
            ? $"conv_msgs:{conversationId}:limit:{limit.Value}"
            : $"conv_msgs:{conversationId}:all";

        var cached = await _cacheService.GetAsync<List<ChatMessage>>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        // Use AsNoTracking for fast read-only chat history.
        List<ChatMessage> messages;

        if (limit.HasValue && limit.Value > 0)
        {
            // Fetch most recent N and then restore chronological order for the UI.
            messages = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit.Value)
                .ToListAsync();

            messages.Reverse();
        }
        else
        {
            messages = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }

        // Cache briefly (hot path when users switch conversations / refresh).
        await _cacheService.SetAsync(cacheKey, messages, TimeSpan.FromMinutes(2));

        return messages;
    }

    public async Task<List<Conversation>> GetUserConversationsAsync(int userId, int organizationId)
    {
        // Try to get from cache first
        var cachedConversations = await _cacheService.GetUserConversationsAsync<Conversation>(userId, organizationId);
        if (cachedConversations != null)
        {
            return cachedConversations;
        }

        var conversations = await _context.Conversations
            .Where(c => c.OrganizationId == organizationId &&
                        c.CreatedById == userId &&
                        !c.IsDeleted)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();

        // Cache the result for future requests
        await _cacheService.SetUserConversationsAsync(userId, organizationId, conversations);

        return conversations;
    }

    public async Task<List<Conversation>> GetUserAIConversationsAsync(int userId, int organizationId)
    {
        // Try to get from cache first
        var cachedConversations = await _cacheService.GetUserAIConversationsAsync<Conversation>(userId, organizationId);
        if (cachedConversations != null)
        {
            return cachedConversations;
        }

        // Get all non-channel conversations created by the user
        // Include all conversations (with or without messages) - the AI chat UI should show all user-created AI conversations
        var conversations = await _context.Conversations
            .Where(c => c.OrganizationId == organizationId && 
                       c.CreatedById == userId && 
                       !c.IsChannel &&
                       !c.IsDeleted) // Only filter out channels and deleted conversations
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();

        // Cache the result for future requests
        await _cacheService.SetUserAIConversationsAsync(userId, organizationId, conversations);

        return conversations;
    }

    public async Task<Conversation?> GetConversationAsync(int conversationId, int organizationId)
    {
        return await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.OrganizationId == organizationId);
    }

    public async Task<List<Conversation>> GetOrganizationConversationsAsync(int organizationId, int? userId = null)
    {
        var query = _context.Conversations
            .Where(c => c.OrganizationId == organizationId);

        if (userId.HasValue)
        {
            query = query.Where(c => c.CreatedById == userId.Value);
        }

        return await query
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();
    }

    public async Task<List<Conversation>> GetChannelsAsync(int organizationId, bool includePrivateChannels = false)
    {
        // Try cache first (Slack style - channel list cached)
        var cacheKey = $"channels:{organizationId}:{includePrivateChannels}";
        var cachedChannels = await _cacheService.GetAsync<List<Conversation>>(cacheKey);
        
        if (cachedChannels != null)
        {
            return cachedChannels;
        }

        // Cache miss - get from database
        var query = _context.Conversations
            .Where(c => c.OrganizationId == organizationId && c.IsChannel);

        if (!includePrivateChannels)
        {
            query = query.Where(c => !c.IsPrivateChannel);
        }

        var channels = await query
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();
        
        // Cache for next time (longer TTL for channel metadata)
        await _cacheService.SetAsync(cacheKey, channels, TimeSpan.FromHours(1));

        return channels;
    }

    public async Task<List<Conversation>> GetUserChannelsAsync(int userId, int organizationId)
    {
        return await _context.Conversations
            .Where(c => c.OrganizationId == organizationId && c.IsChannel && c.CreatedById == userId)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();
    }

    public async Task<List<ChatMessage>> GetChannelMessagesAsync(int conversationId, int? channelId = null)
    {
        // Try to get from cache first (Discord/Slack style - last 50 messages cached)
        var cachedMessages = await _cacheService.GetRecentMessagesAsync<ChatMessage>(conversationId, 50);
        
        if (cachedMessages != null && cachedMessages.Count > 0)
        {
            // Cache hit! Return cached messages
            return cachedMessages;
        }

        // Cache miss - get from database
        var query = _context.ChatMessages
            .Include(m => m.User) // Include User navigation property
            .Where(m => m.ConversationId == conversationId && m.IsChannelMessage);

        if (channelId.HasValue)
        {
            query = query.Where(m => m.ChannelId == channelId.Value);
        }

        var messages = await query
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        
        // Cache the recent messages for next time
        if (messages.Count > 0)
        {
            await _cacheService.SetRecentMessagesAsync(conversationId, messages, 50);
        }

        return messages;
    }

    public async Task<List<ChatMessage>> GetMessagesWithRepliesAsync(int conversationId)
    {
        return await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .Include(m => m.Replies)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }


    public async Task<ClarityExplanation> RequestClarityAsync(int conversationId, string text, string userType)
    {
        return await _aiAgentService.ExplainLegalLanguageAsync(text, userType);
    }

    public async Task<ReplySuggestion> GetReplySuggestionsAsync(int conversationId, List<ChatMessage> messages, string userType)
    {
        return await _aiAgentService.SuggestReplyAsync(conversationId.ToString(), messages, userType);
    }

    public async Task<Dictionary<string, object>> GetAIInsightsAsync(int conversationId)
    {
        try
        {
            var messages = await GetConversationMessagesAsync(conversationId);
            var aiMessages = messages.Where(m => m.IsFromAI && m.MessageType.StartsWith("AI_")).ToList();
            
            var insights = new Dictionary<string, object>();
            
            foreach (var aiMessage in aiMessages)
            {
                try
                {
                    var content = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(aiMessage.Content);
                    if (content != null)
                    {
                        insights[aiMessage.MessageType] = content;
                    }
                }
                catch
                {
                    // If JSON parsing fails, store as raw content
                    insights[aiMessage.MessageType] = aiMessage.Content;
                }
            }
            
            return insights;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting AI insights: {ex.Message}");
            return new Dictionary<string, object>();
        }
    }

    public async Task<ChatSummary?> GetConversationSummaryAsync(int conversationId)
    {
        try
        {
            var messages = await GetConversationMessagesAsync(conversationId);
            var summaryMessage = messages.FirstOrDefault(m => m.MessageType == "AI_Summary" && m.IsFromAI);
            
            if (summaryMessage != null)
            {
                return System.Text.Json.JsonSerializer.Deserialize<ChatSummary>(summaryMessage.Content);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting conversation summary: {ex.Message}");
            return null;
        }
    }

    public async Task<ClientGoal?> GetClientGoalsAsync(int conversationId)
    {
        try
        {
            var messages = await GetConversationMessagesAsync(conversationId);
            var goalsMessage = messages.FirstOrDefault(m => m.MessageType == "AI_Goal" && m.IsFromAI);
            
            if (goalsMessage != null)
            {
                return System.Text.Json.JsonSerializer.Deserialize<ClientGoal>(goalsMessage.Content);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting client goals: {ex.Message}");
            return null;
        }
    }

    public async Task<ReplySuggestion?> GetLatestReplySuggestionAsync(int conversationId)
    {
        try
        {
            var messages = await GetConversationMessagesAsync(conversationId);
            var replyMessage = messages.LastOrDefault(m => m.MessageType == "AI_Reply" && m.IsFromAI);
            
            if (replyMessage != null)
            {
                return System.Text.Json.JsonSerializer.Deserialize<ReplySuggestion>(replyMessage.Content);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting reply suggestion: {ex.Message}");
            return null;
        }
    }

    public async Task<ChatMessage> GenerateAIResponseAsync(int conversationId, string userMessage)
    {
        try
        {
            // Get conversation history
            var messages = await GetConversationMessagesAsync(conversationId);
            
            // Determine user type from conversation context
            var userType = DetermineUserType(messages, userMessage);
            
            // Generate intelligent conversational response
            var aiResponse = await _aiAgentService.GenerateConversationalResponseAsync(
                conversationId.ToString(), messages, userMessage);
            
            // Create AI message with extra metadata
            var aiMessage = CreateAIMessage(conversationId, aiResponse, "AI_Response", "Notal AI", new
            {
                user_type_detected = userType,
                response_type = "intelligent_conversational",
                processing_time = DateTime.UtcNow,
                message_length = userMessage.Length,
                conversation_length = messages.Count
            });

            // Save message and update conversation
            await SaveAIMessageAsync(aiMessage, conversationId);

            return aiMessage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating AI response: {ex.Message}");
            
            // Return intelligent fallback response
            var fallbackMessage = CreateAIMessage(conversationId, 
                GenerateIntelligentFallbackResponse(userMessage), 
                "AI_Response", "ServiceUnavailable");

            await SaveAIMessageAsync(fallbackMessage, conversationId);
            return fallbackMessage;
        }
    }

    public async IAsyncEnumerable<string> GenerateAIResponseStreamAsync(int conversationId, string userMessage, string? aiMode = null)
    {
        // Get conversation history
        var messages = await GetConversationMessagesAsync(conversationId);
        
        // Get organization ID from conversation to retrieve AI tier preference
        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        
        var aiModelTier = Certio.Domain.Organizations.AIModelTier.Auto; // Default to Auto
        if (conversation != null)
        {
            var org = await _context.Organizations.FindAsync(new object[] { conversation.OrganizationId });
            if (org != null)
            {
                aiModelTier = org.GetAIModelTier();
            }
        }
        
        // Determine user type from conversation context
        var userType = DetermineUserType(messages, userMessage);
        
        // Generate streaming intelligent conversational response
        var fullResponse = new System.Text.StringBuilder();
        
        await foreach (var chunk in _aiAgentService.GenerateConversationalResponseStreamAsync(
            conversationId.ToString(),
            messages,
            userMessage,
            aiModelTier: aiModelTier,
            aiMode: aiMode))
        {
            fullResponse.Append(chunk);
            yield return chunk;
        }

        // After streaming is complete, save the full message to database
        try
        {
            var aiMessage = CreateAIMessage(conversationId, fullResponse.ToString(), "AI_Response", "Notal AI", new
            {
                user_type_detected = userType,
                response_type = "intelligent_conversational_stream",
                processing_time = DateTime.UtcNow,
                message_length = userMessage.Length,
                conversation_length = messages.Count
            });

            await SaveAIMessageAsync(aiMessage, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving streaming AI response for conversation {ConversationId}", conversationId);
        }
    }

    public async IAsyncEnumerable<string> GenerateDashboardCardStreamAsync(string userMessage, string userType, int organizationId, int userId, string cardType)
    {
        // Build real data context for dashboard cards
        var dataContextJson = await BuildDashboardCardContextAsync(organizationId);
        var normalizedCardType = string.IsNullOrWhiteSpace(cardType) ? "dashboard-card" : cardType;
        var contextualPrompt = $"""
You are generating the '{normalizedCardType}' dashboard card for organization {organizationId}.
Use ONLY the structured data provided below. If specific information is missing, clearly state that it is unavailable rather than guessing.

DATA_CONTEXT_JSON:
{dataContextJson}

INSTRUCTIONS:
{userMessage}

Rules:
1. Match counts, dates, and names exactly as provided in the dataset.
2. When referencing people, use the role/title specified in the data (do not assume new titles).
3. When listing overdue items, include the task title, event, due date, and priority as given.
4. If a section has zero items, explicitly state that there are none.
5. Never invent events, tasks, or people not present in DATA_CONTEXT_JSON.
""";

        // Generate streaming response using the contextualized prompt
        var placeholderMessages = new List<ChatMessage>
        {
            new ChatMessage
            {
                Id = 0,
                ConversationId = 0,
                UserId = userId,
                UserType = userType,
                Content = contextualPrompt,
                MessageType = "DashboardPrompt",
                IsFromAI = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = true
            }
        };
        
        // Get organization's AI tier preference
        var aiModelTier = Certio.Domain.Organizations.AIModelTier.Auto; // Default to Auto
        var org = await _context.Organizations.FindAsync(new object[] { organizationId });
        if (org != null)
        {
            aiModelTier = org.GetAIModelTier();
        }
        
        // Use a unique identifier for dashboard cards (not a real conversation ID)
        var dashboardCardId = $"dashboard-{DateTime.UtcNow:yyyyMMddHHmmss}";
        
        await foreach (var chunk in _aiAgentService.GenerateConversationalResponseStreamAsync(
            dashboardCardId, 
            placeholderMessages, 
            contextualPrompt, 
            aiModelTier: aiModelTier,
            fallbackUserId: userId,
            fallbackOrganizationId: organizationId))
        {
            yield return chunk;
        }
    }

    private string DetermineUserType(List<ChatMessage> messages, string userMessage)
    {
        // If this is a new conversation, infer from message content
        if (messages.Count == 0)
        {
            var messageLower = userMessage.ToLower();
            return messageLower.Contains("business") || messageLower.Contains("company") || messageLower.Contains("startup") 
                ? "Business" : "Client";
        }

        // Analyze conversation to determine most common user type
        var userTypeCounts = messages
            .Where(m => !m.IsFromAI)
            .GroupBy(m => m.UserType)
            .ToDictionary(g => g.Key, g => g.Count());

        return userTypeCounts.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key ?? "Client";
    }

    private bool ShouldProcessAIAgents(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;

        var trimmedContent = content.Trim().ToLower();
        
        // Check for simple greetings and short responses
        var simpleGreetings = new[]
        {
            "hello", "hi", "hey", "good morning", "good afternoon", "good evening",
            "thanks", "thank you", "ok", "okay", "yes", "no", "sure", "alright",
            "bye", "goodbye", "see you", "later", "ok bye", "thanks bye",
            "how are you", "how are you?", "what's up", "what's up?", "how's it going",
            "how's it going?", "how do you do", "how do you do?", "nice to meet you",
            "good", "great", "fine", "cool", "awesome", "perfect", "excellent"
        };

        // Don't process simple greetings
        if (simpleGreetings.Contains(trimmedContent)) return false;

        // Don't process very short messages (less than 15 characters)
        if (trimmedContent.Length < 15) return false;

        // Don't process messages that are just punctuation or numbers
        if (trimmedContent.All(c => char.IsPunctuation(c) || char.IsWhiteSpace(c) || char.IsDigit(c))) return false;

        // Don't process single word responses
        if (trimmedContent.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 1) return false;

        // Only process messages with substantial legal or business content
        var legalKeywords = new[]
        {
            "contract", "agreement", "legal", "law", "lawyer", "attorney", "court", "lawsuit",
            "business", "company", "incorporation", "llc", "corporation", "partnership",
            "employment", "hiring", "termination", "discrimination", "harassment",
            "intellectual property", "patent", "trademark", "copyright",
            "compliance", "regulation", "audit", "certification",
            "real estate", "property", "lease", "rental", "mortgage",
            "litigation", "dispute", "settlement", "trial", "mediation"
        };

        // Must contain at least one legal/business keyword for background processing
        if (!legalKeywords.Any(keyword => trimmedContent.Contains(keyword))) return false;

        return true;
    }

    private ChatMessage CreateAIMessage(int conversationId, string content, string messageType, string agentType, object? metadata = null)
    {
        return new ChatMessage
        {
            ConversationId = conversationId,
            UserId = null, // AI messages don't have a user ID
            UserType = "AI",
            Content = content,
            MessageType = messageType,
            IsFromAI = true,
            IsAIGenerated = true, // For audit interceptor
            AIAgentType = agentType,
            SourceConversationId = conversationId, // Track provenance
            CreatedAt = DateTime.UtcNow,
            Metadata = metadata != null ? System.Text.Json.JsonSerializer.Serialize(metadata) : null
        };
    }

    private async Task SaveAIMessageAsync(ChatMessage aiMessage, int conversationId)
    {
        _context.ChatMessages.Add(aiMessage);
        
        // Update conversation last message time
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conversation != null)
        {
            conversation.LastMessageAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    private string GenerateIntelligentFallbackResponse(string userMessage)
    {
        // Provide a contextual fallback based on the user message
        var messageLower = userMessage.ToLower();
        var contextualResponse = "";
        
        if (messageLower.Contains("contract") || messageLower.Contains("agreement"))
            contextualResponse = "I understand you have questions about contracts. ";
        else if (messageLower.Contains("event") || messageLower.Contains("vendor"))
            contextualResponse = "I see you need help with an event. ";
        else if (messageLower.Contains("business") || messageLower.Contains("company"))
            contextualResponse = "I understand you have business-related questions. ";
        
        return $"<strong> AI Services Temporarily Unavailable</strong><br><br>" +
               $"{contextualResponse}While our AI services are being updated, you can still use other services normally.<br><br>" +
               "We apologize for any inconvenience. Our AI services will be back online shortly!";
    }

    private sealed record DashboardCardContext(
        int OrganizationId,
        DateTime GeneratedAtUtc,
        DashboardStats Stats,
        List<DashboardTaskSummary> OverdueTasks,
        List<DashboardEventSummary> UpcomingEvents,
        List<DashboardStaleEventSummary> StaleEvents,
        List<DashboardActivitySummary> RecentActivity);

    private sealed record DashboardStats(
        int TotalOverdueTasks,
        int UpcomingCallsOrMeetings,
        int StaleEventCount);

    private sealed record DashboardTaskSummary(
        int TaskId,
        string Title,
        string Event,
        string Priority,
        string Status,
        DateTime? DueDate,
        int? DaysOverdue,
        List<DashboardAssignee> Assignees);

    private sealed record DashboardAssignee(string Name, string Role);

    // Note: represents a calendar occurrence; ParentEvent is the title of the broader
    // event/project (domain entity Matter) it is scheduled under, if any.
    private sealed record DashboardEventSummary(
        int EventId,
        string Title,
        string EventType,
        DateTime StartDateTime,
        DateTime EndDateTime,
        string? ParentEvent,
        string? Location);

    private sealed record DashboardStaleEventSummary(
        int EventId,
        string Title,
        string Status,
        string EventType,
        DateTime? LastActivity,
        double DaysSinceActivity);

    private sealed record DashboardActivitySummary(
        int ItemId,
        string Title,
        string Event,
        string ActivityType,
        DateTime ActivityDate);

    public async Task<bool> RenameConversationAsync(int conversationId, int organizationId, string newTitle)
    {
        try
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.OrganizationId == organizationId);
            
            if (conversation == null)
            {
                return false;
            }
            
            conversation.Title = newTitle;
            await _context.SaveChangesAsync();
            
            // Invalidate caches to ensure the renamed conversation is updated in lists
            // We need to get the user ID who created the conversation to invalidate their cache
            // If the current user is renaming it, that's fine, but ideally we invalidate for the creator
            await _cacheService.InvalidateUserConversationsCacheAsync(conversation.CreatedById, organizationId);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error renaming conversation: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteConversationAsync(int conversationId, int organizationId, int userId, string? ipAddress = null, string? userAgent = null)
    {
        try
        {
            // Get the conversation first to verify it exists and user has access
            var conversation = await _context.Conversations
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.OrganizationId == organizationId);
            
            if (conversation == null)
            {
                return false;
            }
            
            var creatorId = conversation.CreatedById;
            
            // Delete all participants first (even though cascade delete should handle this)
            if (conversation.Participants != null && conversation.Participants.Any())
            {
                _context.ConversationParticipants.RemoveRange(conversation.Participants);
            }
            
            // Delete all messages in the conversation
            var messages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .ToListAsync();
            
            if (messages.Any())
            {
                _context.ChatMessages.RemoveRange(messages);
            }
            
            // Finally delete the conversation itself
            _context.Conversations.Remove(conversation);
            
            // Save all changes in a transaction
            await _context.SaveChangesAsync();
            
            // Invalidate caches to ensure the conversation is removed from lists
            await _cacheService.InvalidateUserConversationsCacheAsync(userId, organizationId);
            
            // Also invalidate cache for the conversation creator if different
            if (creatorId != userId)
            {
                await _cacheService.InvalidateUserConversationsCacheAsync(creatorId, organizationId);
            }
            
            // Audit log the deletion
            // Audit logging now handled automatically by AuditInterceptor
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting conversation: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    public async Task<bool> CanUserAccessConversationAsync(int conversationId, int userId, int organizationId)
    {
        try
        {
            var conversation = await _context.Conversations
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                _logger.LogWarning("Conversation {ConversationId} was not found", conversationId);
                return false;
            }

            if (conversation.IsChannel)
            {
                if (conversation.OrganizationId == organizationId
                    && await _permissionService.IsOrganizationMemberAsync(userId, organizationId))
                {
                    return true;
                }

                if (await _permissionService.HasFirmBasedAccessAsync(userId, conversation.OrganizationId))
                {
                    return true;
                }

                if (conversation.MatterId.HasValue
                    && await _permissionService.CanAccessMatterAsync(userId, conversation.MatterId.Value))
                {
                    return true;
                }

                _logger.LogWarning(
                    "User {UserId} denied channel {ConversationId} in org {OrgId}",
                    userId, conversationId, organizationId);
                return false;
            }

            if (conversation.OrganizationId != organizationId)
            {
                return false;
            }

            return conversation.Participants.Any(p => p.UserId == userId)
                || conversation.CreatedById == userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking access for user {UserId} and conversation {ConversationId}", userId, conversationId);
            return false;
        }
    }
}
