using Microsoft.EntityFrameworkCore;
using Certio.Application.Interfaces;
using Certio.Domain.Services;
using Certio.Infrastructure.Data;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using System.Text.Json;

namespace Certio.Web.Services;

public class EmailToDmService : IEmailToDmService
{
    private readonly ApplicationDbContext _context;
    private readonly IDirectMessageService _directMessageService;
    private readonly ILogger<EmailToDmService> _logger;

    public EmailToDmService(
        ApplicationDbContext context,
        IDirectMessageService directMessageService,
        ILogger<EmailToDmService> logger)
    {
        _context = context;
        _directMessageService = directMessageService;
        _logger = logger;
    }

    public async Task<DirectMessage> ConvertEmailToDirectMessageAsync(EmailMessage emailMessage, int orgId, CancellationToken ct = default)
    {
        // Parse email addresses
        var fromEmail = emailMessage.FromEmail;
        var toEmails = ParseEmailArray(emailMessage.ToEmails);
        var ccEmails = ParseEmailArray(emailMessage.CcEmails);
        var bccEmails = ParseEmailArray(emailMessage.BccEmails);

        // Find users by email addresses within the organization
        var fromUserId = await FindUserByEmailAsync(fromEmail, orgId, ct);
        var toUserIds = new List<int>();
        
        foreach (var email in toEmails)
        {
            var userId = await FindUserByEmailAsync(email, orgId, ct);
            if (userId.HasValue)
            {
                toUserIds.Add(userId.Value);
            }
        }

        // For now, handle 1:1 email conversations (one recipient)
        // Future: could create group threads for multiple recipients
        if (!fromUserId.HasValue || toUserIds.Count != 1)
        {
            _logger.LogWarning("Email {EmailId} cannot be converted to DM - from: {FromEmail}, to: {ToEmails}", 
                emailMessage.Id, fromEmail, string.Join(", ", toEmails));
            throw new InvalidOperationException("Email must have exactly one recipient user in the organization to convert to DM");
        }

        var toUserId = toUserIds[0];

        // Find or create thread
        var thread = await FindOrCreateThreadForEmailAsync(fromEmail, toEmails[0], orgId, ct);

        // Determine the sender (the user who sent the email)
        var senderId = fromUserId.Value;
        var otherUserId = fromUserId.Value == thread.UserAId ? thread.UserBId : thread.UserAId;

        // If the email sender is not in the thread, we need to reassess
        // For now, assume the email account owner is one of the thread participants
        var emailAccount = await _context.EmailAccounts
            .FindAsync(new object[] { emailMessage.EmailAccountId }, ct);
        
        if (emailAccount != null && emailAccount.UserId != senderId && emailAccount.UserId == otherUserId)
        {
            // The email was received by the account owner, sender is the other participant
            senderId = otherUserId;
        }

        // Create email metadata
        var emailMetadata = new Dictionary<string, object>
        {
            ["EmailProvider"] = emailAccount?.Provider ?? "Unknown",
            ["EmailSubject"] = emailMessage.Subject ?? "",
            ["EmailId"] = emailMessage.ExternalEmailId,
            ["EmailThreadId"] = emailMessage.ThreadId ?? "",
            ["FromEmail"] = emailMessage.FromEmail,
            ["FromName"] = emailMessage.FromName ?? "",
            ["ToEmails"] = toEmails,
            ["CcEmails"] = ccEmails,
            ["BccEmails"] = bccEmails,
            ["ReceivedAt"] = emailMessage.ReceivedAt.ToString("O")
        };

        // Create DirectMessage
        var newMessageDto = new Certio.Application.DTOs.NewMessageDto(
            Body: emailMessage.BodyText ?? emailMessage.Body ?? "",
            MessageType: "Email",
            Metadata: emailMetadata
        );

        var directMessage = await _directMessageService.SendAsync(orgId, senderId, thread.Id, newMessageDto, ct);

        // Link email message to direct message
        emailMessage.DirectMessageId = directMessage.Id;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Converted email {EmailId} to DirectMessage {MessageId} in thread {ThreadId}", 
            emailMessage.Id, directMessage.Id, thread.Id);

        return await _context.DirectMessages.FindAsync(new object[] { directMessage.Id }, ct) 
            ?? throw new InvalidOperationException("DirectMessage not found after creation");
    }

    public async Task<DirectThread> FindOrCreateThreadForEmailAsync(string fromEmail, string toEmail, int orgId, CancellationToken ct = default)
    {
        var fromUserId = await FindUserByEmailAsync(fromEmail, orgId, ct);
        var toUserId = await FindUserByEmailAsync(toEmail, orgId, ct);

        if (!fromUserId.HasValue || !toUserId.HasValue)
        {
            throw new InvalidOperationException("Cannot create thread: one or both users not found in organization");
        }

        // Use DirectMessageService to get or create thread
        var thread = await _directMessageService.GetOrCreateThreadAsync(orgId, fromUserId.Value, toUserId.Value, ct);

        // Fetch the actual thread entity
        return await _context.DirectThreads.FindAsync(new object[] { thread.Id }, ct) 
            ?? throw new InvalidOperationException("Thread not found after creation");
    }

    public async Task<int?> FindUserByEmailAsync(string email, int orgId, CancellationToken ct = default)
    {
        // Find user by email address who is a member of the organization
        var user = await _context.Users
            .Include(u => u.UserOrganizations)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && 
                u.UserOrganizations.Any(uo => uo.OrganizationId == orgId && uo.IsActive), ct);

        return user?.Id;
    }

    private List<string> ParseEmailArray(string? jsonArray)
    {
        if (string.IsNullOrEmpty(jsonArray))
        {
            return new List<string>();
        }

        try
        {
            var emails = JsonSerializer.Deserialize<List<string>>(jsonArray);
            return emails ?? new List<string>();
        }
        catch
        {
            // If JSON parsing fails, try treating as comma-separated string
            return jsonArray.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
    }
}

