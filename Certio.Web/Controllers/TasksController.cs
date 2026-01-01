using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.ViewModels;
using Certio.Domain.Matters;
using Certio.Domain.Tasks;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Infrastructure.Data;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;
using Certio.Web.Security;
using Certio.Web.Services;
using Certio.Web.Attributes;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Certio.Web.Configuration;

namespace Certio.Web.Controllers
{
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context; // Used for Index action complex queries (optimization pending)
        private readonly GoogleMapsConfiguration _googleMapsConfig;
        private readonly ITaskService _taskService;
        private readonly ISubTaskService _subTaskService;
        private readonly IMatterService _matterService;
        private readonly IFirmRelationshipCacheService _firmRelationshipCache;
        private readonly ILogger<TasksController> _logger;

        public TasksController(
            ApplicationDbContext context, 
            IOptions<GoogleMapsConfiguration> googleMapsConfig,
            ITaskService taskService,
            ISubTaskService subTaskService,
            IMatterService matterService,
            IFirmRelationshipCacheService firmRelationshipCache,
            ILogger<TasksController> logger)
        {
            _context = context;
            _googleMapsConfig = googleMapsConfig.Value;
            _taskService = taskService;
            _subTaskService = subTaskService;
            _matterService = matterService;
            _firmRelationshipCache = firmRelationshipCache;
            _logger = logger;
        }

