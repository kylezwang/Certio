using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.ViewModels;
using Certio.Domain.Matters;
using Certio.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers
{
    [Authorize(Policy = "OrgMember")]
    public class MatterController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MatterController(ApplicationDbContext context)
        {
            _context = context;
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

            var matters = await _context.Matters
                .Where(p => p.OrganizationId == primaryOrg.OrganizationId)
                .Include(p => p.Assignments)
                    .ThenInclude(a => a.User)
                .ToListAsync();

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
                        Priority = "High",
                        PracticeArea = "AI/ML",
                        MatterType = "Contract Management",
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
                        Priority = "Medium",
                        PracticeArea = "Frontend",
                        MatterType = "Client Portal",
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
                        Priority = "High",
                        PracticeArea = "Backend",
                        MatterType = "Compliance Tracking",
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
                        Priority = "Medium",
                        PracticeArea = "AI/ML",
                        MatterType = "Legal Research",
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
            if (id == null)
            {
                return NotFound();
            }

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

            var matter = await _context.Matters
                .Where(p => p.Id == id && p.OrganizationId == primaryOrg.OrganizationId)
                .Include(p => p.Assignments)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync();

            if (matter == null)
            {
                return NotFound();
            }

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

                    // Trim and cap lengths server-side to defend against oversized input
                    string TrimTo(string? value, int max) => string.IsNullOrWhiteSpace(value) ? "" : (value!.Trim().Length <= max ? value.Trim() : value.Trim()[..max]);

                    var matter = new Matter
                    {
                        Title = TrimTo(model.Title, 200),
                        Description = TrimTo(model.Description, 1000),
                        PracticeArea = TrimTo(model.PracticeArea, 100),
                        MatterType = TrimTo(model.MatterType, 100),
                        Status = TrimTo(model.Status, 50),
                        Priority = TrimTo(model.Priority, 20),
                        StartDate = model.StartDate,
                        DueDate = model.DueDate,
                        PendingDate = model.PendingDate,
                        StatuteOfLimitationsDate = model.StatuteOfLimitationsDate,
                        AccessLevel = TrimTo(model.AccessLevel, 20),
                        OrganizationId = primaryOrg.OrganizationId,
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    };

                    _context.Matters.Add(matter);
                    await _context.SaveChangesAsync();

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
                                Role = TrimTo(fa.Role, 100),
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
                                Role = TrimTo(rc.Involvement, 100),
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
                if (string.IsNullOrWhiteSpace(model.PracticeArea))
                {
                    ModelState.AddModelError("PracticeArea", "Please select a category");
                    isValid = false;
                }
                if (string.IsNullOrWhiteSpace(model.MatterType))
                {
                    ModelState.AddModelError("MatterType", "Please select a matter type");
                    isValid = false;
                }
            }
            else if (model.Step == 2)
            {
                // Validate Step 2 fields
                if (string.IsNullOrWhiteSpace(model.Status))
                {
                    ModelState.AddModelError("Status", "Please select a status");
                    isValid = false;
                }
                if (string.IsNullOrWhiteSpace(model.Priority))
                {
                    ModelState.AddModelError("Priority", "Please select a priority");
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
            if (string.IsNullOrWhiteSpace(model.MatterType))
            {
                ModelState.AddModelError("MatterType", "Please select a matter type");
                isValid = false;
            }

            // Validate Step 2 fields
            if (string.IsNullOrWhiteSpace(model.Status))
            {
                ModelState.AddModelError("Status", "Please select a status");
                isValid = false;
            }
            if (string.IsNullOrWhiteSpace(model.Priority))
            {
                ModelState.AddModelError("Priority", "Please select a priority");
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
                model.Status = "";
                model.Priority = "";
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
            if (id == null)
            {
                return NotFound();
            }

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

            var matter = await _context.Matters.FindAsync(id);
            if (matter == null)
            {
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
                MatterType = matter.MatterType,
                Status = matter.Status,
                Priority = matter.Priority,
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
            if (id != model.Id)
            {
                return NotFound();
            }

            // Get current user and their organization for ViewBag
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            var primaryOrg = customUser?.GetPrimaryOrganization();
            if (primaryOrg != null)
            {
                ViewBag.OrganizationId = primaryOrg.OrganizationId;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var matter = await _context.Matters.FindAsync(id);
                    if (matter == null)
                    {
                        return NotFound();
                    }

                    matter.Title = model.Title;
                    matter.Description = model.Description;
                    matter.PracticeArea = model.PracticeArea;
                    matter.MatterType = model.MatterType;
                    matter.Status = model.Status;
                    matter.Priority = model.Priority;
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

                    TempData["SuccessMessage"] = "Matter updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
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
            if (id == null)
            {
                return NotFound();
            }

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

            var matter = await _context.Matters
                .Include(p => p.Assignments)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (matter == null)
            {
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
            var matter = await _context.Matters.FindAsync(id);
            if (matter != null)
            {
                _context.Matters.Remove(matter);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Matter deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool MatterExists(int id)
        {
            return _context.Matters.Any(e => e.Id == id);
        }
    }
}
