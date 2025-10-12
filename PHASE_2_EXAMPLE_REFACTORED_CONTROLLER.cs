using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.ViewModels;
using Certio.Domain.Users;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;

namespace Certio.Web.Controllers
{
    /// <summary>
    /// EXAMPLE: Refactored MatterController using Phase 2 Service Layer
    /// 
    /// This is a reference implementation showing:
    /// 1. Thin controller actions (< 50 lines each)
    /// 2. Service layer delegation
    /// 3. Proper error handling
    /// 4. Clean separation of concerns
    /// 
    /// Copy this pattern to refactor existing MatterController, TasksController, etc.
    /// </summary>
    [Authorize(Policy = "OrgMember")]
    public class MatterControllerRefactored : Controller
    {
        private readonly IMatterService _matterService;
        private readonly ILogger<MatterControllerRefactored> _logger;

        public MatterControllerRefactored(
            IMatterService matterService,
            ILogger<MatterControllerRefactored> logger)
        {
            _matterService = matterService;
            _logger = logger;
        }

        // ============================================================
        // GET: Matter/Index
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var (user, orgId) = GetUserContext();
            if (user == null || orgId == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            var result = await _matterService.ListMattersAsync(user.Id, orgId);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(new List<MatterDto>());
            }

            // Set ViewBag for UI
            ViewBag.OrganizationId = orgId;
            ViewBag.UserPermissions = user.GetEffectivePermissions(orgId);

            return View(result.Data);
        }

        // ============================================================
        // GET: Matter/Details/5
        // ============================================================
        public async Task<IActionResult> Details(int id)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var result = await _matterService.GetMatterAsync(user.Id, id);

            if (!result.Success)
            {
                return HandleServiceError(result);
            }

