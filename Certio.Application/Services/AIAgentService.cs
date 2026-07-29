using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Globalization;
using System.Linq;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Certio.Domain.Services;
using Certio.Domain.Documents;
using Certio.Domain.Identity;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Application.Services;

public class AIAgentService : IAIAgentService
{
    private const int MaxDocumentContentSnippets = 9;
    private const int MaxChunksPerDocument = 3;
    private const int DocumentContentExcerptLimit = 2000;
    private const int DocumentChunkContextPadding = 400;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IRagContextService _ragContextService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentContentService _documentContentService;
    private readonly IUserDataContextService _userDataContextService;
    private readonly ILogger<AIAgentService>? _logger;
    private readonly ConcurrentDictionary<string, DateTime> _userDataSyncCache = new();
    private static readonly TimeSpan UserDataSyncInterval = TimeSpan.FromMinutes(5);

    public AIAgentService(
        HttpClient httpClient, 
        IConfiguration configuration,
        IRagContextService ragContextService,
        ApplicationDbContext dbContext,
        IDocumentContentService documentContentService,
        IUserDataContextService userDataContextService,
        ILogger<AIAgentService>? logger = null)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _ragContextService = ragContextService;
        _dbContext = dbContext;
        _documentContentService = documentContentService;
        _userDataContextService = userDataContextService;
        _logger = logger;
        
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

