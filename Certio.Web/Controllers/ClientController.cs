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
        public async Task<IActionResult> List(int? lastOpenedOrgId, CancellationToken ct)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return Json(new { success = false, message = "User not authenticated." });
            }

            // Find the user's law firm organization
            var lawFirmMembership = await _db.UserOrganizations
                .Include(uo => uo.Organization)
                .ThenInclude(o => o.OrganizationRelationships)
                .ThenInclude(or => or.TargetOrganization)
                .ThenInclude(to => to.Owner)
                .Include(uo => uo.Organization)
                .ThenInclude(o => o.OrganizationRelationships)
                .ThenInclude(or => or.TargetOrganization)
                .ThenInclude(to => to.UserOrganizations)
                .ThenInclude(uo => uo.User)
                .Include(uo => uo.Organization)
                .ThenInclude(o => o.UserOrganizations.Where(uo2 => uo2.IsActive))
                .FirstOrDefaultAsync(uo => 
                    uo.UserId == customUser.Id && 
                    uo.IsActive && 
                    uo.UserType == Certio.Domain.Users.UserTypes.LawFirm, ct);

            var allOrgs = new List<object>();

            if (lawFirmMembership != null && lawFirmMembership.Organization != null)
            {
                var relationships = lawFirmMembership.Organization.OrganizationRelationships
                    .Where(or => or.IsValid() && 
                                or.RelationshipType == Certio.Domain.Organizations.RelationshipTypes.LawFirmClient)
                    .ToList();

                // Check which relationships the user has access to
                var assignedSet = new HashSet<int>();
                var directSet = new HashSet<int>();
                
                if (relationships.Any())
                {
                    var relationshipIds = relationships.Select(r => r.Id).ToList();
                    var assignedRelationshipIds = await _db.OrganizationRelationshipAssignedUsers
                        .Where(a => a.UserId == customUser.Id && relationshipIds.Contains(a.RelationshipId))
                        .Select(a => a.RelationshipId)
                        .ToListAsync(ct);
                    assignedSet = assignedRelationshipIds.ToHashSet();

                    var targetOrgIds = relationships.Select(r => r.TargetOrganizationId).ToList();
                    var directMembershipOrgIds = await _db.UserOrganizations
                        .Where(uo => uo.UserId == customUser.Id &&
                                     uo.IsActive &&
                                     targetOrgIds.Contains(uo.OrganizationId))
                        .Select(uo => uo.OrganizationId)
                        .ToListAsync(ct);
                    directSet = directMembershipOrgIds.ToHashSet();
                }

                foreach (var rel in relationships)
                {
                    var targetOrg = rel.TargetOrganization;
                    if (targetOrg == null)
                        continue;

                    // Check if this is an External Contacts organization
                    var isExternalGuestsOrg = targetOrg.Name.EndsWith("'s External Contacts", StringComparison.OrdinalIgnoreCase);
                    
                    // Filter out external organizations where the current user is not the owner
                    if (isExternalGuestsOrg && targetOrg.OwnerId != customUser.Id)
                    {
                        continue; // Skip external organizations owned by other users
                    }

                    // For non-external organizations, check if user has access
                    if (!isExternalGuestsOrg)
                    {
                        var hasAccess = assignedSet.Contains(rel.Id) ||
                                      directSet.Contains(targetOrg.Id) ||
                                      targetOrg.OwnerId == customUser.Id ||
                                       rel.CreatedById == customUser.Id;
                        
                        if (!hasAccess)
                        {
                            continue; // Skip organizations user doesn't have access to
                        }
                    }

                    // Get owner's membership (if they have already joined)
                    var ownerMembership = targetOrg.UserOrganizations
                        .FirstOrDefault(uo => uo.UserId == targetOrg.OwnerId && uo.IsActive);

                    var ownerHasMembership = ownerMembership != null;
                    
                    // Get all users in the organization
                    var orgUsers = targetOrg.UserOrganizations
                        .Where(uo => uo.IsActive && uo.User != null)
                        .ToList();
                    
                    var hasRegisteredUsers = orgUsers.Any();
                    
                    // Organization is confirmed if owner has membership OR there are registered users
                    var isConfirmed = ownerHasMembership || hasRegisteredUsers;

                    // Check if this was just confirmed (owner joined within last 7 days)
                    var isNewlyConfirmed = false;
                    if (isConfirmed && ownerMembership != null)
                    {
                        var daysSinceJoined = (DateTime.UtcNow - ownerMembership.JoinedAt).TotalDays;
                        isNewlyConfirmed = daysSinceJoined <= 7;
                    }

                    var ownerFirstName = targetOrg.Owner?.FirstName ?? "";
                    var ownerLastName = targetOrg.Owner?.LastName ?? "";
                    var displayOwnerName = isExternalGuestsOrg 
                        ? "External Contacts" 
                        : $"{ownerFirstName} {ownerLastName}".Trim();

                    allOrgs.Add(new
                    {
                        organizationId = targetOrg.Id,
                        organizationName = targetOrg.Name,
                        ownerFirstName = ownerFirstName,
                        ownerLastName = ownerLastName,
                        isPersonal = targetOrg.IsPersonal,
                        isPrimary = false, // Not applicable for client organizations in popup
                        organizationType = targetOrg.Type.ToString(),
                        displayOwnerName = displayOwnerName,
                        isExternalContacts = isExternalGuestsOrg,
                        ownerHasMembership = ownerHasMembership,
                        hasRegisteredUsers = hasRegisteredUsers,
                        isConfirmed = isConfirmed,
                        isNewlyConfirmed = isNewlyConfirmed,
                        createdAt = rel.CreatedAt,
                        isLastOpened = lastOpenedOrgId.HasValue && lastOpenedOrgId.Value == targetOrg.Id && !isExternalGuestsOrg
                    });
                }
            }

            // Also include direct organization memberships (non-law firm)
            var directOrgs = await _db.UserOrganizations
                .Include(uo => uo.Organization)
                .ThenInclude(o => o.UserOrganizations.Where(uo2 => uo2.IsActive))
                .Where(uo => uo.UserId == customUser.Id && 
                             uo.IsActive && 
                             uo.Organization.Type != Certio.Domain.Organizations.OrganizationType.LawFirm)
                .Select(uo => uo.Organization)
                .ToListAsync(ct);

            foreach (var org in directOrgs)
            {
                // Skip if already added from relationships
                if (allOrgs.Any(o => ((dynamic)o).organizationId == org.Id))
                    continue;

                var orgUsers = org.UserOrganizations.Where(uo => uo.IsActive).ToList();
                var ownerMembership = orgUsers.FirstOrDefault(uo => uo.UserId == org.OwnerId);
                var ownerHasMembership = ownerMembership != null;
                var hasRegisteredUsers = orgUsers.Any();
                var isConfirmed = ownerHasMembership || hasRegisteredUsers;

                // Check if newly confirmed
                var isNewlyConfirmed = false;
                if (isConfirmed && ownerMembership != null)
                {
                    var daysSinceJoined = (DateTime.UtcNow - ownerMembership.JoinedAt).TotalDays;
                    isNewlyConfirmed = daysSinceJoined <= 7;
                }

                var ownerFirstName = org.Owner?.FirstName ?? "";
                var ownerLastName = org.Owner?.LastName ?? "";
                var isExternalGuestsOrg = org.Name.EndsWith("'s External Contacts", StringComparison.OrdinalIgnoreCase);
                var displayOwnerName = isExternalGuestsOrg 
                    ? "External Contacts" 
                    : $"{ownerFirstName} {ownerLastName}".Trim();

                allOrgs.Add(new
                {
                    organizationId = org.Id,
                    organizationName = org.Name,
                    ownerFirstName = ownerFirstName,
                    ownerLastName = ownerLastName,
                    isPersonal = org.IsPersonal,
                    isPrimary = false,
                    organizationType = org.Type.ToString(),
                    displayOwnerName = displayOwnerName,
                    isExternalContacts = isExternalGuestsOrg,
                    ownerHasMembership = ownerHasMembership,
                    hasRegisteredUsers = hasRegisteredUsers,
                    isConfirmed = isConfirmed,
                    isNewlyConfirmed = isNewlyConfirmed,
                    createdAt = org.CreatedAt,
                    isLastOpened = lastOpenedOrgId.HasValue && lastOpenedOrgId.Value == org.Id && !isExternalGuestsOrg
                });
            }

            // Add the law firm organization itself (for other parts of the code that expect it)
            if (lawFirmMembership != null && lawFirmMembership.Organization != null)
            {
                var firmOrg = lawFirmMembership.Organization;
                var firmOrgUsers = firmOrg.UserOrganizations?.Where(uo => uo.IsActive).ToList() ?? new List<Certio.Domain.Users.UserOrganization>();
                var firmOwnerMembership = firmOrgUsers.FirstOrDefault(uo => uo.UserId == firmOrg.OwnerId);
                var firmOwnerHasMembership = firmOwnerMembership != null;
                var firmHasRegisteredUsers = firmOrgUsers.Any();
                var firmIsConfirmed = firmOwnerHasMembership || firmHasRegisteredUsers;

                var firmOwnerFirstName = firmOrg.Owner?.FirstName ?? "";
                var firmOwnerLastName = firmOrg.Owner?.LastName ?? "";
                var firmDisplayOwnerName = $"{firmOwnerFirstName} {firmOwnerLastName}".Trim();

                allOrgs.Add(new
                {
                    organizationId = firmOrg.Id,
                    organizationName = firmOrg.Name,
                    ownerFirstName = firmOwnerFirstName,
                    ownerLastName = firmOwnerLastName,
                    isPersonal = firmOrg.IsPersonal,
                    isPrimary = lawFirmMembership.IsPrimary,
                    organizationType = firmOrg.Type.ToString(),
                    displayOwnerName = firmDisplayOwnerName,
                    isExternalContacts = false,
                    ownerHasMembership = firmOwnerHasMembership,
                    hasRegisteredUsers = firmHasRegisteredUsers,
                    isConfirmed = firmIsConfirmed,
                    isNewlyConfirmed = false,
                    createdAt = firmOrg.CreatedAt,
                    isLastOpened = false
                });
            }

            // Sort organizations:
            // 1. External Contacts organizations always at the bottom
            // 2. Last opened organization at the top (excluding external)
            // 3. Newly confirmed client organizations next
            // 4. Other client organizations sorted by creation date (newest first)
            var sortedOrgs = allOrgs
                .Cast<dynamic>()
                .OrderByDescending(o => o.isExternalContacts == false) // External orgs last
                .ThenByDescending(o => o.isLastOpened == true) // Last opened first (but not external)
                .ThenByDescending(o => o.isNewlyConfirmed == true && o.isConfirmed == true) // Newly confirmed next
                .ThenByDescending(o => (DateTime)o.createdAt) // Then by creation date (newest first)
                .Cast<object>()
                .ToList();

            return Json(new { success = true, organizations = sortedOrgs });
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
            ViewBag.OrganizationType = orgResult.Success ? orgResult.Data!.Type : Certio.Domain.Organizations.OrganizationType.Client;
            ViewBag.CurrentUserId = customUser.Id;
            ViewBag.CurrentUserName = $"{customUser.FirstName} {customUser.LastName}".Trim();

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
                    ViewBag.OrganizationType = currentOrg?.Type ?? Certio.Domain.Organizations.OrganizationType.Client;
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
            ViewBag.OrganizationType = currentOrg?.Type ?? Certio.Domain.Organizations.OrganizationType.Client;
            
            // Reuse the existing view
            return View("~/Views/Matter/Index.cshtml", viewModel);
        }

        // GET /Client/{orgId}/Communications
        // NOTE: Route removed to avoid conflict with CommunicationsController.Index
        // All communications functionality is now in CommunicationsController

        // GET /Client/{orgId}/Documents
        // NOTE: Moved to DocumentsController.Index
        // This route is now handled by DocumentsController

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
            ViewBag.OrganizationType = orgResult.Success ? orgResult.Data!.Type : Certio.Domain.Organizations.OrganizationType.Client;

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

        // GET /Client/{orgId}/Clients
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Clients")]
        public async Task<IActionResult> Clients(int orgId)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.OrganizationId = orgId;
            
            // Use organization service to get organization info
            var orgResult = await _organizationService.GetOrganizationBasicInfoAsync(orgId, customUser.Id);
            ViewBag.OrganizationName = orgResult.Success ? orgResult.Data!.Name : "Organization";
            ViewBag.OrganizationType = orgResult.Success ? orgResult.Data!.Type : Certio.Domain.Organizations.OrganizationType.Client;
            ViewBag.CurrentUserId = customUser.Id;
            ViewBag.CurrentUserName = $"{customUser.FirstName} {customUser.LastName}".Trim();
            ViewBag.CurrentUserInitials = GetInitials(customUser.FirstName, customUser.LastName);

            // Preserve TempData value for NewClientOrgId (TempData is consumed on read)
            var newClientOrgId = TempData["NewClientOrgId"] != null ? (int?)Convert.ToInt32(TempData["NewClientOrgId"]) : null;
            if (newClientOrgId.HasValue)
            {
                TempData["NewClientOrgId"] = newClientOrgId.Value; // Restore for later use
            }

            // Get organization relationships (clients) for this organization
            var organization = await _db.Organizations
                .Include(o => o.OrganizationRelationships)
                    .ThenInclude(or => or.TargetOrganization)
                        .ThenInclude(to => to.Owner)
                .Include(o => o.OrganizationRelationships)
                    .ThenInclude(or => or.TargetOrganization)
                        .ThenInclude(to => to.UserOrganizations)
                            .ThenInclude(uo => uo.User)
                .FirstOrDefaultAsync(o => o.Id == orgId);

            var clientRelationships = new List<object>();
            
            if (organization != null)
            {
                var relationships = organization.OrganizationRelationships
                    .Where(or => or.IsValid() && 
                                or.RelationshipType == Certio.Domain.Organizations.RelationshipTypes.LawFirmClient)
                    .ToList();

                // Check which relationships the user has access to
                var assignedSet = new HashSet<int>();
                var directSet = new HashSet<int>();
                
                if (relationships.Any())
                {
                    var relationshipIds = relationships.Select(r => r.Id).ToList();
                    var assignedRelationshipIds = await _db.OrganizationRelationshipAssignedUsers
                        .Where(a => a.UserId == customUser.Id && relationshipIds.Contains(a.RelationshipId))
                        .Select(a => a.RelationshipId)
                        .ToListAsync();
                    assignedSet = assignedRelationshipIds.ToHashSet();

                    var targetOrgIds = relationships.Select(r => r.TargetOrganizationId).ToList();
                    var directMembershipOrgIds = await _db.UserOrganizations
                        .Where(uo => uo.UserId == customUser.Id &&
                                     uo.IsActive &&
                                     targetOrgIds.Contains(uo.OrganizationId))
                        .Select(uo => uo.OrganizationId)
                        .ToListAsync();
                    directSet = directMembershipOrgIds.ToHashSet();
                }

                foreach (var rel in relationships)
                {
                    var targetOrg = rel.TargetOrganization;
                    if (targetOrg != null)
                    {
                        // Check if this is an External Contacts organization
                        var isExternalGuestsOrg = targetOrg.Name.EndsWith("'s External Contacts", StringComparison.OrdinalIgnoreCase);
                        
                        // Filter out external organizations where the current user is not the owner
                        if (isExternalGuestsOrg && targetOrg.OwnerId != customUser.Id)
                        {
                            continue; // Skip external organizations owned by other users
                        }

                        // For non-external organizations, check if user has access
                        if (!isExternalGuestsOrg)
                        {
                            var hasAccess = assignedSet.Contains(rel.Id) ||
                                          directSet.Contains(targetOrg.Id) ||
                                          targetOrg.OwnerId == customUser.Id ||
                                           rel.CreatedById == customUser.Id;
                            
                            if (!hasAccess)
                            {
                                continue; // Skip organizations user doesn't have access to
                            }
                        }

                        var ownerName = $"{targetOrg.Owner?.FirstName} {targetOrg.Owner?.LastName}".Trim();
                        var ownerInitials = GetInitials(targetOrg.Owner?.FirstName ?? "", targetOrg.Owner?.LastName ?? "");
                        
                        // Get owner's membership (if they have already joined)
                        var ownerMembership = targetOrg.UserOrganizations
                            .FirstOrDefault(uo => uo.UserId == targetOrg.OwnerId && uo.IsActive);

                        var ownerRole = ownerMembership?.Role ?? "Owner";
                        var ownerHasMembership = ownerMembership != null;
                        var ownerDisplayColor = ownerHasMembership ? "#69848C" : "#aaaaaa";
                        
                        // Check if this was just confirmed (owner joined within last 7 days)
                        var isNewlyConfirmed = false;
                        if (ownerHasMembership && ownerMembership != null)
                        {
                            var daysSinceJoined = (DateTime.UtcNow - ownerMembership.JoinedAt).TotalDays;
                            isNewlyConfirmed = daysSinceJoined <= 7;
                        }
                        
                        var displayOrgName = isExternalGuestsOrg ? targetOrg.Name : targetOrg.Name; // Show full name on Clients page
                        
                        // Get all users in the organization (for External Contacts, this will show all external users)
                        var orgUsers = targetOrg.UserOrganizations
                            .Where(uo => uo.IsActive && uo.User != null)
                            .Select(uo => new
                            {
                                UserId = uo.UserId,
                                UserName = $"{uo.User?.FirstName} {uo.User?.LastName}".Trim(),
                                UserInitials = GetInitials(uo.User?.FirstName ?? "", uo.User?.LastName ?? ""),
                                UserRole = uo.Role ?? "Guest",
                                UserEmail = uo.User?.Email ?? "",
                                UserColor = uo.User?.Color ?? "#69848C"
                            })
                            .Where(u => !string.IsNullOrWhiteSpace(u.UserName))
                            .ToList();

                        // Check if this is the newly created client organization
                        var isNewClientOrg = newClientOrgId.HasValue && newClientOrgId.Value == targetOrg.Id;
                        
                        clientRelationships.Add(new
                        {
                            OrganizationId = targetOrg.Id,
                            OrganizationName = targetOrg.Name, // Keep actual name for internal use
                            DisplayOrganizationName = displayOrgName, // Display name for UI
                            OwnerFirstName = targetOrg.Owner?.FirstName ?? "",
                            OwnerLastName = targetOrg.Owner?.LastName ?? "",
                            OwnerName = string.IsNullOrWhiteSpace(ownerName) ? targetOrg.Name : ownerName,
                            OwnerInitials = ownerInitials,
                            OwnerEmail = targetOrg.Owner?.Email ?? "",
                            OwnerRole = ownerRole,
                            OwnerHasMembership = ownerHasMembership,
                            OwnerDisplayColor = ownerDisplayColor,
                            RelationshipId = rel.Id,
                            IsExternalGuests = isExternalGuestsOrg, // Flag for styling
                            IsNewClient = isNewClientOrg, // Flag for newly created client
                            IsNewlyConfirmed = isNewlyConfirmed, // Flag for newly confirmed client
                            CreatedAt = rel.CreatedAt, // For sorting by creation date
                            Users = orgUsers // All users in the organization
                        });
                    }
                }
            }

            // Sort client relationships:
            // 1. External Contacts organizations always at the bottom
            // 2. Newly created client organizations at the top
            // 3. Newly confirmed client organizations next
            // 4. Other client organizations sorted by creation date (newest first)
            var sortedClientRelationships = clientRelationships
                .Cast<dynamic>()
                .OrderByDescending(cr => cr.IsExternalGuests == false) // External orgs last (false < true in descending)
                .ThenByDescending(cr => cr.IsNewClient) // New clients first
                .ThenByDescending(cr => cr.IsNewlyConfirmed == true && cr.OwnerHasMembership == true) // Newly confirmed next
                .ThenByDescending(cr => (DateTime)cr.CreatedAt) // Then by creation date (newest first)
                .Cast<object>()
                .ToList();

            ViewBag.ClientRelationships = sortedClientRelationships;

            return View("~/Views/Client/Clients.cshtml");
        }

        private static string GetInitials(string firstName, string lastName)
        {
            var a = string.IsNullOrWhiteSpace(firstName) ? ' ' : char.ToUpperInvariant(firstName.Trim()[0]);
            var b = string.IsNullOrWhiteSpace(lastName) ? ' ' : char.ToUpperInvariant(lastName.Trim()[0]);
            return $"{a}{b}".Trim();
        }

        // GET /Client/{orgId}/Settings
        // NOTE: Route removed to avoid conflict with SettingsController.Index
        // All firm settings functionality is now in SettingsController

        // GET /Client/{orgId}/AccountSettings
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/AccountSettings")]
        public async Task<IActionResult> AccountSettings(int orgId)
        {
            ViewBag.OrganizationId = orgId;
            var org = await _db.Organizations.Where(o => o.Id == orgId).FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            ViewBag.OrganizationType = org?.Type ?? Certio.Domain.Organizations.OrganizationType.Client;
            return View("~/Views/Settings/AccountSettings.cshtml");
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
            ViewBag.OrganizationType = org.Type;
            
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
            ViewBag.OrganizationType = org.Type;

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
            ViewBag.OrganizationType = org?.Type ?? Certio.Domain.Organizations.OrganizationType.Client;

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

        // GET /Client/GetJoinCode
        [Authorize]
        [HttpGet("/Client/GetJoinCode")]
        public async Task<IActionResult> GetJoinCode(int orgId, CancellationToken ct)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return Json(new { success = false, message = "User not authenticated." });
            }

            // Check if user has direct membership first
            var hasDirectAccess = await _db.UserOrganizations
                .AnyAsync(uo => uo.UserId == customUser.Id && 
                              uo.OrganizationId == orgId && 
                              uo.IsActive, ct);

            // If no direct access, check firm-based access through relationships
            if (!hasDirectAccess)
            {
                var hasFirmAccess = await _firmRelationshipCache.HasFirmAccessAsync(customUser.Id, orgId);
                if (!hasFirmAccess)
                {
                    return Json(new { success = false, message = "You do not have access to this organization." });
                }
            }

            // Get the most recent active join code for this organization
            var joinCode = await _db.OrganizationJoinCodes
                .Where(j => j.OrganizationId == orgId && 
                           j.IsActive && 
                           j.ExpiresAt > DateTime.UtcNow && 
                           j.UsesRemaining > 0)
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (joinCode == null)
            {
                return Json(new { success = false, message = "No active join code found for this organization." });
            }

            var organization = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId, ct);

            return Json(new { 
                success = true, 
                joinCode = joinCode.Code,
                organizationName = organization?.Name ?? "Client Organization"
            });
        }

        // GET /Client/{orgId}/NewClient
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/NewClient")]
        public async Task<IActionResult> NewClient(int orgId)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                TempData["Error"] = "Unable to resolve current user.";
                return RedirectToAction("Clients", new { orgId });
            }

            // Verify user is in a law firm organization
            var lawFirmMembership = await _db.UserOrganizations
                .Include(uo => uo.Organization)
                .FirstOrDefaultAsync(uo => uo.UserId == customUser.Id && 
                                             uo.IsActive && 
                                             uo.Organization.Type == Certio.Domain.Organizations.OrganizationType.LawFirm);

            if (lawFirmMembership == null)
            {
                TempData["Error"] = "Only law firm users can create new client organizations.";
                return RedirectToAction("Clients", new { orgId });
            }

            var lawFirmOrgId = lawFirmMembership.OrganizationId;
            var lawFirmOrgName = lawFirmMembership.Organization?.Name ?? "Law Firm";

            // Get external users from external organization relationships
            var externalUsers = await GetExternalUsersAsync(lawFirmOrgId, CancellationToken.None);

            var model = new NewClientFormViewModel
            {
                LawFirmOrganizationId = lawFirmOrgId,
                LawFirmOrganizationName = lawFirmOrgName,
                AvailableExternalUsers = externalUsers
            };

            ViewBag.OrganizationId = orgId;
            ViewBag.OrganizationName = lawFirmOrgName;

            return View("~/Views/Client/NewClient.cshtml", model);
        }

        // POST /Client/{orgId}/NewClient
        [Authorize(Policy = "OrgMember")]
        [HttpPost("/Client/{orgId:int}/NewClient")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewClient(int orgId, NewClientFormViewModel model, CancellationToken ct)
        {
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                TempData["Error"] = "Unable to resolve current user.";
                return RedirectToAction("Clients", new { orgId });
            }

            if (!ModelState.IsValid)
            {
                // Reload available external users
                var lawFirmMembership = await _db.UserOrganizations
                    .Include(uo => uo.Organization)
                    .FirstOrDefaultAsync(uo => uo.UserId == customUser.Id && 
                                                 uo.IsActive && 
                                                 uo.Organization.Type == Certio.Domain.Organizations.OrganizationType.LawFirm);

                if (lawFirmMembership != null)
                {
                    var externalUsers = await GetExternalUsersAsync(lawFirmMembership.OrganizationId, ct);
                    model.AvailableExternalUsers = externalUsers;
                    model.LawFirmOrganizationId = lawFirmMembership.OrganizationId;
                    model.LawFirmOrganizationName = lawFirmMembership.Organization?.Name ?? "Law Firm";
                }

                ViewBag.OrganizationId = orgId;
                return View("~/Views/Client/NewClient.cshtml", model);
            }

            // Validate that a primary client is selected
            if (!model.SelectedExternalUserId.HasValue)
            {
                ModelState.AddModelError("SelectedExternalUserId", "Please select a primary client.");
                var lawFirmMembershipReload = await _db.UserOrganizations
                    .Include(uo => uo.Organization)
                    .FirstOrDefaultAsync(uo => uo.UserId == customUser.Id && 
                                                 uo.IsActive && 
                                                 uo.Organization.Type == Certio.Domain.Organizations.OrganizationType.LawFirm);

                if (lawFirmMembershipReload != null)
                {
                    var externalUsersReload = await GetExternalUsersAsync(lawFirmMembershipReload.OrganizationId, ct);
                    model.AvailableExternalUsers = externalUsersReload;
                    model.LawFirmOrganizationId = lawFirmMembershipReload.OrganizationId;
                    model.LawFirmOrganizationName = lawFirmMembershipReload.Organization?.Name ?? "Law Firm";
                }
                ViewBag.OrganizationId = orgId;
                return View("~/Views/Client/NewClient.cshtml", model);
            }

            // Verify user is in a law firm organization
            var lawFirmMembershipCheck = await _db.UserOrganizations
                .Include(uo => uo.Organization)
                .FirstOrDefaultAsync(uo => uo.UserId == customUser.Id && 
                                             uo.IsActive && 
                                             uo.Organization.Type == Certio.Domain.Organizations.OrganizationType.LawFirm);

            if (lawFirmMembershipCheck == null)
            {
                TempData["Error"] = "Only law firm users can create new client organizations.";
                return RedirectToAction("Clients", new { orgId });
            }

            var lawFirmOrgId = lawFirmMembershipCheck.OrganizationId;

            // Validate that the selected external user exists and is actually an external user
            var selectedExternalUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == model.SelectedExternalUserId.Value && u.IsActive && !u.IsDeleted, ct);

            if (selectedExternalUser == null)
            {
                TempData["Error"] = "Selected external user not found.";
                var externalUsersReload = await GetExternalUsersAsync(lawFirmOrgId, ct);
                model.AvailableExternalUsers = externalUsersReload;
                model.LawFirmOrganizationId = lawFirmOrgId;
                model.LawFirmOrganizationName = lawFirmMembershipCheck.Organization?.Name ?? "Law Firm";
                ViewBag.OrganizationId = orgId;
                return View("~/Views/Client/NewClient.cshtml", model);
            }

            // Verify the user is actually an external user
            var isExternalUser = await _db.UserOrganizations
                .AnyAsync(uo => uo.UserId == model.SelectedExternalUserId.Value &&
                                uo.UserType == Certio.Domain.Users.UserTypes.External &&
                                uo.IsActive, ct);

            if (!isExternalUser)
            {
                TempData["Error"] = "Selected user is not an external user.";
                var externalUsersReload = await GetExternalUsersAsync(lawFirmOrgId, ct);
                model.AvailableExternalUsers = externalUsersReload;
                model.LawFirmOrganizationId = lawFirmOrgId;
                model.LawFirmOrganizationName = lawFirmMembershipCheck.Organization?.Name ?? "Law Firm";
                ViewBag.OrganizationId = orgId;
                return View("~/Views/Client/NewClient.cshtml", model);
            }

            // Create new client organization with selected user as Owner
            var newClientOrg = new Certio.Domain.Organizations.Organization
            {
                Name = model.ClientOrganizationName.Trim(),
                Description = "Client Organization",
                OwnerId = model.SelectedExternalUserId.Value, // Set selected external user as Owner
                Type = Certio.Domain.Organizations.OrganizationType.Client,
                IsPersonal = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Organizations.Add(newClientOrg);
            await _db.SaveChangesAsync(ct);

            // Create relationship between law firm and new client organization
            var relationship = new Certio.Domain.Organizations.OrganizationRelationship
            {
                SourceOrganizationId = lawFirmOrgId,
                TargetOrganizationId = newClientOrg.Id,
                RelationshipType = Certio.Domain.Organizations.RelationshipTypes.LawFirmClient,
                AccessLevel = Certio.Domain.Organizations.AccessLevels.FullAccess,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedById = customUser.Id
            };

            _db.OrganizationRelationships.Add(relationship);
            await _db.SaveChangesAsync(ct);

            // Assign the selected external user to the relationship
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            
            var assignmentResult = await _relationshipService.AssignUsersToRelationshipAsync(
                relationship.Id,
                customUser.Id,
                new List<int> { model.SelectedExternalUserId.Value },
                ipAddress,
                userAgent);

            if (!assignmentResult.Success)
            {
                TempData["Error"] = $"Failed to assign client: {assignmentResult.ErrorMessage}";
                var externalUsersReload = await GetExternalUsersAsync(lawFirmOrgId, ct);
                model.AvailableExternalUsers = externalUsersReload;
                model.LawFirmOrganizationId = lawFirmOrgId;
                model.LawFirmOrganizationName = lawFirmMembershipCheck.Organization?.Name ?? "Law Firm";
                ViewBag.OrganizationId = orgId;
                return View("~/Views/Client/NewClient.cshtml", model);
            }

            // Store external user ID in team name field with special prefix for transfer logic
            // Format: "NEW_CLIENT_EXTERNAL_IDS:userId"
            string externalUserIdsMetadata = $"NEW_CLIENT_EXTERNAL_IDS:{model.SelectedExternalUserId.Value}";

            // Generate join code for the primary client user (as Owner)
            var join = await _joinCodeService.GenerateAsync(
                organizationId: newClientOrg.Id,
                createdByUserId: customUser.Id,
                invitedUserType: Certio.Domain.Users.UserTypes.Client,
                invitedRole: Certio.Domain.Users.OrganizationRoles.Owner,
                teamName: externalUserIdsMetadata,
                maxUses: 1,
                ttl: TimeSpan.FromDays(7),
                ct: ct);

            TempData["Success"] = $"Client organization '{model.ClientOrganizationName}' created successfully.";
            TempData["JoinCode"] = join.Code;
            TempData["NewClientOrgId"] = newClientOrg.Id;
            ViewBag.OrganizationId = orgId;
            ViewBag.OrganizationName = lawFirmMembershipCheck.Organization?.Name ?? "Law Firm";

            // Redirect to show success with join code
            return RedirectToAction("Clients", new { orgId });
        }

        private async Task<List<ExternalUserDto>> GetExternalUsersAsync(int lawFirmOrgId, CancellationToken ct)
        {
            var externalUsers = new List<ExternalUserDto>();
            var addedUserIds = new HashSet<int>(); // Track users we've already added to prevent duplicates

            // Find all external organizations (ending with "'s External Contacts") related to this law firm
            var externalRelationships = await _db.OrganizationRelationships
                .Include(or => or.TargetOrganization)
                    .ThenInclude(to => to.UserOrganizations)
                        .ThenInclude(uo => uo.User)
                .Where(or => or.SourceOrganizationId == lawFirmOrgId &&
                             or.IsActive &&
                             !or.IsDeleted &&
                             or.TargetOrganization != null &&
                             or.TargetOrganization.Name.ToLower().EndsWith("'s external contacts"))
                .ToListAsync(ct);

            foreach (var relationship in externalRelationships)
            {
                var externalOrg = relationship.TargetOrganization;
                if (externalOrg == null) continue;

                // Get all external users from this organization
                var usersInOrg = externalOrg.UserOrganizations
                    .Where(uo => uo.IsActive && 
                                 uo.User != null && 
                                 uo.User.IsActive && 
                                 !uo.User.IsDeleted &&
                                 uo.UserType == Certio.Domain.Users.UserTypes.External)
                    .ToList();

                foreach (var userOrg in usersInOrg)
                {
                    var user = userOrg.User;
                    if (user == null) continue;

                    // Only add if we haven't seen this user ID before
                    if (!addedUserIds.Contains(user.Id))
                    {
                        externalUsers.Add(new ExternalUserDto
                        {
                            Id = user.Id,
                            Name = $"{user.FirstName} {user.LastName}".Trim(),
                            Email = user.Email ?? "",
                            Role = userOrg.Role ?? "Guest",
                            UserType = userOrg.UserType ?? Certio.Domain.Users.UserTypes.External,
                            ExternalOrganizationId = externalOrg.Id,
                            ExternalOrganizationName = externalOrg.Name
                        });
                        
                        addedUserIds.Add(user.Id);
                    }
                }
            }

            return externalUsers.OrderBy(u => u.Name).ThenBy(u => u.Email).ToList();
        }

        private static Guid CreateDeterministicGuid(string namespacePrefix, int value)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{namespacePrefix}:{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
            Span<byte> guidBytes = stackalloc byte[16];
            hash.AsSpan(0, 16).CopyTo(guidBytes);
            guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40); // Version 4
            guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // Variant RFC 4122
            return new Guid(guidBytes);
        }
    }
}


