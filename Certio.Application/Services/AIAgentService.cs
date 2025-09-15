using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Certio.Domain.Services;

namespace Certio.Application.Services;

public class AIAgentService : IAIAgentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public AIAgentService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        
        // Configure the HTTP client for the Python AI service
        var aiServiceUrl = _configuration["AIService:BaseUrl"] ?? "http://localhost:8000";
        _httpClient.BaseAddress = new Uri(aiServiceUrl);
    }

    public async Task<ChatSummary> SummarizeConversationAsync(string conversationId, List<ChatMessage> messages)
    {
        try
        {
            var request = new
            {
                conversation_id = conversationId,
                messages = messages.Select(m => new
                {
                    id = m.Id,
                    conversation_id = m.ConversationId,
                    user_id = m.UserId,
                    user_type = m.UserType,
                    content = m.Content,
                    message_type = m.MessageType,
                    is_from_ai = m.IsFromAI,
                    ai_agent_type = m.AIAgentType,
                    created_at = m.CreatedAt.ToString("O"),
                    is_read = m.IsRead
                }).ToList()
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/agents/summarize", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ChatSummary>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new ChatSummary();
        }
        catch (Exception ex)
        {
            // Log error and return default summary
            Console.WriteLine($"Error calling AI service: {ex.Message}");
            return new ChatSummary
            {
                Summary = "Unable to process conversation summary at this time.",
                KeyPoints = JsonSerializer.Serialize(new List<string>()),
                Sentiment = "Neutral",
                Urgency = "Medium",
                SuggestedActions = JsonSerializer.Serialize(new List<string>())
            };
        }
    }

    public async Task<ClientGoal> ExtractClientGoalsAsync(string conversationId, List<ChatMessage> messages)
    {
        try
        {
            var request = new
            {
                conversation_id = conversationId,
                messages = messages.Select(m => new
                {
                    id = m.Id,
                    conversation_id = m.ConversationId,
                    user_id = m.UserId,
                    user_type = m.UserType,
                    content = m.Content,
                    message_type = m.MessageType,
                    is_from_ai = m.IsFromAI,
                    ai_agent_type = m.AIAgentType,
                    created_at = m.CreatedAt.ToString("O"),
                    is_read = m.IsRead
                }).ToList()
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/agents/extract-goals", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ClientGoal>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new ClientGoal();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling AI service: {ex.Message}");
            return new ClientGoal
            {
                PrimaryGoal = "Unable to extract goals at this time.",
                SecondaryGoals = JsonSerializer.Serialize(new List<string>()),
                BusinessType = "Unknown",
                LegalArea = "General",
                Timeline = "Not specified",
                Budget = "Not specified",
                RequiredDocuments = JsonSerializer.Serialize(new List<string>())
            };
        }
    }

    public async Task<ReplySuggestion> SuggestReplyAsync(string conversationId, List<ChatMessage> messages, string userType)
    {
        try
        {
            var request = new
            {
                conversation_id = conversationId,
                messages = messages.Select(m => new
                {
                    id = m.Id,
                    conversation_id = m.ConversationId,
                    user_id = m.UserId,
                    user_type = m.UserType,
                    content = m.Content,
                    message_type = m.MessageType,
                    is_from_ai = m.IsFromAI,
                    ai_agent_type = m.AIAgentType,
                    created_at = m.CreatedAt.ToString("O"),
                    is_read = m.IsRead
                }).ToList(),
                user_type = userType
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/agents/suggest-reply", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ReplySuggestion>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new ReplySuggestion();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling AI service: {ex.Message}");
            return new ReplySuggestion
            {
                SuggestedReply = "I'll review this and get back to you shortly.",
                Tone = "Professional",
                Purpose = "Acknowledgment",
                KeyPoints = JsonSerializer.Serialize(new List<string>()),
                RequiresLegalReview = true
            };
        }
    }

    public async Task<ClarityExplanation> ExplainLegalLanguageAsync(string text, string userType)
    {
        try
        {
            var request = new
            {
                conversation_id = "",
                messages = new List<object>(),
                user_type = userType,
                text = text
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/agents/explain-clarity", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ClarityExplanation>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new ClarityExplanation();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling AI service: {ex.Message}");
            return new ClarityExplanation
            {
                OriginalText = text,
                SimplifiedExplanation = "Unable to process this text at the moment.",
                KeyTerms = JsonSerializer.Serialize(new List<string>()),
                Implications = JsonSerializer.Serialize(new List<string>()),
                RiskLevel = "Unknown",
                RecommendedActions = JsonSerializer.Serialize(new List<string>())
            };
        }
    }

    public async Task<string> GenerateConversationalResponseAsync(string conversationId, List<ChatMessage> messages, string userMessage)
    {
        try
        {
            var request = new
            {
                conversation_id = conversationId,
                messages = messages.Select(m => new
                {
                    id = m.Id,
                    conversation_id = m.ConversationId,
                    user_id = m.UserId,
                    user_type = m.UserType,
                    content = m.Content,
                    message_type = m.MessageType,
                    is_from_ai = m.IsFromAI,
                    ai_agent_type = m.AIAgentType,
                    created_at = m.CreatedAt.ToString("O"),
                    is_read = m.IsRead
                }).ToList(),
                user_message = userMessage,
                user_type = "Client" // Default user type, can be enhanced later
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/agents/conversational-response", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent.Trim('"'); // Remove quotes if present
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling AI service for conversational response: {ex.Message}");
            
            // Fallback response based on message content
            return GenerateFallbackResponse(userMessage);
        }
    }

    public async Task<Dictionary<string, object>> ProcessConversationIntelligentlyAsync(string conversationId, List<ChatMessage> messages, string userType = "Client")
    {
        try
        {
            var request = new
            {
                conversation_id = conversationId,
                messages = messages.Select(m => new
                {
                    id = m.Id,
                    conversation_id = m.ConversationId,
                    user_id = m.UserId,
                    user_type = m.UserType,
                    content = m.Content,
                    message_type = m.MessageType,
                    is_from_ai = m.IsFromAI,
                    ai_agent_type = m.AIAgentType,
                    created_at = m.CreatedAt.ToString("O"),
                    is_read = m.IsRead
                }).ToList(),
                user_type = userType
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/agents/process-intelligent", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling intelligent processing: {ex.Message}");
            return new Dictionary<string, object>
            {
                ["error"] = "Unable to process conversation intelligently",
                ["fallback"] = true
            };
        }
    }

    private string GenerateFallbackResponse(string userMessage)
    {
        return "<strong>🤖 AI Services Temporarily Unavailable</strong><br><br>" +
               "Our intelligent AI agents are currently offline for maintenance. " +
               "While we work to restore full AI functionality, you can still:<br><br>" +
               "• Send messages to your legal team<br>" +
               "• Access previous conversations<br>" +
               "• Use basic chat features<br><br>" +
               "We apologize for any inconvenience. Our AI services will be back online shortly!";
    }
}
