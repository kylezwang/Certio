using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Infrastructure.Data;
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
        private readonly IChannelManagementService _channelManagementService;

        public ClientController(
            ApplicationDbContext db, 
            IJoinCodeService joinCodeService, 
            IConfiguration configuration,
            IFirmRelationshipCacheService firmRelationshipCache,
            IChannelManagementService channelManagementService)
        {
            _db = db;
            _joinCodeService = joinCodeService;
            _configuration = configuration;
            _firmRelationshipCache = firmRelationshipCache;
            _channelManagementService = channelManagementService;
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

            // Get direct organization memberships
            var directOrgs = await _db.UserOrganizations
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
                .ToListAsync(ct);

            // Get organizations accessible via law firm relationships
            var firmOrgs = await _db.UserOrganizations
                .Where(uo => uo.UserId == customUser.Id && uo.IsActive && uo.UserType == Certio.Domain.Users.UserTypes.LawFirm)
                .SelectMany(uo => uo.Organization!.OrganizationRelationships
                    .Where(rel => 
                        rel.IsActive && 
                        !rel.IsDeleted && 
                        rel.RelationshipType == Certio.Domain.Organizations.RelationshipTypes.LawFirmClient &&
                        (!rel.ExpiresAt.HasValue || rel.ExpiresAt.Value > DateTime.UtcNow))
                    .Select(rel => new {
                        organizationId = rel.TargetOrganizationId,
                        organizationName = rel.TargetOrganization!.Name,
                        ownerFirstName = rel.TargetOrganization!.Owner.FirstName,
                        ownerLastName = rel.TargetOrganization!.Owner.LastName,
                        isPersonal = rel.TargetOrganization.IsPersonal,
                        isPrimary = false, // Firm-based access is not primary
                        organizationType = rel.TargetOrganization.Type.ToString()
                    }))
                .ToListAsync(ct);

            // Combine and deduplicate by organizationId
            var allOrgs = directOrgs
                .Concat(firmOrgs)
                .GroupBy(o => o.organizationId)
                .Select(g => g.First()) // Take first occurrence (direct membership if exists)
                .OrderByDescending(x => x.isPrimary)
                .ThenBy(x => x.organizationName)
                .ToList();

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
                    .Include(p => p.Permissions)
                    .Include(p => p.Organization) // Include to show which organization the matter belongs to
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync(ct);

                // Apply role-based filtering for matters in the LAW FIRM organization
                // Check user's role in the law firm
                var userRole = await _db.UserOrganizations
                    .Where(uo => uo.UserId == customUser.Id && uo.OrganizationId == orgId && uo.IsActive)
                    .Select(uo => uo.Role)
                    .FirstOrDefaultAsync(ct);

                // Non-partners can only see matters they're assigned to (both law firm and client org matters)
                if (userRole != Certio.Domain.Users.OrganizationRoles.Partner)
                {
                    matters = matters.Where(m =>
                        m.Permissions.Any(p => p.UserId == customUser.Id && p.RevokedAt == null) || // Has explicit permissions
                        m.Assignments.Any(a => a.UserId == customUser.Id && a.RemovedAt == null))   // Or is assigned to the matter
                    .ToList();
                }

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
            ViewBag.OrganizationName = currentOrg?.Name ?? "Client";
            
            // Reuse the existing view
            return View("~/Views/Matter/Index.cshtml", viewModel);
        }

        // GET /Client/{orgId}/Communications
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Communications")]
        public async Task<IActionResult> Communications(int orgId)
        {
            // Membership hint for future scoping
            ViewBag.OrganizationId = orgId;
            var org = await _db.Organizations.Where(o => o.Id == orgId).FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            
            // Set user info for JavaScript
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            ViewBag.CurrentUserId = customUser?.Id;
            ViewBag.CurrentUserName = customUser != null ? $"{customUser.FirstName} {customUser.LastName}".Trim() : "";

            // Load real channels from database
            var channels = await _channelManagementService.GetOrganizationChannelsAsync(orgId);
            
            // Load team members from organization - declare once for use in both paths
            var orgTeamMembers = await _db.UserOrganizations
                .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                .Include(uo => uo.User)
                .Select(uo => new CommunicationsTeamMember
                {
                    Name = $"{uo.User.FirstName} {uo.User.LastName}",
                    Role = uo.Role.ToString(),
                    Status = "offline", // Will be updated by SignalR
                    Avatar = $"{uo.User.FirstName.Substring(0, 1)}{uo.User.LastName.Substring(0, 1)}",
                    Activity = "Available"
                })
                .ToListAsync();

            CommunicationsViewModel viewModel;
            
            // Check if we have real data
            if (channels.Any())
            {
                // Build channel categories from real data
                var channelCategories = new List<ChannelCategory>();
                
                // Group channels by type
                var publicChannels = channels.Where(c => !c.IsPrivateChannel && c.ChannelType != "Voice").ToList();
                var privateChannels = channels.Where(c => c.IsPrivateChannel && c.ChannelType != "Voice").ToList();
                var voiceChannels = channels.Where(c => c.ChannelType == "Voice").ToList();
                
                // CLIENT COMMUNICATIONS category
                if (publicChannels.Any())
                {
                    channelCategories.Add(new ChannelCategory
                    {
                        Name = "CLIENT COMMUNICATIONS",
                        Channels = new List<Channel>(),
                        Subcategories = new List<ChannelSubcategory>
                        {
                            new ChannelSubcategory
                            {
                                Name = "Client Organizations",
                                Channels = publicChannels.Select(c => new Channel
                                {
                                    Id = c.Id,
                                    Name = c.Title,
                                    Unread = 0, // TODO: Calculate unread count
                                    Type = c.ChannelType == "Voice" ? ChannelType.Voice : ChannelType.Text,
                                    IsPrivate = c.IsPrivateChannel
                                }).ToList()
                            }
                        }
                    });
                }
                
                // LEGAL TEAM category
                if (privateChannels.Any())
                {
                    channelCategories.Add(new ChannelCategory
                    {
                        Name = "LEGAL TEAM",
                        Channels = privateChannels.Select(c => new Channel
                        {
                            Id = c.Id,
                            Name = c.Title,
                            Unread = 0, // TODO: Calculate unread count
                            Type = c.ChannelType == "Voice" ? ChannelType.Voice : ChannelType.Text,
                            IsPrivate = c.IsPrivateChannel
                        }).ToList()
                    });
                }
                
                // VOICE CHANNELS category
                if (voiceChannels.Any())
                {
                    channelCategories.Add(new ChannelCategory
                    {
                        Name = "VOICE CHANNELS",
                        Channels = voiceChannels.Select(c => new Channel
                        {
                            Id = c.Id,
                            Name = c.Title,
                            Unread = 0,
                            Type = ChannelType.Voice,
                            IsPrivate = c.IsPrivateChannel,
                            Users = new List<string>() // TODO: Get active users from SignalR
                        }).ToList()
                    });
                }
                
                // Load recent messages from the first channel
                var firstChannel = channels.FirstOrDefault();
                var messages = new List<Message>();
                if (firstChannel != null)
                {
                    var recentMessages = await _db.ChatMessages
                        .Where(m => m.ChannelId == firstChannel.Id && m.IsChannelMessage)
                        .OrderByDescending(m => m.CreatedAt)
                        .Take(50)
                        .Include(m => m.User)
                        .ToListAsync();
                    
                    messages = recentMessages.OrderBy(m => m.CreatedAt).Select(m => new Message
                    {
                        Id = m.Id,
                        User = !string.IsNullOrEmpty(m.Sender) ? m.Sender : $"{m.User?.FirstName} {m.User?.LastName}",
                        Avatar = m.User != null ? $"{m.User.FirstName.Substring(0, 1)}{m.User.LastName.Substring(0, 1)}" : "??",
                        Time = m.CreatedAt.ToLocalTime().ToString("h:mm tt"),
                        Content = m.Content,
                        Reactions = new List<Reaction>() // TODO: Parse reactions from JSON
                    }).ToList();
                }
                
                viewModel = new CommunicationsViewModel
                {
                    ChannelCategories = channelCategories,
                    Messages = messages,
                    TeamMembers = orgTeamMembers,
                    ActiveChannel = firstChannel?.Title ?? "general",
                    OnlineMembersCount = 0 // Will be updated by SignalR
                };
            }
            else
            {
                // Fallback to sample data if no channels exist yet
                // Copy of HomeController.Communications sample data
                var channelCategories = new List<ChannelCategory>
                {
                    new ChannelCategory
                    {
                        Name = "CLIENT COMMUNICATIONS",
                        Channels = new List<Channel>(),
                        Subcategories = new List<ChannelSubcategory>
                        {
                            new ChannelSubcategory
                            {
                                Name = "Client Organizations",
                                Channels = new List<Channel>
                                {
                                    new Channel { Id = 1, Name = "general-client-chat", Unread = 0, Type = ChannelType.Text, IsPrivate = false },
                                    new Channel { Id = 2, Name = "urgent-matters", Unread = 3, Type = ChannelType.Text, IsPrivate = false },
                                    new Channel { Id = 3, Name = "client-onboarding", Unread = 1, Type = ChannelType.Text, IsPrivate = false }
                                }
                            }
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

                // Use real team members or sample data if no real users exist
                var sampleTeamMembers = new List<CommunicationsTeamMember>
                {
                    new CommunicationsTeamMember { Name = "Sarah Johnson", Role = "Senior Legal Counsel", Status = "online", Avatar = "SJ", Activity = "Reviewing Morrison contract" },
                    new CommunicationsTeamMember { Name = "Mike Chen", Role = "Legal Tech Specialist", Status = "online", Avatar = "MC", Activity = "Debugging case management system" }
                };
                
                var displayTeamMembers = orgTeamMembers.Any() ? orgTeamMembers : sampleTeamMembers;

                viewModel = new CommunicationsViewModel
                {
                    ChannelCategories = channelCategories,
                    Messages = messages,
                    TeamMembers = displayTeamMembers,
                    ActiveChannel = "general-client-chat",
                    OnlineMembersCount = displayTeamMembers.Count(m => m.Status == "online")
                };
            }

            return View("~/Views/Home/Communications.cshtml", viewModel);
        }

        // GET /Client/{orgId}/Documents
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Documents")]
        public async Task<IActionResult> Documents(int orgId)
        {
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
            return View("~/Views/Home/Documents.cshtml", model);
        }

        // GET /Client/{orgId}/Teams
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Teams")]
        public async Task<IActionResult> Teams(int orgId)
        {
            ViewBag.OrganizationId = orgId;
            var org = await _db.Organizations.Where(o => o.Id == orgId).FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";

            // Load active memberships for this organization with user info
            var memberships = await _db.UserOrganizations
                .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
                .Include(uo => uo.User)
                .ToListAsync();

            // Fallback colors to match existing UI palette
            static string GetTeamColor(TeamType team)
            {
                return team switch
                {
                    TeamType.Client => "#3b82f6",
                    TeamType.Legal => "#0b365e",
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

            var teamMembers = memberships
                .Where(m => m.User != null)
                .Select(m =>
                {
                    var team = MapTeam(m.UserType);
                    var firstName = m.User!.FirstName ?? string.Empty;
                    var lastName = m.User!.LastName ?? string.Empty;
                    var name = ($"{firstName} {lastName}").Trim();
                    var initials = !string.IsNullOrWhiteSpace(m.User.Avatar)
                        ? m.User.Avatar!
                        : GetInitials(firstName, lastName);
                    var color = !string.IsNullOrWhiteSpace(m.User.Color) ? m.User.Color : GetTeamColor(team);
                    return new TeamMember
                    {
                        Id = m.UserId.ToString(),
                        Name = string.IsNullOrWhiteSpace(name) ? m.User.Email : name,
                        Initials = initials,
                        Role = m.Role,
                        Department = string.IsNullOrWhiteSpace(m.Department) ? m.User.Department : m.Department,
                        Location = m.User.Location,
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
                
                var lawFirmOrg = await _db.Organizations
                    .FirstOrDefaultAsync(o => o.Id == lawFirmOrgId);
                model.LawFirmOrganizationName = lawFirmOrg?.Name;

                // Check if user is in their own law firm org or a client org
                if (orgId == lawFirmOrgId)
                {
                    // User is in their own law firm organization - simple form
                    model.IsClientOrganization = false;
                }
                else
                {
                    // Check if there's a relationship between law firm and current org
                    var relationship = await _db.OrganizationRelationships
                        .FirstOrDefaultAsync(or => 
                            or.SourceOrganizationId == lawFirmOrgId && 
                            or.TargetOrganizationId == orgId &&
                            or.IsActive &&
                            !or.IsDeleted);

                    model.IsClientOrganization = relationship != null;

                    // If in client org context and selecting InternalTeam, load law firm members
                    if (model.IsClientOrganization && model.SelectionType == "InternalTeam")
                    {
                        // Get law firm members
                        var lawFirmMembers = await _db.UserOrganizations
                            .Include(uo => uo.User)
                            .Where(uo => 
                                uo.OrganizationId == lawFirmOrgId &&
                                uo.UserType == UserTypes.LawFirm &&
                                uo.IsActive)
                            .ToListAsync();

                        // Get already assigned users for this relationship
                        var assignedUserIds = await _db.Set<OrganizationRelationshipAssignedUser>()
                            .Where(orau => orau.RelationshipId == relationship.Id)
                            .Select(orau => orau.UserId)
                            .ToListAsync();

                        model.AlreadyAssignedUserIds = assignedUserIds;
                        model.AvailableTeamMembers = lawFirmMembers.Select(uo => new OrgMemberDto
                        {
                            Id = uo.UserId,
                            Name = $"{uo.User.FirstName} {uo.User.LastName}".Trim(),
                            Email = uo.User.Email,
                            Role = uo.Role,
                            UserType = uo.UserType,
                            IsCurrentUser = uo.UserId == customUser.Id,
                            IsAlreadyAssigned = assignedUserIds.Contains(uo.UserId)
                        }).ToList();
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

                // Get the relationship
                var relationship = await _db.OrganizationRelationships
                    .FirstOrDefaultAsync(or => 
                        or.SourceOrganizationId == model.LawFirmOrganizationId &&
                        or.TargetOrganizationId == orgId &&
                        or.IsActive &&
                        !or.IsDeleted, ct);

                if (relationship == null)
                {
                    TempData["Error"] = "Organization relationship not found.";
                    return View("~/Views/Home/AddPeople.cshtml", model);
                }

                // Get already assigned users
                var existingAssignments = await _db.Set<OrganizationRelationshipAssignedUser>()
                    .Where(orau => orau.RelationshipId == relationship.Id)
                    .Select(orau => orau.UserId)
                    .ToListAsync(ct);

                // Create assignments for new users
                var newAssignments = model.SelectedUserIds
                    .Where(userId => !existingAssignments.Contains(userId))
                    .Select(userId => new OrganizationRelationshipAssignedUser
                    {
                        RelationshipId = relationship.Id,
                        UserId = userId,
                        AssignedAt = DateTime.UtcNow,
                        AssignedById = customUser.Id
                    })
                    .ToList();

                if (newAssignments.Any())
                {
                    _db.Set<OrganizationRelationshipAssignedUser>().AddRange(newAssignments);
                    await _db.SaveChangesAsync(ct);
                    TempData["Success"] = $"Successfully assigned {newAssignments.Count} team member(s) to {org?.Name}.";
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


