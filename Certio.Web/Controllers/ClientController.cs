using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Infrastructure.Data;
using Certio.Web.ViewModels;
using Certio.Domain.Matters;
using Certio.Web.Services;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;

namespace Certio.Web.Controllers
{
    [Authorize]
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IJoinCodeService _joinCodeService;
        private readonly IConfiguration _configuration;
        private readonly IFirmRelationshipCacheService _firmRelationshipCache;
        private readonly Certio.Web.Services.IChannelManagementService _channelManagementService;
        private readonly IMatterService _matterService;
        private readonly IChatService _chatService;
        private readonly IOrganizationService _organizationService;
        private readonly ITeamService _teamService;
        private readonly IOrganizationRelationshipService _relationshipService;

        public ClientController(
            ApplicationDbContext db, 
            IJoinCodeService joinCodeService, 
            IConfiguration configuration,
            IFirmRelationshipCacheService firmRelationshipCache,
            Certio.Web.Services.IChannelManagementService channelManagementService,
            IMatterService matterService,
            IChatService chatService,
            IOrganizationService organizationService,
            ITeamService teamService,
            IOrganizationRelationshipService relationshipService)
        {
            _db = db;
            _joinCodeService = joinCodeService;
            _configuration = configuration;
            _firmRelationshipCache = firmRelationshipCache;
            _channelManagementService = channelManagementService;
            _matterService = matterService;
            _chatService = chatService;
            _organizationService = organizationService;
            _teamService = teamService;
            _relationshipService = relationshipService;
        }

