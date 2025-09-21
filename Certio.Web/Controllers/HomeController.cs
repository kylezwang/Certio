using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.ViewModels;
using Certio.Web.Services;
using Certio.Domain.Projects;
using Certio.Domain.Documents;
using Certio.Web.Data;
using Microsoft.EntityFrameworkCore;
using Certio.Domain.Users;

namespace Certio.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ITwoFactorService _twoFactorService;
        private readonly ApplicationDbContext _context;
        private readonly IJoinCodeService _joinCodeService;

        public HomeController(
            SignInManager<IdentityUser> signInManager, 
            UserManager<IdentityUser> userManager,
            ITwoFactorService twoFactorService,
            ApplicationDbContext context,
            IJoinCodeService joinCodeService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _twoFactorService = twoFactorService;
            _context = context;
            _joinCodeService = joinCodeService;
        }

        public IActionResult Index()
        {
            // Debug: Log authentication state
            Console.WriteLine("Authentication State: IsAuthenticated=" + (User.Identity?.IsAuthenticated ?? false));
            Console.WriteLine("User Name: " + (User.Identity?.Name ?? "null"));
            Console.WriteLine("Authentication Type: " + (User.Identity?.AuthenticationType ?? "null"));
            
            // Check if user is actually authenticated with proper claims AND session validation
            var hasValidIdentity = User.Identity?.IsAuthenticated == true && 
                                  !string.IsNullOrEmpty(User.Identity.Name) &&
                                  User.Identity.AuthenticationType == "Identity.Application";
            
            // Additional session-based validation for incognito isolation
            var hasValidSession = false;
            try
            {
                var sessionUserId = HttpContext.Session.GetString("UserId");
                var sessionAuthTime = HttpContext.Session.GetString("AuthTime");
                hasValidSession = !string.IsNullOrEmpty(sessionUserId) && !string.IsNullOrEmpty(sessionAuthTime);
            }
            catch (InvalidOperationException)
            {
                // Session not available, rely only on identity
                hasValidSession = true; // Allow if session is not configured
            }
            
            var isAuthenticated = hasValidIdentity && hasValidSession;
            
            // If user is already logged in, redirect to Projects
            if (isAuthenticated)
            {
                Console.WriteLine("User is properly authenticated with session validation, redirecting to Projects");
                return RedirectToAction("Index", "Project");
            }
            
            Console.WriteLine("User is not authenticated or session invalid, showing login page");
            return View();
        }

        [HttpGet]
        public IActionResult DebugAuth()
        {
            var debugInfo = new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                UserName = User.Identity?.Name ?? "null",
                AuthenticationType = User.Identity?.AuthenticationType ?? "null",
                Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            };
            return Json(debugInfo);
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
                // Set session data for proper incognito isolation
                try
                {
                    var user = await _userManager.FindByEmailAsync(email);
                    if (user != null)
                    {
                        HttpContext.Session.SetString("UserId", user.Id);
                        HttpContext.Session.SetString("AuthTime", DateTime.UtcNow.ToString("O"));
                        HttpContext.Session.SetString("UserEmail", email);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Session not available, continue without session data
                }
                
                return RedirectToAction("Index", "Project");
            }
            else
            {
                TempData["Error"] = "Invalid login attempt";
                return View("Index");
            }
        }

        public IActionResult Register(int? step)
        {
            // Check if we're on a specific step
            if (step.HasValue)
            {
                ViewBag.Step = step.Value;
                
                // Pass registration data to the view for JavaScript
                if (step.Value == 2)
                {
                    var email = TempData["RegistrationEmail"]?.ToString();
                    if (!string.IsNullOrEmpty(email))
                    {
                        ViewBag.RegistrationEmail = email;
                        // Keep the data for the next request
                        TempData.Keep("RegistrationEmail");
                        TempData.Keep("VerificationCode");
                        TempData.Keep("CodeExpiry");
                    }
                }
                else if (step.Value == 3)
                {
                    var email = TempData["RegistrationEmail"]?.ToString();
                    var phone = TempData["RegistrationPhone"]?.ToString();
                    if (!string.IsNullOrEmpty(email))
                    {
                        ViewBag.RegistrationEmail = email;
                    }
                    if (!string.IsNullOrEmpty(phone))
                    {
                        ViewBag.RegistrationPhone = phone;
                    }
                    // Keep the data for the next request
                    TempData.Keep("RegistrationEmail");
                    TempData.Keep("RegistrationPhone");
                    TempData.Keep("IsVerified");
                }
            }
            
            return View();
        }

        [Authorize]
        [HttpGet]
        public IActionResult AddPeople()
        {
            return View(new Certio.Web.ViewModels.AddPeopleViewModel());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPeople(Certio.Web.ViewModels.AddPeopleViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Resolve custom user from middleware
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                TempData["Error"] = "Unable to resolve current user.";
                return View(model);
            }

            // Get the primary organization for the user
            var primaryOrg = customUser.GetPrimaryOrganization();
            if (primaryOrg == null)
            {
                TempData["Error"] = "User must have a primary organization to generate join codes.";
                return RedirectToAction("Teams");
            }

            // Only allow creating join codes if permitted
            if (!customUser.CanCreateJoinCodes(primaryOrg.OrganizationId))
            {
                TempData["Error"] = "You do not have permission to create join codes.";
                return View(model);
            }

            // Validate dependent type selection - now handled at organization level
            // UserType and role validation will be done when creating UserOrganization

            // Generate join code (1 use, 7 days TTL). teamName not used for now.
            var join = await _joinCodeService.GenerateAsync(
                organizationId: primaryOrg.OrganizationId,
                createdByUserId: customUser.Id,
                invitedUserType: model.UserType,
                invitedRole: GetOrganizationRoleFromModel(model),
                teamName: null,
                maxUses: 1,
                ttl: TimeSpan.FromDays(7),
                ct: ct);

            // Output to debug terminal
            Console.WriteLine($"✅ Generated Join Code for {model.Email} → {join.Code}");

            ViewBag.JoinCode = join.Code;
            TempData["Success"] = "Join code generated.";
            return View(new Certio.Web.ViewModels.AddPeopleViewModel
            {
                UserType = model.UserType,
                Role = model.Role
            });
        }

        /// <summary>
        /// Step 1: Start registration with email
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> StartRegistration(string email, string? joinCode)
        {
            if (string.IsNullOrEmpty(email) || !IsValidEmail(email))
            {
                TempData["Error"] = "Please enter a valid email address";
                return View("Register");
            }

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                TempData["Error"] = "An account with this email already exists. Please sign in instead.";
                return View("Register");
            }

            // Generate verification code
            var verificationCode = await _twoFactorService.GenerateVerificationCodeAsync();
            Console.WriteLine($"🎯 Generated verification code: {verificationCode}");
            
            // Send verification code via email
            var emailSent = await _twoFactorService.SendEmailVerificationAsync(email, verificationCode);
            Console.WriteLine($"📤 Email sent result: {emailSent}");
            
            if (!emailSent)
            {
                TempData["Error"] = "Failed to send verification code. Please try again.";
                return View("Register");
            }

            // Store registration data in TempData with Keep() to persist across redirects
            TempData["RegistrationEmail"] = email;
            TempData["VerificationCode"] = verificationCode;
            TempData["CodeExpiry"] = DateTime.UtcNow.AddMinutes(10).ToString("O");
            if (!string.IsNullOrWhiteSpace(joinCode))
            {
                TempData["JoinCode"] = joinCode.Trim();
                TempData.Keep("JoinCode");
            }
            TempData.Keep("RegistrationEmail");
            TempData.Keep("VerificationCode");
            TempData.Keep("CodeExpiry");

            return RedirectToAction("Register", new { step = 2 });
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Step 2: Verify 2FA code
        /// </summary>
        [HttpPost]
        public IActionResult VerifyTwoFactor(string verificationCode, string verificationMethod, string phoneNumber)
        {
            if (string.IsNullOrEmpty(verificationCode))
            {
                TempData["Error"] = "Please enter the verification code";
                return RedirectToAction("Register", new { step = 2 });
            }

            // Get stored verification data
            var storedEmail = TempData["RegistrationEmail"]?.ToString();
            var storedCode = TempData["VerificationCode"]?.ToString();
            var codeExpiryStr = TempData["CodeExpiry"]?.ToString();

            if (string.IsNullOrEmpty(storedEmail) || string.IsNullOrEmpty(storedCode) || string.IsNullOrEmpty(codeExpiryStr))
            {
                TempData["Error"] = "Verification session expired. Please start over.";
                return RedirectToAction("Register");
            }

            var codeExpiry = DateTime.Parse(codeExpiryStr);

            // Verify the code
            var isValid = _twoFactorService.VerifyCode(verificationCode, storedCode, codeExpiry);
            if (!isValid)
            {
                TempData["Error"] = "Invalid or expired verification code. Please try again.";
                return RedirectToAction("Register", new { step = 2 });
            }

            // Store verified data for next step with Keep() to persist across redirects
            TempData["RegistrationEmail"] = storedEmail;
            TempData["RegistrationPhone"] = phoneNumber;
            TempData["IsVerified"] = "true";
            TempData.Keep("RegistrationEmail");
            TempData.Keep("RegistrationPhone");
            TempData.Keep("IsVerified");

            return RedirectToAction("Register", new { step = 3 });
        }

        /// <summary>
        /// Step 3: Complete registration
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CompleteRegistration(string firstName, string lastName, string password, string confirmPassword, string email, string phoneNumber)
        {
            // Basic validation
            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || 
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                TempData["Error"] = "Please fill in all required fields";
                return RedirectToAction("Register", new { step = 3 });
            }

            if (password != confirmPassword)
            {
                TempData["Error"] = "Passwords do not match";
                return RedirectToAction("Register", new { step = 3 });
            }

            if (password.Length < 6)
            {
                TempData["Error"] = "Password must be at least 6 characters long";
                return RedirectToAction("Register", new { step = 3 });
            }

            // Verify the user is still in a valid registration session
            var isVerified = TempData["IsVerified"]?.ToString() == "true";
            var storedEmail = TempData["RegistrationEmail"]?.ToString();
            
            if (!isVerified || string.IsNullOrEmpty(storedEmail))
            {
                TempData["Error"] = "Verification session expired. Please start over.";
                return RedirectToAction("Register");
            }

            try
            {
                // Resolve optional join code
                var joinCode = TempData["JoinCode"]?.ToString();
                Certio.Domain.Organizations.OrganizationJoinCode? validJoin = null;
                if (!string.IsNullOrWhiteSpace(joinCode))
                {
                    var joinSvc = HttpContext.RequestServices.GetService<Certio.Web.Services.IJoinCodeService>();
                    if (joinSvc != null)
                    {
                        validJoin = await joinSvc.GetValidAsync(joinCode);
                    }
                }
                // Create Identity user
                var identityUser = new IdentityUser 
                { 
                    UserName = storedEmail, 
                    Email = storedEmail,
                    PhoneNumber = phoneNumber
                };
                
                var result = await _userManager.CreateAsync(identityUser, password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    TempData["Error"] = $"Failed to create account: {errors}";
                    return RedirectToAction("Register", new { step = 3 });
                }

                // Determine organization and role
                int organizationId;
                bool isPersonal = false;
                var userType = Certio.Domain.Users.UserTypes.Client;
                var organizationRole = Certio.Domain.Users.OrganizationRoles.Member;

                if (validJoin != null)
                {
                    organizationId = validJoin.OrganizationId;
                    userType = validJoin.InvitedUserType;
                    organizationRole = validJoin.InvitedRole;
                }
                else
                {
                    // Auto-create personal organization (temporary owner, set after user is created)
                    var org = new Certio.Domain.Organizations.Organization
                    {
                        Name = $"{firstName} {lastName}",
                        Description = "Personal Organization",
                        OwnerId = 0,
                        IsPersonal = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Organizations.Add(org);
                    await _context.SaveChangesAsync();
                    organizationId = org.Id;
                    isPersonal = true;
                    userType = Certio.Domain.Users.UserTypes.Client;
                    organizationRole = Certio.Domain.Users.OrganizationRoles.Owner;
                }

                // Create custom User record
                var customUser = new Certio.Domain.Users.User
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = storedEmail,
                    PhoneNumber = phoneNumber,
                    IsPersonalOrganization = isPersonal,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Color = GetRandomColor()
                };

                _context.Users.Add(customUser);
                await _context.SaveChangesAsync();

                // Add user to organization
                var userOrg = new Certio.Domain.Users.UserOrganization
                {
                    UserId = customUser.Id,
                    OrganizationId = organizationId,
                    UserType = userType,
                    Role = organizationRole,
                    IsPrimary = true, // First organization is primary
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                };
                _context.UserOrganizations.Add(userOrg);

                // If personal org, set ownerId to the just-created user
                if (isPersonal)
                {
                    var org = await _context.Organizations.FindAsync(organizationId);
                    if (org != null)
                    {
                        org.OwnerId = customUser.Id;
                    }
                }
                
                await _context.SaveChangesAsync();

                // If joined via code, consume code and add to team if specified
                if (validJoin != null)
                {
                    var joinSvc = HttpContext.RequestServices.GetService<Certio.Web.Services.IJoinCodeService>();
                    if (joinSvc != null)
                    {
                        await joinSvc.ConsumeAsync(validJoin.Code);
                    }

                    if (!string.IsNullOrWhiteSpace(validJoin.TeamName))
                    {
                        var team = await _context.Teams.FirstOrDefaultAsync(t => t.OrganizationId == organizationId && t.Name == validJoin.TeamName);
                        if (team == null)
                        {
                            team = new Certio.Domain.Teams.Team
                            {
                                Name = validJoin.TeamName!,
                                OrganizationId = organizationId,
                                TeamType = Certio.Domain.Teams.TeamType.Client,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.Teams.Add(team);
                            await _context.SaveChangesAsync();
                        }

                        _context.TeamMemberships.Add(new Certio.Domain.Teams.TeamMembership
                        {
                            TeamId = team.Id,
                            UserId = customUser.Id,
                            Role = "Member",
                            Status = "Active",
                            JoinedAt = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                // Sign in the user
                await _signInManager.SignInAsync(identityUser, isPersistent: false);

                // Set session data for proper authentication
                try
                {
                    HttpContext.Session.SetString("UserId", identityUser.Id);
                    HttpContext.Session.SetString("AuthTime", DateTime.UtcNow.ToString("O"));
                    HttpContext.Session.SetString("UserEmail", storedEmail);
                }
                catch (InvalidOperationException)
                {
                    // Session not available, continue without session data
                }

                TempData["Success"] = "Account created successfully! Welcome to Certio.";
                return RedirectToAction("Index", "Project");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registration error: {ex.Message}");
                TempData["Error"] = "An error occurred during registration. Please try again.";
                return RedirectToAction("Register", new { step = 3 });
            }
        }

        /// <summary>
        /// Get a random color for the user avatar
        /// </summary>
        private string GetRandomColor()
        {
            var colors = new[]
            {
                "#007bff", "#28a745", "#dc3545", "#ffc107", "#17a2b8",
                "#6f42c1", "#e83e8c", "#fd7e14", "#20c997", "#6c757d"
            };
            
            var random = new Random();
            return colors[random.Next(colors.Length)];
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


        [Authorize]
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
                    FileSize = "2.4 MB",
                    LastModifiedDate = new DateTime(2024, 1, 15),
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
                    FileSize = "5.1 MB",
                    LastModifiedDate = new DateTime(2024, 1, 12),
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
                    FileSize = "1.8 MB",
                    LastModifiedDate = new DateTime(2024, 1, 10),
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
                    FileSize = "12.3 MB",
                    LastModifiedDate = new DateTime(2024, 1, 8),
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
                    FileSize = "8.7 MB",
                    LastModifiedDate = new DateTime(2024, 1, 5),
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

        [Authorize]
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

        [Authorize]
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

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // Sign out the user
            await _signInManager.SignOutAsync();
            
            // Clear all authentication cookies manually for maximum security
            Response.Cookies.Delete("CertioAuth");
            Response.Cookies.Delete(".AspNetCore.Identity.Application");
            Response.Cookies.Delete(".AspNetCore.Antiforgery");
            
            // Clear any session data (if session is available)
            try
            {
                HttpContext.Session.Clear();
            }
            catch (InvalidOperationException)
            {
                // Session not configured, ignore
            }
            
            // Add security headers to prevent caching of sensitive pages
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> ClearAuth()
        {
            // Sign out the user
            await _signInManager.SignOutAsync();
            
            // Clear all authentication cookies manually for maximum security
            Response.Cookies.Delete("CertioAuth");
            Response.Cookies.Delete(".AspNetCore.Identity.Application");
            Response.Cookies.Delete(".AspNetCore.Antiforgery");
            
            // Clear any session data (if session is available)
            try
            {
                HttpContext.Session.Clear();
            }
            catch (InvalidOperationException)
            {
                // Session not configured, ignore
            }
            
            // Add security headers to prevent caching of sensitive pages
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            
            return RedirectToAction("Index");
        }


        public IActionResult Privacy()
        {
            return View();
        }
        
        private string GetOrganizationRoleFromModel(AddPeopleViewModel model)
        {
            // Now the model directly contains the OrganizationRole
            return model.Role;
        }

        [HttpPost]
        public async Task<IActionResult> ApplyJoinCode([FromBody] ApplyJoinCodeRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.JoinCode))
                {
                    return Json(new { success = false, message = "Join code is required." });
                }

                // Resolve custom user from middleware
                var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
                if (customUser == null)
                {
                    return Json(new { success = false, message = "User not authenticated." });
                }

                // Get the join code details
                var validJoin = await _joinCodeService.GetValidAsync(request.JoinCode.Trim());
                if (validJoin == null)
                {
                    return Json(new { success = false, message = "Invalid or expired join code." });
                }

                // Check if user is already a member of this organization
                var existingMembership = await _context.UserOrganizations
                    .FirstOrDefaultAsync(uo => uo.UserId == customUser.Id && uo.OrganizationId == validJoin.OrganizationId);

                if (existingMembership != null)
                {
                    return Json(new { success = false, message = "You are already a member of this organization." });
                }

                // Add user to the organization
                var userOrg = new Certio.Domain.Users.UserOrganization
                {
                    UserId = customUser.Id,
                    OrganizationId = validJoin.OrganizationId,
                    UserType = validJoin.InvitedUserType,
                    Role = validJoin.InvitedRole,
                    JoinedAt = DateTime.UtcNow
                };

                _context.UserOrganizations.Add(userOrg);

                // Consume the join code
                await _joinCodeService.ConsumeAsync(validJoin.Code);

                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = "Successfully joined the organization!",
                    redirectUrl = Url.Action("Index", "Project")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying join code: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while applying the join code." });
            }
        }
    }

    public class ApplyJoinCodeRequest
    {
        public string JoinCode { get; set; } = "";
    }
}
