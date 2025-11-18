using System;
using System.Collections.Generic;

namespace Certio.Application.DTOs;

/// <summary>
/// Request to build comprehensive user data context for AI RAG
/// </summary>
public record UserDataContextRequest(
    int UserId,
    int OrganizationId,
    string Query,
    int TopK = 10,
    List<string>? IncludeModules = null,  // null = all modules, otherwise specific modules
    int? MatterId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
);

/// <summary>
/// Result containing structured user data context
/// </summary>
public record UserDataContextResult(
    Guid QueryId,
    int UserId,
    int OrganizationId,
    string Query,
    Dictionary<string, UserModuleData> ModuleData,
    Dictionary<string, object?> Metadata,
    DateTime GeneratedAt
);

/// <summary>
/// Data from a specific module
/// </summary>
public record UserModuleData(
    string ModuleName,
    int TotalItems,
    List<UserDataChunk> Chunks,
    Dictionary<string, object?> Summary
);

/// <summary>
/// Individual data chunk for RAG
/// </summary>
public record UserDataChunk(
    Guid Id,
    string ModuleName,
    string EntityType,
    int EntityId,
    string Title,
    string Content,
    Dictionary<string, object?> Metadata,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    float RelevanceScore = 0.0f
);

/// <summary>
/// User data context cache entry
/// </summary>
public class UserDataContextCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public int OrganizationId { get; set; }
    public string QueryHash { get; set; } = string.Empty;
    public string ContextJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}

