using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Certio.Domain.Services;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Application.Services;

public class AIAgentService : IAIAgentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IRagContextService _ragContextService;
    private readonly ApplicationDbContext _dbContext;

    public AIAgentService(
        HttpClient httpClient, 
        IConfiguration configuration,
        IRagContextService ragContextService,
        ApplicationDbContext dbContext)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _ragContextService = ragContextService;
        _dbContext = dbContext;
        
        // Configure the HTTP client for the Python AI service
        var aiServiceUrl = _configuration["AIService:BaseUrl"] ?? "http://localhost:8000";
        var timeout = _configuration.GetValue<int>("AIService:TimeoutSeconds", 30);
        
        _httpClient.BaseAddress = new Uri(aiServiceUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(timeout);
        if (_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Remove("User-Agent");
        }
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Certio-AIService/1.0");

        var apiKey = _configuration["AIService:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            if (_httpClient.DefaultRequestHeaders.Contains("X-API-Key"))
            {
                _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            }
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
    }

    public async Task<ChatSummary> SummarizeConversationAsync(string conversationId, List<ChatMessage> messages)
    {
        try
        {
            var request = CreateAIRequest(conversationId, messages);
            var result = await CallAIServiceAsync<ChatSummary>("/agents/summarize", request);
            return result ?? CreateDefaultChatSummary();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling AI summarization service: {ex.Message}");
            return CreateDefaultChatSummary();
        }
    }

    public async Task<ClientGoal> ExtractClientGoalsAsync(string conversationId, List<ChatMessage> messages)
    {
        try
        {
            var request = CreateAIRequest(conversationId, messages);
            var result = await CallAIServiceAsync<ClientGoal>("/agents/extract-goals", request);
            return result ?? CreateDefaultClientGoal();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling AI goal extraction service: {ex.Message}");
            return CreateDefaultClientGoal();
        }
    }

    public async Task<ReplySuggestion> SuggestReplyAsync(string conversationId, List<ChatMessage> messages, string userType)
    {
        try
        {
            var request = CreateAIRequest(conversationId, messages, userType: userType);
            var result = await CallAIServiceAsync<ReplySuggestion>("/agents/suggest-reply", request);
            return result ?? CreateDefaultReplySuggestion();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling AI reply suggestion service: {ex.Message}");
            return CreateDefaultReplySuggestion();
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
            // Fetch document context via RAG if conversation has org/user/matter context
            Dictionary<string, string?>? documentContext = null;
            if (int.TryParse(conversationId, out var convId))
            {
            try
            {
                var conversation = await _dbContext.Conversations
                    .AsNoTracking()
                    .Include(c => c.Organization)
                    .Include(c => c.Matter)
                    .FirstOrDefaultAsync(c => c.Id == convId);

                if (conversation != null)
                {
                    var userId = messages.FirstOrDefault(m => !m.IsFromAI)?.UserId;
                    if (userId.HasValue)
                    {
                        // Look up the user's Guid
                        var user = await _dbContext.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.Id == userId.Value);

                        if (user != null)
                        {
                            var ragRequest = new RagContextRequest(
                                CreateDeterministicGuid("certio:organization", conversation.OrganizationId),
                                CreateDeterministicGuid("certio:user", userId.Value),
                                userMessage,
                                TopK: 5,
                                conversation.MatterId.HasValue ? CreateDeterministicGuid("certio:matter", conversation.MatterId.Value) : null,
                                RestrictToDocumentIds: null);

                            var ragResult = await _ragContextService.BuildContextAsync(ragRequest);
                            documentContext = ragResult.Context.ToDictionary(k => k.Key, v => v.Value);
                        }
                    }
                }
            }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to fetch document context: {ex.Message}");
                    // Continue without document context
                }
            }

            var request = CreateAIRequest(conversationId, messages, userMessage, documentContext: documentContext);
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/agents/conversational-response", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent.Trim('"'); // Remove quotes if present
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling AI conversational service: {ex.Message}");
            return GenerateFallbackResponse(userMessage);
        }
    }

    public async IAsyncEnumerable<string> GenerateConversationalResponseStreamAsync(string conversationId, List<ChatMessage> messages, string userMessage)
    {
        HttpResponseMessage? response = null;
        Stream? stream = null;
        StreamReader? reader = null;
        bool connectionFailed = false;

        // Fetch document context via RAG if conversation has org/user/matter context
        Dictionary<string, string?>? documentContext = null;
        if (int.TryParse(conversationId, out var convId))
        {
            try
            {
                var conversation = await _dbContext.Conversations
                    .AsNoTracking()
                    .Include(c => c.Organization)
                    .Include(c => c.Matter)
                    .FirstOrDefaultAsync(c => c.Id == convId);

                if (conversation != null)
                {
                    var userId = messages.FirstOrDefault(m => !m.IsFromAI)?.UserId;
                    if (userId.HasValue)
                    {
                        // Look up the user's Guid
                        var user = await _dbContext.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.Id == userId.Value);

                        if (user != null)
                        {
                            var ragRequest = new RagContextRequest(
                                CreateDeterministicGuid("certio:organization", conversation.OrganizationId),
                                CreateDeterministicGuid("certio:user", userId.Value),
                                userMessage,
                                TopK: 5,
                                conversation.MatterId.HasValue ? CreateDeterministicGuid("certio:matter", conversation.MatterId.Value) : null,
                                RestrictToDocumentIds: null);

                            var ragResult = await _ragContextService.BuildContextAsync(ragRequest);
                            documentContext = ragResult.Context.ToDictionary(k => k.Key, v => v.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch document context: {ex.Message}");
                // Continue without document context
            }
        }

        // Setup request with optional document context
        var request = CreateAIRequest(conversationId, messages, userMessage, documentContext: documentContext);
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/conversational-response-stream")
        {
            Content = content
        };

        // Try to establish connection
        try
        {
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            stream = await response.Content.ReadAsStreamAsync();
            reader = new StreamReader(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling AI streaming service: {ex.Message}");
            connectionFailed = true;
            // Clean up on error
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
        }

        // If connection failed, yield error and exit
        if (connectionFailed)
        {
            yield return GenerateFallbackResponse(userMessage);
            yield break;
        }

        // Process the stream (reader is guaranteed to be non-null here)
        try
        {
            string? line;
            while ((line = await reader!.ReadLineAsync()) != null)
            {
                if (line.StartsWith("data: "))
                {
                    var jsonData = line.Substring(6);
                    
                    StreamChunk? eventData = null;
                    try
                    {
                        eventData = JsonSerializer.Deserialize<StreamChunk>(jsonData);
                    }
                    catch (JsonException)
                    {
                        continue;
                    }
                    
                    if (eventData?.Done == true)
                    {
                        if (eventData.Error == true && !string.IsNullOrEmpty(eventData.Content))
                        {
                            yield return eventData.Content;
                        }
                        yield break;
                    }
                    
                    if (!string.IsNullOrEmpty(eventData?.Content))
                    {
                        yield return eventData.Content;
                    }
                }
            }
        }
        finally
        {
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
        }
    }

    private class StreamChunk
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        
        [JsonPropertyName("done")]
        public bool Done { get; set; }
        
        [JsonPropertyName("error")]
        public bool Error { get; set; }
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

    private object CreateAIRequest(string conversationId, List<ChatMessage> messages, string? userMessage = null, string userType = "Client", Dictionary<string, string?>? documentContext = null)
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

        if (userMessage != null && documentContext != null)
        {
            return new { request.conversation_id, request.messages, request.user_type, user_message = userMessage, document_context = documentContext };
        }
        else if (userMessage != null)
        {
            return new { request.conversation_id, request.messages, request.user_type, user_message = userMessage };
        }
        else
        {
            return request;
        }
    }

    private async Task<T?> CallAIServiceAsync<T>(string endpoint, object request) where T : class
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static ChatSummary CreateDefaultChatSummary()
    {
        return new ChatSummary
        {
            Summary = "Unable to process conversation summary at this time.",
            KeyPoints = JsonSerializer.Serialize(new List<string>()),
            Sentiment = "Neutral",
            Urgency = "Medium",
            SuggestedActions = JsonSerializer.Serialize(new List<string>())
        };
    }

    private static ClientGoal CreateDefaultClientGoal()
    {
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

    private static ReplySuggestion CreateDefaultReplySuggestion()
    {
        return new ReplySuggestion
        {
            SuggestedReply = "I'll review this and get back to you shortly.",
            Tone = "Professional",
            Purpose = "Acknowledgment",
            KeyPoints = JsonSerializer.Serialize(new List<string>()),
            RequiresLegalReview = true
        };
    }

    private string GenerateFallbackResponse(string userMessage)
    {
        // Analyze message for context
        var messageLower = userMessage.ToLower();
        var contextHint = "";
        
        if (messageLower.Contains("contract") || messageLower.Contains("agreement"))
            contextHint = "I understand you're asking about contracts. ";
        else if (messageLower.Contains("legal") || messageLower.Contains("law"))
            contextHint = "I see you need legal guidance. ";
        
        return $"<strong>🤖 AI Assistant Temporarily Unavailable</strong><br><br>" +
               $"{contextHint}Our AI services are being updated. You can continue chatting with your legal team, " +
               "and I'll be back online shortly to provide intelligent assistance!";
    }

    private static Guid CreateDeterministicGuid(string namespacePrefix, int value)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{namespacePrefix}:{value.ToString(CultureInfo.InvariantCulture)}"));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40); // Version 4
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // Variant RFC 4122
        return new Guid(guidBytes);
    }
}
