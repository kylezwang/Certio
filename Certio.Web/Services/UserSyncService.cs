using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Certio.Web.Data;
using Certio.Domain.Users;

namespace Certio.Web.Services
{
    /// <summary>
    /// Service to synchronize Identity users with custom User records
    /// Automatically creates custom User records when needed
    /// </summary>
    public interface IUserSyncService
    {
        Task<User?> EnsureCustomUserExistsAsync(string identityUserId);
        Task<User?> EnsureCustomUserExistsByEmailAsync(string email);
        Task<bool> UserExistsAsync(string identityUserId);
        Task<bool> UserExistsByEmailAsync(string email);
        Task<User?> EnsureDefaultsAsync(User user);
    }

    public class UserSyncService : IUserSyncService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<UserSyncService> _logger;

        public UserSyncService(
            ApplicationDbContext context, 
            UserManager<IdentityUser> userManager,
            ILogger<UserSyncService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Ensures a custom User record exists for the given Identity user ID
        /// Creates one if it doesn't exist
        /// </summary>
        public async Task<User?> EnsureCustomUserExistsAsync(string identityUserId)
        {
            try
            {
                var identityUser = await _userManager.FindByIdAsync(identityUserId);
                if (identityUser == null)
                {
                    _logger.LogWarning("Identity user with ID {IdentityUserId} not found", identityUserId);
                    return null;
                }

                return await EnsureCustomUserExistsByEmailAsync(identityUser.Email!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring custom user exists for Identity user {IdentityUserId}", identityUserId);
                return null;
            }
        }

        /// <summary>
        /// Ensures a custom User record exists for the given email
        /// Creates one if it doesn't exist
        /// </summary>
        public async Task<User?> EnsureCustomUserExistsByEmailAsync(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("Email is null or empty");
                    return null;
                }

                // Check if custom user already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (existingUser != null)
                {
                    _logger.LogDebug("Custom user already exists for email {Email}", email);
                    // Backfill defaults if needed
                    await EnsureDefaultsAsync(existingUser);
                    return existingUser;
                }

                // Get the Identity user
                var identityUser = await _userManager.FindByEmailAsync(email);
                if (identityUser == null)
                {
                    _logger.LogWarning("Identity user with email {Email} not found", email);
                    return null;
                }

                // Create custom user
                var customUser = await CreateCustomUserFromIdentityAsync(identityUser);
                _context.Users.Add(customUser);
                await _context.SaveChangesAsync();
                await EnsureDefaultsAsync(customUser);

                _logger.LogInformation("Created custom user for email {Email} with ID {UserId}", email, customUser.Id);
                return customUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring custom user exists for email {Email}", email);
                return null;
            }
        }

        /// <summary>
        /// Checks if a custom User record exists for the given Identity user ID
        /// </summary>
        public async Task<bool> UserExistsAsync(string identityUserId)
        {
            try
            {
                var identityUser = await _userManager.FindByIdAsync(identityUserId);
                if (identityUser?.Email == null)
                    return false;

                return await UserExistsByEmailAsync(identityUser.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user exists for Identity user {IdentityUserId}", identityUserId);
                return false;
            }
        }

        /// <summary>
        /// Checks if a custom User record exists for the given email
        /// </summary>
        public async Task<bool> UserExistsByEmailAsync(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                    return false;

                return await _context.Users
                    .AnyAsync(u => u.Email == email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user exists for email {Email}", email);
                return false;
            }
        }

        /// <summary>
        /// Creates a custom User record from an Identity user
        /// </summary>
        private Task<User> CreateCustomUserFromIdentityAsync(IdentityUser identityUser)
        {
            // Extract name from email if no name is available
            var emailParts = identityUser.Email?.Split('@');
            var firstName = "User";
            var lastName = "Account";

            if (emailParts?.Length > 0)
            {
                var nameParts = emailParts[0].Split('.');
                if (nameParts.Length >= 2)
                {
                    firstName = CapitalizeFirstLetter(nameParts[0]);
                    lastName = CapitalizeFirstLetter(nameParts[1]);
                }
                else
                {
                    firstName = CapitalizeFirstLetter(emailParts[0]);
                }
            }

            var customUser = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = identityUser.Email ?? "",
                PhoneNumber = identityUser.PhoneNumber,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginDate = DateTime.UtcNow,
                Color = GetRandomColor()
            };

            return Task.FromResult(customUser);
        }

