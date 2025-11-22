using System.ComponentModel.DataAnnotations;

namespace Certio.Domain.Users;

public class TrustedDevice
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public int UserId { get; set; }

    [Required]
    [StringLength(150)]
    public string DeviceName { get; set; } = "Unknown Device";

    [StringLength(100)]
    public string DeviceType { get; set; } = "Unknown";

    [StringLength(100)]
    public string OperatingSystem { get; set; } = "Unknown OS";

    [StringLength(100)]
    public string Browser { get; set; } = "Unknown Browser";

    [StringLength(1024)]
    public string UserAgent { get; set; } = string.Empty;

    [StringLength(100)]
    public string IpAddress { get; set; } = "Unknown";

    [StringLength(150)]
    public string? LastKnownLocation { get; set; }

    /// <summary>
    /// Indicates whether this device is trusted to bypass 2FA challenges.
    /// </summary>
    public bool IsTrusted { get; set; } = true;

    [Required]
    [StringLength(256)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional expiry for trusted devices. If null, treated as non-expiring.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    [StringLength(200)]
    public string? RevokedReason { get; set; }

    [StringLength(100)]
    public string? DeviceIdentifier { get; set; }

    public virtual User User { get; set; } = null!;
}

