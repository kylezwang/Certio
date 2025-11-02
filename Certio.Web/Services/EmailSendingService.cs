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
using System.Net.Http.Headers;
using System.Text;

namespace Certio.Web.Services;

public class EmailSendingService : IEmailSendingService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<EmailSendingService> _logger;

    public EmailSendingService(
        ApplicationDbContext context,
        IEmailService emailService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<EmailSendingService> logger)
    {
        _context = context;
        _emailService = emailService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
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

