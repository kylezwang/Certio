namespace Certio.Application.DTOs;

// Thread DTOs
public record DirectThreadDto(
    Guid Id,
    int OrganizationId,
    int UserAId,
    int UserBId,
    DateTime CreatedAt,
    DateTime? LastMessageAt
);

public record ThreadListItemDto(
    Guid Id,
    DirectMessageUserDto OtherUser,
    string? LastMessagePreview,
    int UnreadCount,
    DateTime? LastMessageAt,
    bool Pinned,
    bool Archived,
    bool Muted
);

public record DirectMessageUserDto(
    int Id,
    string Name,
    string? Email,
    string? AvatarUrl
);

// Message DTOs
public record NewMessageDto(
    string? Body,
    string MessageType = "Text",
    Dictionary<string, object>? Metadata = null
);

public record MessageDto(
    Guid Id,
    Guid ThreadId,
    int SenderId,
    string SenderName,
    string? Body,
    string MessageType,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsDeleted,
    string? SenderColor = null,
    bool? IsExternalContacts = null
);

// Paging
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    string? NextCursor,
    bool HasMore
);

// Request DTOs
public record CreateThreadRequest(
    int OtherUserId
);

public record MarkReadRequest(
    DateTime ReadAt
);