        // GET /Client/List
        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return Json(new { success = false, message = "User not authenticated." });
            }

            // Use organization service to get accessible organizations
            var result = await _organizationService.GetAccessibleOrganizationsAsync(customUser.Id);
            
            if (!result.Success)
            {
                return Json(new { success = false, message = result.ErrorMessage });
            }

            // Map DTOs to anonymous objects for JSON response
            var allOrgs = result.Data!.Select(org => new
            {
                organizationId = org.Id,
                organizationName = org.Name,
                ownerFirstName = org.OwnerFirstName,
                ownerLastName = org.OwnerLastName,
                isPersonal = org.IsPersonal,
                isPrimary = org.IsPrimary,
                organizationType = org.Type.ToString()
            }).ToList();

            return Json(new { success = true, organizations = allOrgs });
        }

        // GET /Client/{orgId}/Dashboard
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Dashboard")]
        public async Task<IActionResult> Dashboard(int orgId, CancellationToken ct)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Authorization is handled by the [Authorize(Policy = "OrgMember")] attribute

            // Use organization service to get organization info
            var orgResult = await _organizationService.GetOrganizationBasicInfoAsync(orgId, customUser.Id);
            
            ViewBag.OrganizationId = orgId;
            ViewBag.OrganizationName = orgResult.Success ? orgResult.Data!.Name : "Client";

            // Create sample dashboard data
            var viewModel = new DashboardViewModel
            {
                OrganizationType = orgResult.Success ? orgResult.Data!.Type : Certio.Domain.Organizations.OrganizationType.Client,
                NewMattersThisWeek = 4,
                NewMattersPercentageChange = 15,
                BillingBacklogPercentage = 20,
                BillingBacklogChange = -5,
                TrustComplianceWarnings = 3,
                TrustComplianceStatus = "unresolved",
                FilingsDueToday = 2,
                FilingsDueDescription = "Smith v. Jones, ABC Corp",
                ClientCallsToday = 1,
                ClientCallsDescription = "4:30 PM with Johnson",
                OverdueInvoices = 1,
                OverdueInvoicesDescription = "follow-up needed",
                UpcomingDeadlines = new List<UpcomingDeadline>
                {
                    new UpcomingDeadline { Title = "Smith v. Jones - Motion Filing", Time = "2:00 PM", Priority = "High" },
                    new UpcomingDeadline { Title = "Client Review Call", Time = "4:30 PM", Priority = "Medium" },
                    new UpcomingDeadline { Title = "Document Review", Time = "EOD", Priority = "Low" }
                },
                RecentFiles = new List<RecentFile>
                {
                    new RecentFile { Name = "Project_Spec_v3.pdf", ModifiedDate = DateTime.Now.AddHours(-2), TimeAgo = "2h ago" },
                    new RecentFile { Name = "Design_System.fig", ModifiedDate = DateTime.Now.AddHours(-4), TimeAgo = "4h ago" }
                },
                NextSuggestions = new List<NextSuggestion>
                {
                    new NextSuggestion { Title = "Matter Z inactive 12 days", Description = "→ follow up", Priority = "High" },
                    new NextSuggestion { Title = "3 overdue invoices", Description = "→ send reminders", Priority = "Medium" },
                    new NextSuggestion { Title = "Paralegal C overloaded", Description = "→ consider reassigning", Priority = "Medium" }
                }
            };

            return View(viewModel);
        }

        // GET /Client/{orgId}/Matter
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Matter")]
        public async Task<IActionResult> Matter(int orgId, CancellationToken ct)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Get organization info for ViewBag (view-specific, legitimate DB access)
            var currentOrg = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId, ct);

            // Check if this is a LawFirm organization - if so, aggregate matters from all accessible clients
            List<MatterDto> allMatters;
            
            if (currentOrg?.Type == Certio.Domain.Organizations.OrganizationType.LawFirm)
            {
                // Get all accessible client organizations for this user (respects user's access level and assignments)
                var accessibleClients = await _firmRelationshipCache.GetAccessibleClientOrganizationsAsync(customUser.Id);
                var clientOrgIds = accessibleClients.Select(c => c.Id).ToList();
                
                // Add the LawFirm organization's own ID to include its matters too
                clientOrgIds.Add(orgId);
                
                // Aggregate matters from all accessible organizations
                allMatters = new List<MatterDto>();
                
                foreach (var clientOrgId in clientOrgIds)
                {
                    var mattersResult = await _matterService.ListMattersAsync(customUser.Id, clientOrgId);
                    if (mattersResult.Success)
                    {
                        allMatters.AddRange(mattersResult.Data!);
                    }
                }
            }
            else
            {
                // For client organizations, show only matters from this specific client
                var result = await _matterService.ListMattersAsync(customUser.Id, orgId);
                
                if (!result.Success)
                {
                    TempData["Error"] = result.ErrorMessage;
                    ViewBag.OrganizationId = orgId;
                    ViewBag.OrganizationName = currentOrg?.Name ?? "Client";
                    return View("~/Views/Matter/Index.cshtml", new MattersViewModel());
                }
                
                allMatters = result.Data!;
            }

            // Get team members count (view-specific, simple query)
            var teamMembersCount = await _db.UserOrganizations
                .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                .CountAsync(ct);

            // Map DTOs to entities for view (temporary until view uses DTOs directly)
            var matters = allMatters.Select(dto => new Matter
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                Status = dto.Status,
                PracticeArea = dto.PracticeArea,
                CreatedAt = dto.CreatedAt,
                OrganizationId = dto.OrganizationId,
                DueDate = dto.DueDate,
                StartDate = dto.StartDate,
                CompletedDate = dto.CompletedDate,
                AccessLevel = dto.AccessLevel,
                TeamId = dto.TeamId,
                ClientId = dto.ClientId,
                // Map assignments for assignee display
                Assignments = dto.Assignments.Select(a => new MatterAssignment
                {
                    Id = a.Id,
                    MatterId = a.MatterId,
                    UserId = a.UserId,
                    AssignmentType = a.AssignmentType,
                    Role = a.Role,
                    IsNotifyRecipient = a.IsNotifyRecipient,
                    AssignedAt = a.AssignedAt,
                    User = a.User != null ? new Certio.Domain.Users.User
                    {
                        Id = a.User.Id,
                        FirstName = a.User.FirstName,
                        LastName = a.User.LastName,
                        Email = a.User.Email
                    } : null!
                }).ToList(),
                // Map task items for task progress (use DTO counts to satisfy computed properties)
                TaskItems = Enumerable.Range(0, dto.TasksCompleted)
                    .Select(_ => new Certio.Domain.Tasks.TaskItem { Status = "Completed" })
                    .Concat(Enumerable.Range(0, dto.TotalTasks - dto.TasksCompleted)
                        .Select(_ => new Certio.Domain.Tasks.TaskItem { Status = "Pending" }))
                    .ToList()
            }).ToList();

            var viewModel = new MattersViewModel
            {
                Matters = matters,
                ActiveMattersCount = allMatters.Count(m => m.Status == "In Progress"),
                CompletedMattersCount = allMatters.Count(m => m.Status == "Completed"),
                InReviewMattersCount = allMatters.Count(m => m.Status == "Review"),
                TeamMembersCount = teamMembersCount
            };

            ViewBag.OrganizationId = orgId;
            ViewBag.IsLawFirmView = currentOrg?.Type == Certio.Domain.Organizations.OrganizationType.LawFirm;
            ViewBag.OrganizationName = currentOrg?.Name ?? "Client";
            
            // Reuse the existing view
            return View("~/Views/Matter/Index.cshtml", viewModel);
        }

        // GET /Client/{orgId}/Communications
        // NOTE: Route removed to avoid conflict with CommunicationsController.Index
        // All communications functionality is now in CommunicationsController

        // GET /Client/{orgId}/Documents
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Documents")]
        public async Task<IActionResult> Documents(int orgId)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.OrganizationId = orgId;
            var org = await _db.Organizations.Where(o => o.Id == orgId).FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            
            var useSample = _configuration.GetValue<bool>("Features:UseSampleData");
            DocumentsViewModel model;
            if (useSample)
            {
                // Copy of HomeController.Documents sample data
                var documents = new List<Certio.Domain.Documents.Document>
                {
                    new Certio.Domain.Documents.Document { Id = 1, Name = "Morrison Industries - Service Agreement", Type = "Contract", FileSize = "2.4 MB", LastModifiedDate = new DateTime(2024, 1, 15), Status = "Final", Visibility = "Private", Tags = new List<string> { "Contract", "Client", "Morrison" }, Icon = "FileText" },
                    new Certio.Domain.Documents.Document { Id = 2, Name = "Compliance Audit Report Q4 2023", Type = "Report", FileSize = "5.1 MB", LastModifiedDate = new DateTime(2024, 1, 12), Status = "Published", Visibility = "Team", Tags = new List<string> { "Compliance", "Audit", "Q4" }, Icon = "File" },
                    new Certio.Domain.Documents.Document { Id = 3, Name = "Legal Research - AI Regulations", Type = "Research", FileSize = "1.8 MB", LastModifiedDate = new DateTime(2024, 1, 10), Status = "Draft", Visibility = "Private", Tags = new List<string> { "Research", "AI", "Regulations" }, Icon = "FileText" },
                    new Certio.Domain.Documents.Document { Id = 4, Name = "Client Onboarding Presentation", Type = "Presentation", FileSize = "12.3 MB", LastModifiedDate = new DateTime(2024, 1, 8), Status = "Review", Visibility = "Public", Tags = new List<string> { "Presentation", "Onboarding" }, Icon = "Image" },
                    new Certio.Domain.Documents.Document { Id = 5, Name = "Contract Template Library", Type = "Templates", FileSize = "8.7 MB", LastModifiedDate = new DateTime(2024, 1, 5), Status = "Active", Visibility = "Team", Tags = new List<string> { "Templates", "Contracts" }, Icon = "Archive" }
                };

                var folders = new List<Certio.Domain.Documents.FolderInfo>
                {
                    new Certio.Domain.Documents.FolderInfo { Name = "Contracts", Count = 24, Icon = "FileText" },
                    new Certio.Domain.Documents.FolderInfo { Name = "Compliance", Count = 12, Icon = "File" },
                    new Certio.Domain.Documents.FolderInfo { Name = "Research", Count = 18, Icon = "FileText" },
                    new Certio.Domain.Documents.FolderInfo { Name = "Templates", Count = 8, Icon = "Archive" },
                    new Certio.Domain.Documents.FolderInfo { Name = "Client Files", Count = 35, Icon = "File" }
                };

                var storage = new Certio.Domain.Documents.StorageInfo { UsedGB = 156.7, TotalGB = 500, DocumentCount = 247, SharedCount = 42, RecentCount = 15 };

                model = new DocumentsViewModel { Documents = documents, Folders = folders, Storage = storage };
            }
            else
            {
                model = new DocumentsViewModel();
            }

            // Fetch real matters from all accessible organizations (including relationships)
            var accessibleOrgsResult = await _organizationService.GetAccessibleOrganizationsAsync(customUser.Id);
            List<MatterDto> allMatters = new List<MatterDto>();
            
            if (accessibleOrgsResult.Success)
            {
                // Get matters from all accessible organizations
                foreach (var accessibleOrg in accessibleOrgsResult.Data!)
                {
                    var mattersResult = await _matterService.ListMattersAsync(customUser.Id, accessibleOrg.Id);
                    if (mattersResult.Success && mattersResult.Data != null)
                    {
                        allMatters.AddRange(mattersResult.Data);
                    }
                }
            }

            // Map DTOs to entities for view
            model.Matters = allMatters.Select(dto => new Matter
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                Status = dto.Status,
                PracticeArea = dto.PracticeArea,
                CreatedAt = dto.CreatedAt,
                OrganizationId = dto.OrganizationId,
                DueDate = dto.DueDate,
                StartDate = dto.StartDate,
                CompletedDate = dto.CompletedDate,
                AccessLevel = dto.AccessLevel,
                TeamId = dto.TeamId,
                ClientId = dto.ClientId,
                Assignments = dto.Assignments.Select(a => new MatterAssignment
                {
                    Id = a.Id,
                    MatterId = a.MatterId,
                    UserId = a.UserId,
                    AssignmentType = a.AssignmentType,
                    Role = a.Role,
                    IsNotifyRecipient = a.IsNotifyRecipient,
                    AssignedAt = a.AssignedAt,
                    User = a.User != null ? new Certio.Domain.Users.User
                    {
                        Id = a.User.Id,
                        FirstName = a.User.FirstName,
                        LastName = a.User.LastName,
                        Email = a.User.Email
                    } : null!
                }).ToList(),
                TaskItems = Enumerable.Range(0, dto.TasksCompleted)
                    .Select(_ => new Certio.Domain.Tasks.TaskItem { Status = "Completed" })
                    .Concat(Enumerable.Range(0, dto.TotalTasks - dto.TasksCompleted)
                        .Select(_ => new Certio.Domain.Tasks.TaskItem { Status = "Pending" }))
                    .ToList()
            }).ToList();

            return View("~/Views/Home/Documents.cshtml", model);
        }

        // GET /Client/{orgId}/Teams
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Teams")]
        public async Task<IActionResult> Teams(int orgId)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.OrganizationId = orgId;
            
            // Use organization service to get organization info
            var orgResult = await _organizationService.GetOrganizationBasicInfoAsync(orgId, customUser.Id);
            ViewBag.OrganizationName = orgResult.Success ? orgResult.Data!.Name : "Client";

            // Use team service to get team members
            var teamMembersResult = await _teamService.GetTeamMembersAsync(orgId, customUser.Id);
            
            if (!teamMembersResult.Success)
            {
                TempData["Error"] = teamMembersResult.ErrorMessage;
                return View("~/Views/Home/Teams.cshtml", new TeamsViewModel());
            }

            var teamMemberDtos = teamMembersResult.Data!;

            // Fallback colors to match existing UI palette
            static string GetTeamColor(TeamType team)
            {
                return team switch
                {
                    TeamType.Client => "#3b82f6",
                    TeamType.Legal => "#3d1019",
                    TeamType.External => "#10b981",
                    _ => "#3b82f6"
                };
            }

            static string GetInitials(string firstName, string lastName)
            {
                var a = string.IsNullOrWhiteSpace(firstName) ? ' ' : char.ToUpperInvariant(firstName.Trim()[0]);
                var b = string.IsNullOrWhiteSpace(lastName) ? ' ' : char.ToUpperInvariant(lastName.Trim()[0]);
                return $"{a}{b}".Trim();
            }

            static TeamType MapTeam(string userType)
            {
                if (string.Equals(userType, UserTypes.Client, StringComparison.OrdinalIgnoreCase))
                    return TeamType.Client;
                if (string.Equals(userType, UserTypes.External, StringComparison.OrdinalIgnoreCase))
                    return TeamType.External;
                // Treat LawFirm and Certio as Legal team in this UI
                return TeamType.Legal;
            }

            // Map DTOs to view model
            var teamMembers = teamMemberDtos
                .Select(dto =>
                {
                    var team = MapTeam(dto.UserType);
                    var name = ($"{dto.FirstName} {dto.LastName}").Trim();
                    var initials = !string.IsNullOrWhiteSpace(dto.Avatar)
                        ? dto.Avatar
                        : GetInitials(dto.FirstName, dto.LastName);
                    var color = !string.IsNullOrWhiteSpace(dto.Color) ? dto.Color : GetTeamColor(team);
                    return new TeamMember
                    {
                        Id = dto.UserId.ToString(),
                        Name = string.IsNullOrWhiteSpace(name) ? dto.Email : name,
                        Initials = initials,
                        Role = dto.Role,
                        Department = dto.Department,
                        Location = dto.Location,
                        Team = team,
                        Color = color
                    };
                })
                .ToList();

            // If no real data, fallback to empty model (no placeholders)
            var model = new TeamsViewModel
            {
                TeamMembers = teamMembers,
                ClientTeamCount = teamMembers.Count(m => m.Team == TeamType.Client),
                LegalTeamCount = teamMembers.Count(m => m.Team == TeamType.Legal),
                ExternalTeamCount = teamMembers.Count(m => m.Team == TeamType.External),
                TotalMembersCount = teamMembers.Count
            };

            // Fetch real matters from all accessible organizations (including relationships)
            var accessibleOrgsResult = await _organizationService.GetAccessibleOrganizationsAsync(customUser.Id);
            List<MatterDto> allMatters = new List<MatterDto>();
            
            if (accessibleOrgsResult.Success)
            {
                // Get matters from all accessible organizations
                foreach (var accessibleOrg in accessibleOrgsResult.Data!)
                {
                    var mattersResult = await _matterService.ListMattersAsync(customUser.Id, accessibleOrg.Id);
                    if (mattersResult.Success && mattersResult.Data != null)
                    {
                        allMatters.AddRange(mattersResult.Data);
                    }
                }
            }

            // Map DTOs to entities for view
            model.Matters = allMatters.Select(dto => new Matter
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                Status = dto.Status,
                PracticeArea = dto.PracticeArea,
                CreatedAt = dto.CreatedAt,
                OrganizationId = dto.OrganizationId,
                DueDate = dto.DueDate,
                StartDate = dto.StartDate,
                CompletedDate = dto.CompletedDate,
                AccessLevel = dto.AccessLevel,
                TeamId = dto.TeamId,
                ClientId = dto.ClientId,
                Assignments = dto.Assignments.Select(a => new MatterAssignment
                {
                    Id = a.Id,
                    MatterId = a.MatterId,
                    UserId = a.UserId,
                    AssignmentType = a.AssignmentType,
                    Role = a.Role,
                    IsNotifyRecipient = a.IsNotifyRecipient,
                    AssignedAt = a.AssignedAt,
                    User = a.User != null ? new Certio.Domain.Users.User
                    {
                        Id = a.User.Id,
                        FirstName = a.User.FirstName,
                        LastName = a.User.LastName,
                        Email = a.User.Email
                    } : null!
                }).ToList(),
                TaskItems = Enumerable.Range(0, dto.TasksCompleted)
                    .Select(_ => new Certio.Domain.Tasks.TaskItem { Status = "Completed" })
                    .Concat(Enumerable.Range(0, dto.TotalTasks - dto.TasksCompleted)
                        .Select(_ => new Certio.Domain.Tasks.TaskItem { Status = "Pending" }))
                    .ToList()
            }).ToList();

            return View("~/Views/Home/Teams.cshtml", model);
        }

        // GET /Client/{orgId}/Calendar
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Calendar")]
        public async Task<IActionResult> Calendar(int orgId)
        {
            ViewBag.OrganizationId = orgId;
            var org = await _db.Organizations.Where(o => o.Id == orgId).FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            
            // Return the Calendar view
            return View("~/Views/Client/Calendar.cshtml");
        }

        // GET /Client/{orgId}/Settings
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Settings")]
        public async Task<IActionResult> Settings(int orgId)
        {
            ViewBag.OrganizationId = orgId;
            var org = await _db.Organizations.Where(o => o.Id == orgId).FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            return View("~/Views/Settings/Index.cshtml");
        }


        // GET /Client/{orgId}/AddPeople
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/AddPeople")]
        public async Task<IActionResult> AddPeople(int orgId)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                TempData["Error"] = "Unable to resolve current user.";
                return RedirectToAction("Teams", new { orgId });
            }

            var org = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId);
            
            if (org == null)
            {
                TempData["Error"] = "Organization not found.";
                return RedirectToAction("Index", "Home");
            }

            var model = await PrepareAddPeopleViewModel(orgId, customUser);
            
            ViewBag.OrganizationId = orgId;
            ViewBag.OrganizationName = org.Name;
            
            return View("~/Views/Home/AddPeople.cshtml", model);
        }

        // POST /Client/{orgId}/AddPeople
        [Authorize(Policy = "OrgMember")]
        [HttpPost("/Client/{orgId:int}/AddPeople")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPeople(int orgId, AddPeopleFormViewModel model, string? action, CancellationToken ct)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                TempData["Error"] = "Unable to resolve current user.";
                return RedirectToAction("Teams", new { orgId });
            }

            var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);
            if (org == null)
            {
                TempData["Error"] = "Organization not found.";
                return RedirectToAction("Index", "Home");
            }

                ViewBag.OrganizationId = orgId;
            ViewBag.OrganizationName = org.Name;

            // Re-populate context info
            model = await PrepareAddPeopleViewModel(orgId, customUser, model);

            // Handle step navigation
            if (action == "back")
            {
                model.Step = 1;
                model.SelectionType = null;
                ModelState.Clear();
                return View("~/Views/Home/AddPeople.cshtml", model);
            }

            // Client users or step 1 selection
            if (model.Step == 1 || !model.IsLawFirmUser)
            {
                if (!string.IsNullOrEmpty(model.SelectionType))
                {
                    // Law firm user selected a card, move to step 2
                    model.Step = 2;
                    ModelState.Clear();
                    return View("~/Views/Home/AddPeople.cshtml", model);
                }
            }

            // Step 2: Process the form submission
            if (model.Step == 2 || !model.IsLawFirmUser)
            {
                return await ProcessAddPeopleSubmission(orgId, model, customUser, ct);
            }

            return View("~/Views/Home/AddPeople.cshtml", model);
        }

        private async Task<AddPeopleFormViewModel> PrepareAddPeopleViewModel(int orgId, Certio.Domain.Users.User customUser, AddPeopleFormViewModel? existingModel = null)
        {
            var model = existingModel ?? new AddPeopleFormViewModel();
            
            // Determine user context
            var userOrgMembership = customUser.UserOrganizations
                .FirstOrDefault(uo => uo.IsActive && uo.IsPrimary);

            model.IsLawFirmUser = userOrgMembership?.UserType == UserTypes.LawFirm;

            // Check if current org is a client organization (has a relationship with a law firm)
            if (model.IsLawFirmUser && userOrgMembership != null)
            {
                var lawFirmOrgId = userOrgMembership.OrganizationId;
                model.LawFirmOrganizationId = lawFirmOrgId;
                
                // Use organization service to get law firm name
                var lawFirmOrgResult = await _organizationService.GetOrganizationBasicInfoAsync(lawFirmOrgId, customUser.Id);
                model.LawFirmOrganizationName = lawFirmOrgResult.Success ? lawFirmOrgResult.Data!.Name : null;

                // Check if user is in their own law firm org or a client org
                if (orgId == lawFirmOrgId)
                {
                    // User is in their own law firm organization - simple form
                    model.IsClientOrganization = false;
                }
                else
                {
                    // Use relationship service to check if there's a relationship
                    var relationshipResult = await _relationshipService.GetLawFirmRelationshipForClientAsync(
                        lawFirmOrgId, 
                        orgId, 
                        customUser.Id);

                    var relationship = relationshipResult.Success ? relationshipResult.Data : null;
                    model.IsClientOrganization = relationship != null;

                    // If in client org context and selecting InternalTeam, load law firm members
                    if (model.IsClientOrganization && model.SelectionType == "InternalTeam" && relationship != null)
                    {
                        // Use relationship service to get assignable users
                        var assignableUsersResult = await _relationshipService.GetRelationshipAssignableUsersAsync(
                            relationship.Id, 
                            customUser.Id);

                        if (assignableUsersResult.Success)
                        {
                            var assignableUsers = assignableUsersResult.Data!;
                            
                            model.AlreadyAssignedUserIds = assignableUsers
                                .Where(u => u.IsAlreadyAssigned)
                                .Select(u => u.Id)
                                .ToList();
                            
                            model.AvailableTeamMembers = assignableUsers.Select(dto => new OrgMemberDto
                            {
                                Id = dto.Id,
                                Name = dto.Name,
                                Email = dto.Email,
                                Role = dto.Role,
                                UserType = dto.UserType,
                                IsCurrentUser = dto.IsCurrentUser,
                                IsAlreadyAssigned = dto.IsAlreadyAssigned
                            }).ToList();
                        }
                    }
                }
            }
            else
            {
                model.IsClientOrganization = false;
            }

            return model;
        }

        private async Task<IActionResult> ProcessAddPeopleSubmission(int orgId, AddPeopleFormViewModel model, Certio.Domain.Users.User customUser, CancellationToken ct)
        {
            ViewBag.OrganizationId = orgId;
            var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);
            ViewBag.OrganizationName = org?.Name ?? "Client";

            // Client context -> Internal Team -> Assign existing law firm members
            if (model.IsClientOrganization && model.SelectionType == "InternalTeam")
            {
                if (model.SelectedUserIds == null || !model.SelectedUserIds.Any())
                {
                    ModelState.AddModelError("SelectedUserIds", "Please select at least one team member.");
                    return View("~/Views/Home/AddPeople.cshtml", model);
                }

                // Get the relationship using relationship service
                var relationshipResult = await _relationshipService.GetLawFirmRelationshipForClientAsync(
                    model.LawFirmOrganizationId!.Value,
                    orgId,
                    customUser.Id);

                if (!relationshipResult.Success || relationshipResult.Data == null)
                {
                    TempData["Error"] = "Organization relationship not found.";
                    return View("~/Views/Home/AddPeople.cshtml", model);
                }

                var relationship = relationshipResult.Data;

                // Get IP address and User Agent for audit logging
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                // Use relationship service to assign users (handles business logic, duplicate checking, and audit logging)
                var assignmentResult = await _relationshipService.AssignUsersToRelationshipAsync(
                    relationship.Id,
                    customUser.Id,
                    model.SelectedUserIds,
                    ipAddress,
                    userAgent);

                if (!assignmentResult.Success)
                {
                    TempData["Error"] = assignmentResult.ErrorMessage;
                    return View("~/Views/Home/AddPeople.cshtml", model);
                }

                // Set appropriate message based on results
                var assignmentData = assignmentResult.Data!;
                if (assignmentData.AssignedCount > 0)
                {
                    TempData["Success"] = $"Successfully assigned {assignmentData.AssignedCount} team member(s) to {org?.Name}.";
                }
                else
                {
                    TempData["Info"] = "All selected members were already assigned.";
                }

                return RedirectToAction("Teams", new { orgId });
            }

            // All other cases: Generate join code
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError("Email", "Email is required.");
                return View("~/Views/Home/AddPeople.cshtml", model);
            }

            if (!customUser.CanCreateJoinCodes(orgId))
            {
                TempData["Error"] = "You do not have permission to create join codes.";
                return View("~/Views/Home/AddPeople.cshtml", model);
            }

            // Determine UserType and Role based on selection
            string invitedUserType;
            string invitedRole;

            if (!model.IsLawFirmUser)
            {
                // Client users always invite as Client
                invitedUserType = UserTypes.Client;
                invitedRole = model.Role ?? OrganizationRoles.Member;
            }
            else if (model.IsLawFirmUser && !model.IsClientOrganization)
            {
                // Law firm user in their own law firm org -> always invite as LawFirm
                invitedUserType = UserTypes.LawFirm;
                invitedRole = model.Role ?? OrganizationRoles.Staff;
            }
            else if (model.SelectionType == "InternalTeam")
            {
                // Law firm user in client context -> InternalTeam -> invite as LawFirm
                invitedUserType = UserTypes.LawFirm;
                invitedRole = model.Role ?? OrganizationRoles.Staff;
            }
            else if (model.SelectionType == "Client")
            {
                // Law firm user in client context -> Client -> invite as Client
                invitedUserType = UserTypes.Client;
                invitedRole = model.Role ?? OrganizationRoles.Member;
            }
            else if (model.SelectionType == "External")
            {
                invitedUserType = UserTypes.External;
                invitedRole = model.Role ?? OrganizationRoles.Other;
            }
            else
            {
                TempData["Error"] = "Invalid selection type.";
                return View("~/Views/Home/AddPeople.cshtml", model);
            }

            var join = await _joinCodeService.GenerateAsync(
                organizationId: orgId,
                createdByUserId: customUser.Id,
                invitedUserType: invitedUserType,
                invitedRole: invitedRole,
                teamName: null,
                maxUses: 1,
                ttl: TimeSpan.FromDays(7),
                ct: ct);

            ViewBag.JoinCode = join.Code;
            TempData["Success"] = "Join code generated successfully.";
            
            // Reset model for new invitation
            model = await PrepareAddPeopleViewModel(orgId, customUser);
            model.Step = 1;
            
            return View("~/Views/Home/AddPeople.cshtml", model);
        }
    }
}


