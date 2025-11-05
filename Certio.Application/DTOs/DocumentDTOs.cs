using System;
using System.Collections.Generic;
using Certio.Domain.Documents;

namespace Certio.Application.DTOs;

public record ProviderFileMetadata(
    Guid OrgId,
    Guid UserId,
    DocumentSourceType SourceType,
    string ExternalFileId,
    string Title,
    string FileType,
    long FileSizeBytes,
    DocumentStatus Status,
    string Category,
    Guid? MatterId,
    bool IsPrivate,
    DateTime CreatedAt,
    DateTime ModifiedAt,
    string? PreviewUrl,
    string? EmbedUrl,
    string? DownloadUrl,
    IReadOnlyDictionary<string, string?> Metadata,
    IReadOnlyCollection<string> Tags
);

public record DocumentIndexRequest(
    Guid DocumentId,
    Guid OrgId,
    Guid? VersionId,
    string Content,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, string?> Metadata
);

public record VectorUpsertRequest(
    Guid DocumentId,
    Guid OrgId,
    Guid? VersionId,
    IReadOnlyList<DocumentVectorChunk> Chunks
);

public record DocumentVectorChunk(
    int Index,
    string Content,
    float[]? Embedding,
    string? EmbeddingReference,
    IReadOnlyDictionary<string, string?> Tags
);

public record RagContextRequest(
    Guid OrgId,
    Guid UserId,
    string Query,
    int TopK,
    Guid? MatterId,
    IReadOnlyCollection<Guid>? RestrictToDocumentIds
);

public record RagContextResult(
    Guid QueryId,
    Guid OrgId,
    Guid UserId,
    string Query,
    int TopK,
    IReadOnlyList<DocumentVector> Vectors,
    IReadOnlyDictionary<string, string?> Context,
    string? UsedByAgent
);

public record DocumentAuditEvent(
    Guid OrgId,
    Guid? MatterId,
    Guid? DocumentId,
    Guid? VersionId,
    Guid? UserId,
    string EventType,
    string Description,
    DateTime Timestamp,
    int? UserIdInt = null // Optional: actual int user ID for audit logs (takes precedence over Guid conversion)
);

public record GoogleDriveChangeNotification(
    string ResourceId,
    string ResourceUri,
    DateTime Timestamp,
    Guid OrgId,
    Guid UserId
);

public record MicrosoftGraphChangeNotification(
    string Resource,
    string SubscriptionId,
    DateTime Timestamp,
    Guid OrgId,
    Guid UserId
);

public record DocumentSearchResult(
    Document Document,
    IReadOnlyList<DocumentVector> ContextVectors,
    double Score
);
