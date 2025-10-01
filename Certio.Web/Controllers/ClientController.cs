using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Certio.Web.Data;
using Certio.Web.ViewModels;
using Certio.Domain.Matters;
using Certio.Web.Services;

namespace Certio.Web.Controllers
{
    [Authorize]
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IJoinCodeService _joinCodeService;
        private readonly IConfiguration _configuration;
        private readonly IFirmRelationshipCacheService _firmRelationshipCache;

        public ClientController(
            ApplicationDbContext db, 
            IJoinCodeService joinCodeService, 
            IConfiguration configuration,
            IFirmRelationshipCacheService firmRelationshipCache)
        {
            _db = db;
            _joinCodeService = joinCodeService;
            _configuration = configuration;
            _firmRelationshipCache = firmRelationshipCache;
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

            var orgs = await _db.UserOrganizations
                .Where(uo => uo.UserId == customUser.Id && uo.IsActive)
                .Select(uo => new {
                    organizationId = uo.OrganizationId,
                    organizationName = uo.Organization!.Name,
                    ownerFirstName = uo.Organization!.Owner.FirstName,
                    ownerLastName = uo.Organization!.Owner.LastName,
                    isPersonal = uo.Organization.IsPersonal,
                    isPrimary = uo.IsPrimary,
                    organizationType = uo.Organization.Type.ToString()
                })
                .OrderByDescending(x => x.isPrimary)
                .ThenBy(x => x.organizationName)
                .ToListAsync(ct);

            return Json(new { success = true, organizations = orgs });
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

            var org = await _db.Organizations
                .Where(o => o.Id == orgId)
                .FirstOrDefaultAsync(ct);

            ViewBag.OrganizationId = orgId;
            ViewBag.OrganizationName = org?.Name ?? "Client";

            // Create sample dashboard data
            var viewModel = new DashboardViewModel
            {
                OrganizationType = org?.Type ?? Certio.Domain.Organizations.OrganizationType.Client,
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

            // Authorization is handled by the [Authorize(Policy = "OrgMember")] attribute

            // Check if this is a LawFirm organization - if so, aggregate matters from all accessible clients
            var currentOrg = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId, ct);

            List<Matter> matters;
            int teamMembersCount;

            if (currentOrg?.Type == Certio.Domain.Organizations.OrganizationType.LawFirm)
            {
                // Get all accessible client organizations for this user
                var accessibleClients = await _firmRelationshipCache.GetAccessibleClientOrganizationsAsync(customUser.Id);
                var clientOrgIds = accessibleClients.Select(c => c.Id).ToList();

                // Add the LawFirm organization's own ID to include its matters too
                clientOrgIds.Add(orgId);

                // Query matters from the LawFirm AND all accessible client organizations
                matters = await _db.Matters
                    .Where(p => clientOrgIds.Contains(p.OrganizationId))
                    .Include(p => p.Assignments)
                        .ThenInclude(a => a.User)
                    .Include(p => p.Organization) // Include to show which organization the matter belongs to
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync(ct);

                // Team members from the law firm
                teamMembersCount = await _db.UserOrganizations
                    .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                    .CountAsync(ct);
            }
            else
            {
                // For client organizations, show only matters from this specific client
                matters = await _db.Matters
                    .Where(p => p.OrganizationId == orgId)
                    .Include(p => p.Assignments)
                        .ThenInclude(a => a.User)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync(ct);

                teamMembersCount = await _db.UserOrganizations
                    .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                    .CountAsync(ct);
            }

            var viewModel = new MattersViewModel
            {
                Matters = matters,
                ActiveMattersCount = matters.Count(p => p.Status == "In Progress"),
                CompletedMattersCount = matters.Count(p => p.Status == "Completed"),
                InReviewMattersCount = matters.Count(p => p.Status == "Review"),
                TeamMembersCount = teamMembersCount
            };

            ViewBag.OrganizationId = orgId;
            ViewBag.IsLawFirmView = currentOrg?.Type == Certio.Domain.Organizations.OrganizationType.LawFirm;
            
            // Reuse the existing view
            return View("~/Views/Matter/Index.cshtml", viewModel);
        }

