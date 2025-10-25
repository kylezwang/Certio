using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Certio.Web.Services;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RedisCacheService> _logger;
    
    // JSON serializer options to handle circular references
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    // Cache expiration times (Discord/Slack style)
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MessageCacheExpiration = TimeSpan.FromMinutes(15); // Hot data
    private static readonly TimeSpan ChannelMetadataExpiration = TimeSpan.FromHours(1); // Warm data
    private static readonly TimeSpan UserPresenceExpiration = TimeSpan.FromMinutes(5); // Very hot data
    
    // Cache key prefixes
    private const string MESSAGE_PREFIX = "msg:";
    private const string CHANNEL_PREFIX = "ch:";
    private const string RECENT_MESSAGES_PREFIX = "recent_msg:";
    private const string CHANNEL_METADATA_PREFIX = "ch_meta:";
    private const string CONVERSATIONS_PREFIX = "conv:";
    private const string AI_CONVERSATIONS_PREFIX = "ai_conv:";

    public RedisCacheService(
        IDistributedCache distributedCache, 
        IMemoryCache memoryCache,
        ILogger<RedisCacheService> logger)
    {
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            // Try memory cache first (L1 cache - fastest)
            if (_memoryCache.TryGetValue(key, out T? cachedValue))
            {
                return cachedValue;
            }
            
            // Try distributed cache (L2 cache - Redis)
            var cachedData = await _distributedCache.GetStringAsync(key);
            
            if (string.IsNullOrEmpty(cachedData))
            {
                return null;
            }

            var value = JsonSerializer.Deserialize<T>(cachedData, JsonOptions);
            
            // Populate memory cache for next time (cache warming)
            if (value != null)
            {
                _memoryCache.Set(key, value, TimeSpan.FromMinutes(5));
            }
            
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cached data for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var exp = expiration ?? DefaultExpiration;
            
            // Set in memory cache (L1)
            _memoryCache.Set(key, value, TimeSpan.FromMinutes(Math.Min(5, exp.TotalMinutes)));
            
            // Set in distributed cache (L2 - Redis)
            var serializedData = JsonSerializer.Serialize(value, JsonOptions);
            
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = exp
            };

            await _distributedCache.SetStringAsync(key, serializedData, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            // Remove from both caches
            _memoryCache.Remove(key);
            await _distributedCache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache for key: {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        try
        {
            // Note: Pattern-based removal is not directly supported by IDistributedCache
            // This would require direct Redis access via StackExchange.Redis
            // For now, we'll log a warning
            _logger.LogWarning("Pattern-based cache removal is not implemented. Pattern: {Pattern}", pattern);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache by pattern: {Pattern}", pattern);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            // Check memory cache first
            if (_memoryCache.TryGetValue(key, out _))
            {
                return true;
            }
            
            var cachedData = await _distributedCache.GetStringAsync(key);
            return !string.IsNullOrEmpty(cachedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
            return false;
        }
    }
    
    // Discord/Slack style caching methods
    
    public async Task<List<T>?> GetRecentMessagesAsync<T>(int channelId, int count = 50) where T : class
    {
        var key = $"{RECENT_MESSAGES_PREFIX}{channelId}";
        return await GetAsync<List<T>>(key);
    }
    
    public async Task SetRecentMessagesAsync<T>(int channelId, List<T> messages, int count = 50) where T : class
    {
        try
        {
            // Keep only the most recent messages (Discord style)
            var recentMessages = messages.TakeLast(count).ToList();
            var key = $"{RECENT_MESSAGES_PREFIX}{channelId}";
            
            // Use shorter expiration for hot message data
            await SetAsync(key, recentMessages, MessageCacheExpiration);
            
            _logger.LogInformation("Cached {Count} recent messages for channel {ChannelId}", recentMessages.Count, channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching recent messages for channel: {ChannelId}", channelId);
        }
    }
    
    public async Task<T?> GetChannelMetadataAsync<T>(int channelId) where T : class
    {
        var key = $"{CHANNEL_METADATA_PREFIX}{channelId}";
        return await GetAsync<T>(key);
    }
    
    public async Task SetChannelMetadataAsync<T>(int channelId, T metadata) where T : class
    {
        try
        {
            var key = $"{CHANNEL_METADATA_PREFIX}{channelId}";
            // Longer expiration for metadata (Slack style)
            await SetAsync(key, metadata, ChannelMetadataExpiration);
            
            _logger.LogInformation("Cached metadata for channel {ChannelId}", channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching channel metadata: {ChannelId}", channelId);
        }
    }
    
    public async Task InvalidateChannelCacheAsync(int channelId)
    {
        try
        {
            // Invalidate all cache related to this channel
            await RemoveAsync($"{RECENT_MESSAGES_PREFIX}{channelId}");
            await RemoveAsync($"{CHANNEL_METADATA_PREFIX}{channelId}");
            
            _logger.LogInformation("Invalidated cache for channel {ChannelId}", channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating channel cache: {ChannelId}", channelId);
        }
    }
    
    // Conversation caching methods
    
    public async Task<List<T>?> GetUserConversationsAsync<T>(int userId, int organizationId) where T : class
    {
        var key = $"{CONVERSATIONS_PREFIX}user:{userId}:org:{organizationId}";
        return await GetAsync<List<T>>(key);
    }
    
    public async Task SetUserConversationsAsync<T>(int userId, int organizationId, List<T> conversations) where T : class
    {
        try
        {
            var key = $"{CONVERSATIONS_PREFIX}user:{userId}:org:{organizationId}";
            // Cache conversations for 10 minutes (they don't change frequently)
            await SetAsync(key, conversations, TimeSpan.FromMinutes(10));
            
            _logger.LogInformation("Cached {Count} conversations for user {UserId} in org {OrganizationId}", 
                conversations.Count, userId, organizationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching conversations for user: {UserId}, org: {OrganizationId}", userId, organizationId);
        }
    }
    
    public async Task<List<T>?> GetUserAIConversationsAsync<T>(int userId, int organizationId) where T : class
    {
        var key = $"{AI_CONVERSATIONS_PREFIX}user:{userId}:org:{organizationId}";
        return await GetAsync<List<T>>(key);
    }
    
    public async Task SetUserAIConversationsAsync<T>(int userId, int organizationId, List<T> conversations) where T : class
    {
        try
        {
            var key = $"{AI_CONVERSATIONS_PREFIX}user:{userId}:org:{organizationId}";
            // Cache AI conversations for 10 minutes (they don't change frequently)
            await SetAsync(key, conversations, TimeSpan.FromMinutes(10));
            
            _logger.LogInformation("Cached {Count} AI conversations for user {UserId} in org {OrganizationId}", 
                conversations.Count, userId, organizationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching AI conversations for user: {UserId}, org: {OrganizationId}", userId, organizationId);
        }
    }
    
    public async Task InvalidateUserConversationsCacheAsync(int userId, int organizationId)
    {
        try
        {
            // Invalidate both regular and AI conversation caches
            await RemoveAsync($"{CONVERSATIONS_PREFIX}user:{userId}:org:{organizationId}");
            await RemoveAsync($"{AI_CONVERSATIONS_PREFIX}user:{userId}:org:{organizationId}");
            
            _logger.LogInformation("Invalidated conversation cache for user {UserId} in org {OrganizationId}", userId, organizationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating conversation cache for user: {UserId}, org: {OrganizationId}", userId, organizationId);
        }
    }
}

