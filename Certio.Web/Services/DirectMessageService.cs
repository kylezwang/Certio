using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Services;
using Certio.Domain.Exceptions;
using Certio.Infrastructure.Data;

namespace Certio.Web.Services;

public class DirectMessageService : IDirectMessageService
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<DirectMessageService> _logger;

    // Cache key prefixes
    private const string THREAD_MEMBERS_PREFIX = "dm:members:";
    private const string UNREAD_PREFIX = "dm:unread:";
    private const string RECENT_MESSAGES_PREFIX = "dm:recent:";

    public DirectMessageService(
        ApplicationDbContext context,
        ICacheService cacheService,
        IPermissionService permissionService,
        ILogger<DirectMessageService> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<DirectThreadDto> GetOrCreateThreadAsync(int orgId, int currentUserId, int otherUserId, CancellationToken ct = default)
    {
        // Allow self-messaging (users can message themselves)
        // No validation needed for self-messaging

        // Check if users share an organization OR have a relationship
        var currentUserOrgs = await _context.UserOrganizations
            .Where(uo => uo.UserId == currentUserId && uo.IsActive)
            .Select(uo => uo.OrganizationId)
            .ToListAsync(ct);

        var otherUserOrgs = await _context.UserOrganizations
            .Where(uo => uo.UserId == otherUserId && uo.IsActive)
            .Select(uo => uo.OrganizationId)
            .ToListAsync(ct);

        // Check for shared organization
        var sharedOrgId = currentUserOrgs.Intersect(otherUserOrgs).FirstOrDefault();
        
        // Check for relationship between their organizations
        bool hasRelationship = false;
        int threadOrgId = orgId; // Default to requested org

        if (sharedOrgId == 0)
        {
            // No shared org, check for relationship
            var relationship = await _context.OrganizationRelationships
                .Where(or => or.IsActive &&
                            ((currentUserOrgs.Contains(or.SourceOrganizationId) && otherUserOrgs.Contains(or.TargetOrganizationId)) ||
                             (currentUserOrgs.Contains(or.TargetOrganizationId) && otherUserOrgs.Contains(or.SourceOrganizationId))))
                .FirstOrDefaultAsync(ct);

            if (relationship != null)
            {
                hasRelationship = true;
                // Use the law firm's org ID as the thread org (source is always the law firm)
                threadOrgId = relationship.SourceOrganizationId;
                _logger.LogInformation("Users {UserId1} and {UserId2} have relationship via orgs, using law firm org {OrgId}",
                    currentUserId, otherUserId, threadOrgId);
            }
            else
            {
                _logger.LogWarning("User {UserId} attempted to create DM thread with {OtherId} - no shared org or relationship",
                    currentUserId, otherUserId);
                throw new UnauthorizedOperationException(currentUserId, "create", "DirectThread", "Users must share an organization or have an organization relationship");
            }
        }
        else
        {
            threadOrgId = sharedOrgId;
        }

        // Normalize user pair (always store with lower ID first)
        var (userAId, userBId) = NormalizeUserPair(currentUserId, otherUserId);

        // Try to find existing thread - search by users, not just org
        var existingThread = await _context.DirectThreads
            .Where(dt => dt.UserAId == userAId && dt.UserBId == userBId && !dt.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (existingThread != null)
        {
            _logger.LogInformation("Found existing thread {ThreadId} for users {UserA} and {UserB}",
                existingThread.Id, userAId, userBId);
            return MapToThreadDto(existingThread);
        }

        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            // Create new thread with transaction to ensure atomicity
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            try
            {
                var newThread = new DirectThread
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = threadOrgId, // Use the determined org ID
                    UserAId = userAId,
                    UserBId = userBId,
                    CreatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Creating new DM thread for users {UserA} and {UserB} in org {OrgId}",
                    userAId, userBId, threadOrgId);

                _context.DirectThreads.Add(newThread);

                // Create participant records for both users
                var participantA = new DirectParticipant
                {
                    Id = Guid.NewGuid(),
                    ThreadId = newThread.Id,
                    UserId = userAId
                };

                _context.DirectParticipants.Add(participantA);

                // Only add participantB if it's different from participantA (not self-messaging)
                if (userAId != userBId)
                {
                    var participantB = new DirectParticipant
                    {
                        Id = Guid.NewGuid(),
                        ThreadId = newThread.Id,
                        UserId = userBId
                    };
                    _context.DirectParticipants.Add(participantB);
                }

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                // Cache the thread members
                await CacheThreadMembersAsync(newThread.Id, userAId, userBId);

                _logger.LogInformation("Created DM thread {ThreadId} between users {UserA} and {UserB} in org {OrgId}",
                    newThread.Id, userAId, userBId, orgId);

                return MapToThreadDto(newThread);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
            {
                await transaction.RollbackAsync(ct);

                // Race condition: thread was created concurrently, fetch it
                var thread = await _context.DirectThreads
                    .Where(dt => dt.OrganizationId == threadOrgId && dt.UserAId == userAId && dt.UserBId == userBId && !dt.IsDeleted)
                    .FirstOrDefaultAsync(ct);

                if (thread != null)
                {
                    _logger.LogInformation("DM thread already exists (race condition), returning existing {ThreadId}", thread.Id);
                    return MapToThreadDto(thread);
                }

                throw;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<IReadOnlyList<ThreadListItemDto>> ListThreadsAsync(int orgId, int currentUserId, int take = 30, string? cursor = null, CancellationToken ct = default)
    {
        // Validate user is organization member
        var isOrgMember = await _permissionService.IsOrganizationMemberAsync(currentUserId, orgId);
        if (!isOrgMember)
        {
            throw new UnauthorizedOperationException(currentUserId, "list", "DirectThread", "Not an organization member");
        }

        DateTime? cursorTime = null;
        if (!string.IsNullOrEmpty(cursor) && DateTime.TryParse(cursor, out var parsed))
        {
            cursorTime = parsed;
        }

        // Get threads where user is a participant
        var threadsQuery = _context.DirectThreads
            .Where(dt => dt.OrganizationId == orgId && 
                        (dt.UserAId == currentUserId || dt.UserBId == currentUserId) &&
                        !dt.IsDeleted)
            .Include(dt => dt.UserA)
            .Include(dt => dt.UserB)
            .Include(dt => dt.Participants.Where(p => p.UserId == currentUserId))
            .OrderByDescending(dt => dt.LastMessageAt ?? dt.CreatedAt);

        if (cursorTime.HasValue)
        {
            threadsQuery = (IOrderedQueryable<DirectThread>)threadsQuery
                .Where(dt => (dt.LastMessageAt ?? dt.CreatedAt) < cursorTime.Value);
        }

        var threads = await threadsQuery
            .Take(take)
            .ToListAsync(ct);

        // Get last messages and unread counts
        var threadIds = threads.Select(t => t.Id).ToList();
        var lastMessages = await GetLastMessagesForThreads(threadIds, ct);
        var unreadCounts = await GetUnreadCountsForThreads(currentUserId, threadIds, ct);

        var result = new List<ThreadListItemDto>();
        foreach (var thread in threads)
        {
            var otherUser = thread.UserAId == currentUserId ? thread.UserB : thread.UserA;
            var participant = thread.Participants.FirstOrDefault(p => p.UserId == currentUserId);
            
            lastMessages.TryGetValue(thread.Id, out var lastMessage);
            unreadCounts.TryGetValue(thread.Id, out var unreadCount);

            result.Add(new ThreadListItemDto(
                thread.Id,
                new DirectMessageUserDto(otherUser.Id, $"{otherUser.FirstName} {otherUser.LastName}", otherUser.Email, null),
                lastMessage,
                unreadCount,
                thread.LastMessageAt,
                participant?.Pinned ?? false,
                participant?.Archived ?? false,
                participant?.MutedUntil > DateTime.UtcNow
            ));
        }

        return result;
    }

    public async Task<MessageDto> SendAsync(int orgId, int currentUserId, Guid threadId, NewMessageDto dto, CancellationToken ct = default)
    {
        // Validate participant membership
        if (!await IsParticipantAsync(orgId, currentUserId, threadId, ct))
        {
            throw new UnauthorizedOperationException(currentUserId, "send", "DirectMessage", "Not a participant in this thread");
        }

        var thread = await _context.DirectThreads
            .Include(dt => dt.UserA)
            .Include(dt => dt.UserB)
            .FirstOrDefaultAsync(dt => dt.Id == threadId && !dt.IsDeleted, ct);

        if (thread == null)
        {
            throw new KeyNotFoundException($"Thread {threadId} not found");
        }

        var message = new DirectMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = threadId,
            SenderId = currentUserId,
            Body = dto.Body,
            MessageType = dto.MessageType,
            Metadata = dto.Metadata != null ? JsonSerializer.Serialize(dto.Metadata) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.DirectMessages.Add(message);

        // Update thread's last message time
        thread.LastMessageAt = message.CreatedAt;

        await _context.SaveChangesAsync(ct);

        // Update unread count for the other participant
        var otherUserId = thread.UserAId == currentUserId ? thread.UserBId : thread.UserAId;
        await IncrementUnreadCountAsync(otherUserId, threadId);

        // Invalidate recent messages cache
        await _cacheService.RemoveAsync($"{RECENT_MESSAGES_PREFIX}{threadId}");

        var sender = thread.UserAId == currentUserId ? thread.UserA : thread.UserB;

        // Get sender color and check if external contacts
        var senderColor = sender.Color ?? "#3d1019";
        var isExternalContacts = false;
        
        // Check if sender is in ANY External Contacts organization (not just current orgId)
        var senderExternalOrg = await _context.UserOrganizations
            .Include(uo => uo.Organization)
            .Where(uo => uo.UserId == sender.Id && uo.IsActive &&
                         uo.Organization != null &&
                         uo.Organization.Name.EndsWith("'s External Contacts", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefaultAsync(ct);
        
        if (senderExternalOrg != null)
        {
            isExternalContacts = true;
            senderColor = "#aaaaaa";
        }

        _logger.LogInformation("User {UserId} sent message {MessageId} in thread {ThreadId}",
            currentUserId, message.Id, threadId);

        var emailSubject = ExtractMetadataString(message.Metadata, "EmailSubject");

        return new MessageDto(
            message.Id,
            message.ThreadId,
            message.SenderId,
            $"{sender.FirstName} {sender.LastName}",
            message.Body,
            message.MessageType,
            message.CreatedAt,
            message.EditedAt,
            message.IsDeleted,
            senderColor,
            isExternalContacts,
            emailSubject
        );
    }

    public async Task<PagedResult<MessageDto>> GetMessagesAsync(int orgId, int currentUserId, Guid threadId, int take = 50, string? cursor = null, CancellationToken ct = default)
    {
        // Validate participant membership
        if (!await IsParticipantAsync(orgId, currentUserId, threadId, ct))
        {
            throw new UnauthorizedOperationException(currentUserId, "read", "DirectMessage", "Not a participant in this thread");
        }

        Guid? cursorId = null;
        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var parsed))
        {
            cursorId = parsed;
        }

        var messagesQuery = _context.DirectMessages
            .Where(dm => dm.ThreadId == threadId && !dm.IsDeleted)
            .Include(dm => dm.Sender)
            .OrderByDescending(dm => dm.CreatedAt);

        if (cursorId.HasValue)
        {
            var cursorMessage = await _context.DirectMessages
                .Where(dm => dm.Id == cursorId.Value)
                .FirstOrDefaultAsync(ct);
            
            if (cursorMessage != null)
            {
                messagesQuery = (IOrderedQueryable<DirectMessage>)messagesQuery
                    .Where(dm => dm.CreatedAt < cursorMessage.CreatedAt);
            }
        }

        var messages = await messagesQuery
            .Take(take + 1)
            .ToListAsync(ct);

        var hasMore = messages.Count > take;
        if (hasMore)
        {
            messages = messages.Take(take).ToList();
        }

        var nextCursor = hasMore ? messages.Last().Id.ToString() : null;

        // Enrich messages with sender color and external contact status
        // Batch load all sender users and their organization relationships to avoid N+1 queries
        var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
        var senderUsers = await _context.Users
            .Where(u => senderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);
        
        // Load ALL organizations for senders (not just current orgId) to check for External Contacts
        var senderUserOrgs = await _context.UserOrganizations
            .Include(uo => uo.Organization)
            .Where(uo => senderIds.Contains(uo.UserId) && uo.IsActive)
            .ToListAsync(ct);

        // Check if senders are in ANY External Contacts organization
        var externalContactUserIds = senderUserOrgs
            .Where(uo => uo.Organization != null && 
                         uo.Organization.Name.EndsWith("'s External Contacts", StringComparison.OrdinalIgnoreCase))
            .Select(uo => uo.UserId)
            .ToHashSet();

        var messageDtos = messages.Select(m =>
        {
            var senderUser = senderUsers.GetValueOrDefault(m.SenderId);
            var senderColor = senderUser?.Color ?? "#3d1019";
            var isExternalContacts = externalContactUserIds.Contains(m.SenderId);
            
            // If external contacts, use gray color
            if (isExternalContacts)
            {
                senderColor = "#aaaaaa";
            }

            var emailSubject = ExtractMetadataString(m.Metadata, "EmailSubject");

            return new MessageDto(
            m.Id,
            m.ThreadId,
            m.SenderId,
            $"{m.Sender.FirstName} {m.Sender.LastName}",
            m.Body,
            m.MessageType,
            m.CreatedAt,
            m.EditedAt,
                m.IsDeleted,
                senderColor,
                isExternalContacts,
                emailSubject
            );
        }).ToList();

        return new PagedResult<MessageDto>(
            messageDtos,
            messageDtos.Count,
            nextCursor,
            hasMore
        );
    }

    public async Task MarkReadAsync(int orgId, int currentUserId, Guid threadId, DateTime readAt, CancellationToken ct = default)
    {
        // Validate participant membership
        if (!await IsParticipantAsync(orgId, currentUserId, threadId, ct))
        {
            throw new UnauthorizedOperationException(currentUserId, "read", "DirectThread", "Not a participant in this thread");
        }

        var participant = await _context.DirectParticipants
            .FirstOrDefaultAsync(dp => dp.ThreadId == threadId && dp.UserId == currentUserId && !dp.IsDeleted, ct);

        if (participant == null)
        {
            _logger.LogWarning("Participant record not found for user {UserId} in thread {ThreadId}", currentUserId, threadId);
            return;
        }

        participant.LastReadAt = readAt;
        await _context.SaveChangesAsync(ct);

        // Reset unread count in cache
        await ResetUnreadCountAsync(currentUserId, threadId);

        _logger.LogInformation("User {UserId} marked thread {ThreadId} as read at {ReadAt}",
            currentUserId, threadId, readAt);
    }

    public async Task SetTypingAsync(int orgId, int currentUserId, Guid threadId, bool isTyping, CancellationToken ct = default)
    {
        // Validate participant membership
        if (!await IsParticipantAsync(orgId, currentUserId, threadId, ct))
        {
            throw new UnauthorizedOperationException(currentUserId, "type", "DirectThread", "Not a participant in this thread");
        }

        // Typing indicators are ephemeral and handled via SignalR
        // This method exists for consistency and future expansion
        _logger.LogDebug("User {UserId} typing status: {IsTyping} in thread {ThreadId}",
            currentUserId, isTyping, threadId);
    }

    public async Task<bool> IsParticipantAsync(int orgId, int userId, Guid threadId, CancellationToken ct = default)
    {
        // Check cache first (using string representation since cache only works with reference types)
        var cacheKey = $"{THREAD_MEMBERS_PREFIX}{threadId}";
        var cachedMembers = await _cacheService.GetAsync<string>(cacheKey);
        
        if (!string.IsNullOrEmpty(cachedMembers))
        {
            var parts = cachedMembers.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out var userA) && int.TryParse(parts[1], out var userB))
            {
                return userA == userId || userB == userId;
            }
        }

        // Check database - verify user is a participant (regardless of orgId since threads can span orgs via relationships)
        var thread = await _context.DirectThreads
            .Where(dt => dt.Id == threadId && !dt.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (thread == null)
        {
            return false;
        }

        // Verify user is one of the participants
        var isParticipant = thread.UserAId == userId || thread.UserBId == userId;
        
        if (!isParticipant)
        {
            return false;
        }

        // Also verify the orgId matches OR user has access to that organization
        // This allows threads created via relationships to work
        var userHasAccessToOrg = await _context.UserOrganizations
            .AnyAsync(uo => uo.UserId == userId && uo.OrganizationId == thread.OrganizationId && uo.IsActive, ct);
        
        if (!userHasAccessToOrg)
        {
            // Check if user has access via organization relationship
            var userOrgs = await _context.UserOrganizations
                .Where(uo => uo.UserId == userId && uo.IsActive)
                .Select(uo => uo.OrganizationId)
                .ToListAsync(ct);
            
            var hasRelationship = await _context.OrganizationRelationships
                .AnyAsync(or => or.IsActive &&
                               ((userOrgs.Contains(or.SourceOrganizationId) && or.TargetOrganizationId == thread.OrganizationId) ||
                                (userOrgs.Contains(or.TargetOrganizationId) && or.SourceOrganizationId == thread.OrganizationId)), ct);
            
            if (!hasRelationship)
            {
                return false;
            }
        }

        // Cache for future requests
        await CacheThreadMembersAsync(threadId, thread.UserAId, thread.UserBId);

        return true;
    }

    public async Task<(int UserAId, int UserBId)> GetThreadParticipantsAsync(Guid threadId, CancellationToken ct = default)
    {
        // Check cache first
        var cacheKey = $"{THREAD_MEMBERS_PREFIX}{threadId}";
        var cachedMembers = await _cacheService.GetAsync<string>(cacheKey);
        
        if (!string.IsNullOrEmpty(cachedMembers))
        {
            var parts = cachedMembers.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out var userA) && int.TryParse(parts[1], out var userB))
            {
                return (userA, userB);
            }
        }

        // Fetch from database
        var thread = await _context.DirectThreads
            .Where(dt => dt.Id == threadId && !dt.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (thread == null)
        {
            throw new KeyNotFoundException($"Thread {threadId} not found");
        }

        // Cache for future requests
        await CacheThreadMembersAsync(threadId, thread.UserAId, thread.UserBId);

        return (thread.UserAId, thread.UserBId);
    }

    // Helper methods

    private static (int, int) NormalizeUserPair(int userId1, int userId2)
    {
        return userId1 < userId2 ? (userId1, userId2) : (userId2, userId1);
    }

    private static DirectThreadDto MapToThreadDto(DirectThread thread)
    {
        return new DirectThreadDto(
            thread.Id,
            thread.OrganizationId,
            thread.UserAId,
            thread.UserBId,
            thread.CreatedAt,
            thread.LastMessageAt
        );
    }

    private async Task<Dictionary<Guid, string?>> GetLastMessagesForThreads(List<Guid> threadIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, string?>();

        foreach (var threadId in threadIds)
        {
            var lastMessage = await _context.DirectMessages
                .Where(dm => dm.ThreadId == threadId && !dm.IsDeleted)
                .OrderByDescending(dm => dm.CreatedAt)
                .Select(dm => dm.Body)
                .FirstOrDefaultAsync(ct);

            result[threadId] = lastMessage;
        }

        return result;
    }

    private async Task<Dictionary<Guid, int>> GetUnreadCountsForThreads(int userId, List<Guid> threadIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, int>();

        var participant = await _context.DirectParticipants
            .Where(dp => dp.UserId == userId && threadIds.Contains(dp.ThreadId) && !dp.IsDeleted)
            .ToDictionaryAsync(dp => dp.ThreadId, dp => dp.LastReadAt, ct);

        foreach (var threadId in threadIds)
        {
            // Check cache first (using string since cache requires reference types)
            var cacheKey = $"{UNREAD_PREFIX}{userId}:{threadId}";
            var cachedCountStr = await _cacheService.GetAsync<string>(cacheKey);
            
            if (!string.IsNullOrEmpty(cachedCountStr) && int.TryParse(cachedCountStr, out var cachedCount))
            {
                result[threadId] = cachedCount;
                continue;
            }

            // Calculate from database
            participant.TryGetValue(threadId, out var lastReadAt);
            
            var unreadCount = await _context.DirectMessages
                .Where(dm => dm.ThreadId == threadId && 
                            dm.SenderId != userId &&
                            !dm.IsDeleted &&
                            (lastReadAt == null || dm.CreatedAt > lastReadAt))
                .CountAsync(ct);

            result[threadId] = unreadCount;

            // Cache the count
            await _cacheService.SetAsync(cacheKey, unreadCount.ToString(), TimeSpan.FromMinutes(15));
        }

        return result;
    }

    private async Task CacheThreadMembersAsync(Guid threadId, int userAId, int userBId)
    {
        var cacheKey = $"{THREAD_MEMBERS_PREFIX}{threadId}";
        // Store as comma-separated string since cache requires reference types
        await _cacheService.SetAsync(cacheKey, $"{userAId},{userBId}", TimeSpan.FromHours(1));
    }

    private async Task IncrementUnreadCountAsync(int userId, Guid threadId)
    {
        var cacheKey = $"{UNREAD_PREFIX}{userId}:{threadId}";
        var currentStr = await _cacheService.GetAsync<string>(cacheKey);
        var current = !string.IsNullOrEmpty(currentStr) && int.TryParse(currentStr, out var c) ? c : 0;
        await _cacheService.SetAsync(cacheKey, (current + 1).ToString(), TimeSpan.FromMinutes(15));
    }

    private async Task ResetUnreadCountAsync(int userId, Guid threadId)
    {
        var cacheKey = $"{UNREAD_PREFIX}{userId}:{threadId}";
        await _cacheService.SetAsync(cacheKey, "0", TimeSpan.FromMinutes(15));
    }

    private string? ExtractMetadataString(string? metadataJson, string key)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (document.RootElement.TryGetProperty(key, out var element) && element.ValueKind != JsonValueKind.Null)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.GetRawText(),
                    JsonValueKind.True => bool.TrueString,
                    JsonValueKind.False => bool.FalseString,
                    _ => element.ToString()
                };
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to extract key {Key} from message metadata.", key);
        }

        return null;
    }
}

