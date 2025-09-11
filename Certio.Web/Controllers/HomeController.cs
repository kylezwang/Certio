using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.ViewModels;
using Certio.Domain.Projects;
using Certio.Domain.Documents;

namespace Certio.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;

        public HomeController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            // If user is already logged in, redirect to Projects
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Projects");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, bool remember)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["Error"] = "Please fill in all fields";
                return View("Index");
            }

            var result = await _signInManager.PasswordSignInAsync(email, password, remember, lockoutOnFailure: false);
            
            if (result.Succeeded)
            {
                return RedirectToAction("Projects");
            }
            else
            {
                TempData["Error"] = "Invalid login attempt";
                return View("Index");
            }
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string email, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                TempData["Error"] = "Passwords do not match";
                return View();
            }

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["Error"] = "Please fill in all fields";
                return View();
            }

            var user = new IdentityUser { UserName = email, Email = email };
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Projects");
            }
            else
            {
                TempData["Error"] = "Registration failed. Please try again.";
                return View();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index");
        }

        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "Please enter your email address";
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                TempData["Success"] = "If an account with that email exists, we've sent a password reset link.";
                return View();
            }

            // Generate password reset token
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            
            // In a real application, you would send an email here
            // For now, we'll just show a success message
            // You can implement email sending using services like SendGrid, SMTP, etc.
            
            TempData["Success"] = "If an account with that email exists, we've sent a password reset link.";
            return View();
        }

        public IActionResult Projects()
        {
            // Sample data - in a real application, this would come from a service/repository
            var projects = new List<Project>
            {
                new Project
                {
                    Id = 1,
                    Title = "Contract Review Automation",
                    Description = "AI-powered contract analysis and risk assessment system",
                    Status = "In Progress",
                    Priority = "High",
                    DueDate = new DateTime(2024, 2, 15),
                    Assignees = new List<string> { "Sarah Johnson", "Mike Chen" },
                    TasksCompleted = 8,
                    TotalTasks = 12,
                    Category = "AI/ML"
                },
                new Project
                {
                    Id = 2,
                    Title = "Client Portal Dashboard",
                    Description = "Secure client access portal with document sharing capabilities",
                    Status = "Review",
                    Priority = "Medium",
                    DueDate = new DateTime(2024, 2, 28),
                    Assignees = new List<string> { "Alex Rodriguez", "Emily Davis" },
                    TasksCompleted = 15,
                    TotalTasks = 18,
                    Category = "Frontend"
                },
                new Project
                {
                    Id = 3,
                    Title = "Compliance Tracking System",
                    Description = "Automated regulatory compliance monitoring and reporting",
                    Status = "Planning",
                    Priority = "High",
                    DueDate = new DateTime(2024, 3, 10),
                    Assignees = new List<string> { "David Wilson" },
                    TasksCompleted = 3,
                    TotalTasks = 20,
                    Category = "Backend"
                },
                new Project
                {
                    Id = 4,
                    Title = "Legal Research Assistant",
                    Description = "Natural language processing for legal document search",
                    Status = "Completed",
                    Priority = "Medium",
                    DueDate = new DateTime(2024, 1, 30),
                    Assignees = new List<string> { "Sarah Johnson", "Mike Chen", "Alex Rodriguez" },
                    TasksCompleted = 25,
                    TotalTasks = 25,
                    Category = "AI/ML"
                }
            };

            var viewModel = new ProjectsViewModel
            {
                Projects = projects,
                ActiveProjectsCount = projects.Count(p => p.Status == "In Progress"),
                CompletedProjectsCount = projects.Count(p => p.Status == "Completed"),
                InReviewProjectsCount = projects.Count(p => p.Status == "Review"),
                TeamMembersCount = projects.SelectMany(p => p.Assignees).Distinct().Count()
            };

            return View(viewModel);
        }

        public IActionResult Documents()
        {
            // Sample data - in a real application, this would come from a service/repository
            var documents = new List<Document>
            {
                new Document
                {
                    Id = 1,
                    Name = "Morrison Industries - Service Agreement",
                    Type = "Contract",
                    Size = "2.4 MB",
                    Modified = new DateTime(2024, 1, 15),
                    Author = "Sarah Johnson",
                    Status = "Final",
                    Visibility = "Private",
                    Tags = new List<string> { "Contract", "Client", "Morrison" },
                    Icon = "FileText"
                },
                new Document
                {
                    Id = 2,
                    Name = "Compliance Audit Report Q4 2023",
                    Type = "Report",
                    Size = "5.1 MB",
                    Modified = new DateTime(2024, 1, 12),
                    Author = "Emily Davis",
                    Status = "Published",
                    Visibility = "Team",
                    Tags = new List<string> { "Compliance", "Audit", "Q4" },
                    Icon = "File"
                },
                new Document
                {
                    Id = 3,
                    Name = "Legal Research - AI Regulations",
                    Type = "Research",
                    Size = "1.8 MB",
                    Modified = new DateTime(2024, 1, 10),
                    Author = "Mike Chen",
                    Status = "Draft",
                    Visibility = "Private",
                    Tags = new List<string> { "Research", "AI", "Regulations" },
                    Icon = "FileText"
                },
                new Document
                {
                    Id = 4,
                    Name = "Client Onboarding Presentation",
                    Type = "Presentation",
                    Size = "12.3 MB",
                    Modified = new DateTime(2024, 1, 8),
                    Author = "Alex Rodriguez",
                    Status = "Review",
                    Visibility = "Public",
                    Tags = new List<string> { "Presentation", "Onboarding" },
                    Icon = "Image"
                },
                new Document
                {
                    Id = 5,
                    Name = "Contract Template Library",
                    Type = "Templates",
                    Size = "8.7 MB",
                    Modified = new DateTime(2024, 1, 5),
                    Author = "David Wilson",
                    Status = "Active",
                    Visibility = "Team",
                    Tags = new List<string> { "Templates", "Contracts" },
                    Icon = "Archive"
                }
            };

            var folders = new List<FolderInfo>
            {
                new FolderInfo { Name = "Contracts", Count = 24, Icon = "FileText" },
                new FolderInfo { Name = "Compliance", Count = 12, Icon = "File" },
                new FolderInfo { Name = "Research", Count = 18, Icon = "FileText" },
                new FolderInfo { Name = "Templates", Count = 8, Icon = "Archive" },
                new FolderInfo { Name = "Client Files", Count = 35, Icon = "File" }
            };

            var storage = new StorageInfo
            {
                UsedGB = 156.7,
                TotalGB = 500,
                DocumentCount = 247,
                SharedCount = 42,
                RecentCount = 15
            };

            var viewModel = new DocumentsViewModel
            {
                Documents = documents,
                Folders = folders,
                Storage = storage
            };

            return View(viewModel);
        }

        public IActionResult Teams()
        {
            // Sample data - in a real application, this would come from a service/repository
            var teamMembers = new List<TeamMember>
            {
                // Client Team
                new TeamMember
                {
                    Id = "1",
                    Name = "Sarah Johnson",
                    Initials = "SJ",
                    Role = "CEO",
                    Department = "Executive",
                    Location = "New York",
                    Team = TeamType.Client,
                    Color = "#3b82f6"
                },
                new TeamMember
                {
                    Id = "2",
                    Name = "Michael Chen",
                    Initials = "MC",
                    Role = "CTO",
                    Department = "Technology",
                    Location = "San Francisco",
                    Team = TeamType.Client,
                    Color = "#3b82f6"
                },
                new TeamMember
                {
                    Id = "3",
                    Name = "Emma Davis",
                    Initials = "ED",
                    Role = "Legal Counsel",
                    Department = "Legal",
                    Location = "Chicago",
                    Team = TeamType.Client,
                    Color = "#3b82f6"
                },
                // Legal Team
                new TeamMember
                {
                    Id = "4",
                    Name = "David Wilson",
                    Initials = "DW",
                    Role = "Senior Partner",
                    Department = "Corporate Law",
                    Location = "New York",
                    Team = TeamType.Legal,
                    Color = "#0b365e"
                },
                new TeamMember
                {
                    Id = "5",
                    Name = "Jennifer Martinez",
                    Initials = "JM",
                    Role = "Associate",
                    Department = "Litigation",
                    Location = "Los Angeles",
                    Team = TeamType.Legal,
                    Color = "#0b365e"
                },
                new TeamMember
                {
                    Id = "6",
                    Name = "Robert Taylor",
                    Initials = "RT",
                    Role = "Paralegal",
                    Department = "Research",
                    Location = "Boston",
                    Team = TeamType.Legal,
                    Color = "#0b365e"
                },
                new TeamMember
                {
                    Id = "7",
                    Name = "Kyle Wang",
                    Initials = "KW",
                    Role = "Certio Team",
                    Department = "Research",
                    Location = "Boston",
                    Team = TeamType.Legal,
                    Color = "#0b365e"
                },
                // External Team
                new TeamMember
                {
                    Id = "8",
                    Name = "Lisa Anderson",
                    Initials = "LA",
                    Role = "Consultant",
                    Department = "Advisory",
                    Location = "Seattle",
                    Team = TeamType.External,
                    Color = "#10b981"
                },
                new TeamMember
                {
                    Id = "9",
                    Name = "James Brown",
                    Initials = "JB",
                    Role = "Expert Witness",
                    Department = "Technical",
                    Location = "Austin",
                    Team = TeamType.External,
                    Color = "#10b981"
                }
            };

            var viewModel = new TeamsViewModel
            {
                TeamMembers = teamMembers,
                ClientTeamCount = teamMembers.Count(m => m.Team == TeamType.Client),
                LegalTeamCount = teamMembers.Count(m => m.Team == TeamType.Legal),
                ExternalTeamCount = teamMembers.Count(m => m.Team == TeamType.External),
                TotalMembersCount = teamMembers.Count
            };

            return View(viewModel);
        }

        public IActionResult Services()
        {
            // Sample data - in a real application, this would come from a service/repository
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
                new Message
                {
                    Id = 1,
                    User = "Sarah Johnson",
                    Avatar = "SJ",
                    Time = "9:42 AM",
                    Content = "The contract review for Morrison Industries is complete. Found 3 high-priority items that need attention.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "✅", Count = 2 },
                        new Reaction { Emoji = "👍", Count = 1 }
                    }
                },
                new Message
                {
                    Id = 2,
                    User = "Mike Chen",
                    Avatar = "MC",
                    Time = "9:45 AM",
                    Content = "Great work! Can you share the summary report in the contract-reviews channel?",
                    Reactions = new List<Reaction>()
                },
                new Message
                {
                    Id = 3,
                    User = "Alex Rodriguez",
                    Avatar = "AR",
                    Time = "10:15 AM",
                    Content = "New client onboarding documents uploaded to the secure portal. All stakeholders have been notified.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "🎉", Count = 3 }
                    }
                },
                new Message
                {
                    Id = 4,
                    User = "Emily Davis",
                    Avatar = "ED",
                    Time = "10:32 AM",
                    Content = "Compliance audit scheduled for next week. I've prepared the preliminary documentation checklist.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "📋", Count = 1 }
                    }
                },
                new Message
                {
                    Id = 5,
                    User = "Sarah Johnson",
                    Avatar = "SJ",
                    Time = "10:45 AM",
                    Content = "I've uploaded the Morrison contract summary to the shared drive. All high-priority items are highlighted in red.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "📄", Count = 2 },
                        new Reaction { Emoji = "👀", Count = 1 }
                    }
                },
                new Message
                {
                    Id = 6,
                    User = "Mike Chen",
                    Avatar = "MC",
                    Time = "10:52 AM",
                    Content = "Thanks Sarah! I'll review the technical clauses and get back to you by end of day.",
                    Reactions = new List<Reaction>()
                },
                new Message
                {
                    Id = 7,
                    User = "Alex Rodriguez",
                    Avatar = "AR",
                    Time = "11:15 AM",
                    Content = "The compliance meeting went well. We're on track for the Q1 audit. I'll send the updated timeline shortly.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "✅", Count = 3 },
                        new Reaction { Emoji = "📅", Count = 1 }
                    }
                },
                new Message
                {
                    Id = 8,
                    User = "Emily Davis",
                    Avatar = "ED",
                    Time = "11:30 AM",
                    Content = "Client onboarding for TechStart Inc. is complete. All documentation has been processed and filed.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "🎉", Count = 2 }
                    }
                },
                new Message
                {
                    Id = 9,
                    User = "Sarah Johnson",
                    Avatar = "SJ",
                    Time = "11:45 AM",
                    Content = "Reminder: The quarterly legal review meeting is tomorrow at 2 PM. Please prepare your department updates.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "⏰", Count = 1 },
                        new Reaction { Emoji = "📊", Count = 1 }
                    }
                },
                new Message
                {
                    Id = 10,
                    User = "Mike Chen",
                    Avatar = "MC",
                    Time = "12:00 PM",
                    Content = "I've completed the system updates for case tracking. The new features are now live in the staging environment.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "🚀", Count = 2 },
                        new Reaction { Emoji = "💻", Count = 1 }
                    }
                },
                new Message
                {
                    Id = 11,
                    User = "Alex Rodriguez",
                    Avatar = "AR",
                    Time = "12:15 PM",
                    Content = "The regulatory changes for data privacy are now in effect. I've updated our compliance checklist accordingly.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "🔒", Count = 3 },
                        new Reaction { Emoji = "📝", Count = 1 }
                    }
                },
                new Message
                {
                    Id = 12,
                    User = "Emily Davis",
                    Avatar = "ED",
                    Time = "12:30 PM",
                    Content = "Lunch break! Back at 1:30 PM. The client files are ready for the afternoon review session.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "🍽️", Count = 1 },
                        new Reaction { Emoji = "👋", Count = 2 }
                    }
                },
                new Message
                {
                    Id = 13,
                    User = "Sarah Johnson",
                    Avatar = "SJ",
                    Time = "12:45 PM",
                    Content = "I've scheduled a follow-up call with Morrison Industries for next Tuesday. They want to discuss the contract amendments.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "📞", Count = 1 },
                        new Reaction { Emoji = "📋", Count = 1 }
                    }
                },
                new Message
                {
                    Id = 14,
                    User = "Mike Chen",
                    Avatar = "MC",
                    Time = "1:00 PM",
                    Content = "The new document management system is working great! Upload times have improved by 40%.",
                    Reactions = new List<Reaction>
                    {
                        new Reaction { Emoji = "⚡", Count = 2 },
                        new Reaction { Emoji = "📈", Count = 1 }
                    }
                }
            };

            var teamMembers = new List<ServiceTeamMember>
            {
                new ServiceTeamMember
                {
                    Name = "Sarah Johnson",
                    Role = "Senior Legal Counsel",
                    Status = "online",
                    Avatar = "SJ",
                    Activity = "Reviewing Morrison contract",
                },
                new ServiceTeamMember
                {
                    Name = "Mike Chen",
                    Role = "Legal Tech Specialist",
                    Status = "online",
                    Avatar = "MC",
                    Activity = "Debugging case management system",
                },
                new ServiceTeamMember
                {
                    Name = "Alex Rodriguez",
                    Role = "Compliance Officer",
                    Status = "away",
                    Avatar = "AR",
                    Activity = "In compliance meeting",
                },
                new ServiceTeamMember
                {
                    Name = "Emily Davis",
                    Role = "Legal Assistant",
                    Status = "online",
                    Avatar = "ED",
                    Activity = "Organizing client documents",
                },
                new ServiceTeamMember
                {
                    Name = "David Wilson",
                    Role = "Contract Analyst",
                    Status = "offline",
                    Avatar = "DW",
                    Activity = "Last seen 2 hours ago",
                }
            };

            var viewModel = new ServicesViewModel
            {
                ChannelCategories = channelCategories,
                Messages = messages,
                TeamMembers = teamMembers,
                ActiveChannel = "general-client-chat",
                OnlineMembersCount = teamMembers.Count(m => m.Status == "online")
            };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
