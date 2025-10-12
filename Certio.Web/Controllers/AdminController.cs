using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Certio.Infrastructure.Data;
using Certio.Web.Scripts;

namespace Certio.Web.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Admin page for user migration
        /// </summary>
        public async Task<IActionResult> UserMigration()
        {
            var viewModel = await GetMigrationStatusAsync();
            return View(viewModel);
        }

        /// <summary>
        /// Migrate all Identity users to custom User system
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MigrateAllUsers()
        {
            try
            {
                var migrator = new MigrateIdentityUsers(_context, _userManager);
                var result = await migrator.MigrateAllAsync();

                if (result.Success)
                {
                    TempData["SuccessMessage"] = $"Successfully migrated {result.SuccessfulMigrations} users!";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Migration completed with {result.FailedMigrations} failures. Check logs for details.";
                }

                TempData["MigrationResult"] = result.GetSummary();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Migration failed: {ex.Message}";
            }

            return RedirectToAction(nameof(UserMigration));
        }

        /// <summary>
        /// Migrate a specific user by email
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MigrateUser(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Email is required";
                return RedirectToAction(nameof(UserMigration));
            }

            try
            {
                var migrator = new MigrateIdentityUsers(_context, _userManager);
                var result = await migrator.MigrateUserAsync(email);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = $"Successfully migrated user {email}!";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Failed to migrate user {email}: {string.Join(", ", result.Errors)}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Migration failed: {ex.Message}";
            }

            return RedirectToAction(nameof(UserMigration));
        }

        /// <summary>
        /// Get current migration status
        /// </summary>
        private async Task<UserMigrationViewModel> GetMigrationStatusAsync()
        {
            var identityUsers = await _userManager.Users.ToListAsync();
            var customUsers = await _context.Users.ToListAsync();

            var identityEmails = identityUsers.Select(u => u.Email).ToHashSet();
            var customEmails = customUsers.Select(u => u.Email).ToHashSet();

            var usersNeedingMigration = identityUsers
                .Where(iu => !string.IsNullOrEmpty(iu.Email) && !customEmails.Contains(iu.Email))
                .Select(iu => new UserMigrationInfo
                {
                    Email = iu.Email!,
                    UserName = iu.UserName ?? iu.Email!,
                    CreatedAt = iu.LockoutEnd?.DateTime ?? DateTime.MinValue
                })
                .ToList();

            return new UserMigrationViewModel
            {
                TotalIdentityUsers = identityUsers.Count,
                TotalCustomUsers = customUsers.Count,
                UsersNeedingMigration = usersNeedingMigration,
                MigrationComplete = !usersNeedingMigration.Any()
            };
        }
    }

    public class UserMigrationViewModel
    {
        public int TotalIdentityUsers { get; set; }
        public int TotalCustomUsers { get; set; }
        public List<UserMigrationInfo> UsersNeedingMigration { get; set; } = new();
        public bool MigrationComplete { get; set; }
    }

    public class UserMigrationInfo
    {
        public string Email { get; set; } = "";
        public string UserName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