    public async Task<string> GenerateConversationalResponseAsync(string conversationId, List<ChatMessage> messages, string userMessage, string? aiMode = null)
    {
        try
        {
            var documentContext = await TryBuildDocumentContextAsync(conversationId, messages, userMessage);
            var request = CreateAIRequest(conversationId, messages, userMessage, documentContext: documentContext, aiMode: aiMode);
            
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

    public async IAsyncEnumerable<string> GenerateConversationalResponseStreamAsync(
        string conversationId, 
        List<ChatMessage> messages, 
        string userMessage, 
        Certio.Domain.Organizations.AIModelTier? aiModelTier = null,
        int? fallbackUserId = null,
        int? fallbackOrganizationId = null,
        string? aiMode = null)
    {
        HttpResponseMessage? response = null;
        Stream? stream = null;
        StreamReader? reader = null;
        bool connectionFailed = false;

        // Fetch document context via RAG if conversation has org/user/matter context
        var documentContext = await TryBuildDocumentContextAsync(
            conversationId, 
            messages, 
            userMessage, 
            fallbackOrganizationId, 
            fallbackUserId);

        // Setup request with optional document context and AI model tier
        var request = CreateAIRequest(
            conversationId, 
            messages, 
            userMessage, 
            documentContext: documentContext, 
            aiModelTier: aiModelTier,
            fallbackUserId: fallbackUserId,
            fallbackOrganizationId: fallbackOrganizationId,
            aiMode: aiMode);
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

    private static List<ChatMessage> OrderMessages(IEnumerable<ChatMessage> messages)
    {
        return messages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();
    }

    private async Task EnsureUserDataContextSyncedAsync(int organizationId, int userId)
    {
        if (_userDataContextService == null)
        {
            return;
        }

        var cacheKey = $"{organizationId}:{userId}";
        var now = DateTime.UtcNow;

        if (_userDataSyncCache.TryGetValue(cacheKey, out var lastSync) &&
            now - lastSync < UserDataSyncInterval)
        {
            return;
        }

        try
        {
            await _userDataContextService.SyncUserDataToPythonAsync(userId, organizationId);
            _userDataSyncCache[cacheKey] = now;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to sync user data context for User {UserId} Org {OrgId}", userId, organizationId);
        }
    }

    private object CreateAIRequest(
        string conversationId, 
        List<ChatMessage> messages, 
        string? userMessage = null, 
        string userType = "Client", 
        Dictionary<string, string?>? documentContext = null,
        Certio.Domain.Organizations.AIModelTier? aiModelTier = null,
        int? fallbackUserId = null,
        int? fallbackOrganizationId = null,
        string? aiMode = null)
    {
        var orderedMessages = OrderMessages(messages);

        // Try to extract user_id and organization_id from messages for user data context
        int? userId = orderedMessages.LastOrDefault(m => m.UserId.HasValue)?.UserId;
        if (!userId.HasValue && fallbackUserId.HasValue)
        {
            userId = fallbackUserId;
        }
        int? organizationId = null;
        
        // Get organization ID from conversation if available
        if (int.TryParse(conversationId, out var convId))
        {
            try
            {
                var conversation = _dbContext.Conversations
                    .AsNoTracking()
                    .FirstOrDefault(c => c.Id == convId);
                organizationId = conversation?.OrganizationId;
            }
            catch
            {
                // If we can't get the conversation, that's okay - continue without organization_id
            }
        }
        if (!organizationId.HasValue && fallbackOrganizationId.HasValue)
        {
            organizationId = fallbackOrganizationId;
        }

        var request = new
        {
            conversation_id = conversationId,
            messages = orderedMessages.Select(m => new
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
            user_type = userType,
            ai_model_tier = aiModelTier?.ToString() ?? "Auto",
            ai_mode = aiMode,
            user_id = userId,  // Added for user data RAG context
            organization_id = organizationId  // Added for user data RAG context
        };

        if (userMessage != null && documentContext != null)
        {
            return new { request.conversation_id, request.messages, request.user_type, request.ai_model_tier, request.ai_mode, request.user_id, request.organization_id, user_message = userMessage, document_context = documentContext };
        }
        else if (userMessage != null)
        {
            return new { request.conversation_id, request.messages, request.user_type, request.ai_model_tier, request.ai_mode, request.user_id, request.organization_id, user_message = userMessage };
        }
        else
        {
            return request;
        }
    }

    private async Task<Dictionary<string, string?>?> TryBuildDocumentContextAsync(
        string conversationId, 
        List<ChatMessage> messages, 
        string userMessage,
        int? fallbackOrganizationId = null,
        int? fallbackUserId = null)
    {
        var orderedMessages = OrderMessages(messages);

        if (!int.TryParse(conversationId, out var convId))
        {
            if (fallbackOrganizationId.HasValue && fallbackUserId.HasValue)
            {
                await EnsureUserDataContextSyncedAsync(fallbackOrganizationId.Value, fallbackUserId.Value);
            }
            return null;
        }

        try
        {
            var conversation = await _dbContext.Conversations
                .AsNoTracking()
                .Include(c => c.Organization)
                .Include(c => c.Matter)
                .FirstOrDefaultAsync(c => c.Id == convId);

            if (conversation == null)
            {
                if (fallbackOrganizationId.HasValue && fallbackUserId.HasValue)
                {
                    await EnsureUserDataContextSyncedAsync(fallbackOrganizationId.Value, fallbackUserId.Value);
                }
                return null;
            }

            var initiatingMessage = orderedMessages.LastOrDefault(m => !m.IsFromAI);
            if (initiatingMessage?.UserId == null)
            {
                if (fallbackOrganizationId.HasValue && fallbackUserId.HasValue)
                {
                    await EnsureUserDataContextSyncedAsync(fallbackOrganizationId.Value, fallbackUserId.Value);
                }
                return null;
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == initiatingMessage.UserId.Value);

            if (user == null)
            {
                if (fallbackOrganizationId.HasValue && fallbackUserId.HasValue)
                {
                    await EnsureUserDataContextSyncedAsync(fallbackOrganizationId.Value, fallbackUserId.Value);
                }
                return null;
            }

            await EnsureUserDataContextSyncedAsync(
                conversation.OrganizationId,
                initiatingMessage.UserId.Value);

            var ragRequest = new RagContextRequest(
                DeterministicGuid.ForOrganization(conversation.OrganizationId),
                DeterministicGuid.ForUser(initiatingMessage.UserId.Value),
                userMessage,
                TopK: 5,
                conversation.MatterId.HasValue ? DeterministicGuid.ForMatter(conversation.MatterId.Value) : null,
                RestrictToDocumentIds: null);

            var ragResult = await _ragContextService.BuildContextAsync(ragRequest);
            var context = ragResult.Context.ToDictionary(k => k.Key, v => v.Value);

            await EnrichDocumentContextAsync(context, ragResult);
            return context;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch document context: {ex.Message}");
            return null;
        }
    }

    private async Task EnrichDocumentContextAsync(Dictionary<string, string?> documentContext, RagContextResult ragResult)
    {
        if (ragResult.Vectors.Count == 0)
        {
            return;
        }

        var snippets = await BuildDocumentContentSnippetsAsync(ragResult);
        if (snippets.Count == 0)
        {
            return;
        }

        documentContext["documentContents"] = JsonSerializer.Serialize(snippets);
        documentContext["documentContentCount"] = snippets.Count.ToString(CultureInfo.InvariantCulture);
        var documentUniqueCount = snippets.Select(s => s.DocumentId).Distinct().Count();
        documentContext["documentContentDocumentCount"] = documentUniqueCount.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<List<DocumentContentSnippet>> BuildDocumentContentSnippetsAsync(RagContextResult ragResult)
    {
        var snippets = new List<DocumentContentSnippet>();

        if (ragResult.Vectors.Count == 0)
        {
            return snippets;
        }

        var providerFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documentChunkCounts = new Dictionary<Guid, int>();
        var contentCache = new Dictionary<Guid, DocumentContentResult>();

        foreach (var vector in ragResult.Vectors)
        {
            if (snippets.Count >= MaxDocumentContentSnippets)
            {
                break;
            }

            if (vector.DocumentId == Guid.Empty)
            {
                continue;
            }

            var documentId = vector.DocumentId;
            var chunkCountForDocument = documentChunkCounts.TryGetValue(documentId, out var processedChunks)
                ? processedChunks
                : 0;

            if (chunkCountForDocument >= MaxChunksPerDocument)
            {
                continue;
            }

            Document? document = vector.Document;
            if (document == null)
            {
                document = await _dbContext.Documents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == documentId);

                if (document == null)
                {
                    continue;
                }
            }

            var providerKey = document.SourceType.ToString();
            var providerNormalized = providerKey.ToLowerInvariant();

            if (providerFailures.Contains(providerNormalized))
            {
                continue;
            }

            DocumentContentResult? contentResult;
            try
            {
                contentResult = await GetOrFetchContentResultAsync(document, vector.VersionId, contentCache);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch content for document {vector.DocumentId}: {ex.Message}");
                providerFailures.Add(providerNormalized);
                continue;
            }

            var excerpt = BuildExcerptForVector(vector, contentResult);
            var metadata = BuildSnippetMetadata(vector, contentResult, chunkCountForDocument);
            var hasContent = !string.IsNullOrWhiteSpace(excerpt);

            if (!hasContent && metadata.TryGetValue("extractionStatus", out var status) && !string.IsNullOrWhiteSpace(status))
            {
                excerpt = BuildFriendlyStatusMessage(status);
            }

            var sanitizedExcerpt = SanitizeExcerpt(excerpt, out var truncated);
            var finalHasContent = !string.IsNullOrWhiteSpace(sanitizedExcerpt);

            var snippet = new DocumentContentSnippet(
                document.Id,
                string.IsNullOrWhiteSpace(document.Title) ? "Untitled Document" : document.Title,
                providerKey,
                string.IsNullOrWhiteSpace(document.FileType) ? "unknown" : document.FileType,
                sanitizedExcerpt,
                finalHasContent,
                contentResult?.IsPartial ?? false,
                truncated,
                document.DownloadUrl,
                metadata,
                vector.ChunkIndex);

            snippets.Add(snippet);
            documentChunkCounts[documentId] = chunkCountForDocument + 1;

            var extractionStatus = contentResult != null ? TryGetExtractionStatus(contentResult) : null;
            if (IsAuthenticationFailure(extractionStatus))
            {
                providerFailures.Add(providerNormalized);
            }
        }

        return snippets;
    }

    private async Task<DocumentContentResult> GetOrFetchContentResultAsync(
        Document document,
        Guid? versionId,
        Dictionary<Guid, DocumentContentResult> cache)
    {
        if (cache.TryGetValue(document.Id, out var cached))
        {
            return cached;
        }

        var result = await _documentContentService.FetchContentAsync(document, versionId);
        cache[document.Id] = result;
        return result;
    }

    private static string BuildExcerptForVector(DocumentVector vector, DocumentContentResult? contentResult)
    {
        var rawChunk = vector.ContentChunk;
        if (!string.IsNullOrWhiteSpace(rawChunk))
        {
            return ExpandChunkWithContext(rawChunk, contentResult?.Content);
        }

        if (contentResult != null && contentResult.HasContent)
        {
            return ExtractSequentialSegment(contentResult.Content, vector.ChunkIndex);
        }

        return string.Empty;
    }

    private static string ExpandChunkWithContext(string chunk, string? fullContent)
    {
        if (string.IsNullOrWhiteSpace(fullContent))
        {
            return NormalizeWhitespace(chunk);
        }

        var matchIndex = fullContent.IndexOf(chunk, StringComparison.OrdinalIgnoreCase);
        if (matchIndex < 0)
        {
            return NormalizeWhitespace(chunk);
        }

        var start = Math.Max(0, matchIndex - DocumentChunkContextPadding);
        var availableLength = fullContent.Length - start;
        var desiredLength = Math.Min(DocumentContentExcerptLimit + DocumentChunkContextPadding, availableLength);
        var excerpt = fullContent.Substring(start, desiredLength);
        return NormalizeWhitespace(excerpt);
    }

    private static string ExtractSequentialSegment(string content, int chunkIndex)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = NormalizeWhitespace(content);
        var start = Math.Max(0, chunkIndex * DocumentContentExcerptLimit);
        if (start >= normalized.Length)
        {
            start = Math.Max(0, normalized.Length - DocumentContentExcerptLimit);
        }

        var length = Math.Min(DocumentContentExcerptLimit, normalized.Length - start);
        return length > 0 ? normalized.Substring(start, length) : normalized;
    }

    private static Dictionary<string, string?> BuildSnippetMetadata(
        DocumentVector vector,
        DocumentContentResult? contentResult,
        int batchPosition)
    {
        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["chunkIndex"] = vector.ChunkIndex.ToString(CultureInfo.InvariantCulture),
            ["batchPosition"] = (batchPosition + 1).ToString(CultureInfo.InvariantCulture),
            ["vectorId"] = vector.Id.ToString(),
            ["usedVectorChunk"] = (!string.IsNullOrWhiteSpace(vector.ContentChunk)).ToString().ToLowerInvariant()
        };

        if (vector.Tags.TryGetValue("fallback", out var fallbackValue) && !string.IsNullOrWhiteSpace(fallbackValue))
        {
            metadata["chunkSource"] = fallbackValue;
        }
        else
        {
            metadata["chunkSource"] = "vector";
        }

        foreach (var tag in vector.Tags)
        {
            metadata.TryAdd($"tag:{tag.Key}", tag.Value);
        }

        if (contentResult != null)
        {
            foreach (var kvp in contentResult.AdditionalMetadata)
            {
                metadata.TryAdd(kvp.Key, kvp.Value);
            }
        }

        return metadata;
    }

    private static string SanitizeExcerpt(string excerpt, out bool truncated)
    {
        truncated = false;

        if (string.IsNullOrWhiteSpace(excerpt))
        {
            return string.Empty;
        }

        var normalized = NormalizeWhitespace(excerpt);
        if (normalized.Length <= DocumentContentExcerptLimit)
        {
            return normalized;
        }

        truncated = true;
        return normalized[..DocumentContentExcerptLimit].TrimEnd() + " …";
    }

    private static string NormalizeWhitespace(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var previousWhitespace = false;

        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (previousWhitespace)
                {
                    continue;
                }

                builder.Append(' ');
                previousWhitespace = true;
            }
            else
            {
                builder.Append(ch);
                previousWhitespace = false;
            }
        }

        return builder.ToString().Trim();
    }