            return View(result.Data);
        }

        // ============================================================
        // GET: Matter/Create
        // ============================================================
        public async Task<IActionResult> Create()
        {
            var (user, orgId) = GetUserContext();
            if (user == null || orgId == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Prepare view model with data for dropdowns, etc.
            var model = new CreateMatterViewModel
            {
                OrganizationId = orgId
            };

            return View(model);
        }

        // ============================================================
        // POST: Matter/Create
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] CreateMatterViewModel model)
        {
            var (user, orgId) = GetUserContext();
            if (user == null || orgId == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Map ViewModel to DTO
            var createDto = new CreateMatterDto
            {
                Title = model.Title,
                Description = model.Description,
                Status = model.Status,
                PracticeArea = model.PracticeArea,
                AccessLevel = model.AccessLevel,
                TeamId = model.TeamId,
                ClientId = model.ClientId,
                StartDate = model.StartDate,
                DueDate = model.DueDate,
                PendingDate = model.PendingDate,
                StatuteOfLimitationsDate = model.StatuteOfLimitationsDate,
                ClientGoals = model.ClientGoals,
                LegalRequirements = model.LegalRequirements,
                Notes = model.Notes,
                PermissionUserIds = model.PermissionUserIds
            };

            // Call service - ALL business logic delegated
            var result = await _matterService.CreateMatterAsync(
                user.Id,
                orgId,
                createDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                ModelState.AddModelError("", result.ErrorMessage ?? "Failed to create matter");
                return View(model);
            }

            TempData["Success"] = "Matter created successfully";
            return RedirectToAction("Details", new { id = result.Data!.Id });
        }

        // ============================================================
        // GET: Matter/Edit/5
        // ============================================================
        public async Task<IActionResult> Edit(int id)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var result = await _matterService.GetMatterAsync(user.Id, id);

            if (!result.Success)
            {
                return HandleServiceError(result);
            }

            // Map DTO to ViewModel
            var model = new UpdateMatterViewModel
            {
                Id = result.Data!.Id,
                Title = result.Data.Title,
                Description = result.Data.Description,
                Status = result.Data.Status,
                PracticeArea = result.Data.PracticeArea,
                // ... map other properties
            };

            return View(model);
        }

        // ============================================================
        // POST: Matter/Edit/5
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromForm] UpdateMatterViewModel model)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Map ViewModel to DTO
            var updateDto = new UpdateMatterDto
            {
                Title = model.Title,
                Description = model.Description,
                Status = model.Status,
                PracticeArea = model.PracticeArea,
                // ... map other properties
            };

            var result = await _matterService.UpdateMatterAsync(
                user.Id,
                id,
                updateDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                ModelState.AddModelError("", result.ErrorMessage ?? "Failed to update matter");
                return View(model);
            }

            TempData["Success"] = "Matter updated successfully";
            return RedirectToAction("Details", new { id });
        }

        // ============================================================
        // POST: Matter/Delete/5
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Json(new { success = false, error = "Unauthorized" });
            }

            var result = await _matterService.DeleteMatterAsync(
                user.Id,
                id,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return Json(new { success = false, error = result.ErrorMessage });
            }

            return Json(new { success = true });
        }

        // ============================================================
        // POST: Matter/AssignUser
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> AssignUser([FromBody] AssignUserRequest request)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var assignDto = new AssignUserToMatterDto
            {
                UserId = request.UserId,
                AssignmentType = request.AssignmentType,
                Role = request.Role,
                IsNotifyRecipient = request.IsNotifyRecipient
            };

            var result = await _matterService.AssignUserToMatterAsync(
                user.Id,
                request.MatterId,
                assignDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Json(new { success = true, assignment = result.Data });
        }

        // ============================================================
        // POST: Matter/RemoveUser
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> RemoveUser([FromBody] RemoveUserRequest request)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _matterService.RemoveUserFromMatterAsync(
                user.Id,
                request.MatterId,
                request.UserId,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Json(new { success = true });
        }

        // ============================================================
        // POST: Matter/GrantAccess
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> GrantAccess([FromBody] GrantAccessRequest request)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _matterService.GrantMatterAccessAsync(
                user.Id,
                request.MatterId,
                request.UserId,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Json(new { success = true });
        }

        // ============================================================
        // HELPER METHODS (Keep controllers DRY)
        // ============================================================

        /// <summary>
        /// Extracts current user and organization context from HttpContext
        /// </summary>
        private (User? User, int OrganizationId) GetUserContext()
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            var orgId = customUser?.GetPrimaryOrganization()?.OrganizationId ?? 0;
            return (customUser, orgId);
        }

        /// <summary>
        /// Gets the client IP address for audit logging
        /// </summary>
        private string? GetIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        /// <summary>
        /// Gets the user agent string for audit logging
        /// </summary>
        private string? GetUserAgent()
        {
            return HttpContext.Request.Headers["User-Agent"].ToString();
        }

        /// <summary>
        /// Converts ServiceResult errors to appropriate HTTP responses
        /// </summary>
        private IActionResult HandleServiceError<T>(ServiceResult<T> result)
        {
            switch (result.ErrorCode)
            {
                case "RESOURCE_NOT_FOUND":
                    return NotFound();

                case "UNAUTHORIZED_OPERATION":
                    return Forbid();

                case "VALIDATION_ERROR":
                    if (result.ValidationErrors != null)
                    {
                        foreach (var error in result.ValidationErrors)
                        {
                            foreach (var message in error.Value)
                            {
                                ModelState.AddModelError(error.Key, message);
                            }
                        }
                    }
                    return BadRequest(ModelState);

                case "ORGANIZATION_MISMATCH":
                    return Forbid();

                case "BUSINESS_RULE_VIOLATION":
                    return BadRequest(result.ErrorMessage);

                default:
                    _logger.LogError("Service error: {ErrorCode} - {ErrorMessage}",
                        result.ErrorCode, result.ErrorMessage);
                    return StatusCode(500, "An error occurred while processing your request");
            }
        }
    }

    // ============================================================
    // REQUEST MODELS (for API endpoints)
    // ============================================================

    public class AssignUserRequest
    {
        public int MatterId { get; set; }
        public int UserId { get; set; }
        public string AssignmentType { get; set; } = "RelevantContact";
        public string Role { get; set; } = "";
        public bool IsNotifyRecipient { get; set; } = true;
    }

    public class RemoveUserRequest
    {
        public int MatterId { get; set; }
        public int UserId { get; set; }
    }

    public class GrantAccessRequest
    {
        public int MatterId { get; set; }
        public int UserId { get; set; }
    }

    // ============================================================
    // VIEW MODELS (for form binding)
    // These would normally be in a separate ViewModels file
    // ============================================================

    public class CreateMatterViewModel
    {
        public int OrganizationId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "Planning";
        public string PracticeArea { get; set; } = "";
        public string AccessLevel { get; set; } = "Everyone";
        public int? TeamId { get; set; }
        public int? ClientId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? PendingDate { get; set; }
        public DateTime? StatuteOfLimitationsDate { get; set; }
        public string? ClientGoals { get; set; }
        public string? LegalRequirements { get; set; }
        public string? Notes { get; set; }
        public List<int>? PermissionUserIds { get; set; }
    }

    public class UpdateMatterViewModel
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? PracticeArea { get; set; }
        // ... other properties
    }
}

