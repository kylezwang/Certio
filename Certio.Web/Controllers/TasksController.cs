using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.ViewModels;
using Certio.Domain.Matters;
using Certio.Domain.Tasks;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Infrastructure.Data;
using Certio.Application.Interfaces;
using Certio.Web.Security;
using Certio.Web.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Certio.Web.Configuration;

namespace Certio.Web.Controllers
{
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GoogleMapsConfiguration _googleMapsConfig;
        private readonly AuthorizationHelper _authHelper;
        private readonly IAuditService _auditService;
        private readonly ILogger<TasksController> _logger;

        public TasksController(
            ApplicationDbContext context, 
            IOptions<GoogleMapsConfiguration> googleMapsConfig,
            AuthorizationHelper authHelper,
            IAuditService auditService,
            ILogger<TasksController> logger)
        {
            _context = context;
            _googleMapsConfig = googleMapsConfig.Value;
            _authHelper = authHelper;
            _auditService = auditService;
            _logger = logger;
        }

        // GET: /Client/{orgId}/Tasks or /Tasks/Index
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Tasks")]
        [HttpGet("/Tasks/Index")]
        public async Task<IActionResult> Index(int? orgId = null)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // If orgId not provided, use primary organization
            int organizationId;
            if (orgId.HasValue)
            {
                organizationId = orgId.Value;
            }
            else
            {
                var primaryOrg = customUser.GetPrimaryOrganization();
                if (primaryOrg == null)
                {
                    return RedirectToAction("Index", "Home");
                }
                organizationId = primaryOrg.OrganizationId;
            }

            // Set ViewBag for layout
            ViewBag.OrganizationId = organizationId;
            ViewBag.GoogleMapsApiKey = _googleMapsConfig.ApiKey;
            ViewBag.GoogleMapsEnabled = _googleMapsConfig.Enabled;
            
            // Set current user data for JavaScript
            ViewBag.CurrentUserId = customUser.Id;
            ViewBag.CurrentUserName = $"{customUser.FirstName} {customUser.LastName}";
            ViewBag.CurrentUserInitials = $"{customUser.FirstName[0]}{customUser.LastName[0]}".ToUpper();
            ViewBag.CurrentUserEmail = customUser.Email ?? "";
            
            var org = await _context.Organizations
                .Where(o => o.Id == organizationId)
                .FirstOrDefaultAsync();
            
            ViewBag.OrganizationName = org?.Name ?? "Client";

            // Get matters from the user's organization AND from client organizations they have relationships with
            var userOrgMembership = customUser.GetOrganizationMembership(organizationId);
            var isLawFirmUser = userOrgMembership?.UserType == UserTypes.LawFirm;

            // Initialize client organization IDs list
            var clientOrgIds = new List<int>();

            // Get tasks from the user's organization AND from client organizations they have relationships with
            IQueryable<TaskItem> tasksQuery = _context.TaskItems
                .Where(t => t.OrgId == organizationId);  // Own organization tasks

            if (isLawFirmUser)
            {
                // Get all client organizations this law firm has relationships with
                clientOrgIds = await _context.OrganizationRelationships
                    .Where(r => r.SourceOrganizationId == organizationId &&
                               r.RelationshipType == RelationshipTypes.LawFirmClient &&
                               r.IsActive && !r.IsDeleted &&
                               (!r.ExpiresAt.HasValue || r.ExpiresAt.Value > DateTime.UtcNow))
                    .Select(r => r.TargetOrganizationId)
                    .ToListAsync();

                // Include tasks from all connected client organizations
                tasksQuery = _context.TaskItems
                    .Where(t => t.OrgId == organizationId || clientOrgIds.Contains(t.OrgId));
            }

