namespace Certio.Web.Services;

public interface ICacheService
{
    /// <summary>
    /// Get a cached value by key
    /// </summary>
    Task<T?> GetAsync<T>(string key) where T : class;
    
    /// <summary>
    /// Set a value in cache with expiration
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    
    /// <summary>
    /// Remove a value from cache
    /// </summary>
    Task RemoveAsync(string key);
    
    /// <summary>
    /// Remove multiple values from cache by pattern
    /// </summary>
    Task RemoveByPatternAsync(string pattern);
    
    /// <summary>
    /// Check if a key exists in cache
    /// </summary>
    Task<bool> ExistsAsync(string key);
    
    /// <summary>
    /// Get recent messages for a channel (Discord/Slack style - last 50 messages)
    /// </summary>
    Task<List<T>?> GetRecentMessagesAsync<T>(int channelId, int count = 50) where T : class;
    
    /// <summary>
    /// Cache recent messages for a channel
    /// </summary>
    Task SetRecentMessagesAsync<T>(int channelId, List<T> messages, int count = 50) where T : class;
    
    /// <summary>
    /// Get channel metadata (fast access to channel info)
    /// </summary>
    Task<T?> GetChannelMetadataAsync<T>(int channelId) where T : class;
    
    /// <summary>
    /// Set channel metadata
    /// </summary>
    Task SetChannelMetadataAsync<T>(int channelId, T metadata) where T : class;
    
    /// <summary>
    /// Invalidate cache for a specific channel
    /// </summary>
    Task InvalidateChannelCacheAsync(int channelId);
    
    /// <summary>
    /// Get user conversations from cache
    /// </summary>
    Task<List<T>?> GetUserConversationsAsync<T>(int userId, int organizationId) where T : class;
    
    /// <summary>
    /// Cache user conversations
    /// </summary>
    Task SetUserConversationsAsync<T>(int userId, int organizationId, List<T> conversations) where T : class;
    
    /// <summary>
    /// Get user AI conversations from cache
    /// </summary>
    Task<List<T>?> GetUserAIConversationsAsync<T>(int userId, int organizationId) where T : class;
    
    /// <summary>
    /// Cache user AI conversations
    /// </summary>
    Task SetUserAIConversationsAsync<T>(int userId, int organizationId, List<T> conversations) where T : class;
    
    /// <summary>
    /// Invalidate user conversation cache
    /// </summary>
    Task InvalidateUserConversationsCacheAsync(int userId, int organizationId);
}

