using Microsoft.EntityFrameworkCore;
using Certio.Application.Interfaces;
using Certio.Domain.Services;
using Certio.Infrastructure.Data;
using System.Text.Json;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Certio.Web.Services;

public class EmailSendingService : IEmailSendingService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<EmailSendingService> _logger;
    private readonly IConfiguration _configuration;

    public EmailSendingService(
        ApplicationDbContext context,
        IEmailService emailService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<EmailSendingService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _emailService = emailService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<EmailMessage> SendEmailFromDirectMessageAsync(Guid directMessageId, int emailAccountId, CancellationToken ct = default)
    {
        var directMessage = await _context.DirectMessages
            .Include(dm => dm.Thread)
            .Include(dm => dm.Sender)
            .FirstOrDefaultAsync(dm => dm.Id == directMessageId, ct);

        if (directMessage == null)
        {
            throw new InvalidOperationException("DirectMessage not found");
        }

        var emailAccount = await _context.EmailAccounts.FindAsync(new object[] { emailAccountId }, ct);
        if (emailAccount == null || !emailAccount.IsActive)
        {
            throw new InvalidOperationException("Email account not found or inactive");
        }

        // Security check: Verify email account belongs to the message sender
        if (emailAccount.UserId != directMessage.SenderId)
        {
            throw new UnauthorizedAccessException("Email account does not belong to message sender");
        }

        // Ensure token is valid
        await _emailService.RefreshAccessTokenAsync(emailAccountId, ct);
        await _context.Entry(emailAccount).ReloadAsync(ct);

        // Get the other participant in the thread
        var otherUserId = directMessage.Thread.UserAId == directMessage.SenderId 
            ? directMessage.Thread.UserBId 
            : directMessage.Thread.UserAId;
        
        var otherUser = await _context.Users.FindAsync(new object[] { otherUserId }, ct);
        if (otherUser == null)
        {
            throw new InvalidOperationException("Recipient user not found");
        }

        // Format message as email
        var (subject, body) = await FormatDirectMessageAsEmailAsync(directMessage, ct);

        // Get sender's display name
        var sender = directMessage.Sender;
        var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}".Trim() : null;
        if (string.IsNullOrWhiteSpace(senderName))
        {
            senderName = sender?.Email?.Split('@')[0] ?? "Unknown";
        }

        // Send email based on provider
        string externalEmailId;
        if (emailAccount.Provider == "Gmail")
        {
            externalEmailId = await SendGmailEmailAsync(emailAccount, otherUser.Email, subject, body, senderName, ct);
        }
        else if (emailAccount.Provider == "Outlook")
        {
            externalEmailId = await SendOutlookEmailAsync(emailAccount, otherUser.Email, subject, body, senderName, ct);
        }
        else
        {
            throw new NotSupportedException($"Email sending not supported for provider: {emailAccount.Provider}");
        }

        // Create EmailMessage record
        var emailMessage = new EmailMessage
        {
            Id = Guid.NewGuid(),
            EmailAccountId = emailAccountId,
            DirectMessageId = directMessageId,
            ExternalEmailId = externalEmailId,
            Subject = subject,
            FromEmail = emailAccount.EmailAddress,
            ToEmails = JsonSerializer.Serialize(new[] { otherUser.Email }),
            Body = body,
            BodyText = body, // Simplified - in production, convert HTML to plain text
            IsRead = true, // Sent emails are marked as read
            ReceivedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _context.EmailMessages.Add(emailMessage);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Sent email {EmailId} from DirectMessage {MessageId}", externalEmailId, directMessageId);

        return emailMessage;
    }

    public Task<bool> SendSystemEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default)
        => SendSystemEmailAsync(toEmail, subject, bodyHtml, fromNameOverride: null, replyToEmailOverride: null, ct);

    public async Task<bool> SendSystemEmailAsync(
        string toEmail,
        string subject,
        string bodyHtml,
        string? fromNameOverride,
        string? replyToEmailOverride,
        CancellationToken ct = default)
    {
        try
        {
            var provider = _configuration["Security:TwoFactorEmail:Provider"];
            
            // Both SendGrid and Azure support SMTP
            if (string.Equals(provider, "SendGrid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(provider, "Azure", StringComparison.OrdinalIgnoreCase))
            {
                // Try SMTP first if configured
                var smtpHost = _configuration["Security:TwoFactorEmail:SmtpHost"];
                if (!string.IsNullOrWhiteSpace(smtpHost))
                {
                    return await SendViaSmtpAsync(toEmail, subject, bodyHtml, fromNameOverride, replyToEmailOverride, ct);
                }
                
                // Only SendGrid has REST API fallback
                if (!string.Equals(provider, "SendGrid", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("SMTP not configured for provider '{Provider}'. SMTP is required.", provider);
                    return false;
                }

                // Fall back to REST API
                var apiKey = _configuration["Security:TwoFactorEmail:SendGridApiKey"];
                var fromEmail = _configuration["Security:TwoFactorEmail:FromEmail"];
                var fromName = (string.IsNullOrWhiteSpace(fromNameOverride) ? null : fromNameOverride.Trim())
                               ?? _configuration["Security:TwoFactorEmail:FromName"]
                               ?? "Notal";

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
                {
                    _logger.LogWarning("Two-factor email configuration is incomplete. Provider={Provider}", provider);
                    return false;
                }

                var trackingSettings = new
                {
                    click_tracking = new { enable = false, enable_text = false },
                    open_tracking = new { enable = false }
                };

                var payload = new Dictionary<string, object?>
                {
                    ["personalizations"] = new[]
                    {
                        new
                        {
                            to = new[]
                            {
                                new { email = toEmail }
                            }
                        }
                    },
                    ["from"] = new { email = fromEmail, name = fromName },
                    ["subject"] = subject,
                    ["content"] = new[]
                    {
                        new { type = "text/plain", value = StripHtmlToText(bodyHtml) },
                        new { type = "text/html", value = bodyHtml }
                    },
                    // IMPORTANT: disable click tracking so security-sensitive links (e.g. Change Notice actions)
                    // don't get rewritten to a tracking domain that may have invalid SSL.
                    ["tracking_settings"] = trackingSettings
                };

                if (!string.IsNullOrWhiteSpace(replyToEmailOverride))
                {
                    payload["reply_to"] = new { email = replyToEmailOverride.Trim() };
                }

                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send")
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Failed to send system email via SendGrid REST API. Status={StatusCode}, Error={Error}", response.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("System email sent to {Email} via SendGrid REST API", toEmail);
                return true;
            }

            _logger.LogWarning("Unsupported system email provider '{Provider}'.", provider ?? "<null>");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending system email to {Email}", toEmail);
            return false;
        }
    }

    private async Task<bool> SendViaSmtpAsync(
        string toEmail,
        string subject,
        string bodyHtml,
        string? fromNameOverride,
        string? replyToEmailOverride,
        CancellationToken ct)
    {
        try
        {
            var smtpHost = _configuration["Security:TwoFactorEmail:SmtpHost"];
            var smtpPortStr = _configuration["Security:TwoFactorEmail:SmtpPort"];
            var smtpSecureStr = _configuration["Security:TwoFactorEmail:SmtpSecure"];
            var smtpUser = _configuration["Security:TwoFactorEmail:SmtpUser"];
            var smtpPassword = _configuration["Security:TwoFactorEmail:SmtpPassword"];
            var smtpFromEmail = _configuration["Security:TwoFactorEmail:SmtpFromEmail"];

            // Trim and validate host
            smtpHost = smtpHost?.Trim();
            
            // Check if host contains placeholder (environment variable not set) or is empty
            if (string.IsNullOrWhiteSpace(smtpHost) || 
                smtpHost.StartsWith("${") || 
                string.IsNullOrWhiteSpace(smtpPassword) || 
                smtpPassword.Trim().StartsWith("${"))
            {
                _logger.LogWarning("SMTP configuration is incomplete or not set. Host={Host}, HasPassword={HasPassword}", 
                    smtpHost ?? "<null>", !string.IsNullOrWhiteSpace(smtpPassword));
                return false;
            }

            // Parse port (default to 587 for SMTP providers like SendGrid and Azure)
            int smtpPort = 587;
            if (!string.IsNullOrWhiteSpace(smtpPortStr) && int.TryParse(smtpPortStr, out var parsedPort))
            {
                smtpPort = parsedPort;
            }

            // Parse secure flag (false = STARTTLS on port 587, true = SSL on port 465)
            var useSsl = false;
            if (!string.IsNullOrWhiteSpace(smtpSecureStr) && bool.TryParse(smtpSecureStr, out var parsedSecure))
            {
                useSsl = parsedSecure;
            }

            // Parse from email using MimeKit's parser (handles RFC-compliant formats)
            MailboxAddress fromAddress;
            if (!string.IsNullOrWhiteSpace(smtpFromEmail))
            {
                try
                {
                    // MimeKit can parse formats like: "Name <email@domain.com>" or "email@domain.com"
                    var trimmed = smtpFromEmail.Trim().Trim('"');
                    if (InternetAddress.TryParse(trimmed, out var address) && address is MailboxAddress mailbox)
                    {
                        fromAddress = mailbox;
                    }
                    else
                    {
                        // Fallback: try parsing as just email address
                        fromAddress = new MailboxAddress(string.Empty, trimmed);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse SMTP_FROM_EMAIL: {Email}, using fallback", smtpFromEmail);
                    var fallbackEmail = _configuration["Security:TwoFactorEmail:FromEmail"] ?? "info@notal.org";
                    var fallbackName = _configuration["Security:TwoFactorEmail:FromName"] ?? "Notal";
                    fromAddress = new MailboxAddress(fallbackName, fallbackEmail);
                }
            }
            else
            {
                var fallbackEmail = _configuration["Security:TwoFactorEmail:FromEmail"] ?? "info@notal.org";
                var fallbackName = _configuration["Security:TwoFactorEmail:FromName"] ?? "Notal";
                fromAddress = new MailboxAddress(fallbackName, fallbackEmail);
            }

            // Validate email address
            if (string.IsNullOrWhiteSpace(fromAddress.Address) || !fromAddress.Address.Contains('@'))
            {
                _logger.LogError("Invalid from email address: {Email}", fromAddress.Address ?? "<null>");
                return false;
            }

            // Apply display name: fromNameOverride takes precedence, then FromName config, otherwise keep existing
            if (!string.IsNullOrWhiteSpace(fromNameOverride))
            {
                fromAddress = new MailboxAddress(fromNameOverride.Trim(), fromAddress.Address);
            }
            else if (string.IsNullOrWhiteSpace(fromAddress.Name))
            {
                // If no name was set and no override provided, use FromName config
                var fromName = _configuration["Security:TwoFactorEmail:FromName"];
                if (!string.IsNullOrWhiteSpace(fromName))
                {
                    fromAddress = new MailboxAddress(fromName.Trim(), fromAddress.Address);
                }
            }

            // Build and send email via SMTP
            var message = new MimeMessage();
            message.From.Add(fromAddress);
            message.To.Add(new MailboxAddress(string.Empty, toEmail));
            message.Subject = subject;

            if (!string.IsNullOrWhiteSpace(replyToEmailOverride) && replyToEmailOverride.Contains('@'))
            {
                message.ReplyTo.Add(new MailboxAddress(string.Empty, replyToEmailOverride.Trim()));
            }

            // IMPORTANT (SendGrid SMTP only): disable click/open tracking so action links aren't rewritten.
            // This prevents recipients from hitting "Your connection is not private" due to misconfigured link branding SSL.
            // Azure Communication Services Email doesn't need this header.
            var provider = _configuration["Security:TwoFactorEmail:Provider"];
            if (string.Equals(provider, "SendGrid", StringComparison.OrdinalIgnoreCase))
            {
                // SendGrid SMTPAPI header format:
                // { "filters": { "clicktrack": { "settings": { "enable": 0 } }, "opentrack": { "settings": { "enable": 0 } } } }
                try
                {
                    var smtpApi = new
                    {
                        filters = new
                        {
                            clicktrack = new { settings = new { enable = 0 } },
                            opentrack = new { settings = new { enable = 0 } }
                        }
                    };
                    message.Headers.Add("X-SMTPAPI", JsonSerializer.Serialize(smtpApi));
                }
                catch
                {
                    // If we can't add the header for any reason, still send the email.
                }
            }

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = bodyHtml,
                TextBody = StripHtmlToText(bodyHtml)
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            // Connect to SMTP server
            await client.ConnectAsync(smtpHost, smtpPort, useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
            
            // Authenticate
            await client.AuthenticateAsync(smtpUser, smtpPassword, ct);
            
            // Send message
            await client.SendAsync(message, ct);
            
            // Disconnect
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("System email sent via SMTP to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send system email via SMTP to {Email}", toEmail);
            return false;
        }
    }

    private static string StripHtmlToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        // Very small, safe conversion for email clients/spam filters:
        // remove tags and decode entities.
        var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(noTags);
        // collapse whitespace
        decoded = System.Text.RegularExpressions.Regex.Replace(decoded, "\\s+", " ").Trim();
        return decoded;
    }

    public async Task<(string subject, string body)> FormatDirectMessageAsEmailAsync(DirectMessage directMessage, CancellationToken ct = default)
    {
        var sender = await _context.Users.FindAsync(new object[] { directMessage.SenderId }, ct);
        var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}".Trim() : "Unknown";

        // Use metadata if available for email subject
        string subject = "Direct Message";
        if (!string.IsNullOrEmpty(directMessage.Metadata))
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(directMessage.Metadata);
                if (metadata != null && metadata.ContainsKey("EmailSubject"))
                {
                    var rawSubject = metadata["EmailSubject"].ToString() ?? subject;
                    subject = SanitizeEmailSubject(rawSubject);
                }
            }
            catch
            {
                // Ignore metadata parsing errors
            }
        }

        // Format body as HTML email
        // HTML-encode sender name and message body to prevent XSS
        var encodedSenderName = System.Net.WebUtility.HtmlEncode(senderName);
        var encodedBody = System.Net.WebUtility.HtmlEncode(directMessage.Body);
        
        var body = $@"
<html>
<body>
    <p>{encodedSenderName} sent you a message:</p>
    <div style=""border-left: 3px solid #007bff; padding-left: 15px; margin: 15px 0;"">
        {encodedBody}
    </div>
    <p style=""color: #666; font-size: 12px;"">
        Sent via Notal Communications
    </p>
</body>
</html>";

        return (subject, body);
    }

    private async Task<string> SendGmailEmailAsync(EmailAccount emailAccount, string toEmail, string subject, string body, string? senderName, CancellationToken ct)
    {
        var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
        var accessToken = protector.Unprotect(emailAccount.AccessToken);

        var credential = GoogleCredential.FromAccessToken(accessToken);
        var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Certio"
        });

        // Format From header with display name
        var fromHeader = FormatEmailAddress(emailAccount.EmailAddress, senderName);

        // Create email message
        var emailMessage = new Message
        {
            Raw = CreateRawEmailMessage(fromHeader, toEmail, subject, body)
        };

        var sentMessage = await service.Users.Messages.Send(emailMessage, "me").ExecuteAsync(ct);
        return sentMessage.Id ?? throw new InvalidOperationException("No message ID returned");
    }

    private async Task<string> SendOutlookEmailAsync(EmailAccount emailAccount, string toEmail, string subject, string body, string? senderName, CancellationToken ct)
    {
        var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
        var accessToken = protector.Unprotect(emailAccount.AccessToken);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

        // Build from field with display name if available
        var fromField = new { emailAddress = new { address = emailAccount.EmailAddress, name = senderName } };

        var message = new
        {
            message = new
            {
                subject = subject,
                body = new
                {
                    contentType = "HTML",
                    content = body
                },
                from = fromField,
                toRecipients = new[]
                {
                    new { emailAddress = new { address = toEmail } }
                }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("https://graph.microsoft.com/v1.0/me/sendMail", content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to send Outlook email: {Content}", responseContent);
            throw new InvalidOperationException($"Failed to send email: {responseContent}");
        }

        // Outlook doesn't return message ID immediately, generate one
        return $"outlook_{DateTime.UtcNow.Ticks}";
    }

    /// <summary>
    /// Formats an email address with optional display name in RFC 5322 format.
    /// Example: "John Marshall <johnm123@gmail.com>" or just "johnm123@gmail.com" if no name
    /// </summary>
    private string FormatEmailAddress(string emailAddress, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return emailAddress;
        }

        // Escape display name if it contains special characters
        // If it contains quotes, commas, or other special chars, wrap in quotes
        var escapedName = displayName;
        if (displayName.Contains('"') || displayName.Contains(',') || displayName.Contains(';') || displayName.Contains('<') || displayName.Contains('>'))
        {
            escapedName = $"\"{displayName.Replace("\"", "\\\"")}\"";
        }

        return $"{escapedName} <{emailAddress}>";
    }

    private string CreateRawEmailMessage(string from, string to, string subject, string body)
    {
        var message = new StringBuilder();
        message.AppendLine($"From: {from}");
        message.AppendLine($"To: {to}");
        message.AppendLine($"Subject: {subject}");
        message.AppendLine("Content-Type: text/html; charset=utf-8");
        message.AppendLine();
        message.AppendLine(body);

        var bytes = Encoding.UTF8.GetBytes(message.ToString());
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }

    /// <summary>
    /// Sanitizes email subject to prevent header injection attacks.
    /// Removes or replaces control characters (CR, LF, NULL) that could be used to inject additional headers.
    /// </summary>
    private string SanitizeEmailSubject(string subject)
    {
        if (string.IsNullOrEmpty(subject))
        {
            return "Direct Message";
        }

        // Remove control characters that could be used for header injection
        // This includes CR (\r), LF (\n), NULL (\0), and other control characters
        var sanitized = new StringBuilder(subject.Length);
        
        foreach (char c in subject)
        {
            // Allow printable characters and common whitespace (space and tab)
            // Reject carriage return, line feed, null, and other control characters
            if (c == '\r' || c == '\n' || c == '\0' || (char.IsControl(c) && c != '\t'))
            {
                // Replace with space to maintain readability
                sanitized.Append(' ');
            }
            else
            {
                sanitized.Append(c);
            }
        }

        // Trim and collapse multiple spaces
        var result = sanitized.ToString().Trim();
        while (result.Contains("  "))
        {
            result = result.Replace("  ", " ");
        }

        // Limit length to reasonable email subject length (RFC 5322 recommends 78 chars per line)
        // Using 200 as a safe maximum for subject lines
        if (result.Length > 200)
        {
            result = result.Substring(0, 197) + "...";
        }

        return string.IsNullOrWhiteSpace(result) ? "Direct Message" : result;
    }
}