        // GET: /Client/{orgId}/Tasks or /Tasks/Index
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Tasks")]
        [HttpGet("/Tasks/Index")]
        public async Task<IActionResult> Index(int? orgId = null)
        {
            var (user, organizationId) = GetUserContext();
            if (user == null || organizationId == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Use provided orgId if available
            if (orgId.HasValue)
            {
                organizationId = orgId.Value;
            }

            // Set ViewBag for layout
            ViewBag.OrganizationId = organizationId;
            ViewBag.GoogleMapsApiKey = _googleMapsConfig.ApiKey;
            ViewBag.GoogleMapsEnabled = _googleMapsConfig.Enabled;
            ViewBag.CurrentUserId = user.Id;
            ViewBag.CurrentUserName = $"{user.FirstName} {user.LastName}";
            ViewBag.CurrentUserInitials = $"{user.FirstName[0]}{user.LastName[0]}".ToUpper();
            ViewBag.CurrentUserEmail = user.Email ?? "";

            // Get organization name
            var org = await _context.Organizations
                .Where(o => o.Id == organizationId)
                .FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            ViewBag.OrganizationType = org?.Type ?? Certio.Domain.Organizations.OrganizationType.Client;
            ViewBag.OrganizationEntity = org; // For custom terminology

            // Check if this is a LawFirm organization - if so, aggregate tasks/matters from all accessible clients
            var currentOrg = org;
            
            List<TaskDto> allTasks;
            List<MatterDto> allMatters;
            
            if (currentOrg?.Type == Certio.Domain.Organizations.OrganizationType.LawFirm)
            {
                // Get all accessible client organizations for this user
                var accessibleClients = await _firmRelationshipCache.GetAccessibleClientOrganizationsAsync(user.Id);
                var clientOrgIds = accessibleClients.Select(c => c.Id).ToList();
                
                // Add the LawFirm organization's own ID to include its tasks/matters too
                clientOrgIds.Add(organizationId);
                
                // Aggregate tasks from all accessible organizations
                allTasks = new List<TaskDto>();
                allMatters = new List<MatterDto>();
                
                foreach (var clientOrgId in clientOrgIds)
                {
                    var tasksResult = await _taskService.ListTasksAsync(user.Id, clientOrgId);
                    if (tasksResult.Success)
                    {
                        allTasks.AddRange(tasksResult.Data!);
                    }
                    
                    var mattersResult = await _matterService.ListMattersAsync(user.Id, clientOrgId);
                    if (mattersResult.Success)
                    {
                        allMatters.AddRange(mattersResult.Data!);
                    }
                }
            }
            else
            {
                // For client organizations, show only tasks/matters from this specific client
                var tasksResult = await _taskService.ListTasksAsync(user.Id, organizationId);
                if (!tasksResult.Success)
                {
                    TempData["Error"] = tasksResult.ErrorMessage ?? "Failed to load tasks";
                    return View(new TasksViewModel());
                }
                
                var mattersResult = await _matterService.ListMattersAsync(user.Id, organizationId);
                if (!mattersResult.Success)
                {
                    TempData["Error"] = mattersResult.ErrorMessage ?? "Failed to load matters";
                    return View(new TasksViewModel());
                }
                
                allTasks = tasksResult.Data!;
                allMatters = mattersResult.Data!;
            }

            // Get users in organization (direct members + firm-based members)
            var directUsers = await _context.UserOrganizations
                .Where(uo => uo.OrganizationId == organizationId && uo.IsActive)
                .Select(uo => new UserOption
                {
                    Id = uo.UserId,
                    Name = uo.User.FirstName + " " + uo.User.LastName,
                    Email = uo.User.Email ?? "",
                    Initials = (uo.User.FirstName.Substring(0, 1) + uo.User.LastName.Substring(0, 1)).ToUpper()
                })
                .ToListAsync();
            
            // Get users from law firms that have relationships with this organization
            var firmUsers = await _context.UserOrganizations
                .Where(uo => uo.IsActive && uo.UserType == Certio.Domain.Users.UserTypes.LawFirm)
                .Include(uo => uo.User)
                .Include(uo => uo.Organization)
                    .ThenInclude(o => o.OrganizationRelationships)
                .Where(uo => uo.Organization.OrganizationRelationships.Any(rel =>
                    rel.TargetOrganizationId == organizationId &&
                    rel.IsActive &&
                    !rel.IsDeleted &&
                    rel.RelationshipType == Certio.Domain.Organizations.RelationshipTypes.LawFirmClient &&
                    (!rel.ExpiresAt.HasValue || rel.ExpiresAt.Value > DateTime.UtcNow)))
                .Select(uo => new UserOption
                {
                    Id = uo.User.Id,
                    Name = uo.User.FirstName + " " + uo.User.LastName,
                    Email = uo.User.Email ?? "",
                    Initials = (uo.User.FirstName.Substring(0, 1) + uo.User.LastName.Substring(0, 1)).ToUpper()
                })
                .ToListAsync();
            
            // Combine and deduplicate
            var users = directUsers
                .Union(firmUsers, new UserOptionComparer())
                .OrderBy(u => u.Name)
                .ToList();

            // Map DTOs to ViewModels
            var taskViewModels = allTasks.Select(MapDtoToViewModel).ToList();

            // Organize tasks by status
            var viewModel = new TasksViewModel
            {
                AllTasks = taskViewModels,
                PlannedTasks = taskViewModels.Where(t => t.Status == "Pending").ToList(),
                InProgressTasks = taskViewModels.Where(t => t.Status == "InProgress").ToList(),
                ReviewTasks = taskViewModels.Where(t => t.Status == "Review").ToList(),
                CompletedTasks = taskViewModels.Where(t => t.Status == "Completed").ToList(),
                Matters = allMatters.Select(m => new MatterOption
                {
                    Id = m.Id,
                    Title = m.Title,
                    PracticeArea = m.PracticeArea,
                    AssignedUserIds = m.Assignments.Select(a => a.UserId).ToList()
                }).ToList(),
                Users = users
            };

            return View(viewModel);
        }

        // GET: /Client/{orgId}/Matter/{matterId}/Tasks
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Matter/{matterId:int}/Tasks")]
        public async Task<IActionResult> MatterTasks(int orgId, int matterId)
        {
            var (user, organizationId) = GetUserContext();
            if (user == null || organizationId == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Verify the matter exists and user has access to it
            var matterResult = await _matterService.GetMatterAsync(user.Id, matterId);
            if (!matterResult.Success)
            {
                TempData["Error"] = matterResult.ErrorMessage ?? "Matter not found";
                return RedirectToAction("Index", "Matter");
            }

            var matter = matterResult.Data!;
            
            // Set ViewBag for layout
            ViewBag.OrganizationId = orgId;
            ViewBag.MatterId = matterId;
            ViewBag.GoogleMapsApiKey = _googleMapsConfig.ApiKey;
            ViewBag.GoogleMapsEnabled = _googleMapsConfig.Enabled;
            ViewBag.CurrentUserId = user.Id;
            ViewBag.CurrentUserName = $"{user.FirstName} {user.LastName}";
            ViewBag.CurrentUserInitials = $"{user.FirstName[0]}{user.LastName[0]}".ToUpper();
            ViewBag.CurrentUserEmail = user.Email ?? "";

            // Get organization name
            var org = await _context.Organizations
                .Where(o => o.Id == orgId)
                .FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            ViewBag.OrganizationType = org?.Type ?? Certio.Domain.Organizations.OrganizationType.Client;
            ViewBag.OrganizationEntity = org; // For custom terminology

            // Check if this is a LawFirm organization - if so, aggregate tasks from all accessible clients
            var currentOrg = org;
            
            List<TaskDto> allTasks;
            
            if (currentOrg?.Type == Certio.Domain.Organizations.OrganizationType.LawFirm)
            {
                // Get all accessible client organizations for this user
                var accessibleClients = await _firmRelationshipCache.GetAccessibleClientOrganizationsAsync(user.Id);
                var clientOrgIds = accessibleClients.Select(c => c.Id).ToList();
                
                // Add the LawFirm organization's own ID to include its tasks/matters too
                clientOrgIds.Add(orgId);
                
                // Aggregate tasks from all accessible organizations
                allTasks = new List<TaskDto>();
                
                foreach (var clientOrgId in clientOrgIds)
                {
                    var tasksResult = await _taskService.ListTasksAsync(user.Id, clientOrgId);
                    if (tasksResult.Success)
                    {
                        allTasks.AddRange(tasksResult.Data!);
                    }
                }
            }
            else
            {
                // For client organizations, show only tasks from this specific client
                var tasksResult = await _taskService.ListTasksAsync(user.Id, orgId);
                if (!tasksResult.Success)
                {
                    TempData["Error"] = tasksResult.ErrorMessage ?? "Failed to load tasks";
                    return View("~/Views/Matter/_MatterTasks.cshtml", new TasksViewModel());
                }
                
                allTasks = tasksResult.Data!;
            }

            // Filter tasks to only include those for this matter
            var matterTasks = allTasks.Where(t => t.MatterId == matterId).ToList();

            // Get users in organization (direct members + firm-based members)
            var directUsers = await _context.UserOrganizations
                .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                .Select(uo => new UserOption
                {
                    Id = uo.UserId,
                    Name = uo.User.FirstName + " " + uo.User.LastName,
                    Email = uo.User.Email ?? "",
                    Initials = (uo.User.FirstName.Substring(0, 1) + uo.User.LastName.Substring(0, 1)).ToUpper()
                })
                .ToListAsync();
            
            // Get users from law firms that have relationships with this organization
            var firmUsers = await _context.UserOrganizations
                .Where(uo => uo.IsActive && uo.UserType == Certio.Domain.Users.UserTypes.LawFirm)
                .Include(uo => uo.User)
                .Include(uo => uo.Organization)
                    .ThenInclude(o => o.OrganizationRelationships)
                .Where(uo => uo.Organization.OrganizationRelationships.Any(rel =>
                    rel.TargetOrganizationId == orgId &&
                    rel.IsActive &&
                    !rel.IsDeleted &&
                    rel.RelationshipType == Certio.Domain.Organizations.RelationshipTypes.LawFirmClient &&
                    (!rel.ExpiresAt.HasValue || rel.ExpiresAt.Value > DateTime.UtcNow)))
                .Select(uo => new UserOption
                {
                    Id = uo.User.Id,
                    Name = uo.User.FirstName + " " + uo.User.LastName,
                    Email = uo.User.Email ?? "",
                    Initials = (uo.User.FirstName.Substring(0, 1) + uo.User.LastName.Substring(0, 1)).ToUpper()
                })
                .ToListAsync();
            
            // Combine and deduplicate
            var users = directUsers
                .Union(firmUsers, new UserOptionComparer())
                .OrderBy(u => u.Name)
                .ToList();

            // Map DTOs to ViewModels
            var taskViewModels = matterTasks.Select(MapDtoToViewModel).ToList();

            // Organize tasks by status
            var viewModel = new TasksViewModel
            {
                AllTasks = taskViewModels,
                PlannedTasks = taskViewModels.Where(t => t.Status == "Pending").ToList(),
                InProgressTasks = taskViewModels.Where(t => t.Status == "InProgress").ToList(),
                ReviewTasks = taskViewModels.Where(t => t.Status == "Review").ToList(),
                CompletedTasks = taskViewModels.Where(t => t.Status == "Completed").ToList(),
                Matters = new List<MatterOption> // Only show the current matter
                {
                    new MatterOption
                    {
                        Id = matter.Id,
                        Title = matter.Title,
                        PracticeArea = matter.PracticeArea,
                        AssignedUserIds = matter.Assignments.Select(a => a.UserId).ToList()
                    }
                },
                Users = users
            };

            return View("~/Views/Matter/_MatterTasks.cshtml", viewModel);
        }

        // GET: /Client/{orgId}/Matter/{matterId}/Timeline
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Matter/{matterId:int}/Timeline")]
        public async Task<IActionResult> MatterTimeline(int orgId, int matterId)
        {
            var (user, organizationId) = GetUserContext();
            if (user == null || organizationId == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Verify the matter exists and user has access to it
            var matterResult = await _matterService.GetMatterAsync(user.Id, matterId);
            if (!matterResult.Success)
            {
                TempData["Error"] = matterResult.ErrorMessage ?? "Matter not found";
                return RedirectToAction("Index", "Matter");
            }

            var matter = matterResult.Data!;
            
            // Set ViewBag for layout
            ViewBag.OrganizationId = orgId;
            ViewBag.MatterId = matterId;
            ViewBag.GoogleMapsApiKey = _googleMapsConfig.ApiKey;
            ViewBag.GoogleMapsEnabled = _googleMapsConfig.Enabled;
            ViewBag.CurrentUserId = user.Id;
            ViewBag.CurrentUserName = $"{user.FirstName} {user.LastName}";
            ViewBag.CurrentUserInitials = $"{user.FirstName[0]}{user.LastName[0]}".ToUpper();
            ViewBag.CurrentUserEmail = user.Email ?? "";
            
            // Set the matter's DueDate (Event Date) for the "Day of Event" badge
            ViewBag.MatterDueDate = matter.DueDate?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.MatterTitle = matter.Title;

            // Get organization name
            var org = await _context.Organizations
                .Where(o => o.Id == orgId)
                .FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            ViewBag.OrganizationType = org?.Type ?? Certio.Domain.Organizations.OrganizationType.Client;
            ViewBag.OrganizationEntity = org;

            // Get tasks for this matter (same logic as MatterTasks)
            var currentOrg = org;
            List<TaskDto> allTasks;
            
            if (currentOrg?.Type == Certio.Domain.Organizations.OrganizationType.LawFirm)
            {
                var accessibleClients = await _firmRelationshipCache.GetAccessibleClientOrganizationsAsync(user.Id);
                var clientOrgIds = accessibleClients.Select(c => c.Id).ToList();
                clientOrgIds.Add(orgId);
                
                allTasks = new List<TaskDto>();
                foreach (var clientOrgId in clientOrgIds)
                {
                    var tasksResult = await _taskService.ListTasksAsync(user.Id, clientOrgId);
                    if (tasksResult.Success)
                    {
                        allTasks.AddRange(tasksResult.Data!);
                    }
                }
            }
            else
            {
                var tasksResult = await _taskService.ListTasksAsync(user.Id, orgId);
                if (!tasksResult.Success)
                {
                    TempData["Error"] = tasksResult.ErrorMessage ?? "Failed to load tasks";
                    return View("~/Views/Matter/_MatterTimeline.cshtml", new TasksViewModel());
                }
                allTasks = tasksResult.Data!;
            }

            // Filter tasks to only include those for this matter
            var matterTasks = allTasks.Where(t => t.MatterId == matterId).ToList();

            // Get users in organization
            var directUsers = await _context.UserOrganizations
                .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                .Select(uo => new UserOption
                {
                    Id = uo.UserId,
                    Name = uo.User.FirstName + " " + uo.User.LastName,
                    Email = uo.User.Email ?? "",
                    Initials = (uo.User.FirstName.Substring(0, 1) + uo.User.LastName.Substring(0, 1)).ToUpper()
                })
                .ToListAsync();
            
            var firmUsers = await _context.UserOrganizations
                .Where(uo => uo.IsActive && uo.UserType == Certio.Domain.Users.UserTypes.LawFirm)
                .Include(uo => uo.User)
                .Include(uo => uo.Organization)
                    .ThenInclude(o => o.OrganizationRelationships)
                .Where(uo => uo.Organization.OrganizationRelationships.Any(rel =>
                    rel.TargetOrganizationId == orgId &&
                    rel.IsActive &&
                    !rel.IsDeleted &&
                    rel.RelationshipType == Certio.Domain.Organizations.RelationshipTypes.LawFirmClient &&
                    (!rel.ExpiresAt.HasValue || rel.ExpiresAt.Value > DateTime.UtcNow)))
                .Select(uo => new UserOption
                {
                    Id = uo.User.Id,
                    Name = uo.User.FirstName + " " + uo.User.LastName,
                    Email = uo.User.Email ?? "",
                    Initials = (uo.User.FirstName.Substring(0, 1) + uo.User.LastName.Substring(0, 1)).ToUpper()
                })
                .ToListAsync();
            
            var users = directUsers
                .Union(firmUsers, new UserOptionComparer())
                .OrderBy(u => u.Name)
                .ToList();

            // Map DTOs to ViewModels
            var taskViewModels = matterTasks.Select(MapDtoToViewModel).ToList();

            var viewModel = new TasksViewModel
            {
                AllTasks = taskViewModels,
                PlannedTasks = taskViewModels.Where(t => t.Status == "Pending").ToList(),
                InProgressTasks = taskViewModels.Where(t => t.Status == "InProgress").ToList(),
                ReviewTasks = taskViewModels.Where(t => t.Status == "Review").ToList(),
                CompletedTasks = taskViewModels.Where(t => t.Status == "Completed").ToList(),
                Matters = new List<MatterOption>
                {
                    new MatterOption
                    {
                        Id = matter.Id,
                        Title = matter.Title,
                        PracticeArea = matter.PracticeArea,
                        AssignedUserIds = matter.Assignments.Select(a => a.UserId).ToList()
                    }
                },
                Users = users
            };

            return View("~/Views/Matter/_MatterTimeline.cshtml", viewModel);
        }

        // POST: Tasks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Map request to DTO
            var createDto = new CreateTaskDto
            {
                Title = request.Title,
                Description = request.Description,
                Status = request.Status ?? "Pending",
                Priority = request.Priority ?? "Medium",
                StartedAt = request.StartedAt,
                DueDate = request.DueDate,
                Location = request.Location,
                Order = request.Order
            };

            // Call service to create task
            var result = await _taskService.CreateTaskAsync(
                user.Id,
                request.MatterId,
                createDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return Json(new { success = false, message = result.ErrorMessage });
            }

            // Auto-assign creator
            var assignDto = new AssignTaskDto
            {
                UserId = user.Id,
                AssignmentType = "Assignee",
                Role = "Task Creator"
            };

            await _taskService.AssignTaskAsync(
                user.Id,
                result.Data!.Id,
                assignDto,
                GetIpAddress(),
                GetUserAgent());

            return Json(new { success = true, taskId = result.Data.Id });
        }

        // POST: Tasks/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromBody] UpdateTaskRequest request)
        {
            if (!InputValidator.IsValidId(request.Id))
            {
                _logger.LogWarning("Invalid task ID in Update request: {Id}", request.Id);
                return Json(new { success = false, message = "Invalid task ID" });
            }

            var (user, _) = GetUserContext();
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Update request");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Validate inputs
            if (!string.IsNullOrEmpty(request.Title) && !InputValidator.IsValidString(request.Title, InputValidator.MAX_TITLE_LENGTH))
                return Json(new { success = false, message = "Title is too long" });
            
            if (!string.IsNullOrEmpty(request.Description) && !InputValidator.IsValidString(request.Description, InputValidator.MAX_DESCRIPTION_LENGTH))
                return Json(new { success = false, message = "Description is too long" });
            
            if (!string.IsNullOrEmpty(request.Status) && !InputValidator.IsValidTaskStatus(request.Status))
                return Json(new { success = false, message = "Invalid status value" });
            
            if (!string.IsNullOrEmpty(request.Priority) && !InputValidator.IsValidPriority(request.Priority))
                return Json(new { success = false, message = "Invalid priority value" });

            // Map to DTO
            var updateDto = new UpdateTaskDto
            {
                Title = !string.IsNullOrEmpty(request.Title) ? InputValidator.Sanitize(request.Title, InputValidator.MAX_TITLE_LENGTH) : null,
                Description = !string.IsNullOrEmpty(request.Description) ? InputValidator.Sanitize(request.Description, InputValidator.MAX_DESCRIPTION_LENGTH) : null,
                Status = request.Status,
                Priority = request.Priority,
                Location = !string.IsNullOrEmpty(request.Location) ? InputValidator.Sanitize(request.Location, 200) : null,
                StartedAt = request.StartedAt,
                DueDate = request.DueDate,
                Order = request.Order
            };

            // Call service
            var result = await _taskService.UpdateTaskAsync(
                user.Id,
                request.Id,
                updateDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                _logger.LogWarning("Failed to update task {TaskId} for user {UserId}: {Error}", 
                    request.Id, user.Id, result.ErrorMessage);
                return Json(new { success = false, message = result.ErrorMessage });
            }

            _logger.LogInformation("User {UserId} updated task {TaskId}", user.Id, request.Id);
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

            var (user, _) = GetUserContext();
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for UpdateStatus");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Map to DTO
            var updateDto = new UpdateTaskDto
            {
                Status = request.Status,
                Order = request.Order
            };

            // Call service
            var result = await _taskService.UpdateTaskAsync(
                user.Id,
                request.TaskId,
                updateDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                _logger.LogWarning("Failed to update task status {TaskId} for user {UserId}: {Error}",
                    request.TaskId, user.Id, result.ErrorMessage);
                return Json(new { success = false, message = result.ErrorMessage });
            }

            _logger.LogInformation("User {UserId} updated task {TaskId} status to {Status}", 
                user.Id, request.TaskId, request.Status);
            return Json(new { success = true });
        }

        // GET: Tasks/Get/{id}
        [HttpGet]
        [RequireTaskAccess("id")]
        public async Task<IActionResult> Get(int id)
        {
            if (!InputValidator.IsValidId(id))
            {
                _logger.LogWarning("Invalid task ID in Get request: {Id}", id);
                return Json(new { success = false, message = "Invalid task ID" });
            }

            var (user, _) = GetUserContext();
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Get request");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Call service
            var result = await _taskService.GetTaskAsync(user.Id, id);

            if (!result.Success)
            {
                _logger.LogWarning("User {UserId} attempted to view unauthorized task {TaskId}", user.Id, id);
                return Json(new { success = false, message = result.ErrorMessage });
            }

            // Map DTO to ViewModel (temporary - ideally return DTO directly)
            var taskViewModel = MapDtoToViewModel(result.Data!);
            return Json(new { success = true, task = taskViewModel });
        }

        // POST: Tasks/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireTaskAccess("id")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!InputValidator.IsValidId(id))
            {
                _logger.LogWarning("Invalid task ID in Delete request: {Id}", id);
                return Json(new { success = false, message = "Invalid task ID" });
            }

            var (user, _) = GetUserContext();
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Delete request");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Call service
            var result = await _taskService.DeleteTaskAsync(
                user.Id,
                id,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                _logger.LogWarning("Failed to delete task {TaskId} for user {UserId}: {Error}", 
                    id, user.Id, result.ErrorMessage);
                return Json(new { success = false, message = result.ErrorMessage });
            }

            _logger.LogInformation("User {UserId} deleted task {TaskId}", user.Id, id);
            return Json(new { success = true });
        }

        // GET: Tasks/GetComments
        [HttpGet]
        public async Task<IActionResult> GetComments(int taskId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Call service
            var result = await _taskService.GetTaskCommentsAsync(user.Id, taskId);

            if (!result.Success)
            {
                return Json(new { success = false, message = result.ErrorMessage });
            }

            // Map DTOs to ViewModels
            var comments = result.Data!.Select(c => new TaskCommentViewModel
            {
                Id = c.Id,
                UserId = c.UserId,
                UserName = c.User?.FullName ?? "Unknown User",
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                TimeAgo = GetTimeAgo(c.CreatedAt),
                Mentions = c.Mentions.Select(m => new UserOption
                {
                    Id = m.UserId,
                    Name = m.User?.FullName ?? "Unknown",
                    Email = m.User?.Email ?? "",
                    Initials = m.User?.Initials ?? "??"
                }).ToList(),
                Reactions = c.Reactions
                    .GroupBy(r => r.Emoji)
                    .ToDictionary(g => g.Key, g => g.Count()),
                UserReaction = c.Reactions.FirstOrDefault(r => r.UserId == user.Id)?.Emoji ?? string.Empty
            }).ToList();

            return Json(new { success = true, comments });
        }

        // POST: Tasks/AddComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment([FromBody] AddCommentRequest request)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Map to DTO
            var commentDto = new AddTaskCommentDto
            {
                Content = request.Content,
                MentionedUserIds = request.MentionUserIds ?? new List<int>()
            };

            // Call service
            var result = await _taskService.AddTaskCommentAsync(
                user.Id,
                request.TaskItemId,
                commentDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return Json(new { success = false, message = result.ErrorMessage });
            }

            // Map to ViewModel
            var vm = new TaskCommentViewModel
            {
                Id = result.Data!.Id,
                UserId = result.Data.UserId,
                UserName = result.Data.User?.FullName ?? "Unknown User",
                Content = result.Data.Content,
                CreatedAt = result.Data.CreatedAt,
                TimeAgo = GetTimeAgo(result.Data.CreatedAt),
                Mentions = result.Data.Mentions.Select(m => new UserOption
                {
                    Id = m.UserId,
                    Name = m.User?.FullName ?? "Unknown",
                    Email = m.User?.Email ?? "",
                    Initials = m.User?.Initials ?? "??"
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
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Call service to toggle reaction
            var result = await _taskService.ToggleCommentReactionAsync(
                user.Id,
                request.CommentId,
                request.ReactionType);

            if (!result.Success)
            {
                return Json(new { success = false, message = result.ErrorMessage });
            }

            // Convert dictionary to list format for backward compatibility
            var summary = result.Data!.Select(kvp => new { Type = kvp.Key, Count = kvp.Value }).ToList();

            return Json(new { success = true, reactions = summary });
        }

        // POST: Tasks/AddAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAssignment([FromBody] AddAssignmentRequest request)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Map to DTO
            var assignDto = new AssignTaskDto
            {
                UserId = request.UserId,
                AssignmentType = request.AssignmentType ?? "Assignee",
                Role = request.Role
            };

            // Call service
            var result = await _taskService.AssignTaskAsync(
                user.Id,
                request.TaskItemId,
                assignDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return Json(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, assignmentId = result.Data!.Id });
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

            var (user, _) = GetUserContext();
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for RemoveAssignment");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Get the assignment to find taskId and userId
            var assignment = await _context.TaskAssignments
                .Where(ta => ta.Id == id)
                .Select(ta => new { ta.TaskItemId, ta.UserId })
                .FirstOrDefaultAsync();

            if (assignment == null)
            {
                return Json(new { success = false, message = "Assignment not found" });
            }

            // Call service
            var result = await _taskService.RemoveTaskAssignmentAsync(
                user.Id,
                assignment.TaskItemId,
                assignment.UserId,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                _logger.LogWarning("Failed to remove assignment {AssignmentId}: {Error}", id, result.ErrorMessage);
                return Json(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true });
        }

        // POST: Tasks/AddSubTaskAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSubTaskAssignment([FromBody] AddSubTaskAssignmentRequest request)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Map to DTO
            var assignDto = new AssignSubTaskDto
            {
                UserId = request.UserId,
                AssignmentType = request.AssignmentType ?? "Assignee",
                Role = request.Role
            };

            // Call service
            var result = await _subTaskService.AssignSubTaskAsync(
                user.Id,
                request.SubTaskItemId,
                assignDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return Json(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, assignmentId = result.Data!.Id });
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

            var (user, _) = GetUserContext();
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for RemoveSubTaskAssignment");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Get the assignment to find subTaskId and userId
            var assignment = await _context.SubTaskAssignments
                .Where(sta => sta.Id == id)
                .Select(sta => new { sta.SubTaskItemId, sta.UserId })
                .FirstOrDefaultAsync();

            if (assignment == null)
            {
                return Json(new { success = false, message = "Assignment not found" });
            }

            // Call service
            var result = await _subTaskService.RemoveSubTaskAssignmentAsync(
                user.Id,
                assignment.SubTaskItemId,
                assignment.UserId,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                _logger.LogWarning("Failed to remove subtask assignment {AssignmentId}: {Error}", id, result.ErrorMessage);
                return Json(new { success = false, message = result.ErrorMessage });
            }

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

            var (user, _) = GetUserContext();
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for UpdateSubTaskStatus");
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Call service to toggle completion status
            var result = await _subTaskService.ToggleSubTaskCompletionAsync(
                user.Id,
                request.SubTaskId,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                _logger.LogWarning("Failed to update subtask status {SubTaskId} for user {UserId}: {Error}",
                    request.SubTaskId, user.Id, result.ErrorMessage);
                return Json(new { success = false, message = result.ErrorMessage });
            }

            _logger.LogInformation("User {UserId} updated subtask {SubTaskId} status to {Status}",
                user.Id, request.SubTaskId, request.IsCompleted ? "Completed" : "Not Completed");
            return Json(new { success = true });
        }

        // POST: Tasks/CreateSubTask
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubTask([FromBody] CreateSubTaskRequest request)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            // Map to DTO
            var createDto = new CreateSubTaskDto
            {
                Title = request.Title,
                DueDate = request.DueDate,
                AssignedUserIds = request.Assignments?.Select(a => a.UserId).ToList()
            };

            // Call service
            var result = await _subTaskService.CreateSubTaskAsync(
                user.Id,
                request.TaskId,
                createDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return Json(new { success = false, message = result.ErrorMessage });
            }

            // Map DTO to ViewModel for response
            var subTaskViewModel = new TaskItemViewModel
            {
                Id = result.Data!.Id,
                MatterId = result.Data.MatterId,
                Title = result.Data.Title,
                Status = result.Data.IsCompleted ? "Completed" : "Pending",
                DueDate = result.Data.DueDate,
                CompletedAt = result.Data.CompletedAt,
                CreatedAt = result.Data.CreatedAt,
                Assignments = result.Data.Assignments.Select(a => new TaskAssignmentViewModel
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    UserName = a.User?.FullName ?? "Unknown",
                    UserInitials = a.User?.Initials ?? "??",
                    AssignmentType = a.AssignmentType,
                    Role = a.Role ?? ""
                }).ToList()
            };
            
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

