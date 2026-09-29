using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;

namespace Certio.Web.Services;

public interface ITrustedDeviceService
{
    Task<TrustedDevice?> ValidateDeviceAsync(HttpContext httpContext, User customUser, CancellationToken cancellationToken = default);
    Task<TrustedDevice> RegisterOrUpdateDeviceAsync(HttpContext httpContext, User customUser, bool rememberMe, TrustedDevice? existingDevice = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrustedDevice>> GetDevicesAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> RevokeDeviceAsync(int userId, Guid deviceId, HttpContext httpContext, CancellationToken cancellationToken = default);
    Task RevokeAllDevicesAsync(int userId, HttpContext httpContext, CancellationToken cancellationToken = default);
    string? GetDeviceCookie(HttpContext httpContext);
}

public class TrustedDeviceService : ITrustedDeviceService
{
    public const string DeviceCookieName = "CertioDeviceToken";
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TrustedDeviceService> _logger;

    public TrustedDeviceService(ApplicationDbContext context, ILogger<TrustedDeviceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public string? GetDeviceCookie(HttpContext httpContext)
    {
        if (httpContext.Request.Cookies.TryGetValue(DeviceCookieName, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
        return null;
    }

    public async Task<TrustedDevice?> ValidateDeviceAsync(HttpContext httpContext, User customUser, CancellationToken cancellationToken = default)
    {
        var cookieValue = GetDeviceCookie(httpContext);
        if (string.IsNullOrWhiteSpace(cookieValue))
        {
            return null;
        }

        var parts = cookieValue.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var deviceId))
        {
            httpContext.Response.Cookies.Delete(DeviceCookieName);
            return null;
        }

        var token = parts[1];
        if (string.IsNullOrWhiteSpace(token))
        {
            httpContext.Response.Cookies.Delete(DeviceCookieName);
            return null;
        }

        var tokenHash = ComputeTokenHash(token);

        var device = await _context.TrustedDevices
            .FirstOrDefaultAsync(d =>
                d.Id == deviceId &&
                d.UserId == customUser.Id &&
                d.RevokedAt == null,
                cancellationToken);

        if (device == null)
        {
            httpContext.Response.Cookies.Delete(DeviceCookieName);
            return null;
        }

        if (!string.Equals(device.TokenHash, tokenHash, StringComparison.Ordinal))
        {
            httpContext.Response.Cookies.Delete(DeviceCookieName);
            return null;
        }

        if (device.ExpiresAt.HasValue && device.ExpiresAt.Value < DateTime.UtcNow)
        {
            device.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            httpContext.Response.Cookies.Delete(DeviceCookieName);
            return null;
        }

        // Update device metadata
        device.LastSeenAt = DateTime.UtcNow;
        device.IpAddress = GetClientIp(httpContext);
        device.UserAgent = httpContext.Request.Headers.UserAgent.ToString();

        await _context.SaveChangesAsync(cancellationToken);
        return device;
    }

    public async Task<TrustedDevice> RegisterOrUpdateDeviceAsync(
        HttpContext httpContext,
        User customUser,
        bool rememberMe,
        TrustedDevice? existingDevice = null,
        CancellationToken cancellationToken = default)
    {
        TrustedDevice device;
        if (existingDevice != null)
        {
            device = existingDevice;
            device.LastSeenAt = DateTime.UtcNow;
            device.IpAddress = GetClientIp(httpContext);
            device.UserAgent = httpContext.Request.Headers.UserAgent.ToString();

            // Update trust status if remember me is checked
            if (rememberMe)
            {
                device.IsTrusted = true;
                if (!device.ExpiresAt.HasValue || device.ExpiresAt.Value < DateTime.UtcNow.AddDays(7))
                {
                    device.ExpiresAt = DateTime.UtcNow.AddDays(30);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Always refresh cookie if rememberMe is checked to ensure persistence
            // Or if we want to rotate the token for security
            if (rememberMe)
            {
                await RefreshDeviceCookieAsync(httpContext, device, rememberMe, token: null, cancellationToken);
            }

            return device;
        }

        var token = GenerateToken();
        var tokenHash = ComputeTokenHash(token);

        var (deviceName, deviceType, os, browser) = ParseUserAgent(httpContext.Request.Headers.UserAgent.ToString());

        device = new TrustedDevice
        {
            UserId = customUser.Id,
            DeviceName = deviceName,
            DeviceType = deviceType,
            OperatingSystem = os,
            Browser = browser,
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            IpAddress = GetClientIp(httpContext),
            LastKnownLocation = null,
            IsTrusted = rememberMe,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            ExpiresAt = rememberMe ? DateTime.UtcNow.AddDays(30) : null
        };

        _context.TrustedDevices.Add(device);
        await _context.SaveChangesAsync(cancellationToken);

        await RefreshDeviceCookieAsync(httpContext, device, rememberMe, token, cancellationToken);
        return device;
    }

    private async Task RefreshDeviceCookieAsync(HttpContext httpContext, TrustedDevice device, bool rememberMe, string? token, CancellationToken cancellationToken = default)
    {
        token ??= GenerateToken();
        // When refreshing, update stored hash with new token
        var newHash = ComputeTokenHash(token);
        if (!string.Equals(device.TokenHash, newHash, StringComparison.Ordinal))
        {
            device.TokenHash = newHash;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var cookieValue = $"{device.Id:N}:{token}";
        
        // Determine if we're in development (localhost HTTP) or production (HTTPS)
        var isHttps = httpContext.Request.IsHttps;
        var isLocalhost = httpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                         httpContext.Request.Host.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
        var isDevelopment = !isHttps && isLocalhost;
        
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDevelopment, // Allow non-secure cookies on localhost HTTP
            SameSite = SameSiteMode.Lax, // Use Lax to allow cookie during redirects
            IsEssential = true,
            Path = "/"
        };

        if (rememberMe)
        {
            cookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(30);
        }

        httpContext.Response.Cookies.Append(DeviceCookieName, cookieValue, cookieOptions);
    }

    public async Task<IReadOnlyList<TrustedDevice>> GetDevicesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.TrustedDevices
            .Where(d => d.UserId == userId && d.RevokedAt == null)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeDeviceAsync(int userId, Guid deviceId, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var device = await _context.TrustedDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Id == deviceId, cancellationToken);

        if (device == null)
        {
            return false;
        }

        if (device.RevokedAt != null)
        {
            return true;
        }

        device.RevokedAt = DateTime.UtcNow;
        device.RevokedReason = "User revoked session";

        await _context.SaveChangesAsync(cancellationToken);

        // If revoking current device, remove cookie immediately
        var cookie = GetDeviceCookie(httpContext);
        if (!string.IsNullOrWhiteSpace(cookie) && cookie.StartsWith(device.Id.ToString("N"), StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Response.Cookies.Delete(DeviceCookieName);
        }

        return true;
    }

    public async Task RevokeAllDevicesAsync(int userId, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var devices = await _context.TrustedDevices
            .Where(d => d.UserId == userId && d.RevokedAt == null)
            .ToListAsync(cancellationToken);

        if (devices.Count == 0)
        {
            httpContext.Response.Cookies.Delete(DeviceCookieName);
            return;
        }

        foreach (var device in devices)
        {
            device.RevokedAt = DateTime.UtcNow;
            device.RevokedReason = "User revoked all sessions";
        }

        await _context.SaveChangesAsync(cancellationToken);
        httpContext.Response.Cookies.Delete(DeviceCookieName);
    }

    private static string ComputeTokenHash(string token)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[48];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',').FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return NormalizeIp(first.Trim());
            }
        }

