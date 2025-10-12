using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.ViewModels;
using Certio.Domain.Matters;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Certio.Application.Interfaces;
using Certio.Web.Security;
using Certio.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers
{
    [Authorize(Policy = "OrgMember")]
    public class MatterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthorizationHelper _authHelper;
        private readonly IAuditService _auditService;
        private readonly ILogger<MatterController> _logger;

        public MatterController(
            ApplicationDbContext context,
            AuthorizationHelper authHelper,
            IAuditService auditService,
            ILogger<MatterController> logger)
        {
            _context = context;
            _authHelper = authHelper;
            _auditService = auditService;
            _logger = logger;
        }

        // GET: Matter
        public async Task<IActionResult> Index()
        {
            // Get current user and their organization
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Get user's organization membership to determine user type
            var userOrgMembership = customUser.GetOrganizationMembership(primaryOrg.OrganizationId);
            var userType = userOrgMembership?.UserType ?? UserTypes.Client;

            // Build query with proper filtering based on user type and assignments
            IQueryable<Matter> mattersQuery = _context.Matters
                .Where(m => m.OrganizationId == primaryOrg.OrganizationId)
                .Include(m => m.Assignments)
                    .ThenInclude(a => a.User)
                .Include(m => m.Permissions);

            // Filter based on AccessLevel and user assignments
            // Users should see matters where:
            // 1. AccessLevel is "Everyone" (all org members can see)
            // 2. AccessLevel is "Specific" AND user has explicit permission (via MatterPermissions)
            // 3. User is assigned to the matter (via MatterAssignments)
            mattersQuery = mattersQuery.Where(m => 
                // All org members see "Everyone" access level matters
                m.AccessLevel == "Everyone" ||
                // Users with specific permissions
                m.Permissions.Any(p => p.UserId == customUser.Id && p.RevokedAt == null) ||
                // Users assigned to the matter
                m.Assignments.Any(a => a.UserId == customUser.Id && a.RemovedAt == null));

            var matters = await mattersQuery.ToListAsync();

            // If no matters exist, add some sample data for demonstration
            if (!matters.Any())
            {
                var sampleMatters = new List<Matter>
                {
                    new Matter
                    {
                        Title = "Contract Review Automation",
                        Description = "AI-powered contract analysis and risk assessment system",
                        Status = "In Progress",
                        PracticeArea = "AI/ML",
                        OrganizationId = primaryOrg.OrganizationId,
                        DueDate = new DateTime(2024, 2, 15),
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    },
                    new Matter
                    {
                        Title = "Client Portal Dashboard",
                        Description = "Secure client access portal with document sharing capabilities",
                        Status = "Review",
                        PracticeArea = "Frontend",
                        OrganizationId = primaryOrg.OrganizationId,
                        DueDate = new DateTime(2024, 2, 28),
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    },
                    new Matter
                    {
                        Title = "Compliance Tracking System",
                        Description = "Automated regulatory compliance monitoring and reporting",
                        Status = "Planning",
                        PracticeArea = "Backend",
                        OrganizationId = primaryOrg.OrganizationId,
                        DueDate = new DateTime(2024, 3, 10),
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    },
                    new Matter
                    {
                        Title = "Legal Research Assistant",
                        Description = "Natural language processing for legal document search",
                        Status = "Completed",
                        PracticeArea = "AI/ML",
                        OrganizationId = primaryOrg.OrganizationId,
                        DueDate = new DateTime(2024, 1, 30),
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    }
                };

                _context.Matters.AddRange(sampleMatters);
                await _context.SaveChangesAsync();
                
                // Reload matters from database
                matters = await _context.Matters
                    .Where(p => p.OrganizationId == primaryOrg.OrganizationId)
                    .Include(p => p.Assignments)
                        .ThenInclude(a => a.User)
                    .ToListAsync();
            }

            var viewModel = new MattersViewModel
            {
                Matters = matters,
                ActiveMattersCount = matters.Count(p => p.Status == "In Progress"),
                CompletedMattersCount = matters.Count(p => p.Status == "Completed"),
                InReviewMattersCount = matters.Count(p => p.Status == "Review"),
                TeamMembersCount = await _context.UserOrganizations
                    .Where(uo => uo.OrganizationId == primaryOrg.OrganizationId && uo.IsActive)
                    .CountAsync()
            };

            // Set ViewBag for client layout navigation
            ViewBag.OrganizationId = primaryOrg.OrganizationId;

            return View(viewModel);
        }

        // GET: Matter/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            // Input validation
            if (!id.HasValue || !InputValidator.IsValidId(id.Value))
            {
                _logger.LogWarning("Invalid matter ID in Details request: {Id}", id);
                return NotFound();
            }

            // Get current user and their organization
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Details request");
                return RedirectToAction("Index", "Home");
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                _logger.LogWarning("User {UserId} has no primary organization", customUser.Id);
                return RedirectToAction("Index", "Home");
            }

            // Use AuthorizationHelper for secure retrieval
            var matter = await _authHelper.GetMatterIfAuthorizedAsync(id.Value, customUser.Id, primaryOrg.OrganizationId, HttpContext);

            if (matter == null)
            {
                // Return consistent 404 to prevent information disclosure
                _logger.LogWarning("User {UserId} attempted to access unauthorized matter {MatterId}", customUser.Id, id.Value);
                return NotFound();
            }

            // Audit log for viewing sensitive data
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogOperationAsync(
                customUser.Id,
                primaryOrg.OrganizationId,
                "VIEW",
                "Matter",
                matter.Id,
                ipAddress);

            // Set ViewBag for client layout navigation
            ViewBag.OrganizationId = primaryOrg.OrganizationId;

            return View(matter);
        }

        // GET: Matter/Create
        public async Task<IActionResult> Create()
        {
            // Get current user and their organization
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Set ViewBag for client layout navigation
            ViewBag.OrganizationId = primaryOrg.OrganizationId;

            var viewModel = new MatterFormViewModel();
            
            // Populate org members data
            await PopulateOrgMembersData(viewModel);
            
            return View(viewModel);
        }

        // POST: Matter/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MatterFormViewModel model, string action)
        {
            // Get current user and their organization for ViewBag
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            var primaryOrg = customUser?.GetPrimaryOrganization();
            if (primaryOrg != null)
            {
                ViewBag.OrganizationId = primaryOrg.OrganizationId;
            }

            // Always populate org members data
            await PopulateOrgMembersData(model);
            
            
            if (action == "next")
            {
                // Clear previous validation errors for fields not in current step FIRST
                ClearNonCurrentStepErrors(model);
                
                // Clear ModelState completely before validating current step
                ModelState.Clear();
                
                // Validate current step before moving to next
                if (ValidateCurrentStep(model))
                {
                    model.Step++;
                    return View(model);
                }
                // If validation fails, return to current step with only current step errors
                return View(model);
            }
            else if (action == "previous")
            {
                // Move to previous step - data is already cleared by JavaScript
                if (model.Step > 1)
                {
                    // Ensure server-side state is also cleared for the step we're leaving
                    ClearCurrentStepData(model);
                    model.Step--;
                }
                // Clear any validation errors since we're going back
                ModelState.Clear();
                return View(model);
            }
            else if (action == "create")
            {
                // Validate all required fields before creating
                if (ValidateAllSteps(model))
                {
                    // Validate user and organization (already retrieved at top of method)
                    if (customUser == null)
                    {
                        TempData["ErrorMessage"] = "User not found. Please log in again.";
                        return RedirectToAction("Index", "Home");
                    }

                    if (primaryOrg == null)
                    {
                        TempData["ErrorMessage"] = "Organization not found. Please contact support.";
                        return RedirectToAction("Index", "Home");
                    }

                    // Validate that attorney/staff selections are members of the org
                    var orgUserIds = await _context.UserOrganizations
                        .Where(uo => uo.OrganizationId == primaryOrg.OrganizationId && uo.IsActive)
                        .Select(uo => uo.UserId)
                        .ToListAsync();

                    bool invalidAssignment = false;
                    // Validate firm assignments
                    if (model.FirmAssignments != null)
                    {
                        foreach (var fa in model.FirmAssignments)
                        {
                            if (fa.UserId.HasValue && !orgUserIds.Contains(fa.UserId.Value))
                            {
                                ModelState.AddModelError("FirmAssignments", $"Selected {fa.AssignmentType} is not in your organization.");
                                invalidAssignment = true;
                                break;
                            }
                        }
                    }
                    if (model.RelevantContacts != null)
                    {
                        foreach (var rc in model.RelevantContacts)
                        {
                            if (rc.UserId.HasValue && !orgUserIds.Contains(rc.UserId.Value))
                            {
                                ModelState.AddModelError("RelevantContacts", "One or more relevant contacts are not in your organization.");
                                invalidAssignment = true;
                                break;
                            }
                        }
                    }
                    if (invalidAssignment)
                    {
                        return View(model);
                    }

                    // Sanitize and validate input using InputValidator
                    var matter = new Matter
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
                        OrganizationId = primaryOrg.OrganizationId,
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    };

                    _context.Matters.Add(matter);
                    await _context.SaveChangesAsync();

                    // Audit log the creation
                    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                    var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                    await _auditService.LogCreateAsync(
                        customUser.Id,
                        primaryOrg.OrganizationId,
                        "Matter",
                        matter.Id,
                        ipAddress,
                        userAgent);

                    _logger.LogInformation("User {UserId} created matter {MatterId} ({Title}) in org {OrgId}", 
                        customUser.Id, matter.Id, matter.Title, primaryOrg.OrganizationId);

                    // Create MatterAssignments for firm assignments and relevant contacts
                    var allAssignments = new List<MatterAssignment>();

                    // Add firm assignments
                    if (model.FirmAssignments != null && model.FirmAssignments.Any())
                    {
                        var firmAssignments = model.FirmAssignments
                            .Where(fa => fa.UserId.HasValue)
                            .Select(fa => new MatterAssignment
                            {
                                MatterId = matter.Id,
                                UserId = fa.UserId.Value,
                                AssignmentType = fa.AssignmentType,
                                Role = InputValidator.Sanitize(fa.Role, 100),
                                IsNotifyRecipient = fa.IsNotifyRecipient,
                                AssignedAt = DateTime.UtcNow
                            })
                            .ToList();
                        allAssignments.AddRange(firmAssignments);
                    }

                    // Add relevant contacts
                    if (model.RelevantContacts != null && model.RelevantContacts.Any())
                    {
                        var relevantContactAssignments = model.RelevantContacts
                            .Where(rc => rc.UserId.HasValue && !string.IsNullOrWhiteSpace(rc.Involvement))
                            .Select(rc => new MatterAssignment
                            {
                                MatterId = matter.Id,
                                UserId = rc.UserId.Value,
                                AssignmentType = "RelevantContact",
                                Role = InputValidator.Sanitize(rc.Involvement, 100),
                                IsNotifyRecipient = rc.IsNotifyRecipient,
                                AssignedAt = DateTime.UtcNow
                            })
                            .ToList();
                        allAssignments.AddRange(relevantContactAssignments);
                    }

                    if (allAssignments.Any())
                    {
                        _context.MatterAssignments.AddRange(allAssignments);
                        await _context.SaveChangesAsync();
                    }

                    // Create MatterPermissions when AccessLevel is Specific
                    if (string.Equals(model.AccessLevel, "Specific", StringComparison.OrdinalIgnoreCase) && model.PermissionUserIds != null)
                    {
                        var uniqueUserIds = model.PermissionUserIds.Distinct().ToList();
                        if (uniqueUserIds.Count > 0)
                        {
                            // Filter to only org members for safety
                            var orgMemberIds = await _context.UserOrganizations
                                .Where(uo => uo.OrganizationId == primaryOrg.OrganizationId && uo.IsActive)
                                .Select(uo => uo.UserId)
                                .ToListAsync();

                            var validUserIds = uniqueUserIds.Where(id => orgMemberIds.Contains(id)).ToList();
                            if (validUserIds.Count != uniqueUserIds.Count)
                            {
                                ModelState.AddModelError("PermissionUserIds", "One or more selected users are not in your organization.");
                                return View(model);
                            }

                            var permissions = validUserIds.Select(uid => new MatterPermission
                            {
                                MatterId = matter.Id,
                                UserId = uid,
                                GrantedAt = DateTime.UtcNow,
                                GrantedById = customUser.Id
                            }).ToList();

                            _context.MatterPermissions.AddRange(permissions);
                            await _context.SaveChangesAsync();
                        }
                    }

                    TempData["SuccessMessage"] = "Matter created successfully!";
                    return RedirectToAction(nameof(Index));
                }
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
            // Get current user and their organization
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                // If no user context, return empty lists
                model.OrgMembers = new List<OrgMemberOption>();
                return;
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                // If no organization, return empty lists
                model.OrgMembers = new List<OrgMemberOption>();
                return;
            }

            // Get all active organization members
            var orgMembers = await _context.UserOrganizations
                .Where(uo => uo.OrganizationId == primaryOrg.OrganizationId && uo.IsActive)
                .Include(uo => uo.User)
                .Select(uo => new OrgMemberOption
                {
                    Id = uo.User.Id,
                    Name = $"{uo.User.FirstName} {uo.User.LastName}",
                    Email = uo.User.Email,
                    Company = uo.User.Company ?? "No Company"
                })
                .ToListAsync();

            // If no org members exist, add some sample data
            if (!orgMembers.Any())
            {
                orgMembers = new List<OrgMemberOption>
                {
                    new OrgMemberOption { Id = 1, Name = "John Smith", Email = "john@acme.com", Company = "Acme Corp" },
                    new OrgMemberOption { Id = 2, Name = "Jane Doe", Email = "jane@techsolutions.com", Company = "Tech Solutions" },
                    new OrgMemberOption { Id = 3, Name = "Bob Johnson", Email = "bob@legalpartners.com", Company = "Legal Partners" }
                };
            }

            model.OrgMembers = orgMembers;
        }

        // GET: Matter/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            // Input validation
            if (!id.HasValue || !InputValidator.IsValidId(id.Value))
            {
                _logger.LogWarning("Invalid matter ID in Edit request: {Id}", id);
                return NotFound();
            }

            // Get current user and their organization
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Edit request");
                return RedirectToAction("Index", "Home");
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                _logger.LogWarning("User {UserId} has no primary organization", customUser.Id);
                return RedirectToAction("Index", "Home");
            }

            // SECURITY FIX: Use AuthorizationHelper instead of direct FindAsync
            var matter = await _authHelper.GetMatterIfAuthorizedAsync(id.Value, customUser.Id, primaryOrg.OrganizationId, HttpContext);
            if (matter == null)
            {
                // Return consistent 404 to prevent information disclosure
                _logger.LogWarning("SECURITY: User {UserId} attempted to edit unauthorized matter {MatterId}", customUser.Id, id.Value);
                return NotFound();
            }

            // Set ViewBag for client layout navigation
            ViewBag.OrganizationId = primaryOrg.OrganizationId;

            var viewModel = new MatterFormViewModel
            {
                Id = matter.Id,
                Title = matter.Title,
                Description = matter.Description,
                PracticeArea = matter.PracticeArea,
                Status = matter.Status,
                StartDate = matter.StartDate,
                DueDate = matter.DueDate,
                PendingDate = matter.PendingDate,
                StatuteOfLimitationsDate = matter.StatuteOfLimitationsDate,
                FirmAssignments = matter.Assignments
                    .Where(a => a.AssignmentType != "RelevantContact")
                    .Select(a => new FirmAssignmentViewModel
                    {
                        AssignmentType = a.AssignmentType,
                        UserId = a.UserId,
                        Role = a.Role,
                        IsNotifyRecipient = a.IsNotifyRecipient
                    }).ToList(),
                RelevantContacts = matter.Assignments
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
        public async Task<IActionResult> Edit(int id, MatterFormViewModel model)
        {
            // Input validation
            if (!InputValidator.IsValidId(id) || id != model.Id)
            {
                _logger.LogWarning("Invalid ID mismatch in Edit POST: {Id} vs {ModelId}", id, model.Id);
                return NotFound();
            }

            // Get current user and their organization for ViewBag
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Edit POST");
                return RedirectToAction("Index", "Home");
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                _logger.LogWarning("User {UserId} has no primary organization", customUser.Id);
                return RedirectToAction("Index", "Home");
            }

            ViewBag.OrganizationId = primaryOrg.OrganizationId;

            // Validate input strings
            if (!InputValidator.IsValidString(model.Title, InputValidator.MAX_TITLE_LENGTH, required: true))
            {
                ModelState.AddModelError("Title", "Title is required and must be less than 200 characters");
            }
            if (!InputValidator.IsValidString(model.Description, InputValidator.MAX_DESCRIPTION_LENGTH, required: false))
            {
                ModelState.AddModelError("Description", "Description must be less than 5000 characters");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // SECURITY FIX: Verify user has access before updating
                    var matter = await _authHelper.GetMatterIfAuthorizedAsync(id, customUser.Id, primaryOrg.OrganizationId, HttpContext);
                    if (matter == null)
                    {
                        _logger.LogWarning("SECURITY: User {UserId} attempted to edit unauthorized matter {MatterId}", customUser.Id, id);
                        return NotFound();
                    }

                    matter.Title = model.Title;
                    matter.Description = model.Description;
                    matter.PracticeArea = model.PracticeArea;
                    matter.Status = model.Status;
                    matter.StartDate = model.StartDate;
                    matter.DueDate = model.DueDate;
                    matter.PendingDate = model.PendingDate;
                    matter.StatuteOfLimitationsDate = model.StatuteOfLimitationsDate;
                    matter.LastModifiedDate = DateTime.UtcNow;

                    // Update MatterAssignments
                    var existingAssignments = await _context.MatterAssignments
                        .Where(ma => ma.MatterId == matter.Id)
                        .ToListAsync();

                    // Remove existing assignments
                    _context.MatterAssignments.RemoveRange(existingAssignments);

                    // Add new assignments
                    var allAssignments = new List<MatterAssignment>();

                    // Add firm assignments
                    if (model.FirmAssignments != null && model.FirmAssignments.Any())
                    {
                        var firmAssignments = model.FirmAssignments
                            .Where(fa => fa.UserId.HasValue)
                            .Select(fa => new MatterAssignment
                            {
                                MatterId = matter.Id,
                                UserId = fa.UserId.Value,
                                AssignmentType = fa.AssignmentType,
                                Role = fa.Role ?? "",
                                IsNotifyRecipient = fa.IsNotifyRecipient,
                                AssignedAt = DateTime.UtcNow
                            })
                            .ToList();
                        allAssignments.AddRange(firmAssignments);
                    }

                    // Add relevant contacts
                    if (model.RelevantContacts != null && model.RelevantContacts.Any())
                    {
                        var relevantContactAssignments = model.RelevantContacts
                            .Where(rc => rc.UserId.HasValue && !string.IsNullOrWhiteSpace(rc.Involvement))
                            .Select(rc => new MatterAssignment
                            {
                                MatterId = matter.Id,
                                UserId = rc.UserId.Value,
                                AssignmentType = "RelevantContact",
                                Role = rc.Involvement,
                                IsNotifyRecipient = rc.IsNotifyRecipient,
                                AssignedAt = DateTime.UtcNow
                            })
                            .ToList();
                        allAssignments.AddRange(relevantContactAssignments);
                    }

                    if (allAssignments.Any())
                    {
                        _context.MatterAssignments.AddRange(allAssignments);
                    }

                    _context.Update(matter);
                    await _context.SaveChangesAsync();

                    // Audit log the update
                    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                    var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                    await _auditService.LogUpdateAsync(
                        customUser.Id,
                        primaryOrg.OrganizationId,
                        "Matter",
                        matter.Id,
                        $"Updated: {model.Title}",
                        ipAddress,
                        userAgent);

                    TempData["SuccessMessage"] = "Matter updated successfully!";
                    _logger.LogInformation("User {UserId} updated matter {MatterId} in org {OrgId}", customUser.Id, matter.Id, primaryOrg.OrganizationId);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, "Concurrency error updating matter {MatterId} by user {UserId}", id, customUser.Id);
                    if (!MatterExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Matter/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            // Input validation
            if (!id.HasValue || !InputValidator.IsValidId(id.Value))
            {
                _logger.LogWarning("Invalid matter ID in Delete request: {Id}", id);
                return NotFound();
            }

            // Get current user and their organization
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for Delete request");
                return RedirectToAction("Index", "Home");
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                _logger.LogWarning("User {UserId} has no primary organization", customUser.Id);
                return RedirectToAction("Index", "Home");
            }

            // SECURITY FIX: Verify user has access before showing delete confirmation
            var matter = await _authHelper.GetMatterIfAuthorizedAsync(id.Value, customUser.Id, primaryOrg.OrganizationId, HttpContext);
            if (matter == null)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to access delete page for unauthorized matter {MatterId}", customUser.Id, id.Value);
                return NotFound();
            }

            // Set ViewBag for client layout navigation
            ViewBag.OrganizationId = primaryOrg.OrganizationId;

            return View(matter);
        }

        // POST: Matter/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Input validation
            if (!InputValidator.IsValidId(id))
            {
                _logger.LogWarning("Invalid matter ID in DeleteConfirmed: {Id}", id);
                return NotFound();
            }

            // Get current user and their organization
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                _logger.LogWarning("CustomUser not found in HttpContext for DeleteConfirmed");
                return RedirectToAction("Index", "Home");
            }

            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                _logger.LogWarning("User {UserId} has no primary organization", customUser.Id);
                return RedirectToAction("Index", "Home");
            }

            // SECURITY FIX: Verify user has access before deleting
            var matter = await _authHelper.GetMatterIfAuthorizedAsync(id, customUser.Id, primaryOrg.OrganizationId, HttpContext);
            if (matter == null)
            {
                _logger.LogWarning("SECURITY: User {UserId} attempted to delete unauthorized matter {MatterId}", customUser.Id, id);
                return NotFound();
            }

            try
            {
                // Store title for audit log before deletion
                var matterTitle = matter.Title;

                _context.Matters.Remove(matter);
                await _context.SaveChangesAsync();

                // Audit log the deletion
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditService.LogDeleteAsync(
                    customUser.Id,
                    primaryOrg.OrganizationId,
                    "Matter",
                    id,
                    ipAddress);

                TempData["SuccessMessage"] = "Matter deleted successfully!";
                _logger.LogInformation("User {UserId} deleted matter {MatterId} ({Title}) in org {OrgId}", 
                    customUser.Id, id, matterTitle, primaryOrg.OrganizationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting matter {MatterId} by user {UserId}", id, customUser.Id);
                TempData["ErrorMessage"] = "An error occurred while deleting the matter.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool MatterExists(int id)
        {
            return _context.Matters.Any(e => e.Id == id);
        }
    }
}