        /// <summary>
        /// Ensures the user has an organization and required default types for permissions.
        /// </summary>
        public async Task<User?> EnsureDefaultsAsync(User user)
        {
            try
            {
                var changed = false;

                // Check if user already has active organizations
                var existingOrganizations = await _context.UserOrganizations
                    .Where(uo => uo.UserId == user.Id && uo.IsActive)
                    .ToListAsync();

                _logger.LogDebug("User {UserId} has {Count} active organizations", user.Id, existingOrganizations.Count);

                if (existingOrganizations.Count == 0)
                {
                    _logger.LogInformation("Creating personal organization for user {UserId}", user.Id);
                    
                    // Use a transaction to prevent race conditions
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    
                    try
                    {
                        // Double-check within transaction to prevent race conditions
                        var hasOrganizationsInTransaction = await _context.UserOrganizations
                            .AnyAsync(uo => uo.UserId == user.Id && uo.IsActive);

                        if (!hasOrganizationsInTransaction)
                        {
                            // Create a personal organization for the user
                            var org = new Certio.Domain.Organizations.Organization
                            {
                                Name = $"{user.FirstName} {user.LastName}'s Organization",
                                OwnerId = user.Id,
                                Type = Certio.Domain.Organizations.OrganizationType.Personal,
                                CreatedAt = DateTime.UtcNow,
                                IsActive = true,
                                IsPersonal = true
                            };
                            _context.Organizations.Add(org);
                            await _context.SaveChangesAsync();

                            // Add user to organization as owner
                            var userOrg = new Certio.Domain.Users.UserOrganization
                            {
                                UserId = user.Id,
                                OrganizationId = org.Id,
                                UserType = Certio.Domain.Users.UserTypes.Client,
                                Role = Certio.Domain.Users.OrganizationRoles.Owner,
                                IsPrimary = true,
                                IsActive = true,
                                JoinedAt = DateTime.UtcNow
                            };
                            _context.UserOrganizations.Add(userOrg);
                            user.IsPersonalOrganization = true;
                            changed = true;

                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            
                            _logger.LogInformation("Successfully created personal organization {OrgId} for user {UserId}", org.Id, user.Id);
                        }
                        else
                        {
                            _logger.LogDebug("User {UserId} already has organizations within transaction, skipping creation", user.Id);
                            await transaction.RollbackAsync();
                        }
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
                else
                {
                    _logger.LogDebug("User {UserId} already has {Count} organizations, skipping creation", user.Id, existingOrganizations.Count);
                }

                // ClientType is now handled at the organization level in UserOrganization
                // No need to set global ClientType anymore

                // No defaults needed for External subtype here

                if (changed)
                {
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring defaults for user {UserId}", user.Id);
                return user;
            }
        }


        /// <summary>
        /// Capitalizes the first letter of a string
        /// </summary>
        private string CapitalizeFirstLetter(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return char.ToUpper(input[0]) + input.Substring(1).ToLower();
        }

        /// <summary>
        /// Gets a random color for the user avatar
        /// </summary>
        private string GetRandomColor()
        {
            var colors = new[]
            {
                "#007bff", "#28a745", "#dc3545", "#ffc107", "#17a2b8",
                "#6f42c1", "#e83e8c", "#fd7e14", "#20c997", "#6c757d"
            };
            
            var random = new Random();
            return colors[random.Next(colors.Length)];
        }
    }
}
