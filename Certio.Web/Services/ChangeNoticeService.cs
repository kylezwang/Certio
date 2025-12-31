using System.Text.Json;
using System.Text.RegularExpressions;
using Certio.Application.DTOs.ChangeControl;
using Certio.Application.Interfaces;
using Certio.Domain.ChangeControl;
using Certio.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Services;

public sealed class ChangeNoticeService : IChangeNoticeService
{
    private static readonly Regex SplitEmailsRegex = new(@"[,\n;\r\t ]+", RegexOptions.Compiled);
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailSendingService _emailSendingService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<ChangeNoticeService> _logger;

    public ChangeNoticeService(
        ApplicationDbContext dbContext,
        IEmailSendingService emailSendingService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ChangeNoticeService> logger)
    {
        _dbContext = dbContext;
        _emailSendingService = emailSendingService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public async Task<ChangeControlSummaryDto> GetSummaryAsync(int organizationId, int matterId, CancellationToken ct = default)
    {
        var notices = await _dbContext.ChangeNotices
            .AsNoTracking()
            .Include(n => n.Recipients)
            .Where(n => n.OrganizationId == organizationId && n.MatterId == matterId && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        return MapSummary(organizationId, matterId, notices);
    }

    public async Task<ChangeNoticeDetailDto?> GetByIdAsync(int organizationId, int matterId, int changeNoticeId, CancellationToken ct = default)
    {
        var notice = await _dbContext.ChangeNotices
            .AsNoTracking()
            .Include(n => n.Recipients)
            .FirstOrDefaultAsync(n =>
                n.Id == changeNoticeId &&
                n.OrganizationId == organizationId &&
                n.MatterId == matterId &&
                !n.IsDeleted, ct);

        if (notice == null)
            return null;

        return new ChangeNoticeDetailDto
        {
            Id = notice.Id,
            Title = notice.Title,
            Description = notice.Description,
            Status = notice.Status,
            Priority = notice.Priority,
            ChangeType = notice.ChangeType,
            AcknowledgementDueDate = notice.AcknowledgementDueDate,
            CreatedAt = notice.CreatedAt,
            SentAt = notice.SentAt,
            SendCount = notice.SendCount,
            RecipientCount = notice.Recipients.Count,
            PendingRecipientCount = notice.Recipients.Count(r => r.Status == ChangeNoticeRecipientStatuses.Pending),
            AcknowledgedRecipientCount = notice.Recipients.Count(r => r.Status == ChangeNoticeRecipientStatuses.Acknowledged),
            NeedsClarificationRecipientCount = notice.Recipients.Count(r => r.Status == ChangeNoticeRecipientStatuses.NeedsClarification),
            Recipients = notice.Recipients.Select(r => new ChangeNoticeRecipientDto
            {
                Id = r.Id,
                Email = r.Email,
                UserId = r.UserId,
                Status = r.Status,
                RespondedAt = r.RespondedAt,
                ClarificationNote = r.ClarificationNote
            }).ToList()
        };
    }

    public async Task<ChangeControlSummaryDto> CreateDraftAsync(
        int organizationId,
        int matterId,
        int createdByUserId,
        CreateChangeNoticeRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title is required.", nameof(request));

        var matterExists = await _dbContext.Matters
            .AsNoTracking()
            .AnyAsync(m => m.Id == matterId && m.OrganizationId == organizationId && !m.IsDeleted, ct);

        if (!matterExists)
            throw new InvalidOperationException("Matter not found.");

        var emails = ParseEmails(request.RecipientEmails);
        if (emails.Count == 0)
            throw new ArgumentException("At least one recipient email is required.", nameof(request));

        var notice = new ChangeNotice
        {
            MatterId = matterId,
            OrganizationId = organizationId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ChangeType = string.IsNullOrWhiteSpace(request.ChangeType) ? "General" : request.ChangeType.Trim(),
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Medium" : request.Priority.Trim(),
            Status = ChangeNoticeStatuses.Draft,
            AcknowledgementDueDate = request.AcknowledgementDueDate,
            CreatedAt = DateTime.UtcNow,
            CreatedById = createdByUserId
        };

        foreach (var email in emails)
        {
            notice.Recipients.Add(new ChangeNoticeRecipient
            {
                Email = email,
                Status = ChangeNoticeRecipientStatuses.Pending,
                TokenVersion = 1,
                CreatedAt = DateTime.UtcNow
            });
        }

        _dbContext.ChangeNotices.Add(notice);
        await _dbContext.SaveChangesAsync(ct);

        return await GetSummaryAsync(organizationId, matterId, ct);
    }

    public async Task<ChangeControlSummaryDto> UpdateDraftAsync(
        int organizationId,
        int matterId,
        int changeNoticeId,
        int modifiedByUserId,
        CreateChangeNoticeRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title is required.", nameof(request));

        var notice = await _dbContext.ChangeNotices
            .Include(n => n.Recipients)
            .FirstOrDefaultAsync(n =>
                n.Id == changeNoticeId &&
                n.OrganizationId == organizationId &&
                n.MatterId == matterId &&
                !n.IsDeleted, ct);

        if (notice == null)
            throw new InvalidOperationException("Change notice not found.");

        // Allow updating both Draft and Sent notices (for Nudge/re-send with edits)
        // Only restrict if notice is in a terminal state that shouldn't be modified

        // Update basic fields
        notice.Title = request.Title.Trim();
        notice.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        notice.ChangeType = string.IsNullOrWhiteSpace(request.ChangeType) ? "General" : request.ChangeType.Trim();
        notice.Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Medium" : request.Priority.Trim();
        notice.AcknowledgementDueDate = request.AcknowledgementDueDate;
        notice.ModifiedAt = DateTime.UtcNow;
        notice.ModifiedById = modifiedByUserId;

        // Update recipients
        var newEmails = ParseEmails(request.RecipientEmails);
        var existingEmails = notice.Recipients.Select(r => r.Email.ToLowerInvariant()).ToHashSet();
        var newEmailsLower = newEmails.Select(e => e.ToLowerInvariant()).ToHashSet();

        // Remove recipients that are no longer in the list
        var toRemove = notice.Recipients.Where(r => !newEmailsLower.Contains(r.Email.ToLowerInvariant())).ToList();
        foreach (var r in toRemove)
        {
            notice.Recipients.Remove(r);
        }

        // Add new recipients
        foreach (var email in newEmails)
        {
            if (!existingEmails.Contains(email.ToLowerInvariant()))
            {
                notice.Recipients.Add(new ChangeNoticeRecipient
                {
                    Email = email,
                    Status = ChangeNoticeRecipientStatuses.Pending,
                    TokenVersion = 1,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        return await GetSummaryAsync(organizationId, matterId, ct);
    }

    public async Task DeleteAsync(int organizationId, int matterId, int changeNoticeId, CancellationToken ct = default)
    {
        var notice = await _dbContext.ChangeNotices
            .FirstOrDefaultAsync(n =>
                n.Id == changeNoticeId &&
                n.OrganizationId == organizationId &&
                n.MatterId == matterId &&
                !n.IsDeleted, ct);

        if (notice == null)
            throw new InvalidOperationException("Change notice not found.");

        if (notice.Status != ChangeNoticeStatuses.Draft)
            throw new InvalidOperationException("Only draft notices can be deleted.");

        notice.IsDeleted = true;
        notice.ModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<ChangeControlSummaryDto> SendAsync(
        int organizationId,
        int matterId,
        int changeNoticeId,
        string baseUrl,
        int sentByUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required.", nameof(baseUrl));

        var notice = await _dbContext.ChangeNotices
            .Include(n => n.Recipients)
            .FirstOrDefaultAsync(n =>
                n.Id == changeNoticeId &&
                n.OrganizationId == organizationId &&
                n.MatterId == matterId &&
                !n.IsDeleted, ct);

        if (notice == null)
            throw new InvalidOperationException("Change notice not found.");

        if (notice.Recipients.Count == 0)
            throw new InvalidOperationException("Change notice has no recipients.");

        // Increment token version to invalidate old links
        foreach (var r in notice.Recipients)
        {
            r.TokenVersion += 1;
        }

        var now = DateTime.UtcNow;
        notice.SentAt ??= now;
        notice.LastResentAt = now;
        notice.SendCount += 1;
        notice.ModifiedAt = now;
        notice.ModifiedById = sentByUserId;

        // Set to Sent if not already in an active response state
        if (notice.Status == ChangeNoticeStatuses.Draft)
            notice.Status = ChangeNoticeStatuses.Sent;

        await _dbContext.SaveChangesAsync(ct);

        var sender = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == sentByUserId)
            .Select(u => new
            {
                Name = (u.FirstName + " " + u.LastName).Trim(),
                Email = u.Email
            })
            .FirstOrDefaultAsync(ct);

        var fromNameOverride = string.IsNullOrWhiteSpace(sender?.Name) ? null : $"{sender.Name} in Notal";
        var replyToEmailOverride = string.IsNullOrWhiteSpace(sender?.Email) ? null : sender.Email;

        // Send emails after persistence so tokens match latest TokenVersion
        // Only send to recipients who haven't confirmed yet (Nudge should not re-notify confirmed users)
        var recipientsToNotify = notice.Recipients
            .Where(r => r.Status != ChangeNoticeRecipientStatuses.Acknowledged)
            .ToList();

        foreach (var recipient in recipientsToNotify)
        {
            var ackToken = ProtectToken(new TokenPayload
            {
                NoticeId = notice.Id,
                RecipientId = recipient.Id,
                Action = "ack",
                TokenVersion = recipient.TokenVersion,
                ExpiresAtUtc = now.AddDays(14)
            });

            var clarifyToken = ProtectToken(new TokenPayload
            {
                NoticeId = notice.Id,
                RecipientId = recipient.Id,
                Action = "clarify",
                TokenVersion = recipient.TokenVersion,
                ExpiresAtUtc = now.AddDays(14)
            });

            var ackUrl = $"{baseUrl.TrimEnd('/')}/public/change-notice/respond?t={Uri.EscapeDataString(ackToken)}";
            var clarifyUrl = $"{baseUrl.TrimEnd('/')}/public/change-notice/respond?t={Uri.EscapeDataString(clarifyToken)}";

            var subject = $"Action Required: {notice.Title}";
            var body = BuildEmailHtml(notice, ackUrl, clarifyUrl);

            var ok = await _emailSendingService.SendSystemEmailAsync(
                recipient.Email,
                subject,
                body,
                fromNameOverride,
                replyToEmailOverride,
                ct);
            if (!ok)
            {
                _logger.LogWarning("Failed to send Change Notice {NoticeId} to {Email}", notice.Id, recipient.Email);
            }
        }

        return await GetSummaryAsync(organizationId, matterId, ct);
    }

    public async Task<PublicChangeNoticeResponseResult> ProcessPublicResponseAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var payload = UnprotectToken(token);
            if (payload == null)
                return new PublicChangeNoticeResponseResult { Success = false, Message = "Invalid or expired link." };

            if (payload.ExpiresAtUtc < DateTime.UtcNow)
                return new PublicChangeNoticeResponseResult { Success = false, Message = "This link has expired." };

            var recipient = await _dbContext.ChangeNoticeRecipients
                .Include(r => r.ChangeNotice)
                .ThenInclude(n => n.Recipients)
                .FirstOrDefaultAsync(r => r.Id == payload.RecipientId && r.ChangeNoticeId == payload.NoticeId, ct);

            if (recipient?.ChangeNotice == null || recipient.ChangeNotice.IsDeleted)
                return new PublicChangeNoticeResponseResult { Success = false, Message = "This change notice is no longer available." };

            if (recipient.TokenVersion != payload.TokenVersion)
                return new PublicChangeNoticeResponseResult { Success = false, Message = "This link is no longer valid (a newer email was sent)." };

            var action = (payload.Action ?? "").Trim().ToLowerInvariant();
            var newRecipientStatus = action switch
            {
                "ack" => ChangeNoticeRecipientStatuses.Acknowledged,
                "clarify" => ChangeNoticeRecipientStatuses.NeedsClarification,
                _ => null
            };

            if (newRecipientStatus == null)
                return new PublicChangeNoticeResponseResult { Success = false, Message = "Unknown action." };

            recipient.Status = newRecipientStatus;
            recipient.RespondedAt = DateTime.UtcNow;

            var notice = recipient.ChangeNotice;
            notice.Status = ComputeNoticeStatus(notice);
            notice.ModifiedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            return new PublicChangeNoticeResponseResult
            {
                Success = true,
                Message = newRecipientStatus == ChangeNoticeRecipientStatuses.Acknowledged
                    ? "Acknowledged. Thank you!"
                    : "Marked as needing clarification. Thank you!",
                ChangeNoticeId = notice.Id,
                RecipientId = recipient.Id,
                RecipientEmail = recipient.Email,
                HasNotalAccount = recipient.UserId.HasValue,
                Action = action,
                NewRecipientStatus = recipient.Status,
                NewNoticeStatus = notice.Status
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing public Change Notice response");
            return new PublicChangeNoticeResponseResult { Success = false, Message = "We couldn't process your response. Please try again later." };
        }
    }

    public async Task<PublicChangeNoticeResponseResult> SavePublicClarificationNoteAsync(
        string token,
        string clarificationNote,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clarificationNote))
                return new PublicChangeNoticeResponseResult { Success = false, Message = "Please enter what you need clarified." };

            var payload = UnprotectToken(token);
            if (payload == null)
                return new PublicChangeNoticeResponseResult { Success = false, Message = "Invalid or expired link." };

            if (payload.ExpiresAtUtc < DateTime.UtcNow)
                return new PublicChangeNoticeResponseResult { Success = false, Message = "This link has expired." };

            var action = (payload.Action ?? "").Trim().ToLowerInvariant();
            if (action != "clarify")
                return new PublicChangeNoticeResponseResult { Success = false, Message = "This link is not for a clarification response." };

            var recipient = await _dbContext.ChangeNoticeRecipients
                .Include(r => r.ChangeNotice)
                .ThenInclude(n => n.Recipients)
                .FirstOrDefaultAsync(r => r.Id == payload.RecipientId && r.ChangeNoticeId == payload.NoticeId, ct);

            if (recipient?.ChangeNotice == null || recipient.ChangeNotice.IsDeleted)
                return new PublicChangeNoticeResponseResult { Success = false, Message = "This change notice is no longer available." };

            if (recipient.TokenVersion != payload.TokenVersion)
                return new PublicChangeNoticeResponseResult { Success = false, Message = "This link is no longer valid (a newer email was sent)." };

            recipient.Status = ChangeNoticeRecipientStatuses.NeedsClarification;
            recipient.RespondedAt = DateTime.UtcNow;
            recipient.ClarificationNote = clarificationNote.Trim();

            var notice = recipient.ChangeNotice;
            notice.Status = ComputeNoticeStatus(notice);
            notice.ModifiedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            return new PublicChangeNoticeResponseResult
            {
                Success = true,
                Message = "Thanks — your clarification request was sent.",
                ChangeNoticeId = notice.Id,
                RecipientId = recipient.Id,
                RecipientEmail = recipient.Email,
                HasNotalAccount = recipient.UserId.HasValue,
                Action = action,
                NewRecipientStatus = recipient.Status,
                NewNoticeStatus = notice.Status,
                ClarificationNoteSaved = true,
                ClarificationNoteMessage = "Sent."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving public Change Notice clarification note");
            return new PublicChangeNoticeResponseResult
            {
                Success = false,
                Message = "We couldn't save your clarification note. Please try again later.",
                ClarificationNoteSaved = false,
                ClarificationNoteMessage = "Not sent — please try again."
            };
        }
    }

    private static ChangeControlSummaryDto MapSummary(int organizationId, int matterId, List<ChangeNotice> notices)
    {
        var dto = new ChangeControlSummaryDto
        {
            OrganizationId = organizationId,
            MatterId = matterId,
            DraftCount = notices.Count(n => n.Status == ChangeNoticeStatuses.Draft),
            PendingCount = notices.Count(n => n.Status == ChangeNoticeStatuses.Sent || n.Status == ChangeNoticeStatuses.PartiallyAcknowledged),
            NeedsClarificationCount = notices.Count(n => n.Status == ChangeNoticeStatuses.NeedsClarification),
            AcknowledgedCount = notices.Count(n => n.Status == ChangeNoticeStatuses.Acknowledged),
            Notices = notices.Select(n => new ChangeNoticeItemDto
            {
                Id = n.Id,
                Title = n.Title,
                Description = n.Description,
                Status = n.Status,
                Priority = n.Priority,
                ChangeType = n.ChangeType,
                CreatedAt = n.CreatedAt,
                SentAt = n.SentAt,
                RecipientCount = n.Recipients.Count,
                PendingRecipientCount = n.Recipients.Count(r => r.Status == ChangeNoticeRecipientStatuses.Pending),
                AcknowledgedRecipientCount = n.Recipients.Count(r => r.Status == ChangeNoticeRecipientStatuses.Acknowledged),
                NeedsClarificationRecipientCount = n.Recipients.Count(r => r.Status == ChangeNoticeRecipientStatuses.NeedsClarification)
            }).ToList()
        };

        return dto;
    }

    private static string ComputeNoticeStatus(ChangeNotice notice)
    {
        if (notice.Recipients.Count == 0)
            return notice.Status;

        if (notice.Recipients.Any(r => r.Status == ChangeNoticeRecipientStatuses.NeedsClarification))
            return ChangeNoticeStatuses.NeedsClarification;

        if (notice.Recipients.All(r => r.Status == ChangeNoticeRecipientStatuses.Acknowledged))
            return ChangeNoticeStatuses.Acknowledged;

        if (notice.Recipients.Any(r => r.Status == ChangeNoticeRecipientStatuses.Acknowledged))
            return ChangeNoticeStatuses.PartiallyAcknowledged;

        return notice.SentAt.HasValue ? ChangeNoticeStatuses.Sent : ChangeNoticeStatuses.Draft;
    }

    private static HashSet<string> ParseEmails(string raw)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in SplitEmailsRegex.Split(raw ?? string.Empty))
        {
            var email = part.Trim();
            if (string.IsNullOrWhiteSpace(email))
                continue;
            // Minimal email sanity check
            if (!email.Contains('@') || email.Length > 320)
                continue;
            result.Add(email);
        }
        return result;
    }

    private sealed class TokenPayload
    {
        public int NoticeId { get; init; }
        public int RecipientId { get; init; }
        public string Action { get; init; } = "";
        public int TokenVersion { get; init; }
        public DateTime ExpiresAtUtc { get; init; }
    }

    private string ProtectToken(TokenPayload payload)
    {
        var protector = _dataProtectionProvider.CreateProtector("ChangeNotice.PublicResponse.v1");
        var json = JsonSerializer.Serialize(payload);
        return protector.Protect(json);
    }

    private TokenPayload? UnprotectToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var protector = _dataProtectionProvider.CreateProtector("ChangeNotice.PublicResponse.v1");
        try
        {
            var json = protector.Unprotect(token);
            return JsonSerializer.Deserialize<TokenPayload>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildEmailHtml(ChangeNotice notice, string ackUrl, string clarifyUrl)
    {
        var title = System.Net.WebUtility.HtmlEncode(notice.Title);
        var description = System.Net.WebUtility.HtmlEncode(notice.Description ?? "");
        var due = notice.AcknowledgementDueDate.HasValue
            ? $"<p style=\"margin: 0 0 12px 0; color: #555;\">Acknowledgement due: <strong>{notice.AcknowledgementDueDate.Value:MMM dd, yyyy}</strong></p>"
            : "";

        return $@"
<html>
<body style=""font-family: -apple-system, Segoe UI, Roboto, Arial, sans-serif; line-height: 1.4;"">
  <h2 style=""margin: 0 0 8px 0;"">Change Notice</h2>
  <p style=""margin: 0 0 12px 0; color: #111;""><strong>{title}</strong></p>
  {(string.IsNullOrWhiteSpace(description) ? "" : $"<p style=\"margin: 0 0 12px 0; color: #333;\">{description}</p>")}
  {due}
  <div style=""margin-top: 18px;"">
    <a href=""{ackUrl}"" style=""display: inline-block; padding: 10px 14px; background: #0d6efd; color: #fff; text-decoration: none; font-weight: 600; border-radius: 6px; margin-right: 10px;"">Confirm</a>
    <a href=""{clarifyUrl}"" style=""display: inline-block; padding: 10px 14px; background: #6c757d; color: #fff; text-decoration: none; font-weight: 600; border-radius: 6px;"">Needs Clarification</a>
  </div>
  <p style=""margin-top: 18px; color: #777; font-size: 12px;"">
    This link is unique to you. If you received this in error, you can ignore it.
  </p>
</body>
</html>";
    }
}


