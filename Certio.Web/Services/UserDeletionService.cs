using Microsoft.EntityFrameworkCore;
using Certio.Domain.Users;
using Certio.Domain.Audit;
using Certio.Web.Data;
using Certio.Web.Configuration;
using Microsoft.Extensions.Options;

namespace Certio.Web.Services
{
    public interface IUserDeletionService
    {
        Task<bool> ProcessUserDeletionRequestAsync(UserDeletionRequest request, int processedById);
        Task<bool> DeactivateUserAsync(int userId, int deletedById, string reason = "Account deactivation");
        Task<bool> CompleteDataDeletionAsync(int userId, int deletedById, string reason = "Complete data deletion");
    }

    public class UserDeletionService : IUserDeletionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserDeletionService> _logger;
        private readonly AnonymizationSettings _anonymizationSettings;

        public UserDeletionService(ApplicationDbContext context, ILogger<UserDeletionService> logger, IOptions<AnonymizationSettings> anonymizationSettings)
        {
            _context = context;
            _logger = logger;
            _anonymizationSettings = anonymizationSettings.Value;
        }

        public async Task<bool> ProcessUserDeletionRequestAsync(UserDeletionRequest request, int processedById)
        {
            _logger.LogInformation("Processing {DeletionType} request for user {UserId} by {ProcessedById}", 
                request.DeletionType, request.UserId, processedById);
                
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                bool result = request.DeletionType switch
                {
                    UserDeletionType.Deactivate => await DeactivateUserAsync(request.UserId, processedById, request.Reason),
                    UserDeletionType.CompleteDeletion => await CompleteDataDeletionAsync(request.UserId, processedById, request.Reason),
                    _ => throw new ArgumentException($"Unknown deletion type: {request.DeletionType}")
                };

                if (result)
                {
                    request.IsProcessed = true;
                    request.ProcessedAt = DateTime.UtcNow;
                    request.ProcessedById = processedById;
                    request.ProcessingNotes = $"Successfully processed {request.DeletionType} request";
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("User deletion request {RequestId} processed successfully", request.Id);
                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process user deletion request {RequestId}", request.Id);
                throw;
            }
        }