/*
 * NOTES FOR APPLYING THIS PATTERN TO EXISTING CONTROLLERS:
 * 
 * 1. Replace constructor dependencies:
 *    - Remove: ApplicationDbContext, AuthorizationHelper
 *    - Add: IMatterService (or ITaskService, etc.)
 * 
 * 2. In each action method:
 *    - Extract user context first: var (user, orgId) = GetUserContext();
 *    - Validate ModelState for POST actions
 *    - Map ViewModel → DTO
 *    - Call service method
 *    - Handle ServiceResult
 *    - Return appropriate IActionResult
 * 
 * 3. Remove from controller actions:
 *    - Direct _context.Matters queries
 *    - Permission checking logic
 *    - _authHelper calls
 *    - _auditService calls
 *    - Business validation
 *    - Entity creation/modification
 *    - SaveChangesAsync calls
 * 
 * 4. Keep in controller actions:
 *    - Model binding
 *    - ModelState validation
 *    - TempData messages
 *    - ViewBag data
 *    - Response selection (View, Json, Redirect, etc.)
 * 
 * 5. Typical refactored action structure:
 *    ```csharp
 *    public async Task<IActionResult> ActionName(...)
 *    {
 *        // 1. Get user context (5 lines)
 *        var (user, orgId) = GetUserContext();
 *        if (user == null) return RedirectToAction(...);
 *        
 *        // 2. Validate input (3 lines)
 *        if (!ModelState.IsValid) return View(model);
 *        
 *        // 3. Map to DTO (5-10 lines)
 *        var dto = new SomeDto { ... };
 *        
 *        // 4. Call service (5 lines)
 *        var result = await _service.OperationAsync(...);
 *        
 *        // 5. Handle result (5-10 lines)
 *        if (!result.Success) return HandleError(result);
 *        
 *        // 6. Return response (2 lines)
 *        return View(result.Data);
 *    }
 *    // Total: 25-40 lines per action ✅
 *    ```
 * 
 * 6. Benefits you'll see:
 *    - Actions become 50-70% shorter
 *    - No database queries in controllers
 *    - No permission checking in controllers
 *    - Easy to understand flow
 *    - Easy to test
 *    - Consistent error handling
 * 
 * 7. Testing becomes simple:
 *    ```csharp
 *    // Before: Hard to test (needs DbContext, AuthHelper, etc.)
 *    [Fact]
 *    public async Task Create_ValidMatter_ReturnsRedirect()
 *    {
 *        // Setup mock DbContext, AuthHelper, HttpContext...
 *        // 50+ lines of test setup
 *    }
 *    
 *    // After: Easy to test (just mock service)
 *    [Fact]
 *    public async Task Create_ValidMatter_ReturnsRedirect()
 *    {
 *        // Setup
 *        var mockService = new Mock<IMatterService>();
 *        mockService.Setup(s => s.CreateMatterAsync(...))
 *            .ReturnsAsync(ServiceResult<MatterDto>.SuccessResult(new MatterDto()));
 *        
 *        var controller = new MatterController(mockService.Object, ...);
 *        
 *        // Act & Assert
 *        var result = await controller.Create(model);
 *        Assert.IsType<RedirectToActionResult>(result);
 *    }
 *    ```
 */

