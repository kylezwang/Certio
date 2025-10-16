using Microsoft.EntityFrameworkCore;
using Certio.Domain.Services;
using Certio.Infrastructure.Data;
using Certio.Application.Services;
using Certio.Application.Interfaces;
using Certio.Domain.Exceptions;
using Microsoft.Extensions.Logging;

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

    public async Task<ChatMessage> SendMessageAsync(int conversationId, int? userId, string userType, string content, string messageType = "Text")
    {
        var message = new ChatMessage
        {
            ConversationId = conversationId,
            UserId = userId,
            UserType = userType,
            Content = content,
            MessageType = messageType,
            IsFromAI = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);

        // Update conversation last message time
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conversation != null)
        {
            conversation.LastMessageAt = DateTime.UtcNow;
        }

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
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conversation != null)
        {
            conversation.LastMessageAt = DateTime.UtcNow;
        }

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

    public async Task<List<ChatMessage>> GetConversationMessagesAsync(int conversationId)
    {
        return await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
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
            .Where(c => c.OrganizationId == organizationId && c.CreatedById == userId)
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
                       !c.IsChannel) // Only filter out channels - include all conversations regardless of message count
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
            
            // Create AI message with enhanced metadata
            var aiMessage = CreateAIMessage(conversationId, aiResponse, "AI_Response", "IntelligentAI", new
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
            AIAgentType = agentType,
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
        else if (messageLower.Contains("legal") || messageLower.Contains("law"))
            contextualResponse = "I see you need legal assistance. ";
        else if (messageLower.Contains("business") || messageLower.Contains("company"))
            contextualResponse = "I understand you have business-related questions. ";
        
        return $"<strong>🤖 AI Assistant Temporarily Unavailable</strong><br><br>" +
               $"{contextualResponse}While our AI services are being updated, you can still:<br><br>" +
               "• Continue messaging with your legal team<br>" +
               "• Access all previous conversations<br>" +
               "• Use all chat features normally<br><br>" +
               "Our AI assistant will be back online shortly to provide intelligent responses!";
    }

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
            // First delete all messages in the conversation
            var messages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .ToListAsync();
            
            _context.ChatMessages.RemoveRange(messages);
            
            // Then delete the conversation itself
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.OrganizationId == organizationId);
            
            if (conversation != null)
            {
                _context.Conversations.Remove(conversation);
            }
            
            await _context.SaveChangesAsync();
            
            // Audit log the deletion
            // Audit logging now handled automatically by AuditInterceptor
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting conversation: {ex.Message}");
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
                return false;
            }

            // For channels, check multiple access paths
            if (conversation.IsChannel)
            {
                // 1. Direct organization membership - conversation belongs to current organization
                if (conversation.OrganizationId == organizationId)
                {
                    // Check if user is a member of the organization
                    var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, organizationId);
                    if (isOrgMember)
                    {
                        return true;
                    }
                }

                // 2. Cross-organization access through firm relationships
                // Check if user's law firm has access to the channel's organization
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, conversation.OrganizationId);
                if (hasFirmAccess)
                {
                    _logger.LogInformation("User {UserId} granted firm-based access to channel {ConversationId} in org {OrgId}", userId, conversationId, conversation.OrganizationId);
                    return true;
                }

                // 3. Matter-based access - if channel is linked to a matter, check matter access
                if (conversation.MatterId.HasValue)
                {
                    var canAccessMatter = await _permissionService.CanAccessMatterAsync(userId, conversation.MatterId.Value);
                    if (canAccessMatter)
                    {
                        return true;
                    }
                }

                return false;
            }

            // For non-channel conversations, use original logic
            // Check if conversation belongs to the organization
            if (conversation.OrganizationId != organizationId)
            {
                return false;
            }

            // Check if user is a participant or creator
            var isParticipant = conversation.Participants.Any(p => p.UserId == userId);
            var isCreator = conversation.CreatedById == userId;

            return isParticipant || isCreator;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating conversation access for user {UserId} and conversation {ConversationId}", userId, conversationId);
            return false;
        }
    }
}
