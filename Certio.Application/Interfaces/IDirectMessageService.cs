using Certio.Application.DTOs;

namespace Certio.Application.Interfaces;

/// <summary>
/// Service interface for direct messaging (1:1 conversations)
/// All operations are organization-scoped and enforce participant membership
/// </summary>
public interface IDirectMessageService
{
    /// <summary>
    /// Get existing thread or create a new one between two users
    /// </summary>
    Task<DirectThreadDto> GetOrCreateThreadAsync(int orgId, int currentUserId, int otherUserId, CancellationToken ct = default);
    
    /// <summary>
    /// List all direct message threads for the current user
    /// </summary>
    Task<IReadOnlyList<ThreadListItemDto>> ListThreadsAsync(int orgId, int currentUserId, int take = 30, string? cursor = null, CancellationToken ct = default);
    
    /// <summary>
    /// Send a message in a thread
    /// </summary>
    Task<MessageDto> SendAsync(int orgId, int currentUserId, Guid threadId, NewMessageDto dto, CancellationToken ct = default);
    
    /// <summary>
    /// Get messages from a thread with pagination
    /// </summary>
    Task<PagedResult<MessageDto>> GetMessagesAsync(int orgId, int currentUserId, Guid threadId, int take = 50, string? cursor = null, CancellationToken ct = default);
    
    /// <summary>
    /// Mark messages as read up to a specific time
    /// </summary>
    Task MarkReadAsync(int orgId, int currentUserId, Guid threadId, DateTime readAt, CancellationToken ct = default);
    
    /// <summary>
    /// Set typing indicator (ephemeral, for realtime only)
    /// </summary>
    Task SetTypingAsync(int orgId, int currentUserId, Guid threadId, bool isTyping, CancellationToken ct = default);
    
    /// <summary>
    /// Check if user is a participant in the thread
    /// </summary>
    Task<bool> IsParticipantAsync(int orgId, int userId, Guid threadId, CancellationToken ct = default);
    
    /// <summary>
    /// Get thread participants (for caching)
    /// </summary>
    Task<(int UserAId, int UserBId)> GetThreadParticipantsAsync(Guid threadId, CancellationToken ct = default);
}