        return NormalizeIp(context.Connection.RemoteIpAddress?.ToString() ?? "Unknown");
    }

    private static string NormalizeIp(string ip)
    {
        // Avoid duplicate "sessions" caused by loopback presenting as IPv6 vs IPv4 on localhost
        // (e.g.::1 vs 127.0.0.1).
        if (string.Equals(ip, "::1", StringComparison.OrdinalIgnoreCase))
            return "127.0.0.1";

        return ip;
    }

    private static (string deviceName, string deviceType, string operatingSystem, string browser) ParseUserAgent(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return ("Unknown Device", "Unknown", "Unknown OS", "Unknown Browser");
        }

        var os = "Unknown OS";
        var deviceType = "Unknown";
        var browser = "Unknown Browser";
        var deviceName = "Unknown Device";

        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            os = "Windows";
            deviceType = "Desktop";
            deviceName = "Windows PC";
        }
        else if (userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase))
        {
            os = "macOS";
            deviceType = "Desktop";
            deviceName = "Mac";
        }
        else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
        {
            os = "iOS";
            deviceType = "Mobile";
            deviceName = "iPhone";
        }
        else if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
        {
            os = "iPadOS";
            deviceType = "Tablet";
            deviceName = "iPad";
        }
        else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            os = "Android";
            deviceType = userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ? "Mobile" : "Tablet";
            deviceName = deviceType == "Mobile" ? "Android Phone" : "Android Tablet";
        }
        else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
        {
            os = "Linux";
            deviceType = "Desktop";
            deviceName = "Linux Device";
        }

        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
        {
            browser = "Microsoft Edge";
        }
        else if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
        {
            browser = "Chrome";
        }
        else if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
        {
            browser = "Safari";
        }
        else if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
        {
            browser = "Firefox";
        }
        else if (userAgent.Contains("OPR", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase))
        {
            browser = "Opera";
        }

        return (deviceName, deviceType, os, browser);
    }
}

