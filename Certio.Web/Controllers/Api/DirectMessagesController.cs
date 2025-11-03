using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;
using Certio.Web.Security;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Domain.Services;
using Certio.Domain.Exceptions;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Certio.Web.Controllers.Api;

[ApiController]
[Route("api/dm")]
[Authorize(Policy = "OrgMember")]
public class DirectMessagesController : ControllerBase
{
    private readonly IDirectMessageService _directMessageService;
    private readonly IEmailSendingService _emailSendingService;
    private readonly IEmailService _emailService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DirectMessagesController> _logger;

    public DirectMessagesController(
        IDirectMessageService directMessageService,
        IEmailSendingService emailSendingService,
        IEmailService emailService,
        ApplicationDbContext context,
        ILogger<DirectMessagesController> logger)
    {
        _directMessageService = directMessageService;
        _emailSendingService = emailSendingService;
        _emailService = emailService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Create or get existing thread with another user
    /// </summary>
    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequest request, [FromQuery] int orgId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var thread = await _directMessageService.GetOrCreateThreadAsync(orgId, currentUserId, request.OtherUserId);

            return Ok(new { success = true, thread });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to create thread in org {OrgId}", orgId);
            return Unauthorized(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating thread in org {OrgId}", orgId);
            return StatusCode(500, new { success = false, error = "Failed to create thread" });
        }
    }

    /// <summary>
    /// List all direct message threads for current user
    /// </summary>
    [HttpGet("threads")]
    public async Task<IActionResult> ListThreads([FromQuery] int orgId, [FromQuery] int take = 30, [FromQuery] string? cursor = null)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var threads = await _directMessageService.ListThreadsAsync(orgId, currentUserId, take, cursor);

