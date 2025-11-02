using Certio.Domain.Services;

namespace Certio.Application.Interfaces;

/// <summary>
/// Service interface for sending emails from Direct Messages
/// </summary>
public interface IEmailSendingService
{
    /// <summary>
    /// Send Direct Message as email
    /// </summary>
    Task<EmailMessage> SendEmailFromDirectMessageAsync(Guid directMessageId, int emailAccountId, CancellationToken ct = default);
    
    /// <summary>
    /// Format Direct Message content for email
    /// </summary>
    Task<(string subject, string body)> FormatDirectMessageAsEmailAsync(DirectMessage directMessage, CancellationToken ct = default);
}

