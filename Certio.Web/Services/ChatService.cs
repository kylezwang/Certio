using Microsoft.EntityFrameworkCore;
using Certio.Domain.Services;
using Certio.Web.Data;
using Certio.Application.Services;
using Certio.Application.Interfaces;

namespace Certio.Web.Services;

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _context;
    private readonly IAIAgentService _aiAgentService;
    private readonly AIBackgroundService _aiBackgroundService;

    public ChatService(ApplicationDbContext context, IAIAgentService aiAgentService, AIBackgroundService aiBackgroundService)
    {
        _context = context;
        _aiAgentService = aiAgentService;
        _aiBackgroundService = aiBackgroundService;
    }

    public async Task<Conversation> CreateConversationAsync(int organizationId, int userId, string title, string description, int? matterId = null, int? serviceRequestId = null)
    {
        var conversation = new Conversation
        {
            OrganizationId = organizationId,
            CreatedById = userId,
            Title = title,
            Description = description,
            MatterId = matterId,
            ServiceRequestId = serviceRequestId,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

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

    public async Task<List<ChatMessage>> GetConversationMessagesAsync(int conversationId)
    {
        return await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Conversation>> GetUserConversationsAsync(int userId, int organizationId)
    {
        return await _context.Conversations
            .Where(c => c.OrganizationId == organizationId && c.CreatedById == userId)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();
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

    public async Task<bool> DeleteConversationAsync(int conversationId, int organizationId)
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
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting conversation: {ex.Message}");
            return false;
        }
    }
}
