using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.Services;
using Certio.Domain.Users;
using Certio.Web.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Certio.Web.Controllers
{
    [Authorize]
    public class UserDeletionController : Controller
    {
        private readonly IUserDeletionService _userDeletionService;
        private readonly ApplicationDbContext _context;

        public UserDeletionController(IUserDeletionService userDeletionService, ApplicationDbContext context)
        {
            _userDeletionService = userDeletionService;
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> RequestDeletion()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RequestDeletion(UserDeletionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            // Validate that user is requesting deletion for themselves
            if (request.UserId != currentUserId.Value)
            {
                ModelState.AddModelError("", "You can only request deletion for your own account.");
                return View(request);
            }

            // Check if user already has a pending request
            var existingRequest = await _context.UserDeletionRequests
                .FirstOrDefaultAsync(udr => udr.UserId == currentUserId.Value && !udr.IsProcessed);
            
            if (existingRequest != null)
            {
                ModelState.AddModelError("", "You already have a pending deletion request.");
                return View(request);
            }

            request.UserId = currentUserId.Value;
            request.RequestedAt = DateTime.UtcNow;

            _context.UserDeletionRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your deletion request has been submitted successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")] // Only admins can process deletion requests
        public async Task<IActionResult> ProcessRequests()
        {
            var pendingRequests = await _context.UserDeletionRequests
                .Include(udr => udr.User)
                .Where(udr => !udr.IsProcessed)
                .OrderBy(udr => udr.RequestedAt)
                .ToListAsync();

            return View(pendingRequests);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ProcessRequest(int requestId, bool approve)
        {
            // Validate input
            if (requestId <= 0)
            {
                TempData["Error"] = "Invalid request ID.";
                return RedirectToAction(nameof(ProcessRequests));
            }

            var request = await _context.UserDeletionRequests
                .Include(udr => udr.User)
                .FirstOrDefaultAsync(udr => udr.Id == requestId);

            if (request == null)
            {
                TempData["Error"] = "Deletion request not found.";
                return RedirectToAction(nameof(ProcessRequests));
            }

            if (request.IsProcessed)
            {
                TempData["Error"] = "This request has already been processed.";
                return RedirectToAction(nameof(ProcessRequests));
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (approve)
            {
                var result = await _userDeletionService.ProcessUserDeletionRequestAsync(request, currentUserId.Value);
                if (result)
                {
                    TempData["Success"] = $"User {request.User.Email} has been successfully processed.";
                }
                else
                {
                    TempData["Error"] = "Failed to process the deletion request.";
                }
            }
            else
            {
                request.IsProcessed = true;
                request.ProcessedAt = DateTime.UtcNow;
                request.ProcessedById = currentUserId.Value;
                request.ProcessingNotes = "Request denied by administrator";
                await _context.SaveChangesAsync();
                TempData["Success"] = "Deletion request has been denied.";
            }

            return RedirectToAction(nameof(ProcessRequests));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")] // Only admins can test deactivation
        public async Task<IActionResult> TestDeactivate(int userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            // Validate input
            if (userId <= 0)
            {
                TempData["Error"] = "Invalid user ID.";
                return RedirectToAction(nameof(Index));
            }

            // Check if user exists and is not already deleted
            var targetUser = await _context.Users.FindAsync(userId);
            if (targetUser == null || targetUser.IsDeleted)
            {
                TempData["Error"] = "User not found or already deleted.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userDeletionService.DeactivateUserAsync(userId, currentUserId.Value, "Test deactivation");
            
            if (result)
            {
                TempData["Success"] = $"User {targetUser.Email} has been deactivated successfully.";
            }
            else
            {
                TempData["Error"] = "Failed to deactivate user.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")] // Only admins can test complete deletion
        public async Task<IActionResult> TestCompleteDeletion(int userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            // Validate input
            if (userId <= 0)
            {
                TempData["Error"] = "Invalid user ID.";
                return RedirectToAction(nameof(Index));
            }

            // Check if user exists and is not already deleted
            var targetUser = await _context.Users.FindAsync(userId);
            if (targetUser == null || targetUser.IsDeleted)
            {
                TempData["Error"] = "User not found or already deleted.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userDeletionService.CompleteDataDeletionAsync(userId, currentUserId.Value, "Test complete deletion");
            
            if (result)
            {
                TempData["Success"] = $"User {targetUser.Email} has been completely deleted successfully.";
            }
            else
            {
                TempData["Error"] = "Failed to completely delete user.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ActiveUsers()
        {
            var activeUsers = await _context.ActiveUsers
                .Include(u => u.UserOrganizations)
                .ThenInclude(uo => uo.Organization)
                .ToListAsync();

            return View(activeUsers);
        }

        private int? GetCurrentUserId()
        {
            // First try to get the custom user ID from context (set by UserSyncMiddleware)
            if (HttpContext.Items.TryGetValue("CustomUserId", out var customUserId) && customUserId is int userId)
            {
                return userId;
            }
            
            // Fallback to claims (for backwards compatibility)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int claimUserId))
            {
                return claimUserId;
            }
            
            // Fallback for development/testing - remove in production
            var emailClaim = User.FindFirst(ClaimTypes.Email);
            if (emailClaim != null)
            {
                // Try to find user by email
                var user = _context.Users.FirstOrDefault(u => u.Email == emailClaim.Value && !u.IsDeleted);
                return user?.Id;
            }
            
            return null;
        }
    }
}
