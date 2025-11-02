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
            
            // Parse email body to plain text
            var emailBodyText = StripHtmlToPlainText(request.EmailBody ?? request.EmailBodyText ?? "");
            
            // Find or create user by email
            var senderUser = await FindOrCreateUserByEmailAsync(request.SenderEmail, request.SenderName, orgId);
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
                ["FromEmail"] = request.SenderEmail,
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
                Body = emailBodyText,
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
                    otherUserEmail = request.SenderEmail,
                    isExternalUser = isExternalUser,
                    isNewThread = isNewThread, // Indicates if thread was just created
                    otherUserColor = userEntity.Color ?? "#9ca3af" // Return user's color for avatar styling
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
        var externalGuestsOrgId = await GetOrCreateExternalGuestsOrganizationIdAsync(currentOrgId);

        // First, try to find existing user in the current organization (skip external guests org)
        var existingUserInCurrentOrg = await _context.Users
            .Include(u => u.UserOrganizations)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && 
                u.UserOrganizations.Any(uo => uo.OrganizationId == currentOrgId && 
                                              uo.OrganizationId != externalGuestsOrgId && 
                                              uo.IsActive));
        
        if (existingUserInCurrentOrg != null)
        {
            return (existingUserInCurrentOrg.Id, $"{existingUserInCurrentOrg.FirstName} {existingUserInCurrentOrg.LastName}".Trim(), false);
        }
        
        // Try to find user anywhere in the system
        var userAnywhere = await _context.Users
            .Include(u => u.UserOrganizations)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        
        if (userAnywhere != null)
        {
            // User exists - ensure they are part of the External Guests organization
            // Also check if user's Color should be set to gray if not already set
            if (string.IsNullOrEmpty(userAnywhere.Color) || userAnywhere.Color == "#007bff" || userAnywhere.Color == "#3d1019")
            {
                userAnywhere.Color = "#9ca3af"; // Set to gray for external contacts
                _context.Users.Update(userAnywhere);
                await _context.SaveChangesAsync();
            }
            
            var userOrgInExternalGuests = await _context.UserOrganizations
                .FirstOrDefaultAsync(uo => uo.UserId == userAnywhere.Id && uo.OrganizationId == externalGuestsOrgId);
            
            if (userOrgInExternalGuests == null)
            {
                userOrgInExternalGuests = new UserOrganization
                {
                    UserId = userAnywhere.Id,
                    OrganizationId = externalGuestsOrgId, // Add to External Guests org, not current org
                    UserType = UserTypes.External,
                    Role = OrganizationRoles.Guest,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                };
                _context.UserOrganizations.Add(userOrgInExternalGuests);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Added existing user {UserId} ({Email}) to 'External Guests' organization {OrgId}", 
                    userAnywhere.Id, email, externalGuestsOrgId);
            }
            
            return (userAnywhere.Id, $"{userAnywhere.FirstName} {userAnywhere.LastName}".Trim(), true);
        }
        
        // Create new external user
        var nameParts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 0 ? nameParts[0] : email.Split('@')[0];
        var lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";
        
        var newUser = new User
        {
            Email = email.ToLower(),
            FirstName = firstName,
            LastName = lastName,
            Color = "#9ca3af", // Gray background for external contacts
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
            OrganizationId = externalGuestsOrgId, // Add to External Guests org, not current org
            UserType = UserTypes.External,
            Role = OrganizationRoles.Guest,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };
        
        _context.UserOrganizations.Add(newUserOrg);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created external user {UserId} ({Email}) and added to 'External Guests' organization {OrgId}", 
            newUser.Id, email, externalGuestsOrgId);
        
        return (newUser.Id, name ?? email, true);
    }
    
    private string StripHtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";

        // Remove script and style elements
        html = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", "", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Replace HTML entities
        html = html.Replace("&nbsp;", " ")
                  .Replace("&amp;", "&")
                  .Replace("&lt;", "<")
                  .Replace("&gt;", ">")
                  .Replace("&quot;", "\"")
                  .Replace("&#39;", "'");

        // Remove HTML tags
        html = Regex.Replace(html, "<[^>]+>", "");

        // Decode HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);

        // Normalize whitespace
        html = Regex.Replace(html, @"\s+", " ");
        
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