        // GET /Client/{orgId}/Services
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Services")]
        public IActionResult Services(int orgId)
        {
            // Membership hint for future scoping
            ViewBag.OrganizationId = orgId;

            var useSample = _configuration.GetValue<bool>("Features:UseSampleData");
            ServicesViewModel viewModel;
            if (useSample)
            {
                // Copy of HomeController.Services sample data
                var channelCategories = new List<ChannelCategory>
                {
                    new ChannelCategory
                    {
                        Name = "CLIENT COMMUNICATIONS",
                        Channels = new List<Channel>
                        {
                            new Channel { Id = 1, Name = "general-client-chat", Unread = 0, Type = ChannelType.Text, IsPrivate = false },
                            new Channel { Id = 2, Name = "urgent-matters", Unread = 3, Type = ChannelType.Text, IsPrivate = false },
                            new Channel { Id = 3, Name = "client-onboarding", Unread = 1, Type = ChannelType.Text, IsPrivate = false }
                        }
                    },
                    new ChannelCategory
                    {
                        Name = "LEGAL TEAM",
                        Channels = new List<Channel>
                        {
                            new Channel { Id = 4, Name = "contract-reviews", Unread = 0, Type = ChannelType.Text, IsPrivate = true },
                            new Channel { Id = 5, Name = "case-discussions", Unread = 2, Type = ChannelType.Text, IsPrivate = true },
                            new Channel { Id = 6, Name = "compliance-alerts", Unread = 0, Type = ChannelType.Text, IsPrivate = true }
                        }
                    },
                    new ChannelCategory
                    {
                        Name = "VOICE CHANNELS",
                        Channels = new List<Channel>
                        {
                            new Channel { Id = 7, Name = "Client Consultations", Unread = 0, Type = ChannelType.Voice, IsPrivate = false, Users = new List<string> { "SJ", "MC" } },
                            new Channel { Id = 8, Name = "Team Meetings", Unread = 0, Type = ChannelType.Voice, IsPrivate = true, Users = new List<string>() }
                        }
                    }
                };

                var messages = new List<Message>
                {
                    new Message { Id = 1, User = "Sarah Johnson", Avatar = "SJ", Time = "9:42 AM", Content = "The contract review for Morrison Industries is complete. Found 3 high-priority items that need attention.", Reactions = new List<Reaction> { new Reaction { Emoji = "✅", Count = 2 }, new Reaction { Emoji = "👍", Count = 1 } } },
                    new Message { Id = 2, User = "Mike Chen", Avatar = "MC", Time = "9:45 AM", Content = "Great work! Can you share the summary report in the contract-reviews channel?", Reactions = new List<Reaction>() },
                    new Message { Id = 3, User = "Alex Rodriguez", Avatar = "AR", Time = "10:15 AM", Content = "New client onboarding documents uploaded to the secure portal. All stakeholders have been notified.", Reactions = new List<Reaction> { new Reaction { Emoji = "🎉", Count = 3 } } },
                };

                var teamMembers = new List<ServiceTeamMember>
                {
                    new ServiceTeamMember { Name = "Sarah Johnson", Role = "Senior Legal Counsel", Status = "online", Avatar = "SJ", Activity = "Reviewing Morrison contract" },
                    new ServiceTeamMember { Name = "Mike Chen", Role = "Legal Tech Specialist", Status = "online", Avatar = "MC", Activity = "Debugging case management system" }
                };

                viewModel = new ServicesViewModel
                {
                    ChannelCategories = channelCategories,
                    Messages = messages,
                    TeamMembers = teamMembers,
                    ActiveChannel = "general-client-chat",
                    OnlineMembersCount = teamMembers.Count(m => m.Status == "online")
                };
            }
            else
            {
                viewModel = new ServicesViewModel();
            }

            return View("~/Views/Home/Services.cshtml", viewModel);
        }

        // GET /Client/{orgId}/Documents
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Documents")]
        public IActionResult Documents(int orgId)
        {
            ViewBag.OrganizationId = orgId;
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
            return View("~/Views/Home/Documents.cshtml", model);
        }