        public async Task<bool> DeactivateUserAsync(int userId, int deletedById, string reason = "Account deactivation")
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("User {UserId} not found or already deleted for deactivation", userId);
                    return false;
                }

                // Soft delete - just mark as deleted, keep all data
                user.IsDeleted = true;
                user.DeletedAt = DateTime.UtcNow;
                user.DeletedById = deletedById;
                user.DeletionReason = reason;
                user.IsActive = false;

                // Create audit log
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    EntityType = "User",
                    EntityId = userId,
                    Action = "Deactivate",
                    UserId = deletedById,
                    Description = $"User {user.Email} deactivated: {reason}",
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("User {UserId} successfully deactivated by {DeletedById}", userId, deletedById);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to deactivate user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> CompleteDataDeletionAsync(int userId, int deletedById, string reason = "Complete data deletion")
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("User {UserId} not found or already deleted for complete deletion", userId);
                    return false;
                }

                // Step 1: Anonymize personal data
                await AnonymizePersonalDataAsync(userId);

                // Step 2: Remove personal records
                await RemovePersonalRecordsAsync(userId);

                // Step 3: Anonymize business metrics
                await AnonymizeBusinessMetricsAsync(userId);

                // Step 4: Soft delete user account
                user.IsDeleted = true;
                user.DeletedAt = DateTime.UtcNow;
                user.DeletedById = deletedById;
                user.DeletionReason = reason;
                user.IsActive = false;

                // Create audit log
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    EntityType = "User",
                    EntityId = userId,
                    Action = "CompleteDeletion",
                    UserId = deletedById,
                    Description = $"User {user.Email} completely deleted: {reason}",
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("User {UserId} completely deleted by {DeletedById}", userId, deletedById);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to completely delete user {UserId}", userId);
                throw;
            }
        }

        private async Task AnonymizePersonalDataAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            _logger.LogInformation("Anonymizing personal data for user {UserId}", userId);

            // Anonymize personal information
            user.FirstName = _anonymizationSettings.DeletedFirstName;
            user.LastName = _anonymizationSettings.DeletedLastName;
            user.Email = $"deleted_{userId}@{_anonymizationSettings.DeletedEmailDomain}";
            user.PhoneNumber = null;
            user.Company = null;
            user.JobTitle = null;
            user.Department = null;
            user.Location = null;
            user.Avatar = null;

            // Anonymize personal messages
            var personalMessages = await _context.ChatMessages
                .Where(cm => cm.UserId == userId)
                .ToListAsync();

            foreach (var message in personalMessages)
            {
                message.Content = _anonymizationSettings.DeletedContentMessage;
                message.Sender = _anonymizationSettings.DeletedSenderName;
            }

            // Anonymize personal comments
            var personalComments = await _context.DocumentComments
                .Where(dc => dc.UserId == userId)
                .ToListAsync();

            foreach (var comment in personalComments)
            {
                comment.Content = _anonymizationSettings.DeletedCommentMessage;
            }

            // Anonymize status item comments
            var statusItemComments = await _context.StatusItemComments
                .Where(sic => sic.UserId == userId)
                .ToListAsync();

            foreach (var comment in statusItemComments)
            {
                comment.Content = _anonymizationSettings.DeletedCommentMessage;
            }
        }

        private async Task RemovePersonalRecordsAsync(int userId)
        {
            _logger.LogInformation("Removing personal records for user {UserId}", userId);
            
            // Remove personal assignments
            var matterAssignments = await _context.MatterAssignments
                .Where(ma => ma.UserId == userId)
                .ToListAsync();
            _context.MatterAssignments.RemoveRange(matterAssignments);
            _logger.LogInformation("Removed {Count} matter assignments", matterAssignments.Count);

            // Remove user organization memberships
            var userOrganizations = await _context.UserOrganizations
                .Where(uo => uo.UserId == userId)
                .ToListAsync();
            _context.UserOrganizations.RemoveRange(userOrganizations);
            _logger.LogInformation("Removed {Count} organization memberships", userOrganizations.Count);

            // Remove team memberships
            var teamMemberships = await _context.TeamMemberships
                .Where(tm => tm.UserId == userId)
                .ToListAsync();
            _context.TeamMemberships.RemoveRange(teamMemberships);
            _logger.LogInformation("Removed {Count} team memberships", teamMemberships.Count);

            // Remove personal status item assignments
            var statusItemAssignments = await _context.StatusItemAssignments
                .Where(sia => sia.UserId == userId)
                .ToListAsync();
            _context.StatusItemAssignments.RemoveRange(statusItemAssignments);

            // Remove personal notifications
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .ToListAsync();
            _context.Notifications.RemoveRange(notifications);

            // Remove personal chat messages
            var chatMessages = await _context.ChatMessages
                .Where(cm => cm.UserId == userId)
                .ToListAsync();
            _context.ChatMessages.RemoveRange(chatMessages);

            // Remove personal document comments
            var documentComments = await _context.DocumentComments
                .Where(dc => dc.UserId == userId)
                .ToListAsync();
            _context.DocumentComments.RemoveRange(documentComments);

            // Remove personal service request messages
            var serviceRequestMessages = await _context.ServiceRequestMessages
                .Where(srm => srm.UserId == userId)
                .ToListAsync();
            _context.ServiceRequestMessages.RemoveRange(serviceRequestMessages);

            // Remove personal matter permissions
            var matterPermissions = await _context.MatterPermissions
                .Where(mp => mp.UserId == userId)
                .ToListAsync();
            _context.MatterPermissions.RemoveRange(matterPermissions);

            // Remove personal conversation participants
            var conversationParticipants = await _context.ConversationParticipants
                .Where(cp => cp.UserId == userId)
                .ToListAsync();
            _context.ConversationParticipants.RemoveRange(conversationParticipants);

            // Remove personal document reviews
            var documentReviews = await _context.DocumentReviews
                .Where(dr => dr.ReviewerId == userId)
                .ToListAsync();
            _context.DocumentReviews.RemoveRange(documentReviews);

            // Remove personal document signatures
            var documentSignatures = await _context.DocumentSignatures
                .Where(ds => ds.SignerId == userId)
                .ToListAsync();
            _context.DocumentSignatures.RemoveRange(documentSignatures);
        }

        private async Task AnonymizeBusinessMetricsAsync(int userId)
        {
            // Convert user-specific business data to anonymized metrics
            var matterAssignments = await _context.MatterAssignments
                .Where(ma => ma.UserId == userId)
                .Include(ma => ma.Matter)
                .ToListAsync();

            foreach (var assignment in matterAssignments)
            {
                // Create anonymized metric record
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    EntityType = "Matter",
                    EntityId = assignment.MatterId,
                    Action = "UserDeleted",
                    UserId = null, // Anonymized
                    UserName = "Deleted User",
                    Description = $"Matter {assignment.Matter.Title} - User assignment removed",
                    Timestamp = DateTime.UtcNow
                });

                // Remove the assignment
                _context.MatterAssignments.Remove(assignment);
            }

            // Handle service requests
            var serviceRequests = await _context.ServiceRequests
                .Where(sr => sr.ClientId == userId || sr.AssignedToId == userId)
                .ToListAsync();

            foreach (var serviceRequest in serviceRequests)
            {
                // Create anonymized metric record
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    EntityType = "ServiceRequest",
                    EntityId = serviceRequest.Id,
                    Action = "UserDeleted",
                    UserId = null, // Anonymized
                    UserName = "Deleted User",
                    Description = $"Service Request {serviceRequest.Title} - User data anonymized",
                    Timestamp = DateTime.UtcNow
                });

                // Clear user references
                if (serviceRequest.ClientId == userId) serviceRequest.ClientId = null;
                if (serviceRequest.AssignedToId == userId) serviceRequest.AssignedToId = null;
            }
        }
    }
}