        // ============================================================
        // HELPER METHODS (Added during Phase 2 refactoring)
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
        /// Maps TaskDto to TaskItemViewModel (temporary - ideally use DTOs in views)
        /// </summary>
        private TaskItemViewModel MapDtoToViewModel(TaskDto dto)
        {
            return new TaskItemViewModel
            {
                Id = dto.Id,
                MatterId = dto.MatterId,
                Title = dto.Title,
                Description = dto.Description ?? "",
                Status = dto.Status,
                Priority = dto.Priority,
                Location = dto.Location ?? "",
                Order = dto.Order,
                CreatedAt = dto.CreatedAt,
                StartedAt = dto.StartedAt,
                DueDate = dto.DueDate,
                CompletedAt = dto.CompletedAt,
                MatterTitle = dto.MatterTitle ?? "",
                Assignments = dto.Assignments.Select(a => new TaskAssignmentViewModel
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    UserName = a.User?.FullName ?? "Unknown",
                    UserInitials = a.User?.Initials ?? "??",
                    AssignmentType = a.AssignmentType,
                    Role = a.Role ?? ""
                }).ToList(),
                Comments = dto.Comments.Select(c => new TaskCommentViewModel
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    UserName = c.User?.FullName ?? "Unknown",
                    Content = c.Content,
                    CreatedAt = c.CreatedAt,
                    TimeAgo = GetTimeAgo(c.CreatedAt)
                }).ToList(),
                SubTasks = dto.SubTasks.Select(st => new TaskItemViewModel
                {
                    Id = st.Id,
                    MatterId = st.MatterId,
                    Title = st.Title,
                    Status = st.IsCompleted ? "Completed" : "Pending",
                    DueDate = st.DueDate,
                    CompletedAt = st.CompletedAt,
                    CreatedAt = st.CreatedAt,
                    Assignments = st.Assignments.Select(a => new TaskAssignmentViewModel
                    {
                        Id = a.Id,
                        UserId = a.UserId,
                        UserName = a.User?.FullName ?? "Unknown",
                        UserInitials = a.User?.Initials ?? "??",
                        AssignmentType = a.AssignmentType,
                        Role = a.Role ?? ""
                    }).ToList()
                }).ToList(),
                TotalSubTasks = dto.SubTasks.Count,
                CompletedSubTasks = dto.SubTasks.Count(st => st.IsCompleted)
            };
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
    
    // Helper class for deduplicating UserOption by user ID
    public class UserOptionComparer : IEqualityComparer<UserOption>
    {
        public bool Equals(UserOption? x, UserOption? y)
        {
            if (x == null || y == null) return false;
            return x.Id == y.Id;
        }

        public int GetHashCode(UserOption obj)
        {
            return obj.Id.GetHashCode();
        }
    }
}