            // Apply task-level filtering based on user assignments and matter access
            // Users should see tasks where:
            // 1. They are directly assigned to the task (via TaskAssignments)
            // 2. They are assigned to the task's matter (via MatterAssignments)
            // 3. The matter has "Everyone" access level
            // 4. They have specific permission to the matter (via MatterPermissions)
            tasksQuery = tasksQuery
                .Include(t => t.Matter)
                    .ThenInclude(m => m.Assignments)
                .Include(t => t.Matter)
                    .ThenInclude(m => m.Permissions)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.User)
                .Include(t => t.Comments)
                    .ThenInclude(c => c.User)
                .Include(t => t.SubTasks)
                    .ThenInclude(st => st.Assignments)
                        .ThenInclude(sta => sta.User)
                .Where(t => 
                    // User is assigned to the task directly
                    t.TaskAssignments.Any(ta => ta.UserId == customUser.Id && ta.RemovedAt == null) ||
                    // User is assigned to the matter
                    t.Matter.Assignments.Any(ma => ma.UserId == customUser.Id && ma.RemovedAt == null) ||
                    // Matter has "Everyone" access level
                    t.Matter.AccessLevel == "Everyone" ||
                    // User has specific permission to the matter
                    t.Matter.Permissions.Any(p => p.UserId == customUser.Id && p.RevokedAt == null))
                .OrderBy(t => t.Order);

            var tasks = await tasksQuery.ToListAsync();

            IQueryable<Matter> mattersQuery = _context.Matters
                .Include(m => m.Assignments)
                .Include(m => m.Permissions)
                .Where(m => m.OrganizationId == organizationId);  // Own organization matters

            if (isLawFirmUser)
            {
                // Use the same clientOrgIds we already fetched for tasks
                // Include matters from all connected client organizations
                mattersQuery = _context.Matters
                    .Include(m => m.Assignments)
                    .Include(m => m.Permissions)
                    .Where(m => m.OrganizationId == organizationId || clientOrgIds.Contains(m.OrganizationId));
            }

            // Apply matter-level filtering based on user access
            // Users should see matters where:
            // 1. AccessLevel is "Everyone" (all org members can see)
            // 2. AccessLevel is "Specific" AND user has explicit permission
            // 3. User is assigned to the matter
            mattersQuery = mattersQuery.Where(m => 
                m.AccessLevel == "Everyone" ||
                m.Permissions.Any(p => p.UserId == customUser.Id && p.RevokedAt == null) ||
                m.Assignments.Any(a => a.UserId == customUser.Id && a.RemovedAt == null));

            var viewModel = new TasksViewModel
            {
                AllTasks = tasks.Select(t => MapToViewModel(t)).ToList(),
                PlannedTasks = tasks.Where(t => t.Status == "Pending").Select(t => MapToViewModel(t)).ToList(),
                InProgressTasks = tasks.Where(t => t.Status == "InProgress").Select(t => MapToViewModel(t)).ToList(),
                ReviewTasks = tasks.Where(t => t.Status == "Review").Select(t => MapToViewModel(t)).ToList(),
                CompletedTasks = tasks.Where(t => t.Status == "Completed").Select(t => MapToViewModel(t)).ToList(),
                Matters = await mattersQuery
                    .OrderBy(m => m.Title)
                    .Select(m => new MatterOption
                    {
                        Id = m.Id,
                        Title = m.Title,
                        PracticeArea = m.PracticeArea,
                        AssignedUserIds = m.Assignments.Select(a => a.UserId).ToList()
                    })
                    .ToListAsync(),
                Users = await _context.UserOrganizations
                    .Where(uo => uo.OrganizationId == organizationId && uo.IsActive)
                    .Select(uo => new UserOption
                    {
                        Id = uo.UserId,
                        Name = uo.User.FirstName + " " + uo.User.LastName,
                        Email = uo.User.Email ?? "",
                        Initials = (uo.User.FirstName.Substring(0, 1) + uo.User.LastName.Substring(0, 1)).ToUpper()
                    })
                    .ToListAsync()
            };

            return View(viewModel);
        }

        // POST: Tasks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                return Json(new { success = false, message = "Organization not found" });
            }

            // Get the matter to determine the correct organization
            var matter = await _context.Matters
                .FirstOrDefaultAsync(m => m.Id == request.MatterId);

            if (matter == null)
            {
                return Json(new { success = false, message = "Matter not found" });
            }

            var taskItem = new TaskItem
            {
                OrgId = matter.OrganizationId,
                MatterId = request.MatterId,
                Title = request.Title,
                Description = request.Description,
                Status = request.Status ?? "Pending",
                Priority = request.Priority ?? "Medium",
                StartedAt = request.StartedAt,
                DueDate = request.DueDate,
                Location = request.Location,
                Order = request.Order,
                CreatedAt = DateTime.UtcNow
            };

            _context.TaskItems.Add(taskItem);
            await _context.SaveChangesAsync();

            // Auto-assign the creator to ensure visibility and notifications
            var creatorAssignment = new TaskAssignment
            {
                TaskItemId = taskItem.Id,
                UserId = customUser.Id,
                AssignmentType = "Assignee",
                Role = "Task Creator",
                AssignedAt = DateTime.UtcNow
            };
            _context.TaskAssignments.Add(creatorAssignment);
            await _context.SaveChangesAsync();

            return Json(new { success = true, taskId = taskItem.Id });
        }

        // POST: Tasks/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromBody] UpdateTaskRequest request)
        {
            // Input validation
            if (!InputValidator.IsValidId(request.Id))
            {
                _logger.LogWarning("Invalid task ID in Update request: {Id}", request.Id);
                return Json(new { success = false, message = "Invalid task ID" });
            }

            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Update request");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // SECURITY FIX: Verify user has access to this task
            var task = await _authHelper.GetTaskIfAuthorizedAsync(request.Id, customUser.Id, HttpContext);
            if (task == null)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to update unauthorized task {TaskId}", customUser.Id, request.Id);
                return Json(new { success = false, message = "Task not found" });
            }

            // Validate and sanitize inputs
            if (!string.IsNullOrEmpty(request.Title))
            {
                if (!InputValidator.IsValidString(request.Title, InputValidator.MAX_TITLE_LENGTH))
                    return Json(new { success = false, message = "Title is too long" });
                task.Title = InputValidator.Sanitize(request.Title, InputValidator.MAX_TITLE_LENGTH);
            }

            if (!string.IsNullOrEmpty(request.Description))
            {
                if (!InputValidator.IsValidString(request.Description, InputValidator.MAX_DESCRIPTION_LENGTH))
                    return Json(new { success = false, message = "Description is too long" });
                task.Description = InputValidator.Sanitize(request.Description, InputValidator.MAX_DESCRIPTION_LENGTH);
            }

            if (!string.IsNullOrEmpty(request.Status))
            {
                if (!InputValidator.IsValidTaskStatus(request.Status))
                    return Json(new { success = false, message = "Invalid status value" });
                task.Status = request.Status;
            }

            if (!string.IsNullOrEmpty(request.Priority))
            {
                if (!InputValidator.IsValidPriority(request.Priority))
                    return Json(new { success = false, message = "Invalid priority value" });
                task.Priority = request.Priority;
            }

            if (request.StartedAt.HasValue)
                task.StartedAt = request.StartedAt.Value;
            if (request.DueDate.HasValue)
                task.DueDate = request.DueDate.Value;
            if (!string.IsNullOrEmpty(request.Location))
                task.Location = InputValidator.Sanitize(request.Location, 200);
            if (request.Order.HasValue)
                task.Order = request.Order.Value;

            task.LastModifiedAt = DateTime.UtcNow;

            if (task.Status == "Completed" && !task.CompletedAt.HasValue)
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Audit log the update
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            await _auditService.LogUpdateAsync(
                customUser.Id,
                task.OrgId,
                "Task",
                task.Id,
                $"Updated: {task.Title}",
                ipAddress,
                userAgent);

            _logger.LogInformation("User {UserId} updated task {TaskId} in org {OrgId}", customUser.Id, task.Id, task.OrgId);

            return Json(new { success = true });
        }

        // POST: Tasks/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest request)
        {
            // Input validation
            if (!InputValidator.IsValidId(request.TaskId))
            {
                _logger.LogWarning("Invalid task ID in UpdateStatus request: {TaskId}", request.TaskId);
                return Json(new { success = false, message = "Invalid task ID" });
            }

            if (!InputValidator.IsValidTaskStatus(request.Status))
            {
                return Json(new { success = false, message = "Invalid status value" });
            }

            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for UpdateStatus");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // SECURITY FIX: Verify user has access to this task
            var task = await _authHelper.GetTaskIfAuthorizedAsync(request.TaskId, customUser.Id, HttpContext);
            if (task == null)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to update status of unauthorized task {TaskId}", customUser.Id, request.TaskId);
                return Json(new { success = false, message = "Task not found" });
            }

            task.Status = request.Status;
            task.Order = request.Order;
            task.LastModifiedAt = DateTime.UtcNow;

            if (request.Status != "Pending" && !task.StartedAt.HasValue)
            {
                task.StartedAt = DateTime.UtcNow;
            }

            if (request.Status == "Completed" && !task.CompletedAt.HasValue)
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Audit log the status change
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            await _auditService.LogUpdateAsync(
                customUser.Id,
                task.OrgId,
                "Task",
                task.Id,
                $"Status changed to: {request.Status}",
                ipAddress,
                userAgent);

            return Json(new { success = true });
        }

        // GET: Tasks/Get/{id}
        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            // Input validation
            if (!InputValidator.IsValidId(id))
            {
                _logger.LogWarning("Invalid task ID in Get request: {Id}", id);
                return Json(new { success = false, message = "Invalid task ID" });
            }

            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Get request");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // SECURITY FIX: Verify user has access to this task
            var task = await _authHelper.GetTaskIfAuthorizedAsync(id, customUser.Id, HttpContext);
            if (task == null)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to view unauthorized task {TaskId}", customUser.Id, id);
                return Json(new { success = false, message = "Task not found" });
            }

            var taskViewModel = MapToViewModel(task);
            return Json(new { success = true, task = taskViewModel });
        }

        // POST: Tasks/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            // Input validation
            if (!InputValidator.IsValidId(id))
            {
                _logger.LogWarning("Invalid task ID in Delete request: {Id}", id);
                return Json(new { success = false, message = "Invalid task ID" });
            }

            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Delete request");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // SECURITY FIX: Verify user has access to this task before deleting
            var task = await _authHelper.GetTaskIfAuthorizedAsync(id, customUser.Id, HttpContext);
            if (task == null)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to delete unauthorized task {TaskId}", customUser.Id, id);
                return Json(new { success = false, message = "Task not found" });
            }

            // Store info for audit log before deletion
            var taskTitle = task.Title;
            var taskOrgId = task.OrgId;

            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();

            // Audit log the deletion
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogDeleteAsync(
                customUser.Id,
                taskOrgId,
                "Task",
                id,
                ipAddress);

            _logger.LogInformation("User {UserId} deleted task {TaskId} ({Title}) in org {OrgId}", 
                customUser.Id, id, taskTitle, taskOrgId);

            return Json(new { success = true });
        }

        // GET: Tasks/GetComments
        [HttpGet]
        public async Task<IActionResult> GetComments(int taskId)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var comments = await _context.TaskItemComments
                .Where(c => c.TaskItemId == taskId)
                .Include(c => c.User)
                .Include(c => c.Mentions)
                    .ThenInclude(m => m.MentionedUser)
                .Include(c => c.Reactions)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var result = comments.Select(c => new TaskCommentViewModel
            {
                Id = c.Id,
                UserId = c.UserId,
                UserName = $"{c.User.FirstName} {c.User.LastName}",
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                TimeAgo = GetTimeAgo(c.CreatedAt),
                Mentions = c.Mentions.Select(m => new UserOption
                {
                    Id = m.MentionedUserId,
                    Name = $"{m.MentionedUser.FirstName} {m.MentionedUser.LastName}",
                    Email = m.MentionedUser.Email ?? string.Empty,
                    Initials = ($"{m.MentionedUser.FirstName[0]}{m.MentionedUser.LastName[0]}").ToUpper()
                }).ToList(),
                Reactions = c.Reactions
                    .GroupBy(r => r.ReactionType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                UserReaction = c.Reactions.FirstOrDefault(r => r.UserId == customUser.Id)?.ReactionType ?? string.Empty
            }).ToList();

            return Json(new { success = true, comments = result });
        }

        // POST: Tasks/AddComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment([FromBody] AddCommentRequest request)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var comment = new TaskItemComment
            {
                TaskItemId = request.TaskItemId,
                UserId = customUser.Id,
                Content = request.Content,
                CreatedAt = DateTime.UtcNow
            };

            _context.TaskItemComments.Add(comment);
            await _context.SaveChangesAsync();

            // Mentions
            if (request.MentionUserIds != null && request.MentionUserIds.Count > 0)
            {
                var mentionEntities = request.MentionUserIds.Distinct().Select(uid => new TaskCommentMention
                {
                    CommentId = comment.Id,
                    MentionedUserId = uid
                }).ToList();
                _context.TaskCommentMentions.AddRange(mentionEntities);
                await _context.SaveChangesAsync();
            }

            // Return full view model for convenience
            var vm = new TaskCommentViewModel
            {
                Id = comment.Id,
                UserId = customUser.Id,
                UserName = $"{customUser.FirstName} {customUser.LastName}",
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                TimeAgo = GetTimeAgo(comment.CreatedAt),
                Mentions = (request.MentionUserIds ?? new List<int>()).Select(uid =>
                {
                    var u = _context.Users.FirstOrDefault(x => x.Id == uid);
                    return new UserOption
                    {
                        Id = uid,
                        Name = u != null ? $"{u.FirstName} {u.LastName}" : string.Empty,
                        Email = u?.Email ?? string.Empty,
                        Initials = u != null ? ($"{u.FirstName[0]}{u.LastName[0]}").ToUpper() : string.Empty
                    };
                }).ToList(),
                Reactions = new Dictionary<string, int>(),
                UserReaction = string.Empty
            };

            return Json(new { success = true, comment = vm });
        }

        // POST: Tasks/ReactToComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactToComment([FromBody] ReactToCommentRequest request)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var comment = await _context.TaskItemComments.FindAsync(request.CommentId);
            if (comment == null)
            {
                return Json(new { success = false, message = "Comment not found" });
            }

            // Toggle behavior: if same reaction exists, remove; else upsert
            var existing = await _context.TaskCommentReactions
                .FirstOrDefaultAsync(r => r.CommentId == request.CommentId && r.UserId == customUser.Id);

            if (existing != null)
            {
                if (existing.ReactionType == request.ReactionType)
                {
                    _context.TaskCommentReactions.Remove(existing);
                }
                else
                {
                    existing.ReactionType = request.ReactionType;
                }
            }
            else
            {
                _context.TaskCommentReactions.Add(new TaskCommentReaction
                {
                    CommentId = request.CommentId,
                    UserId = customUser.Id,
                    ReactionType = request.ReactionType,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // Return updated reaction summary
            var summary = await _context.TaskCommentReactions
                .Where(r => r.CommentId == request.CommentId)
                .GroupBy(r => r.ReactionType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            return Json(new { success = true, reactions = summary });
        }

        // POST: Tasks/AddAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAssignment([FromBody] AddAssignmentRequest request)
        {
            var assignment = new TaskAssignment
            {
                TaskItemId = request.TaskItemId,
                UserId = request.UserId,
                AssignmentType = request.AssignmentType ?? "Assignee",
                Role = request.Role,
                AssignedAt = DateTime.UtcNow
            };

            _context.TaskAssignments.Add(assignment);
            await _context.SaveChangesAsync();

            return Json(new { success = true, assignmentId = assignment.Id });
        }

        // POST: Tasks/RemoveAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAssignment(int id)
        {
            // Input validation
            if (!InputValidator.IsValidId(id))
            {
                _logger.LogWarning("Invalid assignment ID in RemoveAssignment request: {Id}", id);
                return Json(new { success = false, message = "Invalid assignment ID" });
            }

            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for RemoveAssignment");
                return Json(new { success = false, message = "User not authenticated" });
            }

            var assignment = await _context.TaskAssignments
                .Include(ta => ta.TaskItem)
                .FirstOrDefaultAsync(ta => ta.Id == id);

            if (assignment == null)
            {
                return Json(new { success = false, message = "Assignment not found" });
            }

            // SECURITY FIX: Verify user has access to the parent task
            var canAccess = await _authHelper.ValidateUserCanAccessTaskAsync(assignment.TaskItemId, customUser.Id);
            if (!canAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to remove assignment from unauthorized task {TaskId}", 
                    customUser.Id, assignment.TaskItemId);
                return Json(new { success = false, message = "Assignment not found" });
            }

            _context.TaskAssignments.Remove(assignment);
            await _context.SaveChangesAsync();

            // Audit log
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogOperationAsync(
                customUser.Id,
                assignment.TaskItem.OrgId,
                "REMOVE_ASSIGNMENT",
                "TaskAssignment",
                id,
                ipAddress);

            return Json(new { success = true });
        }

        // POST: Tasks/AddSubTaskAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSubTaskAssignment([FromBody] AddSubTaskAssignmentRequest request)
        {
            var assignment = new SubTaskAssignment
            {
                SubTaskItemId = request.SubTaskItemId,
                UserId = request.UserId,
                AssignmentType = request.AssignmentType ?? "Assignee",
                Role = request.Role,
                AssignedAt = DateTime.UtcNow
            };

            _context.SubTaskAssignments.Add(assignment);
            await _context.SaveChangesAsync();

            return Json(new { success = true, assignmentId = assignment.Id });
        }

        // POST: Tasks/RemoveSubTaskAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSubTaskAssignment(int id)
        {
            // Input validation
            if (!InputValidator.IsValidId(id))
            {
                _logger.LogWarning("Invalid subtask assignment ID in RemoveSubTaskAssignment request: {Id}", id);
                return Json(new { success = false, message = "Invalid assignment ID" });
            }

            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for RemoveSubTaskAssignment");
                return Json(new { success = false, message = "User not authenticated" });
            }

            var assignment = await _context.SubTaskAssignments
                .Include(sta => sta.SubTaskItem)
                .FirstOrDefaultAsync(sta => sta.Id == id);

            if (assignment == null)
            {
                return Json(new { success = false, message = "Assignment not found" });
            }

            // SECURITY FIX: Verify user has access to the parent subtask
            var canAccess = await _authHelper.ValidateUserCanAccessSubTaskAsync(assignment.SubTaskItemId, customUser.Id);
            if (!canAccess)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to remove assignment from unauthorized subtask {SubTaskId}", 
                    customUser.Id, assignment.SubTaskItemId);
                return Json(new { success = false, message = "Assignment not found" });
            }

            _context.SubTaskAssignments.Remove(assignment);
            await _context.SaveChangesAsync();

            // Audit log
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogOperationAsync(
                customUser.Id,
                assignment.SubTaskItem.OrgId,
                "REMOVE_SUBTASK_ASSIGNMENT",
                "SubTaskAssignment",
                id,
                ipAddress);

            return Json(new { success = true });
        }

        // POST: Tasks/UpdateSubTaskStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSubTaskStatus([FromBody] UpdateSubTaskStatusRequest request)
        {
            // Input validation
            if (!InputValidator.IsValidId(request.SubTaskId))
            {
                _logger.LogWarning("Invalid subtask ID in UpdateSubTaskStatus request: {SubTaskId}", request.SubTaskId);
                return Json(new { success = false, message = "Invalid subtask ID" });
            }

            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for UpdateSubTaskStatus");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // SECURITY FIX: Verify user has access to this subtask
            var subTask = await _authHelper.GetSubTaskIfAuthorizedAsync(request.SubTaskId, customUser.Id);
            if (subTask == null)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to update unauthorized subtask {SubTaskId}", customUser.Id, request.SubTaskId);
                return Json(new { success = false, message = "SubTask not found" });
            }

            subTask.IsCompleted = request.IsCompleted;
            subTask.LastModifiedAt = DateTime.UtcNow;
            
            if (request.IsCompleted && !subTask.CompletedAt.HasValue)
            {
                subTask.CompletedAt = DateTime.UtcNow;
            }
            else if (!request.IsCompleted)
            {
                subTask.CompletedAt = null;
            }

            await _context.SaveChangesAsync();

            // Audit log
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            await _auditService.LogUpdateAsync(
                customUser.Id,
                subTask.OrgId,
                "SubTask",
                subTask.Id,
                $"Status: {(request.IsCompleted ? "Completed" : "Not Completed")}",
                ipAddress,
                userAgent);

            return Json(new { success = true });
        }

        // POST: Tasks/CreateSubTask
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubTask([FromBody] CreateSubTaskRequest request)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var task = await _context.TaskItems.FindAsync(request.TaskId);
            if (task == null)
            {
                return Json(new { success = false, message = "Task not found" });
            }

            var subTask = new SubTaskItem
            {
                TaskId = request.TaskId,
                MatterId = task.MatterId,
                OrgId = task.OrgId,
                Title = request.Title,
                DueDate = request.DueDate,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.SubTaskItems.Add(subTask);
            await _context.SaveChangesAsync();

            // Add assignments if provided
            if (request.Assignments != null && request.Assignments.Any())
            {
                var assignments = request.Assignments.Select(a => new SubTaskAssignment
                {
                    SubTaskItemId = subTask.Id,
                    UserId = a.UserId,
                    AssignmentType = a.AssignmentType ?? "Assignee",
                    Role = a.Role,
                    AssignedAt = DateTime.UtcNow
                }).ToList();

                _context.SubTaskAssignments.AddRange(assignments);
                await _context.SaveChangesAsync();
            }

            // Reload the subtask with all its navigation properties
            var createdSubTask = await _context.SubTaskItems
                .Where(st => st.Id == subTask.Id)
                .Include(st => st.Assignments)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync();

            if (createdSubTask == null)
            {
                return Json(new { success = false, message = "Failed to retrieve created subtask" });
            }

            var subTaskViewModel = MapToViewModel(createdSubTask);
            
            return Json(new { 
                success = true, 
                subTask = subTaskViewModel
            });
        }

        // Helper method to map TaskItem to ViewModel
        private TaskItemViewModel MapToViewModel(TaskItem task)
        {
            return new TaskItemViewModel
            {
                Id = task.Id,
                MatterId = task.MatterId,
                MatterTitle = task.Matter?.Title ?? "",
                Title = task.Title,
                Description = task.Description ?? "",
                Status = task.Status,
                Priority = task.Priority,
                Location = task.Location,
                Order = task.Order,
                DueDate = task.DueDate,
                CompletedAt = task.CompletedAt,
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                TotalSubTasks = task.SubTasks?.Count ?? 0,
                CompletedSubTasks = task.SubTasks?.Count(st => st.IsCompleted) ?? 0,
                Assignments = task.TaskAssignments?.Select(ta => new TaskAssignmentViewModel
                {
                    Id = ta.Id,
                    UserId = ta.UserId,
                    UserName = $"{ta.User.FirstName} {ta.User.LastName}",
                    UserInitials = $"{ta.User.FirstName[0]}{ta.User.LastName[0]}".ToUpper(),
                    AssignmentType = ta.AssignmentType,
                    Role = ta.Role ?? ""
                }).ToList() ?? new List<TaskAssignmentViewModel>(),
                Comments = task.Comments?.Select(c => new TaskCommentViewModel
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    UserName = $"{c.User.FirstName} {c.User.LastName}",
                    Content = c.Content,
                    CreatedAt = c.CreatedAt,
                    TimeAgo = GetTimeAgo(c.CreatedAt)
                }).ToList() ?? new List<TaskCommentViewModel>(),
                SubTasks = task.SubTasks?.Select(st => MapToViewModel(st)).ToList() ?? new List<TaskItemViewModel>()
            };
        }

        // Helper method to map SubTaskItem to ViewModel
        private TaskItemViewModel MapToViewModel(SubTaskItem subTask)
        {
            return new TaskItemViewModel
            {
                Id = subTask.Id,
                MatterId = subTask.MatterId,
                Title = subTask.Title,
                Status = subTask.IsCompleted ? "Completed" : "Pending",
                DueDate = subTask.DueDate,
                CompletedAt = subTask.CompletedAt,
                CreatedAt = subTask.CreatedAt,
                Assignments = subTask.Assignments?.Select(a => new TaskAssignmentViewModel
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    UserName = $"{a.User.FirstName} {a.User.LastName}",
                    UserInitials = $"{a.User.FirstName[0]}{a.User.LastName[0]}".ToUpper(),
                    AssignmentType = a.AssignmentType,
                    Role = a.Role ?? ""
                }).ToList() ?? new List<TaskAssignmentViewModel>()
            };
        }

        private string GetTimeAgo(DateTime date)
        {
            var timeSpan = DateTime.UtcNow - date;
            
            if (timeSpan.TotalMinutes < 1)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}m ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}h ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays}d ago";
            
            return date.ToString("MMM dd, yyyy");
        }
    }

    // Request models
    public class CreateTaskRequest
    {
        public int MatterId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Location { get; set; }
        public int Order { get; set; }
    }

    public class UpdateTaskRequest
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Location { get; set; }
        public int? Order { get; set; }
    }

    public class UpdateStatusRequest
    {
        public int TaskId { get; set; }
        public string Status { get; set; } = "";
        public int Order { get; set; }
    }

    public class AddCommentRequest
    {
        public int TaskItemId { get; set; }
        public string Content { get; set; } = "";
        public List<int> MentionUserIds { get; set; } = new List<int>();
    }

    public class ReactToCommentRequest
    {
        public int CommentId { get; set; }
        public string ReactionType { get; set; } = "like";
    }

    public class AddAssignmentRequest
    {
        public int TaskItemId { get; set; }
        public int UserId { get; set; }
        public string? AssignmentType { get; set; }
        public string? Role { get; set; }
    }

    public class CreateSubTaskRequest
    {
        public int TaskId { get; set; }
        public string Title { get; set; } = "";
        public DateTime? DueDate { get; set; }
        public List<SubTaskAssignmentRequest>? Assignments { get; set; }
    }

    public class SubTaskAssignmentRequest
    {
        public int UserId { get; set; }
        public string? AssignmentType { get; set; }
        public string? Role { get; set; }
    }

    public class AddSubTaskAssignmentRequest
    {
        public int SubTaskItemId { get; set; }
        public int UserId { get; set; }
        public string? AssignmentType { get; set; }
        public string? Role { get; set; }
    }

    public class UpdateSubTaskStatusRequest
    {
        public int SubTaskId { get; set; }
        public bool IsCompleted { get; set; }
    }
}

