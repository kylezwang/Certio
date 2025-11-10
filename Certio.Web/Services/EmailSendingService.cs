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

        // Send email based on provider
        string externalEmailId;
        if (emailAccount.Provider == "Gmail")
        {
            externalEmailId = await SendGmailEmailAsync(emailAccount, otherUser.Email, subject, body, ct);
        }
        else if (emailAccount.Provider == "Outlook")
        {
            externalEmailId = await SendOutlookEmailAsync(emailAccount, otherUser.Email, subject, body, ct);
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

    public async Task<bool> SendSystemEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default)
    {
        try
        {
            var provider = _configuration["Security:TwoFactorEmail:Provider"];
            if (string.Equals(provider, "SendGrid", StringComparison.OrdinalIgnoreCase))
            {
                // Try SMTP first if configured
                var smtpHost = _configuration["Security:TwoFactorEmail:SmtpHost"];
                if (!string.IsNullOrWhiteSpace(smtpHost))
                {
                    return await SendViaSmtpAsync(toEmail, subject, bodyHtml, ct);
                }

                // Fall back to REST API
                var apiKey = _configuration["Security:TwoFactorEmail:SendGridApiKey"];
                var fromEmail = _configuration["Security:TwoFactorEmail:FromEmail"];
                var fromName = _configuration["Security:TwoFactorEmail:FromName"] ?? "Certio";

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
                {
                    _logger.LogWarning("Two-factor email configuration is incomplete. Provider={Provider}", provider);
                    return false;
                }

                var payload = new
                {
                    personalizations = new[]
                    {
                        new
                        {
                            to = new[]
                            {
                                new { email = toEmail }
                            }
                        }
                    },
                    from = new { email = fromEmail, name = fromName },
                    subject,
                    content = new[]
                    {
                        new { type = "text/html", value = bodyHtml }
                    }
                };

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

    private async Task<bool> SendViaSmtpAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct)
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

            // Parse port (default to 587 for SendGrid)
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
                    var fallbackEmail = _configuration["Security:TwoFactorEmail:FromEmail"] ?? "no-reply@certio.app";
                    var fallbackName = _configuration["Security:TwoFactorEmail:FromName"] ?? "Certio";
                    fromAddress = new MailboxAddress(fallbackName, fallbackEmail);
                }
            }
            else
            {
                var fallbackEmail = _configuration["Security:TwoFactorEmail:FromEmail"] ?? "no-reply@certio.app";
                var fallbackName = _configuration["Security:TwoFactorEmail:FromName"] ?? "Certio";
                fromAddress = new MailboxAddress(fallbackName, fallbackEmail);
            }

            // Validate email address
            if (string.IsNullOrWhiteSpace(fromAddress.Address) || !fromAddress.Address.Contains('@'))
            {
                _logger.LogError("Invalid from email address: {Email}", fromAddress.Address ?? "<null>");
                return false;
            }

            // SMTP DISABLED - Logging email content to console instead
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("EMAIL (SMTP DISABLED - Logging to console)");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"From: {fromAddress}");
            Console.WriteLine($"To: {toEmail}");
            Console.WriteLine($"Subject: {subject}");
            Console.WriteLine($"Body (HTML):");
            Console.WriteLine(bodyHtml);
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            _logger.LogInformation("SMTP disabled - Email logged to console instead of sending. To: {Email}, Subject: {Subject}", toEmail, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send system email via SMTP to {Email}", toEmail);
            return false;
        }
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
                    subject = metadata["EmailSubject"].ToString() ?? subject;
                }
            }
            catch
            {
                // Ignore metadata parsing errors
            }
        }

        // Format body as HTML email
        var body = $@"
<html>
<body>
    <p>{senderName} sent you a message:</p>
    <div style=""border-left: 3px solid #007bff; padding-left: 15px; margin: 15px 0;"">
        {directMessage.Body}
    </div>
    <p style=""color: #666; font-size: 12px;"">
        Sent via Certio Direct Messaging
    </p>
</body>
</html>";

        return (subject, body);
    }

    private async Task<string> SendGmailEmailAsync(EmailAccount emailAccount, string toEmail, string subject, string body, CancellationToken ct)
    {
        var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
        var accessToken = protector.Unprotect(emailAccount.AccessToken);

        var credential = GoogleCredential.FromAccessToken(accessToken);
        var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Certio"
        });

        // Create email message
        var emailMessage = new Message
        {
            Raw = CreateRawEmailMessage(emailAccount.EmailAddress, toEmail, subject, body)
        };

        var sentMessage = await service.Users.Messages.Send(emailMessage, "me").ExecuteAsync(ct);
        return sentMessage.Id ?? throw new InvalidOperationException("No message ID returned");
    }

    private async Task<string> SendOutlookEmailAsync(EmailAccount emailAccount, string toEmail, string subject, string body, CancellationToken ct)
    {
        var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
        var accessToken = protector.Unprotect(emailAccount.AccessToken);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

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
}

