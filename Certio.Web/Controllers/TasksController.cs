using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.ViewModels;
using Certio.Domain.Matters;
using Certio.Domain.Tasks;
using Certio.Web.Data;
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

        public TasksController(ApplicationDbContext context, IOptions<GoogleMapsConfiguration> googleMapsConfig)
        {
            _context = context;
            _googleMapsConfig = googleMapsConfig.Value;
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
            
            var org = await _context.Organizations
                .Where(o => o.Id == organizationId)
                .FirstOrDefaultAsync();
            
            ViewBag.OrganizationName = org?.Name ?? "Client";

            // Get all tasks for the organization
            var tasks = await _context.TaskItems
                .Where(t => t.OrgId == organizationId)
                .Include(t => t.Matter)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.User)
                .Include(t => t.Comments)
                    .ThenInclude(c => c.User)
                .Include(t => t.SubTaskItems)
                .OrderBy(t => t.Order)
                .ToListAsync();

            var viewModel = new TasksViewModel
            {
                AllTasks = tasks.Select(t => MapToViewModel(t)).ToList(),
                PlannedTasks = tasks.Where(t => t.Status == "Pending").Select(t => MapToViewModel(t)).ToList(),
                InProgressTasks = tasks.Where(t => t.Status == "InProgress").Select(t => MapToViewModel(t)).ToList(),
                ReviewTasks = tasks.Where(t => t.Status == "Review").Select(t => MapToViewModel(t)).ToList(),
                CompletedTasks = tasks.Where(t => t.Status == "Completed").Select(t => MapToViewModel(t)).ToList(),
                Matters = await _context.Matters
                    .Where(m => m.OrganizationId == organizationId)
                    .Select(m => new MatterOption
                    {
                        Id = m.Id,
                        Title = m.Title,
                        PracticeArea = m.PracticeArea
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

            var taskItem = new TaskItem
            {
                OrgId = primaryOrg.OrganizationId,
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

            return Json(new { success = true, taskId = taskItem.Id });
        }

        // POST: Tasks/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromBody] UpdateTaskRequest request)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var task = await _context.TaskItems.FindAsync(request.Id);
            if (task == null)
            {
                return Json(new { success = false, message = "Task not found" });
            }

            if (!string.IsNullOrEmpty(request.Title))
                task.Title = request.Title;
            if (!string.IsNullOrEmpty(request.Description))
                task.Description = request.Description;
            if (!string.IsNullOrEmpty(request.Status))
                task.Status = request.Status;
            if (!string.IsNullOrEmpty(request.Priority))
                task.Priority = request.Priority;
            if (request.StartedAt.HasValue)
                task.StartedAt = request.StartedAt.Value;
            if (request.DueDate.HasValue)
                task.DueDate = request.DueDate.Value;
            if (!string.IsNullOrEmpty(request.Location))
                task.Location = request.Location;
            if (request.Order.HasValue)
                task.Order = request.Order.Value;

            task.LastModifiedAt = DateTime.UtcNow;

            if (task.Status == "Completed" && !task.CompletedAt.HasValue)
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: Tasks/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest request)
        {
            var task = await _context.TaskItems.FindAsync(request.TaskId);
            if (task == null)
            {
                return Json(new { success = false, message = "Task not found" });
            }

            task.Status = request.Status;
            task.Order = request.Order;
            task.LastModifiedAt = DateTime.UtcNow;

            if (request.Status == "InProgress" && !task.StartedAt.HasValue)
            {
                task.StartedAt = DateTime.UtcNow;
            }

            if (request.Status == "Completed" && !task.CompletedAt.HasValue)
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: Tasks/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var task = await _context.TaskItems.FindAsync(id);
            if (task == null)
            {
                return Json(new { success = false, message = "Task not found" });
            }

            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
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

            return Json(new { 
                success = true, 
                comment = new {
                    id = comment.Id,
                    userName = $"{customUser.FirstName} {customUser.LastName}",
                    content = comment.Content,
                    createdAt = comment.CreatedAt.ToString("MMM dd, yyyy HH:mm")
                }
            });
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
            var assignment = await _context.TaskAssignments.FindAsync(id);
            if (assignment == null)
            {
                return Json(new { success = false, message = "Assignment not found" });
            }

            _context.TaskAssignments.Remove(assignment);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
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
                TotalSubTasks = task.SubTaskItems?.Count ?? 0,
                CompletedSubTasks = task.SubTaskItems?.Count(st => st.Status == "Completed") ?? 0,
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
                SubTasks = task.SubTaskItems?.Select(st => MapToViewModel(st)).ToList() ?? new List<TaskItemViewModel>()
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
    }

    public class AddAssignmentRequest
    {
        public int TaskItemId { get; set; }
        public int UserId { get; set; }
        public string? AssignmentType { get; set; }
        public string? Role { get; set; }
    }
}