        // GET /Client/{orgId}/Teams
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Teams")]
        public IActionResult Teams(int orgId)
        {
            ViewBag.OrganizationId = orgId;
            var useSample = _configuration.GetValue<bool>("Features:UseSampleData");
            TeamsViewModel model;
            if (useSample)
            {
                // Copy of HomeController.Teams sample data
                var teamMembers = new List<TeamMember>
                {
                    // Client Team
                    new TeamMember { Id = "1", Name = "Sarah Johnson", Initials = "SJ", Role = "CEO", Department = "Executive", Location = "New York", Team = TeamType.Client, Color = "#3b82f6" },
                    new TeamMember { Id = "2", Name = "Michael Chen", Initials = "MC", Role = "CTO", Department = "Technology", Location = "San Francisco", Team = TeamType.Client, Color = "#3b82f6" },
                    new TeamMember { Id = "3", Name = "Emma Davis", Initials = "ED", Role = "Legal Counsel", Department = "Legal", Location = "Chicago", Team = TeamType.Client, Color = "#3b82f6" },
                    // Legal Team
                    new TeamMember { Id = "4", Name = "David Wilson", Initials = "DW", Role = "Senior Partner", Department = "Corporate Law", Location = "New York", Team = TeamType.Legal, Color = "#0b365e" },
                    new TeamMember { Id = "5", Name = "Jennifer Martinez", Initials = "JM", Role = "Associate", Department = "Litigation", Location = "Los Angeles", Team = TeamType.Legal, Color = "#0b365e" },
                    new TeamMember { Id = "6", Name = "Robert Taylor", Initials = "RT", Role = "Paralegal", Department = "Research", Location = "Boston", Team = TeamType.Legal, Color = "#0b365e" },
                    new TeamMember { Id = "7", Name = "Kyle Wang", Initials = "KW", Role = "Certio Team", Department = "Research", Location = "Boston", Team = TeamType.Legal, Color = "#0b365e" },
                    // External Team
                    new TeamMember { Id = "8", Name = "Lisa Anderson", Initials = "LA", Role = "Consultant", Department = "Advisory", Location = "Seattle", Team = TeamType.External, Color = "#10b981" },
                    new TeamMember { Id = "9", Name = "James Brown", Initials = "JB", Role = "Expert Witness", Department = "Technical", Location = "Austin", Team = TeamType.External, Color = "#10b981" }
                };
                model = new TeamsViewModel
                {
                    TeamMembers = teamMembers,
                    ClientTeamCount = teamMembers.Count(m => m.Team == TeamType.Client),
                    LegalTeamCount = teamMembers.Count(m => m.Team == TeamType.Legal),
                    ExternalTeamCount = teamMembers.Count(m => m.Team == TeamType.External),
                    TotalMembersCount = teamMembers.Count
                };
            }
            else
            {
                model = new TeamsViewModel();
            }
            return View("~/Views/Home/Teams.cshtml", model);
        }

        // GET /Client/{orgId}/Settings
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Settings")]
        public IActionResult Settings(int orgId)
        {
            ViewBag.OrganizationId = orgId;
            return View("~/Views/Settings/Index.cshtml");
        }


        // GET /Client/{orgId}/AddPeople
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/AddPeople")]
        public IActionResult AddPeople(int orgId)
        {
            ViewBag.OrganizationId = orgId;
            return View("~/Views/Home/AddPeople.cshtml", new AddPeopleViewModel());
        }

        // POST /Client/{orgId}/AddPeople
        [Authorize(Policy = "OrgMember")]
        [HttpPost("/Client/{orgId:int}/AddPeople")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPeople(int orgId, AddPeopleViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.OrganizationId = orgId;
                return View("~/Views/Home/AddPeople.cshtml", model);
            }

            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                TempData["Error"] = "Unable to resolve current user.";
                ViewBag.OrganizationId = orgId;
                return View("~/Views/Home/AddPeople.cshtml", model);
            }

            // Authorization is handled by the [Authorize(Policy = "OrgMember")] attribute

            if (!customUser.CanCreateJoinCodes(orgId))
            {
                TempData["Error"] = "You do not have permission to create join codes.";
                ViewBag.OrganizationId = orgId;
                return View("~/Views/Home/AddPeople.cshtml", model);
            }

            // If Law Firm is selected, ignore the provided role and set a conservative default; otherwise pass through
            var invitedUserType = model.UserType;
            var invitedRole = model.Role;
            if (string.Equals(invitedUserType, Certio.Domain.Users.UserTypes.LawFirm, StringComparison.OrdinalIgnoreCase))
            {
                invitedRole = Certio.Domain.Users.OrganizationRoles.Staff;
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

            ViewBag.OrganizationId = orgId;
            ViewBag.JoinCode = join.Code;
            TempData["Success"] = "Join code generated.";
            return View("~/Views/Home/AddPeople.cshtml", new AddPeopleViewModel
            {
                UserType = model.UserType,
                Role = model.Role
            });
        }
    }
}


