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

    public async Task<Conversation> CreateConversationAsync(string title, string description, string clientId, string? certioId = null, string? lawyerId = null, string? businessId = null)
    {
        var conversation = new Conversation
        {
            Title = title,
            Description = description,
            ClientId = clientId,
            CertioId = certioId,
            LawyerId = lawyerId,
            BusinessId = businessId,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        return conversation;
    }

    public async Task<ChatMessage> SendMessageAsync(string conversationId, string userId, string userType, string content, string messageType = "Text")
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
            .FirstOrDefaultAsync(c => c.Id.ToString() == conversationId);
        if (conversation != null)
        {
            conversation.LastMessageAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Only process AI agents if the message is meaningful and not a simple greeting
        if (ShouldProcessAIAgents(content))
        {
            _ = _aiBackgroundService.ProcessAIAgentsAsync(conversationId);
        }

        return message;
    }

    public async Task<List<ChatMessage>> GetConversationMessagesAsync(string conversationId)
    {
        return await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Conversation>> GetUserConversationsAsync(string userId)
    {
        return await _context.Conversations
            .Where(c => c.ClientId == userId || c.CertioId == userId || c.LawyerId == userId || c.BusinessId == userId)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();
    }


    public async Task<ClarityExplanation> RequestClarityAsync(string conversationId, string text, string userType)
    {
        return await _aiAgentService.ExplainLegalLanguageAsync(text, userType);
    }

    public async Task<ReplySuggestion> GetReplySuggestionsAsync(string conversationId, List<ChatMessage> messages, string userType)
    {
        return await _aiAgentService.SuggestReplyAsync(conversationId, messages, userType);
    }

    public async Task<Dictionary<string, object>> GetAIInsightsAsync(string conversationId)
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

    public async Task<ChatSummary?> GetConversationSummaryAsync(string conversationId)
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

    public async Task<ClientGoal?> GetClientGoalsAsync(string conversationId)
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

    public async Task<ReplySuggestion?> GetLatestReplySuggestionAsync(string conversationId)
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

    public async Task<ChatMessage> GenerateAIResponseAsync(string conversationId, string userMessage)
    {
        try
        {
            // Get conversation history
            var messages = await GetConversationMessagesAsync(conversationId);
            
            // Determine user type from conversation context
            var userType = DetermineUserType(messages, userMessage);
            
            // Generate intelligent conversational response
            var aiResponse = await _aiAgentService.GenerateConversationalResponseAsync(conversationId, messages, userMessage);
            
            // Create AI message with enhanced metadata
            var aiMessage = new ChatMessage
            {
                ConversationId = conversationId,
                UserId = "AI",
                UserType = "AI",
                Content = aiResponse,
                MessageType = "AI_Response", // Changed to AI_Response for proper formatting
                IsFromAI = true,
                AIAgentType = "IntelligentAI",
                CreatedAt = DateTime.UtcNow,
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    user_type_detected = userType,
                    response_type = "intelligent_conversational",
                    processing_time = DateTime.UtcNow,
                    message_length = userMessage.Length,
                    conversation_length = messages.Count
                })
            };

            // Save to database
            _context.ChatMessages.Add(aiMessage);
            
            // Update conversation last message time
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id.ToString() == conversationId);
            if (conversation != null)
            {
                conversation.LastMessageAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Temporarily disable background processing to avoid rate limits
            // _ = _aiBackgroundService.ProcessAIAgentsAsync(conversationId);

            return aiMessage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating AI response: {ex.Message}");
            
            // Return a more intelligent fallback response
            var fallbackMessage = new ChatMessage
            {
                ConversationId = conversationId,
                UserId = "AI",
                UserType = "AI",
                Content = GenerateIntelligentFallbackResponse(userMessage),
                MessageType = "AI_Response", // Use AI_Response for proper formatting
                IsFromAI = true,
                AIAgentType = "ServiceUnavailable",
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(fallbackMessage);
            await _context.SaveChangesAsync();

            return fallbackMessage;
        }
    }

    private string DetermineUserType(List<ChatMessage> messages, string userMessage)
    {
        // Analyze conversation to determine user type
        var clientMessages = messages.Count(m => m.UserType == "Client" || m.UserType == "Business");
        var certioMessages = messages.Count(m => m.UserType == "Certio");
        var lawyerMessages = messages.Count(m => m.UserType == "Lawyer");

        // If this is a new conversation, try to infer from message content
        if (messages.Count == 0)
        {
            var messageLower = userMessage.ToLower();
            if (messageLower.Contains("business") || messageLower.Contains("company") || messageLower.Contains("startup"))
                return "Business";
            return "Client";
        }

        // Return the most common user type in the conversation
        if (clientMessages > certioMessages && clientMessages >= lawyerMessages)
            return "Client";
        else if (certioMessages >= lawyerMessages)
            return "Certio";
        else
            return "Lawyer";
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

    private string GenerateIntelligentFallbackResponse(string userMessage)
    {
        return "<strong>🤖 AI Services Temporarily Unavailable</strong><br><br>" +
               "Our intelligent AI agents are currently offline for maintenance. " +
               "While we work to restore full AI functionality, you can still:<br><br>" +
               "• Send messages to your legal team<br>" +
               "• Access previous conversations<br>" +
               "• Use basic chat features<br><br>" +
               "We apologize for any inconvenience. Our AI services will be back online shortly!";
    }

    public async Task<bool> RenameConversationAsync(string conversationId, string newTitle)
    {
        try
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id.ToString() == conversationId);
            
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

    public async Task<bool> DeleteConversationAsync(string conversationId)
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
                .FirstOrDefaultAsync(c => c.Id.ToString() == conversationId);
            
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
