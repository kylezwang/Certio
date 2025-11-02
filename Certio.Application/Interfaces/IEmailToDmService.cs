using Certio.Domain.Services;

namespace Certio.Application.Interfaces;

/// <summary>
/// Service interface for converting emails to Direct Messages
/// </summary>
public interface IEmailToDmService
{
    /// <summary>
    /// Convert email message to Direct Message
    /// </summary>
    Task<DirectMessage> ConvertEmailToDirectMessageAsync(EmailMessage emailMessage, int orgId, CancellationToken ct = default);
    
    /// <summary>
    /// Find or create DirectThread for email participants
    /// </summary>
    Task<DirectThread> FindOrCreateThreadForEmailAsync(string fromEmail, string toEmail, int orgId, CancellationToken ct = default);
    
    /// <summary>
    /// Find user by email address within organization
    /// </summary>
    Task<int?> FindUserByEmailAsync(string email, int orgId, CancellationToken ct = default);
}

