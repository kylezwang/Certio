using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Certio.Web.Data;
using Certio.Domain.Users;

namespace Certio.Web.Scripts
{
    /// <summary>
    /// Migration script to create custom User records for existing Identity users
    /// Run this script to fix broken account states where users exist in Identity but not in custom User system
    /// </summary>
    public class MigrateIdentityUsers
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public MigrateIdentityUsers(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Migrates all Identity users without corresponding custom User records
        /// </summary>
        public async Task<MigrationResult> MigrateAllAsync()
        {
            var result = new MigrationResult();
            
            try
            {
                // Get all Identity users
                var identityUsers = await _userManager.Users.ToListAsync();
                
                // Get all existing custom User emails
                var existingUserEmails = await _context.Users
                    .Select(u => u.Email)
                    .ToListAsync();

                var usersToMigrate = identityUsers
                    .Where(iu => !string.IsNullOrEmpty(iu.Email) && !existingUserEmails.Contains(iu.Email))
                    .ToList();

                result.TotalIdentityUsers = identityUsers.Count;
                result.ExistingCustomUsers = existingUserEmails.Count;
                result.UsersToMigrate = usersToMigrate.Count;

                foreach (var identityUser in usersToMigrate)
                {
                    try
                    {
                        var customUser = await CreateCustomUserFromIdentityAsync(identityUser);
                        _context.Users.Add(customUser);
                        result.SuccessfulMigrations++;
                    }
                    catch (Exception ex)
                    {
                        result.FailedMigrations++;
                        result.Errors.Add($"Failed to migrate user {identityUser.Email}: {ex.Message}");
                    }
                }

                if (result.SuccessfulMigrations > 0)
                {
                    await _context.SaveChangesAsync();
                }

                result.Success = result.FailedMigrations == 0;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Migration failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Migrates a specific user by email
        /// </summary>
        public async Task<MigrationResult> MigrateUserAsync(string email)
        {
            var result = new MigrationResult();
            
            try
            {
                var identityUser = await _userManager.FindByEmailAsync(email);
                if (identityUser == null)
                {
                    result.Success = false;
                    result.Errors.Add($"Identity user with email {email} not found");
                    return result;
                }

                // Check if custom user already exists
                var existingCustomUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (existingCustomUser != null)
                {
                    result.Success = true;
                    result.UsersToMigrate = 0;
                    result.SuccessfulMigrations = 0;
                    result.Errors.Add($"Custom user with email {email} already exists");
                    return result;
                }

                var customUser = await CreateCustomUserFromIdentityAsync(identityUser);
                _context.Users.Add(customUser);
                await _context.SaveChangesAsync();

                result.Success = true;
                result.UsersToMigrate = 1;
                result.SuccessfulMigrations = 1;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Failed to migrate user {email}: {ex.Message}");
            }

            return result;
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

    /// <summary>
    /// Result of the migration operation
    /// </summary>
    public class MigrationResult
    {
        public bool Success { get; set; }
        public int TotalIdentityUsers { get; set; }
        public int ExistingCustomUsers { get; set; }
        public int UsersToMigrate { get; set; }
        public int SuccessfulMigrations { get; set; }
        public int FailedMigrations { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public string GetSummary()
        {
            return $@"
Migration Summary:
- Total Identity Users: {TotalIdentityUsers}
- Existing Custom Users: {ExistingCustomUsers}
- Users to Migrate: {UsersToMigrate}
- Successful Migrations: {SuccessfulMigrations}
- Failed Migrations: {FailedMigrations}
- Success: {Success}

Errors:
{string.Join("\n", Errors)}
";
        }
    }
}