            return Ok(new { success = true, threads });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to list threads in org {OrgId}", orgId);
            return Unauthorized(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing threads in org {OrgId}", orgId);
            return StatusCode(500, new { success = false, error = "Failed to list threads" });
        }
    }

    /// <summary>
    /// Get messages from a thread
    /// </summary>
    [HttpGet("threads/{threadId}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid threadId,
        [FromQuery] int orgId,
        [FromQuery] int take = 50,
        [FromQuery] string? cursor = null)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var result = await _directMessageService.GetMessagesAsync(orgId, currentUserId, threadId, take, cursor);

            // Messages are already enriched with sender color and external contact status by the service layer
            var enrichedMessages = result.Items.Select(msg => new
            {
                msg.Id,
                msg.ThreadId,
                msg.SenderId,
                msg.SenderName,
                msg.Body,
                msg.MessageType,
                msg.CreatedAt,
                msg.EditedAt,
                msg.IsDeleted,
                senderColor = msg.SenderColor ?? "#3d1019",
                isExternalContacts = msg.IsExternalContacts ?? false
            }).ToList();

            return Ok(new
            {
                success = true,
                messages = enrichedMessages,
                nextCursor = result.NextCursor,
                hasMore = result.HasMore
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to read messages in thread {ThreadId}", threadId);
            return Unauthorized(new { success = false, error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Thread {ThreadId} not found", threadId);
            return NotFound(new { success = false, error = "Thread not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages from thread {ThreadId}", threadId);
            return StatusCode(500, new { success = false, error = "Failed to get messages" });
        }
    }

    /// <summary>
    /// Mark a thread as read
    /// </summary>
    [HttpPost("threads/{threadId}/read")]
    public async Task<IActionResult> MarkRead(Guid threadId, [FromBody] MarkReadRequest request, [FromQuery] int orgId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            await _directMessageService.MarkReadAsync(orgId, currentUserId, threadId, request.ReadAt);

            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to mark thread {ThreadId} as read", threadId);
            return Unauthorized(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking thread {ThreadId} as read", threadId);
            return StatusCode(500, new { success = false, error = "Failed to mark as read" });
        }
    }

    /// <summary>
    /// Notalize an email - Create a DM thread with the sender and add the email content as a message
    /// </summary>
    [HttpPost("notalize")]
    public async Task<IActionResult> NotalizeEmail([FromBody] NotalizeRequest request, [FromQuery] int orgId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            
            // Validate request
            if (string.IsNullOrWhiteSpace(request.SenderEmail))
            {
                return BadRequest(new { success = false, error = "Sender email is required" });
            }
            
            var normalizedSenderEmail = NormalizeEmail(request.SenderEmail);
            if (string.IsNullOrEmpty(normalizedSenderEmail))
            {
                return BadRequest(new { success = false, error = "Sender email is invalid" });
            }

            var trimmedSenderEmail = request.SenderEmail.Trim();
            
            // Parse email body to plain text
            // Always strip HTML from both EmailBody and EmailBodyText to ensure clean text
            var emailBodyText = "";
            
            // If EmailBodyText is provided and looks like plain text (doesn't contain HTML tags), use it
            if (!string.IsNullOrEmpty(request.EmailBodyText) && 
                !request.EmailBodyText.Contains('<') && 
                !request.EmailBodyText.Contains('>'))
            {
                emailBodyText = request.EmailBodyText;
            }
            // Otherwise, extract plain text from HTML body
            else if (!string.IsNullOrEmpty(request.EmailBody))
            {
                emailBodyText = StripHtmlToPlainText(request.EmailBody);
            }
            // Fallback: try to strip HTML from EmailBodyText if it exists
            else if (!string.IsNullOrEmpty(request.EmailBodyText))
            {
                emailBodyText = StripHtmlToPlainText(request.EmailBodyText);
            }
            
            // If we still don't have text, return empty string
            if (string.IsNullOrWhiteSpace(emailBodyText))
            {
                emailBodyText = "[No email content]";
            }
            
            // Prepend subject to the message body as the first line
            var subject = request.Subject ?? "";
            var messageBody = string.IsNullOrWhiteSpace(subject) 
                ? emailBodyText 
                : $"{subject}\n\n{emailBodyText}";
            
            // Find or create user by email
            var senderUser = await FindOrCreateUserByEmailAsync(trimmedSenderEmail, request.SenderName, orgId);
            var isExternalUser = senderUser.IsExternal;
            
            // Reload user from database to ensure organization membership is loaded
            // This ensures GetOrCreateThreadAsync can see the user's organization membership
            var userEntity = await _context.Users
                .Include(u => u.UserOrganizations)
                .FirstOrDefaultAsync(u => u.Id == senderUser.Id);
            
            if (userEntity == null)
            {
                return BadRequest(new { success = false, error = "User not found after creation" });
            }
            
            // Get or create thread - check if it already existed
            // Normalize user pair (same as DirectMessageService does)
            var userAId = Math.Min(currentUserId, senderUser.Id);
            var userBId = Math.Max(currentUserId, senderUser.Id);
            
            var existingThreadBefore = await _context.DirectThreads
                .Where(dt => dt.UserAId == userAId && 
                            dt.UserBId == userBId && 
                            !dt.IsDeleted)
                .FirstOrDefaultAsync();
            
            var thread = await _directMessageService.GetOrCreateThreadAsync(orgId, currentUserId, senderUser.Id);
            var isNewThread = existingThreadBefore == null; // Thread didn't exist before
            
            // Create email metadata
            var emailMetadata = new Dictionary<string, object>
            {
                ["EmailProvider"] = request.Provider ?? "Unknown",
                ["EmailSubject"] = request.Subject ?? "",
                ["EmailId"] = request.ExternalEmailId ?? "",
                ["EmailThreadId"] = request.EmailThreadId ?? "",
                ["FromEmail"] = trimmedSenderEmail,
                ["FromName"] = request.SenderName ?? "",
                ["ReceivedAt"] = request.ReceivedAt?.ToString("O") ?? DateTime.UtcNow.ToString("O"),
                ["Notalized"] = true
            };
            
            // Create message from sender (the email sender)
            // Note: We can't use SendAsync because it requires the sender to be currentUserId
            // Instead, we'll create the message directly
            var message = new DirectMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                SenderId = senderUser.Id, // Send as the email sender, not current user
                Body = messageBody,
                MessageType = "Email",
                Metadata = JsonSerializer.Serialize(emailMetadata),
                CreatedAt = DateTime.UtcNow
            };
            
            _context.DirectMessages.Add(message);
            
            // Update thread's last message time
            var threadEntity = await _context.DirectThreads.FindAsync(thread.Id);
            if (threadEntity != null)
            {
                threadEntity.LastMessageAt = message.CreatedAt;
            }
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Notalized email from {SenderEmail} to thread {ThreadId} by user {UserId}", 
                request.SenderEmail, thread.Id, currentUserId);
            
            return Ok(new { 
                success = true, 
                thread = new {
                    id = thread.Id,
                    otherUserId = senderUser.Id,
                    otherUserName = senderUser.Name ?? request.SenderEmail,
                    otherUserEmail = trimmedSenderEmail,
                    isExternalUser = isExternalUser,
                    isNewThread = isNewThread, // Indicates if thread was just created
                    otherUserColor = userEntity.Color ?? "#aaaaaa" // Return user's color for avatar styling
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to notalize email from {SenderEmail} in org {OrgId}", request.SenderEmail, orgId);
            return Unauthorized(new { success = false, error = ex.Message });
        }
        catch (UnauthorizedOperationException ex)
        {
            _logger.LogWarning(ex, "Unauthorized operation when notalizing email from {SenderEmail}: {Message}", request.SenderEmail, ex.Message);
            return BadRequest(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notalizing email from {SenderEmail}", request.SenderEmail);
            return StatusCode(500, new { success = false, error = $"Failed to notalize email: {ex.Message}" });
        }
    }
    
    private async Task<int> GetOrCreateExternalGuestsOrganizationIdAsync(int currentOrgId)
    {
        var currentUserId = GetCurrentUserId();
        
        // Get the current user's name for the organization name
        var currentUser = await _context.Users.FindAsync(currentUserId);
        var currentUserName = $"{currentUser?.FirstName} {currentUser?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(currentUserName))
        {
            currentUserName = "My"; // Fallback if name is not set
        }
        
        var desiredOrgName = $"{currentUserName}'s External Contacts";

        // First, try to find organization with the dynamic name
        var externalOrg = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Name == desiredOrgName);

        if (externalOrg == null)
        {
            // If not found by dynamic name, check for the old "External Guests" name
            // and update it if it exists and is owned by the current user
            var oldExternalOrg = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Name == "External Guests" && o.OwnerId == currentUserId);

            if (oldExternalOrg != null)
            {
                oldExternalOrg.Name = desiredOrgName;
                _context.Organizations.Update(oldExternalOrg);
                await _context.SaveChangesAsync();
                externalOrg = oldExternalOrg;
            }
            else
            {
                // Create new organization with the dynamic name
                externalOrg = new Organization
                {
                    Name = desiredOrgName,
                    Description = "Dedicated organization for external users created via Notalize feature.",
                    OwnerId = currentUserId,
                    Type = OrganizationType.Client,
                    IsPersonal = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Organizations.Add(externalOrg);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created new '{OrgName}' organization with ID {OrgId}", desiredOrgName, externalOrg.Id);
            }
        }
        
        // Ensure relationship exists between current organization and External Contacts
        var existingRelationship = await _context.OrganizationRelationships
            .FirstOrDefaultAsync(or => or.SourceOrganizationId == currentOrgId &&
                                       or.TargetOrganizationId == externalOrg.Id &&
                                       or.RelationshipType == RelationshipTypes.LawFirmClient);
        
        if (existingRelationship == null)
        {
            var relationship = new OrganizationRelationship
            {
                SourceOrganizationId = currentOrgId,
                TargetOrganizationId = externalOrg.Id,
                RelationshipType = RelationshipTypes.LawFirmClient,
                AccessLevel = AccessLevels.FullAccess,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedById = currentUserId
            };
            _context.OrganizationRelationships.Add(relationship);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created OrganizationRelationship between org {CurrentOrgId} and External Contacts org {ExternalOrgId}", 
                currentOrgId, externalOrg.Id);
        }
        
        return externalOrg.Id;
    }

    private async Task<(int Id, string? Name, bool IsExternal)> FindOrCreateUserByEmailAsync(string email, string? name, int currentOrgId)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrEmpty(normalizedEmail))
        {
            throw new ArgumentException("Email is required", nameof(email));
        }

        var fallbackEmail = string.IsNullOrWhiteSpace(email) ? normalizedEmail : email.Trim();

        var externalGuestsOrgId = await GetOrCreateExternalGuestsOrganizationIdAsync(currentOrgId);

        // First, try to find existing user in the current organization (skip external guests org)
        var existingUserInCurrentOrg = await _context.Users
            .Include(u => u.UserOrganizations)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail &&
                u.UserOrganizations.Any(uo => uo.OrganizationId == currentOrgId && 
                                              uo.OrganizationId != externalGuestsOrgId && 
                                              uo.IsActive));
        
        if (existingUserInCurrentOrg != null)
        {
            var displayName = BuildDisplayName(existingUserInCurrentOrg, name, fallbackEmail);
            var hasExternalMembership = existingUserInCurrentOrg.UserOrganizations
                .Any(uo => uo.OrganizationId == externalGuestsOrgId && uo.IsActive && uo.UserType == UserTypes.External);

            return (existingUserInCurrentOrg.Id, displayName, hasExternalMembership);
        }
        
        // Try to find user anywhere in the system
        var userAnywhere = await _context.Users
            .Include(u => u.UserOrganizations)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        
        if (userAnywhere != null)
        {
            var hasUserChanges = false;

            if (!string.Equals(userAnywhere.Email, normalizedEmail, StringComparison.Ordinal))
            {
                userAnywhere.Email = normalizedEmail;
                hasUserChanges = true;
            }

            if (string.IsNullOrEmpty(userAnywhere.Color) || userAnywhere.Color == "#007bff" || userAnywhere.Color == "#3d1019")
            {
                userAnywhere.Color = "#aaaaaa"; // Set to gray for external contacts
                hasUserChanges = true;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                var (potentialFirst, potentialLast) = ExtractNameParts(name, fallbackEmail);

                if (string.IsNullOrWhiteSpace(userAnywhere.FirstName) ||
                    userAnywhere.FirstName.Equals(userAnywhere.Email, StringComparison.OrdinalIgnoreCase))
                {
                    userAnywhere.FirstName = potentialFirst;
                    hasUserChanges = true;
                }

                if (string.IsNullOrWhiteSpace(userAnywhere.LastName))
                {
                    userAnywhere.LastName = potentialLast;
                    hasUserChanges = true;
                }
            }
            
            var userOrgInExternalGuests = userAnywhere.UserOrganizations
                .FirstOrDefault(uo => uo.OrganizationId == externalGuestsOrgId);

            var membershipUpdated = false;
            var externalMembershipActive = false;
            
            if (userOrgInExternalGuests == null)
            {
                userOrgInExternalGuests = new UserOrganization
                {
                    UserId = userAnywhere.Id,
                    OrganizationId = externalGuestsOrgId,
                    UserType = UserTypes.External,
                    Role = OrganizationRoles.Guest,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                };
                _context.UserOrganizations.Add(userOrgInExternalGuests);
                userAnywhere.UserOrganizations.Add(userOrgInExternalGuests);
                membershipUpdated = true;
                externalMembershipActive = true;
                _logger.LogInformation("Added existing user {UserId} ({Email}) to 'External Guests' organization {OrgId}",
                    userAnywhere.Id, normalizedEmail, externalGuestsOrgId);
            }
            else
            {
                externalMembershipActive = userOrgInExternalGuests.IsActive &&
                    userOrgInExternalGuests.UserType == UserTypes.External &&
                    userOrgInExternalGuests.Role == OrganizationRoles.Guest;

                if (!externalMembershipActive)
                {
                    userOrgInExternalGuests.IsActive = true;
                    if (userOrgInExternalGuests.JoinedAt == default)
                    {
                        userOrgInExternalGuests.JoinedAt = DateTime.UtcNow;
                    }
                    userOrgInExternalGuests.UserType = UserTypes.External;
                    userOrgInExternalGuests.Role = OrganizationRoles.Guest;
                    _context.UserOrganizations.Update(userOrgInExternalGuests);
                    membershipUpdated = true;
                    externalMembershipActive = true;
                    _logger.LogInformation("Reactivated external membership for user {UserId} in organization {OrgId}",
                        userAnywhere.Id, externalGuestsOrgId);
                }
            }

            if (hasUserChanges)
            {
                _context.Users.Update(userAnywhere);
            }

            if (hasUserChanges || membershipUpdated)
            {
                await _context.SaveChangesAsync();
            }
            
            var displayName = BuildDisplayName(userAnywhere, name, fallbackEmail);
            return (userAnywhere.Id, displayName, externalMembershipActive);
        }
        
        // Create new external user
        var (firstName, lastName) = ExtractNameParts(name, fallbackEmail);
        
        var newUser = new User
        {
            Email = normalizedEmail,
            FirstName = firstName,
            LastName = lastName,
            Color = "#aaaaaa", // Gray background for external contacts
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        _context.Users.Add(newUser);
        
        // Save the user first to get the ID
        await _context.SaveChangesAsync();
        
        // Now add to External Guests organization as guest (not current organization)
        var newUserOrg = new UserOrganization
        {
            UserId = newUser.Id,
            OrganizationId = externalGuestsOrgId,
            UserType = UserTypes.External,
            Role = OrganizationRoles.Guest,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };
        
        _context.UserOrganizations.Add(newUserOrg);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created external user {UserId} ({Email}) and added to 'External Guests' organization {OrgId}", 
            newUser.Id, normalizedEmail, externalGuestsOrgId);
        
        var displayNameForNewUser = BuildDisplayName(newUser, name, fallbackEmail);
        return (newUser.Id, displayNameForNewUser, true);
    }

    private static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }

    private static (string FirstName, string LastName) ExtractNameParts(string? fullName, string fallbackEmail)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                return (parts[0], string.Empty);
            }

            if (parts.Length > 1)
            {
                var lastName = string.Join(" ", parts, 1, parts.Length - 1);
                return (parts[0], lastName);
            }
        }

        var localPart = fallbackEmail;
        var atIndex = fallbackEmail.IndexOf('@');
        if (atIndex > 0)
        {
            localPart = fallbackEmail.Substring(0, atIndex);
        }

        return (localPart, string.Empty);
    }

    private static string BuildDisplayName(User user, string? providedName, string fallbackEmail)
    {
        var fromUser = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(fromUser))
        {
            return fromUser;
        }

        if (!string.IsNullOrWhiteSpace(providedName))
        {
            return providedName.Trim();
        }

        return fallbackEmail;
    }
    
    private string StripHtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";

        // Remove script and style elements completely
        html = Regex.Replace(html, @"<(script|style|noscript|iframe|embed|object)[^>]*>.*?</\1>", "", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Replace HTML line breaks and block elements with newlines
        // Handle <br> tags first (including self-closing variants)
        html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        
        // Replace closing tags for block elements with newlines
        html = Regex.Replace(html, @"</(p|div|h[1-6]|li|tr|td|th|blockquote|pre|table|tbody|thead|tfoot|section|article|header|footer|nav|aside)[^>]*>", "\n", 
            RegexOptions.IgnoreCase);
        
        // Replace opening tags for block elements (but preserve spacing)
        html = Regex.Replace(html, @"<(p|div|h[1-6]|li|tr|td|th|blockquote|pre|table|tbody|thead|tfoot|section|article|header|footer|nav|aside)[^>]*>", "\n", 
            RegexOptions.IgnoreCase);
        
        // Replace list items with proper formatting
        html = Regex.Replace(html, @"</(ul|ol)[^>]*>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(ul|ol)[^>]*>", "\n", RegexOptions.IgnoreCase);
        
        // Extract alt text from images before removing them
        html = Regex.Replace(html, @"<img[^>]*alt\s*=\s*[""']([^""']*)[""'][^>]*>", "$1", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<img[^>]*alt\s*=\s*([^\s>]+)[^>]*>", "$1", RegexOptions.IgnoreCase);
        // Remove any remaining img tags
        html = Regex.Replace(html, @"<img[^>]*>", "", RegexOptions.IgnoreCase);
        
        // Replace anchor tags - extract text content (simplified to handle nested tags)
        // This will extract text between <a> and </a> tags, handling simple cases
        html = Regex.Replace(html, @"<a[^>]*>([^<]*)</a>", "$1", RegexOptions.IgnoreCase);
        // Remove any remaining anchor tags (for cases with nested tags)
        html = Regex.Replace(html, @"<a[^>]*>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</a>", "", RegexOptions.IgnoreCase);

        // Replace HTML entities before removing tags
        html = html.Replace("&nbsp;", " ")
                  .Replace("&amp;", "&")
                  .Replace("&lt;", "<")
                  .Replace("&gt;", ">")
                  .Replace("&quot;", "\"")
                  .Replace("&#39;", "'")
                  .Replace("&apos;", "'");

        // Remove remaining HTML tags (including any we missed)
        html = Regex.Replace(html, "<[^>]+>", "");

        // Decode any remaining HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);

        // Normalize whitespace while preserving newlines
        // Replace multiple spaces/tabs with single space (but preserve newlines)
        html = Regex.Replace(html, @"[ \t]+", " ");
        
        // Replace multiple consecutive newlines with max 2 newlines (for paragraph breaks)
        html = Regex.Replace(html, @"\n{3,}", "\n\n");
        
        // Clean up spaces at start/end of lines (but preserve intentional spacing)
        html = Regex.Replace(html, @"[ \t]+\n", "\n");
        html = Regex.Replace(html, @"\n[ \t]+", "\n");
        
        // Remove leading/trailing whitespace from each line but preserve empty lines
        var lines = html.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }
        html = string.Join("\n", lines);
        
        return html.Trim();
    }

    /// <summary>
    /// Send Direct Message as email
    /// </summary>
    [HttpPost("threads/{threadId}/send-email")]
    public async Task<IActionResult> SendEmail(Guid threadId, [FromBody] SendEmailRequest request, [FromQuery] int orgId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            
            // Verify user is participant in thread
            var isParticipant = await _directMessageService.IsParticipantAsync(orgId, currentUserId, threadId);
            if (!isParticipant)
            {
                return Unauthorized(new { success = false, error = "Not a participant in this thread" });
            }

            // Get user's email account
            var emailAccount = await _emailService.GetEmailAccountAsync(currentUserId);
            if (emailAccount == null)
            {
                return BadRequest(new { success = false, error = "No email account connected" });
            }

            // Find the direct message to send
            var messages = await _directMessageService.GetMessagesAsync(orgId, currentUserId, threadId, 1);
            var messageToSend = messages.Items.FirstOrDefault(m => m.Id == request.MessageId);
            
            if (messageToSend == null)
            {
                return NotFound(new { success = false, error = "Message not found" });
            }

            // Send email
            var emailMessage = await _emailSendingService.SendEmailFromDirectMessageAsync(
                request.MessageId, 
                emailAccount.Id);

            return Ok(new { success = true, emailMessage = new {
                id = emailMessage.Id,
                externalEmailId = emailMessage.ExternalEmailId,
                subject = emailMessage.Subject
            }});
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to send email from thread {ThreadId}", threadId);
            return Unauthorized(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email from thread {ThreadId}", threadId);
            return StatusCode(500, new { success = false, error = "Failed to send email" });
        }
    }

    private int GetCurrentUserId()
    {
        // First try to get the custom user ID from context (set by UserSyncMiddleware)
        if (HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int userId)
        {
            return userId;
        }
        
        // Fallback to claims (for backwards compatibility)
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var claimUserId))
        {
            return claimUserId;
        }
        
        throw new UnauthorizedAccessException("User ID not found in context or claims");
    }
}

public class SendEmailRequest
{
    public Guid MessageId { get; set; }
}

public class NotalizeRequest
{
    public string SenderEmail { get; set; } = "";
    public string? SenderName { get; set; }
    public string? EmailBody { get; set; }
    public string? EmailBodyText { get; set; }
    public string? Subject { get; set; }
    public string? Provider { get; set; }
    public string? ExternalEmailId { get; set; }
    public string? EmailThreadId { get; set; }
    public DateTime? ReceivedAt { get; set; }
}

