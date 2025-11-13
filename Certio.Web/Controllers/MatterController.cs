using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.ViewModels;
using Certio.Domain.Matters;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;
using Certio.Web.Security;
using Certio.Web.Services;
using Certio.Web.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers
{
    [Authorize(Policy = "OrgMember")]
    public class MatterController : Controller
    {
        private readonly ApplicationDbContext _context; // Used for PopulateOrgMembersData helper method only
        private readonly IMatterService _matterService;
        private readonly IFirmRelationshipCacheService _firmRelationshipCache;
        private readonly ILogger<MatterController> _logger;

        public MatterController(
            ApplicationDbContext context,
            IMatterService matterService,
            IFirmRelationshipCacheService firmRelationshipCache,
            ILogger<MatterController> logger)
        {
            _context = context;
            _matterService = matterService;
            _firmRelationshipCache = firmRelationshipCache;
            _logger = logger;
        }

        // GET: Matter
        public async Task<IActionResult> Index()
        {
            var (user, orgId) = GetUserContext();
            if (user == null || orgId == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Check if this is a LawFirm organization - if so, aggregate matters from all accessible clients
            var currentOrg = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId);
            
            List<MatterDto> allMatters;
            
            if (currentOrg?.Type == Certio.Domain.Organizations.OrganizationType.LawFirm)
            {
                // Get all accessible client organizations for this user (respects user's access level and assignments)
                var accessibleClients = await _firmRelationshipCache.GetAccessibleClientOrganizationsAsync(user.Id);
                var clientOrgIds = accessibleClients.Select(c => c.Id).ToList();
                
                // Add the LawFirm organization's own ID to include its matters too
                clientOrgIds.Add(orgId);
                
                // Aggregate matters from all accessible organizations
                allMatters = new List<MatterDto>();
                
                foreach (var clientOrgId in clientOrgIds)
                {
                    var mattersResult = await _matterService.ListMattersAsync(user.Id, clientOrgId);
                    if (mattersResult.Success)
                    {
                        allMatters.AddRange(mattersResult.Data!);
                    }
                }
            }
            else
            {
                // For client organizations, show only matters from this specific client
                var result = await _matterService.ListMattersAsync(user.Id, orgId);

                if (!result.Success)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    _logger.LogWarning("Failed to list matters for user {UserId} in org {OrgId}: {Error}", 
                        user.Id, orgId, result.ErrorMessage);
                    
                    // Return empty view on error
                    var emptyViewModel = new MattersViewModel
                    {
                        Matters = new List<Matter>(),
                        ActiveMattersCount = 0,
                        CompletedMattersCount = 0,
                        InReviewMattersCount = 0,
                        TeamMembersCount = 0
                    };
                    ViewBag.OrganizationId = orgId;
                    return View(emptyViewModel);
                }
                
                allMatters = result.Data!;
            }

            // Map DTOs to domain entities for the view (temporary - views should be updated to use DTOs)
            var matters = allMatters.Select(dto => new Matter
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                Status = dto.Status,
                PracticeArea = dto.PracticeArea,
                AccessLevel = dto.AccessLevel,
                OrganizationId = dto.OrganizationId,
                TeamId = dto.TeamId,
                ClientId = dto.ClientId,
                StartDate = dto.StartDate,
                DueDate = dto.DueDate,
                CompletedDate = dto.CompletedDate,
                PendingDate = dto.PendingDate,
                StatuteOfLimitationsDate = dto.StatuteOfLimitationsDate,
                CreatedAt = dto.CreatedAt,
                LastModifiedDate = dto.LastModifiedDate,
                Assignments = dto.Assignments.Select(a => new MatterAssignment
                {
                    Id = a.Id,
                    MatterId = a.MatterId,
                    UserId = a.UserId,
                    AssignmentType = a.AssignmentType,
                    Role = a.Role,
                    IsNotifyRecipient = a.IsNotifyRecipient,
                    AssignedAt = a.AssignedAt
                }).ToList()
            }).ToList();

            var viewModel = new MattersViewModel
            {
                Matters = matters,
                ActiveMattersCount = matters.Count(p => p.Status == "In Progress"),
                CompletedMattersCount = matters.Count(p => p.Status == "Completed"),
                InReviewMattersCount = matters.Count(p => p.Status == "Review"),
                TeamMembersCount = await _context.UserOrganizations
                    .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                    .CountAsync()
            };

            ViewBag.OrganizationId = orgId;
            
            // Add Status and PracticeArea options for dropdowns
            ViewBag.Statuses = new List<string>
            {
                "Planning",
                "In Progress",
                "Review",
                "On Hold",
                "Completed"
            };
            
            ViewBag.PracticeAreas = new List<string>
            {
                "Business Formation / Compliance",
                "Commercial Litigation",
                "Construction",
                "Corporate Litigation",
                "Criminal",
                "Elder",
                "Employment / Labor",
                "Family",
                "Healthcare",
                "Intellectual Property",
                "Medical Malpractice",
                "Personal Injury",
                "Privacy / Information Security",
                "Product Liability",
                "Real Estate",
                "Securities / Mergers & Acquisitions",
                "Sports / Entertainment / Gaming",
                "Tax",
                "Trusts",
                "Wills & Estates",
                "Other"
            };
            
            return View(viewModel);
        }

        // GET: /Client/{orgId}/Matter/Details/{id}
        [HttpGet("/Client/{orgId:int}/Matter/Details/{id:int}")]
        [ViewAudit("Matter", "id")]
        // TEMPORARILY DISABLED FOR DEBUGGING: [RequireMatterAccess("id")]
        public async Task<IActionResult> Details(int orgId, int? id)
        {
            if (!id.HasValue || !InputValidator.IsValidId(id.Value))
            {
                _logger.LogWarning("Invalid matter ID in Details request: {Id}", id);
                return NotFound();
            }

            var user = HttpContext.Items["CustomUser"] as User;
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Details request");
                return RedirectToAction("Index", "Home");
            }

            // Call service to get matter with permission check
            var result = await _matterService.GetMatterAsync(user.Id, id.Value);

            if (!result.Success)
            {
                return HandleServiceError(result);
            }

            // Map DTO to domain entity
            var dto = result.Data!;
            
            // If the orgId in the route doesn't match the matter's actual organization,
            // redirect to the correct URL with the matter's organization ID
            if (orgId != dto.OrganizationId)
            {
                _logger.LogInformation(
                    "Redirecting matter {MatterId} from route org {RouteOrgId} to correct org {MatterOrgId}",
                    id.Value, orgId, dto.OrganizationId);
                return RedirectToAction("Details", new { orgId = dto.OrganizationId, id = id.Value });
            }
            var matter = new Matter
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                Status = dto.Status,
                PracticeArea = dto.PracticeArea,
                AccessLevel = dto.AccessLevel,
                OrganizationId = dto.OrganizationId,
                TeamId = dto.TeamId,
                ClientId = dto.ClientId,
                StartDate = dto.StartDate,
                DueDate = dto.DueDate,
                CompletedDate = dto.CompletedDate,
                PendingDate = dto.PendingDate,
                StatuteOfLimitationsDate = dto.StatuteOfLimitationsDate,
                StatuteOfLimitationsSatisfied = dto.StatuteOfLimitationsSatisfied,
                ClientGoals = dto.ClientGoals,
                LegalRequirements = dto.LegalRequirements,
                Notes = dto.Notes,
                CreatedAt = dto.CreatedAt,
                LastModifiedDate = dto.LastModifiedDate,
                Assignments = dto.Assignments
                    .Where(a => a.User != null) // Only include assignments with user data
                    .Select(a => new MatterAssignment
                    {
                        Id = a.Id,
                        MatterId = a.MatterId,
                        UserId = a.UserId,
                        AssignmentType = a.AssignmentType,
                        Role = a.Role,
                        IsNotifyRecipient = a.IsNotifyRecipient,
                        AssignedAt = a.AssignedAt,
                        User = a.User != null ? new User
                        {
                            Id = a.User.Id,
                            FirstName = a.User.FirstName,
                            LastName = a.User.LastName,
                            Email = a.User.Email,
                            PhoneNumber = a.User.PhoneNumber
                        } : null
                    }).ToList()
            };

            // Get all tasks for this matter
            var tasks = await _context.TaskItems
                .Include(t => t.TaskAssignments)
                .ThenInclude(ta => ta.User)
                .Where(t => t.MatterId == id.Value && !t.IsDeleted)
                .ToListAsync();

            // Calculate statistics
            var now = DateTime.UtcNow;
            var sevenDaysAgo = now.AddDays(-7);
            var sevenDaysFromNow = now.AddDays(7);

            var viewModel = new MatterDetailsViewModel
            {
                Matter = matter,
                
                // Task statistics (last 7 days)
                TasksCompletedLast7Days = tasks.Count(t => 
                    t.Status == "Completed" && 
                    t.CompletedAt.HasValue && 
                    t.CompletedAt.Value >= sevenDaysAgo),
                    
                TasksUpdatedLast7Days = tasks.Count(t => 
                    t.ModifiedAt.HasValue && 
                    t.ModifiedAt.Value >= sevenDaysAgo),
                    
                TasksCreatedLast7Days = tasks.Count(t => 
                    t.CreatedAt >= sevenDaysAgo),
                    
                TasksDueSoon = tasks.Count(t => 
                    t.DueDate.HasValue && 
                    t.DueDate.Value >= now && 
                    t.DueDate.Value <= sevenDaysFromNow &&
                    t.Status != "Completed"),
                
                // Priority breakdown
                HighPriorityCount = tasks.Count(t => t.Priority == "High"),
                MediumPriorityCount = tasks.Count(t => t.Priority == "Medium"),
                LowPriorityCount = tasks.Count(t => t.Priority == "Low"),
                CriticalPriorityCount = tasks.Count(t => t.Priority == "Critical"),
                TotalTasksCount = tasks.Count,
                
                // Status distribution
                TasksPending = tasks.Count(t => t.Status == "Pending"),
                TasksInProgress = tasks.Count(t => t.Status == "InProgress" || t.Status == "In Progress"),
                TasksInReview = tasks.Count(t => t.Status == "Review"),
                TasksCompleted = tasks.Count(t => t.Status == "Completed"),
                
                // Recent activity (last 10 items)
                RecentActivity = tasks
                    .Where(t => t.ModifiedAt.HasValue || t.CompletedAt.HasValue)
                    .OrderByDescending(t => t.ModifiedAt ?? t.CompletedAt ?? t.CreatedAt)
                    .Take(10)
                    .Select(t => new RecentActivityItem
                    {
                        Title = t.Title,
                        Description = t.Status == "Completed" ? "Task completed" : "Task updated",
                        UserName = t.TaskAssignments.FirstOrDefault()?.User.FirstName + " " + 
                                   t.TaskAssignments.FirstOrDefault()?.User.LastName ?? "Unknown",
                        Timestamp = t.ModifiedAt ?? t.CompletedAt ?? t.CreatedAt,
                        ActivityType = t.Status == "Completed" ? "Completed" : "Updated"
                    })
                    .ToList()
            };

            // Always use the matter's actual organization ID for API calls
            // This ensures calendar, tasks, and communications tabs work correctly for cross-org matters
            ViewBag.OrganizationId = dto.OrganizationId;
            ViewBag.RouteOrganizationId = orgId; // Keep route org for navigation/breadcrumbs if needed
            
            // Add Status and PracticeArea options for dropdowns
            ViewBag.Statuses = new List<string>
            {
                "Planning",
                "In Progress",
                "Review",
                "On Hold",
                "Completed"
            };
            
            ViewBag.PracticeAreas = new List<string>
            {
                "Business Formation / Compliance",
                "Commercial Litigation",
                "Construction",
                "Corporate Litigation",
                "Criminal",
                "Elder",
                "Employment / Labor",
                "Family",
                "Healthcare",
                "Intellectual Property",
                "Medical Malpractice",
                "Personal Injury",
                "Privacy / Information Security",
                "Product Liability",
                "Real Estate",
                "Securities / Mergers & Acquisitions",
                "Sports / Entertainment / Gaming",
                "Tax",
                "Trusts",
                "Wills & Estates",
                "Other"
            };
            
            // Always set the organization name for proper display in the header
            // For cross-organization matter access, show the matter's originating organization name
            // For firm-owned matters, show the firm's organization name
            var displayOrgId = dto.OrganizationId != orgId ? dto.OrganizationId : orgId;
            var displayOrg = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Id == displayOrgId);
            if (displayOrg != null)
            {
                ViewBag.OrganizationName = displayOrg.Name;
                ViewBag.OrganizationType = displayOrg.Type;
            }
            
            return View(viewModel);
        }

        // TEST: Simple endpoint to verify routing works
        [HttpGet("/Client/{orgId:int}/Matter/Create/Test")]
        public IActionResult CreateTest(int orgId)
        {
            _logger.LogWarning("✅ TEST ENDPOINT HIT: /Client/{OrgId}/Matter/Create/Test", orgId);
            return Content($"Test endpoint works! OrgId: {orgId}");
        }

        // GET: Matter/Create
        [RequirePermission(Permission.CreateMatters)]
        [HttpGet("/Client/{orgId:int}/Matter/Create")]
        public async Task<IActionResult> Create(int orgId)
        {
            _logger.LogInformation("Matter/Create GET - Route orgId: {RouteOrgId}, Request Path: {Path}", 
                orgId, HttpContext.Request.Path);
            
            HttpContext.Items["CurrentOrganizationId"] = orgId;

            var (user, resolvedOrgId) = GetUserContext();
            _logger.LogInformation("Matter/Create GET - User: {UserId}, Route OrgId: {RouteOrgId}, Resolved OrgId: {ResolvedOrgId}", 
                user?.Id, orgId, resolvedOrgId);
            
            if (user == null || resolvedOrgId == 0)
            {
                _logger.LogWarning("Matter/Create GET - Redirecting to Home/Index because user={UserId}, resolvedOrgId={ResolvedOrgId}", 
                    user?.Id, resolvedOrgId);
                TempData["ErrorMessage"] = "We couldn't determine which organization to use for the new matter. Please select an organization and try again.";
                return RedirectToAction("Index", "Home");
            }

            if (orgId != resolvedOrgId)
            {
                _logger.LogDebug("Route organization {RouteOrgId} differed from resolved context {ContextOrgId}. Using route organization.", orgId, resolvedOrgId);
            }

            ViewBag.OrganizationId = orgId;

            var viewModel = new MatterFormViewModel();
            await PopulateOrgMembersData(viewModel);
            
            return View(viewModel);
        }

        // POST: Matter/Create
        [HttpPost("/Client/{orgId:int}/Matter/Create")]
        [ValidateAntiForgeryToken]
        [RequirePermission(Permission.CreateMatters)]
        public async Task<IActionResult> Create(int orgId, MatterFormViewModel model, string action)
        {
            HttpContext.Items["CurrentOrganizationId"] = orgId;

            var (user, resolvedOrgId) = GetUserContext();
            if (user == null || resolvedOrgId == 0)
            {
                TempData["ErrorMessage"] = "Your session no longer has an active organization. Please select an organization and try again.";
                return RedirectToAction("Index", "Home");
            }

            if (orgId != resolvedOrgId)
            {
                _logger.LogDebug("Route organization {RouteOrgId} differed from resolved context {ContextOrgId} for matter creation POST. Using route organization.", orgId, resolvedOrgId);
            }

            ViewBag.OrganizationId = orgId;
            
            // Debug logging for assignments
            _logger.LogInformation($"Matter/Create POST - Step: {model.Step}, Action: {action}");
            _logger.LogInformation($"FirmAssignments Count: {model.FirmAssignments?.Count ?? 0}");
            if (model.FirmAssignments != null)
            {
                foreach (var fa in model.FirmAssignments)
                {
                    _logger.LogInformation($"  - FirmAssignment: Type={fa.AssignmentType}, UserId={fa.UserId}, Role={fa.Role}");
                }
            }
            _logger.LogInformation($"RelevantContacts Count: {model.RelevantContacts?.Count ?? 0}");
            if (model.RelevantContacts != null)
            {
                foreach (var rc in model.RelevantContacts)
                {
                    _logger.LogInformation($"  - RelevantContact: UserId={rc.UserId}, Involvement={rc.Involvement}");
                }
            }
            
            // Always populate org members data
            await PopulateOrgMembersData(model);
            
            // Handle multi-step form navigation (UI concern - stays in controller)
            if (action == "next")
            {
                ClearNonCurrentStepErrors(model);
                ModelState.Clear();
                
                if (ValidateCurrentStep(model))
                {
                    _logger.LogInformation($"Step {model.Step} validation passed, moving to step {model.Step + 1}");
                    _logger.LogInformation($"Before step increment - FirmAssignments with UserIds: {model.FirmAssignments?.Count(fa => fa.UserId.HasValue) ?? 0}");
                    
                    // When moving from Step 3 to Step 4, auto-populate PermissionUserIds with assigned users
                    if (model.Step == 3)
                    {
                        var assignedUserIds = new HashSet<int>();
                        
                        // Add all firm assignment users
                        if (model.FirmAssignments != null)
                        {
                            foreach (var fa in model.FirmAssignments.Where(fa => fa.UserId.HasValue))
                            {
                                assignedUserIds.Add(fa.UserId!.Value);
                            }
                        }
                        
                        // Add all relevant contact users
                        if (model.RelevantContacts != null)
                        {
                            foreach (var rc in model.RelevantContacts.Where(rc => rc.UserId.HasValue))
                            {
                                assignedUserIds.Add(rc.UserId!.Value);
                            }
                        }
                        
                        model.PermissionUserIds = assignedUserIds.ToList();
                        // Don't set AccessLevel - let user explicitly choose
                        
                        _logger.LogInformation($"Auto-populated {model.PermissionUserIds.Count} users for matter permissions: {string.Join(", ", model.PermissionUserIds)}");
                    }
                    
                    model.Step++;
                    return View(model);
                }
                _logger.LogInformation($"Step {model.Step} validation failed");
                return View(model);
            }
            else if (action == "previous")
            {
                if (model.Step > 1)
                {
                    ClearCurrentStepData(model);
                    model.Step--;
                }
                ModelState.Clear();
                return View(model);
            }
            else if (action == "create")
            {
                if (!ValidateAllSteps(model))
                {
                    _logger.LogWarning($"Matter creation validation failed. Errors: {string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
                    TempData["ErrorMessage"] = "Please fix validation errors before creating the matter.";
                    return View(model);
                }

                if (user == null || orgId == 0)
                {
                    TempData["ErrorMessage"] = "User or organization not found. Please log in again.";
                    return RedirectToAction("Index", "Home");
                }

                // Map ViewModel to DTO
                var createDto = new CreateMatterDto
                {
                    Title = InputValidator.Sanitize(model.Title, InputValidator.MAX_TITLE_LENGTH),
                    Description = InputValidator.Sanitize(model.Description, InputValidator.MAX_DESCRIPTION_LENGTH),
                    PracticeArea = InputValidator.Sanitize(model.PracticeArea, 100),
                    Status = InputValidator.Sanitize(model.Status, 50),
                    StartDate = model.StartDate,
                    DueDate = model.DueDate,
                    PendingDate = model.PendingDate,
                    StatuteOfLimitationsDate = model.StatuteOfLimitationsDate,
                    AccessLevel = InputValidator.Sanitize(model.AccessLevel, 20),
                    PermissionUserIds = model.PermissionUserIds
                };

                // Call service to create matter
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

                var matterId = result.Data!.Id;

                // Handle assignments after matter creation
                var allAssignments = new List<(int UserId, string AssignmentType, string Role, bool IsNotifyRecipient)>();

                if (model.FirmAssignments != null)
                {
                    foreach (var fa in model.FirmAssignments.Where(fa => fa.UserId.HasValue))
                    {
                        allAssignments.Add((fa.UserId!.Value, fa.AssignmentType, fa.Role ?? "", fa.IsNotifyRecipient));
                    }
                }

                if (model.RelevantContacts != null)
                {
                    foreach (var rc in model.RelevantContacts.Where(rc => rc.UserId.HasValue && !string.IsNullOrWhiteSpace(rc.Involvement)))
                    {
                        allAssignments.Add((rc.UserId!.Value, "RelevantContact", rc.Involvement!, rc.IsNotifyRecipient));
                    }
                }

                // Create assignments via service
                var assignmentErrors = new List<string>();
                foreach (var (userId, assignmentType, role, isNotify) in allAssignments)
                {
                    var assignDto = new AssignUserToMatterDto
                    {
                        UserId = userId,
                        AssignmentType = assignmentType,
                        Role = InputValidator.Sanitize(role, 100),
                        IsNotifyRecipient = isNotify
                    };

                    var assignResult = await _matterService.AssignUserToMatterAsync(
                        user.Id,
                        matterId,
                        assignDto,
                        GetIpAddress(),
                        GetUserAgent());
                    
                    if (!assignResult.Success)
                    {
                        _logger.LogWarning("Failed to assign user {UserId} to matter {MatterId}: {Error}", 
                            userId, matterId, assignResult.ErrorMessage);
                        assignmentErrors.Add($"User {userId} assignment failed: {assignResult.ErrorMessage}");
                    }
                }

                if (assignmentErrors.Any())
                {
                    TempData["WarningMessage"] = $"Matter created successfully, but some assignments failed: {string.Join("; ", assignmentErrors)}";
                }
                else
                {
                TempData["SuccessMessage"] = "Matter created successfully!";
                }
                
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        private bool ValidateCurrentStep(MatterFormViewModel model)
        {
            bool isValid = true;

            if (model.Step == 1)
            {
                // Validate Step 1 fields
                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    ModelState.AddModelError("Title", "Matter title is required");
                    isValid = false;
                }
            }
            else if (model.Step == 2)
            {
                // Validate Step 2 fields
                if (string.IsNullOrWhiteSpace(model.PracticeArea))
                {
                    ModelState.AddModelError("PracticeArea", "Please select a practice area");
                    isValid = false;
                }
                if (string.IsNullOrWhiteSpace(model.Status))
                {
                    ModelState.AddModelError("Status", "Please select a status");
                    isValid = false;
                }
            }
            else if (model.Step == 3)
            {
                // Step 3: require firm role selections
                if (model.FirmAssignments == null || !model.FirmAssignments.Any(fa => fa.AssignmentType == "ResponsibleAttorney" && fa.UserId.HasValue))
                {
                    ModelState.AddModelError("FirmAssignments", "Responsible Attorney is required");
                    isValid = false;
                }
                if (model.FirmAssignments == null || !model.FirmAssignments.Any(fa => fa.AssignmentType == "ResponsibleStaff" && fa.UserId.HasValue))
                {
                    ModelState.AddModelError("FirmAssignments", "Responsible Staff is required");
                    isValid = false;
                }
                if (model.FirmAssignments == null || !model.FirmAssignments.Any(fa => fa.AssignmentType == "OriginatingAttorney" && fa.UserId.HasValue))
                {
                    ModelState.AddModelError("FirmAssignments", "Originating Attorney is required");
                    isValid = false;
                }
            }

            return isValid;
        }

        private bool ValidateAllSteps(MatterFormViewModel model)
        {
            bool isValid = true;

            // Validate Step 1 fields
            if (string.IsNullOrWhiteSpace(model.Title))
            {
                ModelState.AddModelError("Title", "Matter title is required");
                isValid = false;
            }
            if (string.IsNullOrWhiteSpace(model.PracticeArea))
            {
                ModelState.AddModelError("PracticeArea", "Please select a category");
                isValid = false;
            }

            // Validate Step 2 fields
            if (string.IsNullOrWhiteSpace(model.Status))
            {
                ModelState.AddModelError("Status", "Please select a status");
                isValid = false;
            }

            // Step 3 validation - firm assignments are required
            if (model.FirmAssignments == null || !model.FirmAssignments.Any(fa => fa.AssignmentType == "ResponsibleAttorney" && fa.UserId.HasValue))
            {
                ModelState.AddModelError("FirmAssignments", "Responsible Attorney is required");
                isValid = false;
            }
            if (model.FirmAssignments == null || !model.FirmAssignments.Any(fa => fa.AssignmentType == "ResponsibleStaff" && fa.UserId.HasValue))
            {
                ModelState.AddModelError("FirmAssignments", "Responsible Staff is required");
                isValid = false;
            }
            if (model.FirmAssignments == null || !model.FirmAssignments.Any(fa => fa.AssignmentType == "OriginatingAttorney" && fa.UserId.HasValue))
            {
                ModelState.AddModelError("FirmAssignments", "Originating Attorney is required");
                isValid = false;
            }

            // Step 4 validation - AccessLevel required
            if (string.IsNullOrWhiteSpace(model.AccessLevel))
            {
                ModelState.AddModelError("AccessLevel", "Access Level is required");
                isValid = false;
            }
            
            // Validate relevant contacts have involvement text
            if (model.RelevantContacts != null)
            {
                foreach (var rc in model.RelevantContacts.Where(rc => rc.UserId.HasValue))
                {
                    if (string.IsNullOrWhiteSpace(rc.Involvement))
                    {
                        ModelState.AddModelError("RelevantContacts", "The Involvement field is required for all contacts");
                        isValid = false;
                        break; // Only show error once
                    }
                }
            }

            return isValid;
        }

        private void ClearNonCurrentStepErrors(MatterFormViewModel model)
        {
            // This method is now simplified since we clear ModelState completely
            // before validating the current step
        }

        private void ClearCurrentStepData(MatterFormViewModel model)
        {
            if (model.Step == 2)
            {
                // Clear Step 2 data when going back from Step 2 to Step 1
                model.PracticeArea = "";
                model.Status = "";
                model.StartDate = null;
                model.DueDate = null;
                model.PendingDate = null;
                model.StatuteOfLimitationsDate = null;
            }
            else if (model.Step == 3)
            {
                // Clear Step 3 data when going back from Step 3 to Step 2
                model.FirmAssignments = new List<FirmAssignmentViewModel>();
                model.RelevantContacts = new List<RelevantContactViewModel>();
            }
        }

        private async Task PopulateOrgMembersData(MatterFormViewModel model)
        {
            // Get current user and organization context (respects partner org access)
            var (user, orgId) = GetUserContext();
            if (user == null || orgId == 0)
            {
                // If no user context, return empty lists
                model.OrgMembers = new List<OrgMemberOption>();
                return;
            }

            _logger.LogInformation($"PopulateOrgMembersData: Loading members for organization {orgId}");

            // Get all active organization members from the CURRENT organization context (direct membership)
            var directMembers = await _context.UserOrganizations
                .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                .Include(uo => uo.User)
                .Select(uo => new OrgMemberOption
                {
                    Id = uo.User.Id,
                    Name = $"{uo.User.FirstName} {uo.User.LastName}",
                    Email = uo.User.Email,
                    Company = uo.User.Company ?? "No Company"
                })
                .ToListAsync();
            
            _logger.LogInformation($"PopulateOrgMembersData: Found {directMembers.Count} direct members in organization {orgId}");

            // Get users from law firms that have relationships with this organization
            var firmMembers = await _context.UserOrganizations
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
                .Select(uo => new OrgMemberOption
                {
                    Id = uo.User.Id,
                    Name = $"{uo.User.FirstName} {uo.User.LastName}",
                    Email = uo.User.Email,
                    Company = uo.User.Company ?? uo.Organization.Name
                })
                .ToListAsync();
            
            _logger.LogInformation($"PopulateOrgMembersData: Found {firmMembers.Count} law firm members with access to organization {orgId}");

            // Combine and deduplicate (in case a user has both direct and firm-based access)
            var allMembers = directMembers
                .Union(firmMembers, new OrgMemberOptionComparer())
                .OrderBy(m => m.Name)
                .ToList();
            
            _logger.LogInformation($"PopulateOrgMembersData: Total unique members: {allMembers.Count}");

            // If no org members exist, add some sample data
            if (!allMembers.Any())
            {
                allMembers = new List<OrgMemberOption>
                {
                    new OrgMemberOption { Id = 1, Name = "John Smith", Email = "john@acme.com", Company = "Acme Corp" },
                    new OrgMemberOption { Id = 2, Name = "Jane Doe", Email = "jane@techsolutions.com", Company = "Tech Solutions" },
                    new OrgMemberOption { Id = 3, Name = "Bob Johnson", Email = "bob@legalpartners.com", Company = "Legal Partners" }
                };
            }

            model.OrgMembers = allMembers;
        }

        // GET: /Client/{orgId}/Matter/{matterId}/Users - Get users for matter context (includes law firm members)
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Matter/{matterId:int}/Users")]
        public async Task<IActionResult> GetMatterUsers(int orgId, int matterId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            // Verify user has access to this matter
            var matterResult = await _matterService.GetMatterAsync(user.Id, matterId);
            if (!matterResult.Success)
            {
                return BadRequest(new { success = false, message = "Matter not found or access denied" });
            }

            // Get all active organization members from the CURRENT organization context (direct membership)
            var directMembersData = await _context.UserOrganizations
                .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                .Include(uo => uo.User)
                .Select(uo => new
                {
                    Id = uo.User.Id,
                    FirstName = uo.User.FirstName,
                    LastName = uo.User.LastName,
                    Email = uo.User.Email
                })
                .ToListAsync();
            
            // Get users from law firms that have relationships with this organization
            var firmMembersData = await _context.UserOrganizations
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
                .Select(uo => new
                {
                    Id = uo.User.Id,
                    FirstName = uo.User.FirstName,
                    LastName = uo.User.LastName,
                    Email = uo.User.Email
                })
                .ToListAsync();
            
            // Convert to UserDto with proper initials generation
            var directMembers = directMembersData.Select(u => new UserDto
            {
                Id = u.Id,
                Name = $"{u.FirstName} {u.LastName}".Trim(),
                Initials = GetInitials(u.FirstName, u.LastName),
                Email = u.Email
            }).ToList();
            
            var firmMembers = firmMembersData.Select(u => new UserDto
            {
                Id = u.Id,
                Name = $"{u.FirstName} {u.LastName}".Trim(),
                Initials = GetInitials(u.FirstName, u.LastName),
                Email = u.Email
            }).ToList();
            
            // Combine and deduplicate (in case a user has both direct and firm-based access)
            var allUsers = directMembers
                .Union(firmMembers, new UserDtoComparer())
                .OrderBy(u => u.Name)
                .Select(u => new
                {
                    id = u.Id,
                    name = u.Name,
                    initials = u.Initials,
                    email = u.Email
                })
                .ToList();

            return Json(new { success = true, users = allUsers });
        }

        // Helper method to generate user initials
        private string GetInitials(string firstName, string lastName)
        {
            var first = !string.IsNullOrWhiteSpace(firstName) ? firstName[0].ToString().ToUpper() : "";
            var last = !string.IsNullOrWhiteSpace(lastName) ? lastName[0].ToString().ToUpper() : "";
            return first + last;
        }

        // Helper class for user data
        private class UserDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Initials { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }

        // Helper class for deduplicating users by user ID
        private class UserDtoComparer : IEqualityComparer<UserDto>
        {
            public bool Equals(UserDto? x, UserDto? y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                return x.Id == y.Id;
            }

            public int GetHashCode(UserDto obj)
            {
                return obj.Id.GetHashCode();
            }
        }
        
        // Helper class for deduplicating OrgMemberOption by user ID
        private class OrgMemberOptionComparer : IEqualityComparer<OrgMemberOption>
        {
            public bool Equals(OrgMemberOption? x, OrgMemberOption? y)
            {
                if (x == null || y == null) return false;
                return x.Id == y.Id;
            }

            public int GetHashCode(OrgMemberOption obj)
            {
                return obj.Id.GetHashCode();
            }
        }

        // GET: Matter/Edit/5
        [RequireMatterOperation(Permission.EditMatters, "id")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue || !InputValidator.IsValidId(id.Value))
            {
                _logger.LogWarning("Invalid matter ID in Edit request: {Id}", id);
                return NotFound();
            }

            var (user, orgId) = GetUserContext();
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Edit request");
                return RedirectToAction("Index", "Home");
            }

            // Call service to get matter with permission check
            var result = await _matterService.GetMatterAsync(user.Id, id.Value);

            if (!result.Success)
            {
                _logger.LogWarning("User {UserId} attempted to edit unauthorized matter {MatterId}", user.Id, id.Value);
                return HandleServiceError(result);
            }

            var dto = result.Data!;
            ViewBag.OrganizationId = dto.OrganizationId;

            var viewModel = new MatterFormViewModel
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                PracticeArea = dto.PracticeArea,
                Status = dto.Status,
                StartDate = dto.StartDate,
                DueDate = dto.DueDate,
                PendingDate = dto.PendingDate,
                StatuteOfLimitationsDate = dto.StatuteOfLimitationsDate,
                FirmAssignments = dto.Assignments
                    .Where(a => a.AssignmentType != "RelevantContact")
                    .Select(a => new FirmAssignmentViewModel
                    {
                        AssignmentType = a.AssignmentType,
                        UserId = a.UserId,
                        Role = a.Role,
                        IsNotifyRecipient = a.IsNotifyRecipient
                    }).ToList(),
                RelevantContacts = dto.Assignments
                    .Where(a => a.AssignmentType == "RelevantContact")
                    .Select(a => new RelevantContactViewModel
                    {
                        UserId = a.UserId,
                        Involvement = a.Role,
                        IsNotifyRecipient = a.IsNotifyRecipient
                    }).ToList()
            };

            return View(viewModel);
        }

        // POST: Matter/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireMatterOperation(Permission.EditMatters, "id")]
        public async Task<IActionResult> Edit(int id, MatterFormViewModel model)
        {
            if (!InputValidator.IsValidId(id) || id != model.Id)
            {
                _logger.LogWarning("Invalid ID mismatch in Edit POST: {Id} vs {ModelId}", id, model.Id);
                return NotFound();
            }

            var (user, orgId) = GetUserContext();
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Edit POST");
                return RedirectToAction("Index", "Home");
            }

            ViewBag.OrganizationId = orgId;

            // Validate input
            if (!InputValidator.IsValidString(model.Title, InputValidator.MAX_TITLE_LENGTH, required: true))
            {
                ModelState.AddModelError("Title", "Title is required and must be less than 200 characters");
            }
            if (!InputValidator.IsValidString(model.Description, InputValidator.MAX_DESCRIPTION_LENGTH, required: false))
            {
                ModelState.AddModelError("Description", "Description must be less than 5000 characters");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Map ViewModel to UpdateDto
            var updateDto = new UpdateMatterDto
            {
                Title = model.Title,
                Description = model.Description,
                PracticeArea = model.PracticeArea,
                Status = model.Status,
                StartDate = model.StartDate,
                DueDate = model.DueDate,
                PendingDate = model.PendingDate,
                StatuteOfLimitationsDate = model.StatuteOfLimitationsDate
            };

            // Call service to update matter
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

            // Handle assignment updates - remove all existing and add new ones
            // First, get current assignments
            var currentMatter = await _matterService.GetMatterAsync(user.Id, id);
            if (currentMatter.Success && currentMatter.Data!.Assignments.Any())
            {
                // Remove all existing assignments
                foreach (var assignment in currentMatter.Data.Assignments)
                {
                    await _matterService.RemoveUserFromMatterAsync(
                        user.Id,
                        id,
                        assignment.UserId,
                        GetIpAddress(),
                        GetUserAgent());
                }
            }

            // Add new assignments
            var allAssignments = new List<(int UserId, string AssignmentType, string Role, bool IsNotifyRecipient)>();

            if (model.FirmAssignments != null)
            {
                foreach (var fa in model.FirmAssignments.Where(fa => fa.UserId.HasValue))
                {
                    allAssignments.Add((fa.UserId!.Value, fa.AssignmentType, fa.Role ?? "", fa.IsNotifyRecipient));
                }
            }

            if (model.RelevantContacts != null)
            {
                foreach (var rc in model.RelevantContacts.Where(rc => rc.UserId.HasValue && !string.IsNullOrWhiteSpace(rc.Involvement)))
                {
                    allAssignments.Add((rc.UserId!.Value, "RelevantContact", rc.Involvement!, rc.IsNotifyRecipient));
                }
            }

            foreach (var (userId, assignmentType, role, isNotify) in allAssignments)
            {
                var assignDto = new AssignUserToMatterDto
                {
                    UserId = userId,
                    AssignmentType = assignmentType,
                    Role = role,
                    IsNotifyRecipient = isNotify
                };

                await _matterService.AssignUserToMatterAsync(
                    user.Id,
                    id,
                    assignDto,
                    GetIpAddress(),
                    GetUserAgent());
            }

            TempData["SuccessMessage"] = "Matter updated successfully!";
            _logger.LogInformation("User {UserId} updated matter {MatterId} in org {OrgId}", user.Id, id, orgId);
            
            return RedirectToAction(nameof(Index));
        }

        // GET: Matter/Delete/5
        [RequireMatterOperation(Permission.DeleteMatters, "id")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue || !InputValidator.IsValidId(id.Value))
            {
                _logger.LogWarning("Invalid matter ID in Delete request: {Id}", id);
                return NotFound();
            }

            var (user, _) = GetUserContext();
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Delete request");
                return RedirectToAction("Index", "Home");
            }

            // Call service to get matter with permission check
            var result = await _matterService.GetMatterAsync(user.Id, id.Value);

            if (!result.Success)
            {
                _logger.LogWarning("User {UserId} attempted to access delete page for unauthorized matter {MatterId}", user.Id, id.Value);
                return HandleServiceError(result);
            }

            // Map DTO to entity for view
            var dto = result.Data!;
            var matter = new Matter
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                Status = dto.Status,
                PracticeArea = dto.PracticeArea,
                OrganizationId = dto.OrganizationId,
                CreatedAt = dto.CreatedAt
            };

            ViewBag.OrganizationId = dto.OrganizationId;
            return View(matter);
        }

        // POST: Matter/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!InputValidator.IsValidId(id))
            {
                _logger.LogWarning("Invalid matter ID in DeleteConfirmed: {Id}", id);
                return NotFound();
            }

            var (user, _) = GetUserContext();
            if (user == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for DeleteConfirmed");
                return RedirectToAction("Index", "Home");
            }

            // Call service to delete matter
            var result = await _matterService.DeleteMatterAsync(
                user.Id,
                id,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                _logger.LogWarning("Failed to delete matter {MatterId} for user {UserId}: {Error}", 
                    id, user.Id, result.ErrorMessage);
                TempData["ErrorMessage"] = result.ErrorMessage ?? "An error occurred while deleting the matter.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Matter deleted successfully!";
            _logger.LogInformation("User {UserId} deleted matter {MatterId}", user.Id, id);
            
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        /// <summary>
        /// Extracts current user and organization context from HttpContext
        /// </summary>
        private (User? User, int OrganizationId) GetUserContext()
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            // Use CurrentOrganizationId from middleware (handles partner org context)
            var orgId = HttpContext.Items.TryGetValue("CurrentOrganizationId", out var orgObj) && orgObj is int currentOrgId
                ? currentOrgId
                : customUser?.GetPrimaryOrganization()?.OrganizationId ?? 0;
            
            _logger.LogInformation($"GetUserContext: User={customUser?.Id}, OrgId={orgId} (from {(HttpContext.Items.ContainsKey("CurrentOrganizationId") ? "CurrentOrganizationId" : "PrimaryOrg")})");
            
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

        // POST: /Client/{orgId}/Matter/{matterId}/Assignment - Assign user to matter
        [Authorize(Policy = "OrgMember")]
        [HttpPost("/Client/{orgId:int}/Matter/{matterId:int}/Assignment")]
        public async Task<IActionResult> AssignUserToMatter(int orgId, int matterId, [FromBody] AssignUserToMatterDto assignDto)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "User not authenticated" });
            }

            // Verify matter belongs to organization
            var matterResult = await _matterService.GetMatterAsync(user.Id, matterId);
            if (!matterResult.Success || matterResult.Data!.OrganizationId != orgId)
            {
                return BadRequest(new { success = false, message = "Matter not found or access denied" });
            }

            // Remove existing assignment of this type if exists (different user)
            // Note: MatterAssignmentDto from service already excludes removed assignments
            var existingTypeAssignment = matterResult.Data.Assignments
                .FirstOrDefault(a => a.AssignmentType == assignDto.AssignmentType);
            
            if (existingTypeAssignment != null)
            {
                if (existingTypeAssignment.UserId == assignDto.UserId)
                {
                    // Same user already assigned - return success (no change needed)
                    return Json(new { success = true, message = "User already assigned" });
                }
                
                // Remove the existing assignment for this type (different user)
                await _matterService.RemoveUserFromMatterAsync(
                    user.Id,
                    matterId,
                    existingTypeAssignment.UserId,
                    GetIpAddress(),
                    GetUserAgent());
            }

            // Add new assignment
            var result = await _matterService.AssignUserToMatterAsync(
                user.Id,
                matterId,
                assignDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, assignment = result.Data });
        }

        // DELETE: /Client/{orgId}/Matter/{matterId}/Assignment/{assignmentId} - Remove assignment by ID
        [Authorize(Policy = "OrgMember")]
        [HttpDelete("/Client/{orgId:int}/Matter/{matterId:int}/Assignment/{assignmentId:int}")]
        public async Task<IActionResult> RemoveAssignment(int orgId, int matterId, int assignmentId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "User not authenticated" });
            }

            // Get matter to find the assignment
            var matterResult = await _matterService.GetMatterAsync(user.Id, matterId);
            if (!matterResult.Success || matterResult.Data!.OrganizationId != orgId)
            {
                return BadRequest(new { success = false, message = "Matter not found or access denied" });
            }

            var assignment = matterResult.Data.Assignments.FirstOrDefault(a => a.Id == assignmentId);
            if (assignment == null)
            {
                return NotFound(new { success = false, message = "Assignment not found" });
            }

            var result = await _matterService.RemoveUserFromMatterAsync(
                user.Id,
                matterId,
                assignment.UserId,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true });
        }

        // PUT: /Client/{orgId}/Matter/{matterId} - Update matter fields
        [Authorize(Policy = "OrgMember")]
        [HttpPut("/Client/{orgId:int}/Matter/{matterId:int}")]
        public async Task<IActionResult> UpdateMatter(int orgId, int matterId, [FromBody] UpdateMatterDto updateDto)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "User not authenticated" });
            }

            // Verify matter belongs to organization
            var matterResult = await _matterService.GetMatterAsync(user.Id, matterId);
            if (!matterResult.Success || matterResult.Data!.OrganizationId != orgId)
            {
                return BadRequest(new { success = false, message = "Matter not found or access denied" });
            }

            var result = await _matterService.UpdateMatterAsync(
                user.Id,
                matterId,
                updateDto,
                GetIpAddress(),
                GetUserAgent());

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, matter = result.Data });
        }
    }
}
