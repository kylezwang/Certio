using Microsoft.EntityFrameworkCore;
using Certio.Web.Data;
using Certio.Application.Services;
using Certio.Domain.Services;

namespace Certio.Web.Services;

public class AIBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AIBackgroundService> _logger;

    public AIBackgroundService(IServiceProvider serviceProvider, ILogger<AIBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken); // Check every second
        }
    }

    public async Task ProcessAIAgentsAsync(string conversationId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<IAIAgentService>();

        try
        {
            var messages = await context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            if (!messages.Any()) return;

            // Only process AI agents if the conversation has meaningful content
            if (!ShouldProcessAIAgents(messages))
            {
                _logger.LogInformation("Skipping AI processing for conversation {ConversationId} - insufficient meaningful content", conversationId);
                return;
            }

            // Use intelligent processing for comprehensive analysis
            var intelligentResults = await aiAgentService.ProcessConversationIntelligentlyAsync(conversationId, messages, "Client");
            
            // Process results and create appropriate AI messages
            await ProcessIntelligentResults(context, conversationId, intelligentResults);

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AI agents for conversation {ConversationId}", conversationId);
        }
    }

    private async Task ProcessIntelligentResults(ApplicationDbContext context, string conversationId, Dictionary<string, object> results)
    {
        try
        {
            // Process summary if available
            if (results.ContainsKey("summary"))
            {
                var summaryJson = System.Text.Json.JsonSerializer.Serialize(results["summary"]);
                var summaryMessage = new ChatMessage
                {
                    ConversationId = conversationId,
                    UserId = null, // AI messages don't have a user ID
                    UserType = "AI",
                    Content = summaryJson,
                    MessageType = "AI_Summary",
                    IsFromAI = true,
                    AIAgentType = "ChatSummarizer",
                    CreatedAt = DateTime.UtcNow
                };
                context.ChatMessages.Add(summaryMessage);
            }

            // Process goals if available
            if (results.ContainsKey("goals"))
            {
                var goalsJson = System.Text.Json.JsonSerializer.Serialize(results["goals"]);
                var goalsMessage = new ChatMessage
                {
                    ConversationId = conversationId,
                    UserId = null, // AI messages don't have a user ID
                    UserType = "AI",
                    Content = goalsJson,
                    MessageType = "AI_Goal",
                    IsFromAI = true,
                    AIAgentType = "ClientGoalExtractor",
                    CreatedAt = DateTime.UtcNow
                };
                context.ChatMessages.Add(goalsMessage);
            }

            // Process reply suggestion if available
            if (results.ContainsKey("reply_suggestion"))
            {
                var replyJson = System.Text.Json.JsonSerializer.Serialize(results["reply_suggestion"]);
                var replyMessage = new ChatMessage
                {
                    ConversationId = conversationId,
                    UserId = null, // AI messages don't have a user ID
                    UserType = "AI",
                    Content = replyJson,
                    MessageType = "AI_Reply",
                    IsFromAI = true,
                    AIAgentType = "ReplySuggester",
                    CreatedAt = DateTime.UtcNow
                };
                context.ChatMessages.Add(replyMessage);
            }

            // Don't add raw metadata as a message - it's just for internal use
            // The intelligent processing metadata is used internally but not displayed to users
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing intelligent results for conversation {ConversationId}", conversationId);
        }
    }

    private bool ShouldProcessAIAgents(List<ChatMessage> messages)
    {
        // Don't process if we have less than 3 messages
        if (messages.Count < 3) return false;

        // Get the last user message (non-AI message)
        var lastUserMessage = messages
            .Where(m => !m.IsFromAI)
            .OrderBy(m => m.CreatedAt)
            .LastOrDefault();

        if (lastUserMessage == null) return false;

        // Don't process if the last message is a simple greeting
        if (IsSimpleGreeting(lastUserMessage.Content))
        {
            _logger.LogInformation("Skipping AI processing - last message is a simple greeting: '{Message}'", lastUserMessage.Content);
            return false;
        }

        // Count meaningful messages (non-AI messages with substantial content)
        var meaningfulMessages = messages
            .Where(m => !m.IsFromAI && IsMeaningfulMessage(m.Content))
            .ToList();

        // Need at least 3 meaningful messages to trigger AI processing
        if (meaningfulMessages.Count < 3)
        {
            _logger.LogInformation("Skipping AI processing - insufficient meaningful messages: {Count}", meaningfulMessages.Count);
            return false;
        }

        // Check if conversation has substantial legal/business content
        var hasLegalContent = HasLegalContent(messages);
        if (!hasLegalContent)
        {
            _logger.LogInformation("Skipping AI processing - no legal content detected");
            return false;
        }

        // Check if we've already processed this conversation recently (within last 5 minutes)
        var recentAIProcessing = messages
            .Where(m => m.IsFromAI && m.MessageType.StartsWith("AI_") && 
                       m.CreatedAt > DateTime.UtcNow.AddMinutes(-5))
            .Any();

        if (recentAIProcessing)
        {
            _logger.LogInformation("Skipping AI processing - already processed recently");
            return false;
        }

        return true;
    }

    private bool IsMeaningfulMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;

        var trimmedContent = content.Trim().ToLower();
        
        // Skip very short messages (less than 10 characters)
        if (trimmedContent.Length < 10) return false;

        // Skip messages that are just punctuation or numbers
        if (trimmedContent.All(c => char.IsPunctuation(c) || char.IsWhiteSpace(c) || char.IsDigit(c))) return false;

        return true;
    }

    private bool IsSimpleGreeting(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return true;

        var trimmedContent = content.Trim().ToLower();
        
        // Check for simple greetings and short responses
        var simpleGreetings = new[]
        {
            "hello", "hi", "hey", "good morning", "good afternoon", "good evening",
            "thanks", "thank you", "ok", "okay", "yes", "no", "sure", "alright",
            "bye", "goodbye", "see you", "later", "ok bye", "thanks bye",
            "how are you", "how are you?", "what's up", "what's up?", "how's it going",
            "how's it going?", "how do you do", "how do you do?", "nice to meet you"
        };

        return simpleGreetings.Contains(trimmedContent);
    }

    private bool HasLegalContent(List<ChatMessage> messages)
    {
        var legalKeywords = new[]
        {
            "contract", "agreement", "legal", "law", "lawyer", "attorney", "court", "lawsuit",
            "business", "company", "incorporation", "llc", "corporation", "partnership",
            "employment", "hiring", "termination", "discrimination", "harassment",
            "intellectual property", "patent", "trademark", "copyright",
            "compliance", "regulation", "audit", "certification",
            "real estate", "property", "lease", "rental", "mortgage",
            "litigation", "dispute", "settlement", "trial", "mediation",
            "liability", "breach", "damages", "warranty", "indemnification",
            "confidentiality", "non-disclosure", "non-compete", "arbitration"
        };

        var allContent = string.Join(" ", messages.Select(m => m.Content)).ToLower();
        return legalKeywords.Any(keyword => allContent.Contains(keyword));
    }
}
