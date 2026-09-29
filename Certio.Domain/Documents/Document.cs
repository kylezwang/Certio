using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Certio.Domain.Documents;

public enum DocumentSourceType
{
    GoogleDrive = 1,
    OneDrive = 2,
    InternalUpload = 3
}

public enum DocumentStatus
{
    Draft = 0,
    Review = 1,
    Final = 2,
    Published = 3
}

public enum DocumentPermissionLevel
{
    Read = 0,
    Comment = 1,
    Edit = 2,
    Owner = 3
}

public enum ExternalConnectionProvider
{
    Google = 1,
    Microsoft = 2
}

    public class Document
    {
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
    public Guid OrgId { get; set; }
        
    public Guid? MatterId { get; set; }
        
        [Required]
    public DocumentSourceType SourceType { get; set; }
        
    [MaxLength(256)]
    public string? ExternalFileId { get; set; }
        
        [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;
        
        [Required]
    [MaxLength(150)]
    public string FileType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [Required]
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
        
    [MaxLength(120)]
    public string Category { get; set; } = "General";
        
    public Guid? OwnerUserId { get; set; }
        
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    public bool IsPrivate { get; set; }

    [MaxLength(512)]
    public string? PreviewUrl { get; set; }

    [MaxLength(512)]
    public string? EmbedUrl { get; set; }

    [MaxLength(512)]
    public string? DownloadUrl { get; set; }

    public Guid? VectorId { get; set; }

    public DateTime? LastEmbeddedAt { get; set; }

    [MaxLength(128)]
    public string? Checksum { get; set; }

    public List<string> Tags { get; set; } = new();

    public Dictionary<string, string?> Metadata { get; set; } = new();

    public Guid? AuditTrailId { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? StorageBucket { get; set; }

    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();

    public ICollection<DocumentVector> Vectors { get; set; } = new List<DocumentVector>();

    public ICollection<DocumentPermission> Permissions { get; set; } = new List<DocumentPermission>();

    [NotMapped]
    public string Name
    {
        get => Title;
        set => Title = value;
    }

    [NotMapped]
    public string Type
    {
        get => FileType;
        set => FileType = value;
    }

    [NotMapped]
    public string Icon => SourceType switch
    {
        DocumentSourceType.GoogleDrive => "File",
        DocumentSourceType.OneDrive => "File",
        _ => "FileText"
    };

    [NotMapped]
    public string Visibility
    {
        get => IsPrivate ? "Private" : "Shared";
        set => IsPrivate = string.Equals(value, "Private", StringComparison.OrdinalIgnoreCase);
    }

    [NotMapped]
    public string Author
    {
        get
        {
            if (Metadata.TryGetValue("ownerName", out var owner) && !string.IsNullOrWhiteSpace(owner))
            {
                return owner!;
            }

            return "Unknown";
        }
        set
        {
            Metadata["ownerName"] = value;
        }
    }

    [NotMapped]
    public DateTime Modified => ModifiedAt;

    [NotMapped]
    public string Size => FormatFileSize(FileSizeBytes);

    private static string FormatFileSize(long size)
    {
        if (size <= 0)
        {
            return "-";
        }

        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        var index = 0;
        double displaySize = size;

        while (displaySize >= 1024 && index < suffixes.Length - 1)
        {
            displaySize /= 1024;
            index++;
        }

        return $"{displaySize:0.#} {suffixes[index]}";
    }
    }
    
    public class DocumentVersion
    {
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DocumentId { get; set; }

    [Required]
        public int VersionNumber { get; set; }
        
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    public Guid ModifiedByUserId { get; set; }

    [MaxLength(256)]
    public string? ExternalRevisionId { get; set; }

    [MaxLength(128)]
    public string? Checksum { get; set; }

    [MaxLength(512)]
    public string StorageUrl { get; set; } = string.Empty;

    public Guid? VectorId { get; set; }

    public DateTime? EmbeddingTimestamp { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Document Document { get; set; } = null!;
}

public class DocumentVector
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DocumentId { get; set; }

    public Guid? VersionId { get; set; }

    [Required]
    public int ChunkIndex { get; set; }

    [Required]
    public string ContentChunk { get; set; } = string.Empty;

    public float[]? Embedding { get; set; }

    [MaxLength(200)]
    public string? EmbeddingReference { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(100)]
    public string SourceType { get; set; } = string.Empty;

    [Required]
    public Guid OrgId { get; set; }

    public Guid? MatterId { get; set; }

    public Dictionary<string, string?> Tags { get; set; } = new();

    public Document Document { get; set; } = null!;

    public DocumentVersion? Version { get; set; }
    }
    
public class RagQuery
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid OrgId { get; set; }

    [Required]
    public Guid UserId { get; set; }
        
        [Required]
    public string QueryText { get; set; } = string.Empty;

    public int TopK { get; set; } = 5;

    public List<Guid> RetrievedVectorIds { get; set; } = new();

    public Dictionary<string, string?> ContextJson { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    [MaxLength(120)]
    public string? UsedByAgent { get; set; }
}

public class ExternalConnection
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid OrgId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public ExternalConnectionProvider Provider { get; set; }

    [Required]
    public string AccessToken { get; set; } = string.Empty; // Encrypted, nvarchar(max)
        
    [Required]
    public string RefreshToken { get; set; } = string.Empty; // Encrypted, nvarchar(max)
        
    public DateTime TokenExpiry { get; set; }

    public List<string> Scopes { get; set; } = new();
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class DocumentPermission
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DocumentId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public DocumentPermissionLevel PermissionLevel { get; set; }

    [Required]
    public Guid GrantedBy { get; set; }

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
    }
    
public class RagCacheEntry
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid OrgId { get; set; }

    [Required]
    public Guid UserId { get; set; }
        
    [Required]
    [MaxLength(128)]
    public string QueryHash { get; set; } = string.Empty;
        
    [Required]
    public string ContextJson { get; set; } = string.Empty;
        
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    public DateTime ExpiresAt { get; set; }
}
