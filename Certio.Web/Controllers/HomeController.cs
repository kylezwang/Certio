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

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