    private static string? TryGetExtractionStatus(DocumentContentResult contentResult) =>
        contentResult.AdditionalMetadata.TryGetValue("extractionStatus", out var status)
            ? status
            : null;

    private static bool IsAuthenticationFailure(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var normalized = status.ToLowerInvariant();
        return normalized.Contains("download_failed_401")
            || normalized.Contains("missing_google_connection")
            || normalized.Contains("missing_onedrive_connection")
            || normalized.Contains("unauthorized");
    }
    private record DocumentContentSnippet(
        Guid DocumentId,
        string Title,
        string Provider,
        string ContentType,
        string Excerpt,
        bool HasContent,
        bool IsPartial,
        bool IsTruncated,
        string? DownloadUrl,
        IReadOnlyDictionary<string, string?> Metadata,
        int ChunkIndex);

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

    private static string BuildFriendlyStatusMessage(string status)
    {
        var normalized = status.ToLowerInvariant();

        if (normalized.Contains("google") && normalized.Contains("401"))
        {
            return "Google Drive connection has expired. Ask the user to reconnect Google Drive from Settings, then try again.";
        }

        if (normalized.Contains("missing_google_connection"))
        {
            return "Google Drive is not connected for this user or organization. Connect Google Drive to read this document.";
        }

        if (normalized.Contains("missing_onedrive_connection"))
        {
            return "OneDrive is not connected for this user or organization. Connect OneDrive to read this document.";
        }

        if (normalized.Contains("file_too_large"))
        {
            return "The file is larger than the secure extraction limit, so no text is available.";
        }

        if (normalized.Contains("unsupported_content_type"))
        {
            return "This file type is not supported for secure text extraction.";
        }

        if (normalized.Contains("azure_document_intelligence_not_configured"))
        {
            return "Full text extraction is not enabled yet; only metadata is available for this document.";
        }

        if (normalized.Contains("analysis_timeout"))
        {
            return "Text extraction timed out. Try again later or download the document directly.";
        }

        return $"Content unavailable ({status}).";
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
        else if (messageLower.Contains("event") || messageLower.Contains("vendor"))
            contextHint = "I see you need help with an event. ";
        
        return $"<strong>⚠️ AI Services Temporarily Unavailable</strong><br><br>" +
               $"{contextHint}Our AI services are being updated. You can continue other services normally, " +
               "and I'll be back online shortly to provide assistance!";
    }

}
