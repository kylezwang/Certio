using Certio.Domain.Services;

namespace Certio.Application.Interfaces;

/// <summary>
/// Service interface for email account management and OAuth integration
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Complete Gmail OAuth flow and store credentials
    /// </summary>
    Task<EmailAccount> ConnectGmailAccountAsync(int userId, string authCode, string redirectUri, CancellationToken ct = default);
    
    /// <summary>
    /// Complete Outlook OAuth flow and store credentials
    /// </summary>
    Task<EmailAccount> ConnectOutlookAccountAsync(int userId, string authCode, string redirectUri, CancellationToken ct = default);
    
    /// <summary>
    /// Get user's connected email account
    /// </summary>
    Task<EmailAccount?> GetEmailAccountAsync(int userId, CancellationToken ct = default);
    
    /// <summary>
    /// Disconnect email account
    /// </summary>
    Task DisconnectEmailAccountAsync(int userId, CancellationToken ct = default);
    
    /// <summary>
    /// Manually trigger email sync
    /// </summary>
    Task SyncEmailsAsync(int emailAccountId, CancellationToken ct = default);
    
    /// <summary>
    /// Setup webhook subscription with email provider
    /// </summary>
    Task<string> SetupWebhookAsync(int emailAccountId, string webhookUrl, CancellationToken ct = default);
    
    /// <summary>
    /// Refresh expired access token
    /// </summary>
    Task RefreshAccessTokenAsync(int emailAccountId, CancellationToken ct = default);
    
    /// <summary>
    /// Get OAuth authorization URL for Gmail
    /// </summary>
    string GetGmailAuthUrl(string redirectUri, string? state = null);
    
    /// <summary>
    /// Get OAuth authorization URL for Outlook
    /// </summary>
    string GetOutlookAuthUrl(string redirectUri, string? state = null);
}

