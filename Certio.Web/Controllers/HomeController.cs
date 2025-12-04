using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Certio.Web.ViewModels;
using Certio.Web.Services;
using Certio.Domain.Matters;
using Certio.Domain.Documents;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Domain.Services;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Certio.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ITwoFactorService _twoFactorService;
        private readonly IBriefingMessageService _briefingMessageService;
        private readonly ApplicationDbContext _context;
        private readonly IJoinCodeService _joinCodeService;
        private readonly Certio.Web.Services.IChannelManagementService _channelManagementService;
        private readonly IClientContextAccessor _clientContextAccessor;
        private readonly ILogger<HomeController> _logger;
        private readonly ITwoFactorSessionStore _twoFactorSessionStore;
        private readonly ITrustedDeviceService _trustedDeviceService;

        public HomeController(
            SignInManager<IdentityUser> signInManager, 
            UserManager<IdentityUser> userManager,
            ITwoFactorService twoFactorService,
            IBriefingMessageService briefingMessageService,
            ApplicationDbContext context,
            IJoinCodeService joinCodeService,
            Certio.Web.Services.IChannelManagementService channelManagementService,
            IClientContextAccessor clientContextAccessor,
            ITrustedDeviceService trustedDeviceService,
            ITwoFactorSessionStore twoFactorSessionStore,
            ILogger<HomeController>? logger = null)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _twoFactorService = twoFactorService;
            _briefingMessageService = briefingMessageService;
            _context = context;
            _joinCodeService = joinCodeService;
            _channelManagementService = channelManagementService;
            _clientContextAccessor = clientContextAccessor;
            _trustedDeviceService = trustedDeviceService;
            _twoFactorSessionStore = twoFactorSessionStore;
            _logger = logger ?? NullLogger<HomeController>.Instance;
        }

        public IActionResult Index()
        {
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
            
            // If user is already logged in, redirect to their LawFirm organization dashboard
            if (isAuthenticated)
            {
                // Resolve custom user and find their LawFirm or EventPlanner organization
                var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
                if (customUser != null)
                {
                    // First try to find LawFirm or EventPlanner organization
                    var lawFirmOrEventPlannerOrg = customUser.UserOrganizations
                        .FirstOrDefault(uo => uo.IsActive && uo.Organization != null && 
                            (uo.Organization.Type == Certio.Domain.Organizations.OrganizationType.LawFirm ||
                             uo.Organization.Type == Certio.Domain.Organizations.OrganizationType.EventPlanner));
                    
                    if (lawFirmOrEventPlannerOrg != null)
                    {
                        return RedirectToAction("Dashboard", "Client", new { orgId = lawFirmOrEventPlannerOrg.OrganizationId });
                    }
                    
                    // Fallback to primary organization dashboard
                    var primaryOrg = customUser.GetPrimaryOrganization();
                    if (primaryOrg != null)
                    {
                        return RedirectToAction("Dashboard", "Client", new { orgId = primaryOrg.OrganizationId });
                    }
                    
                    // Last fallback to any active organization dashboard
                    var anyOrg = customUser.UserOrganizations.FirstOrDefault(uo => uo.IsActive && uo.Organization != null);
                    if (anyOrg != null)
                    {
                        return RedirectToAction("Dashboard", "Client", new { orgId = anyOrg.OrganizationId });
                    }
                }
                // If no organization found, fall back to home view
                return View();
            }
            
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool remember)
        {
            var normalizedEmail = email?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "Please fill in all fields";
                return View("Index");
            }

            var user = await _userManager.FindByEmailAsync(normalizedEmail);
            if (user == null)
            {
                TempData["Error"] = "Invalid login attempt";
                return View("Index");
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                TempData["Error"] = "Your account is locked due to multiple failed attempts. Please try again later or contact support.";
                return View("Index");
            }

            if (!user.EmailConfirmed)
            {
                TempData["Error"] = "You must verify your email before signing in.";
                return View("Index");
            }

            var passwordResult = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
            if (passwordResult.IsLockedOut)
            {
                TempData["Error"] = "Your account is locked due to multiple failed attempts. Please try again later.";
                return View("Index");
            }

            if (!passwordResult.Succeeded)
            {
                TempData["Error"] = "Invalid login attempt";
                return View("Index");
            }

            // Check if user has 2FA enabled
            var customUser = await _context.Users
                .Include(u => u.UserOrganizations)
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail);
            
            TrustedDevice? existingDevice = null;
            bool isTrustedDevice = false;
            if (customUser != null)
            {
                existingDevice = await _trustedDeviceService.ValidateDeviceAsync(HttpContext, customUser);
                isTrustedDevice = existingDevice?.IsTrusted == true;
            }

            var shouldBypassTwoFactor = customUser != null && (!customUser.Enable2FA || isTrustedDevice);

            if (shouldBypassTwoFactor && customUser != null)
            {
                var persistent = remember || isTrustedDevice;
                
                await _signInManager.SignInAsync(user, persistent);
                await _trustedDeviceService.RegisterOrUpdateDeviceAsync(HttpContext, customUser, persistent, existingDevice);
                
                // Update last login date and session
                customUser.LastLoginDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                // Set session data
                try
                {
                    HttpContext.Session.SetString("UserId", user.Id);
                    HttpContext.Session.SetString("AuthTime", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    HttpContext.Session.SetString("UserEmail", user.Email ?? normalizedEmail);
                }
                catch (InvalidOperationException)
                {
                    // Session unavailable - continue
                }
                
                // Queue daily briefing for dashboard - fire and forget, non-blocking
                try
                {
                    if (customUser.UserOrganizations.Any())
                    {
                        var primaryOrg = customUser.UserOrganizations.FirstOrDefault(uo => uo.IsActive);
                        if (primaryOrg != null)
                        {
                            HttpContext.Session.SetString("QueueBriefing", "true");
                            HttpContext.Session.SetInt32("BriefingUserId", customUser.Id);
                            HttpContext.Session.SetInt32("BriefingOrgId", primaryOrg.OrganizationId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error queueing login briefing for user {UserId}", user.Email);
                    // Continue with login - briefing is non-critical
                }
                
                // Redirect EventPlanner and LawFirm users to their organization dashboard
                var lawFirmOrEventPlannerOrg = customUser.UserOrganizations
                    .FirstOrDefault(uo => uo.IsActive && uo.Organization != null && 
                        (uo.Organization.Type == Certio.Domain.Organizations.OrganizationType.EventPlanner ||
                         uo.Organization.Type == Certio.Domain.Organizations.OrganizationType.LawFirm));
                
                if (lawFirmOrEventPlannerOrg != null)
                {
                    return RedirectToAction("Dashboard", "Client", new { orgId = lawFirmOrEventPlannerOrg.OrganizationId });
                }
                
                // Fallback to Matter index for client organizations
                return RedirectToAction("Index", "Matter");
            }

            // 2FA is enabled - proceed with verification flow
            var code = await _twoFactorService.GenerateVerificationCodeAsync();
            var protectedCode = _twoFactorService.ProtectCode(code);
            var expiry = DateTimeOffset.UtcNow.AddMinutes(10);

            var recipientEmail = user.Email ?? normalizedEmail;
            var emailSent = await _twoFactorService.SendEmailVerificationAsync(recipientEmail, code);
            if (!emailSent)
            {
                TempData["Error"] = "We couldn't deliver your verification code. Please try again or contact support.";
                return View("Index");
            }

            try
            {
                var state = new TwoFactorLoginState(
                    UserId: user.Id,
                    Email: recipientEmail,
                    ProtectedCode: protectedCode,
                    ExpiresAtUtc: expiry,
                    RememberMe: remember,
                    VerificationMethod: "email");

                var token = await _twoFactorSessionStore.CreateAsync(state, HttpContext.RequestAborted);
                TempData["TwoFactorInfo"] = $"We sent a verification code to {MaskEmail(recipientEmail)}";
                return RedirectToAction(nameof(LoginTwoFactor), new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create two-factor session for user {UserId}", user.Id);
                TempData["Error"] = "Two-factor authentication is temporarily unavailable. Please try again.";
                return View("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> LoginTwoFactor(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Your verification session expired. Please sign in again.";
                return RedirectToAction("Index");
            }

            var state = await _twoFactorSessionStore.GetAsync(token, HttpContext.RequestAborted);
            if (state is null)
            {
                TempData["Error"] = "Your verification session expired. Please sign in again.";
                return RedirectToAction("Index");
            }

            var model = new TwoFactorVerificationViewModel
            {
                Email = MaskEmail(state.Email),
                VerificationMethod = state.VerificationMethod,
                Token = token,
                RememberMe = state.RememberMe
            };

            ViewBag.Info = TempData.ContainsKey("TwoFactorInfo")
                ? TempData["TwoFactorInfo"]
                : "Enter the verification code we sent to your email.";

            if (TempData.ContainsKey("TwoFactorError"))
            {
                ViewBag.Error = TempData["TwoFactorError"];
            }

            return View("LoginTwoFactor", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyLoginTwoFactor(TwoFactorVerificationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["TwoFactorError"] = "Enter the six-digit code we sent to you.";
                return RedirectToAction(nameof(LoginTwoFactor), new { token = model.Token });
            }

            if (string.IsNullOrWhiteSpace(model.Token))
            {
                TempData["Error"] = "Your verification session expired. Please sign in again.";
                return RedirectToAction("Index");
            }

            TwoFactorLoginState? state;
            try
            {
                state = await _twoFactorSessionStore.GetAsync(model.Token, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read two-factor state for token {Token}", model.Token);
                TempData["Error"] = "Two-factor verification is currently unavailable. Please sign in again.";
                return RedirectToAction("Index");
            }

            if (state is null)
            {
                TempData["Error"] = "Your verification session expired. Please sign in again.";
                return RedirectToAction("Index");
            }

            if (state.IsExpired())
            {
                await _twoFactorSessionStore.RemoveAsync(model.Token, HttpContext.RequestAborted);
                TempData["Error"] = "Your verification session expired. Please sign in again.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.FindByIdAsync(state.UserId);
            if (user == null)
            {
                await _twoFactorSessionStore.RemoveAsync(model.Token, HttpContext.RequestAborted);
                TempData["Error"] = "Your verification session expired. Please sign in again.";
                return RedirectToAction("Index");
            }

            var isValid = _twoFactorService.VerifyProtectedCode(
                model.VerificationCode,
                state.ProtectedCode,
                state.ExpiresAtUtc.UtcDateTime);
            if (!isValid)
            {
                var attempts = state.Attempts + 1;
                if (attempts >= 5)
                {
                    await _userManager.AccessFailedAsync(user);
                    await _twoFactorSessionStore.RemoveAsync(model.Token, HttpContext.RequestAborted);
                    TempData["Error"] = "Too many invalid codes. Your account was locked. Please try again later.";
                    return RedirectToAction("Index");
                }

                try
                {
                    var updatedState = state with { Attempts = attempts };
                    await _twoFactorSessionStore.UpdateAsync(model.Token, updatedState, HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update attempt count for user {UserId}", state.UserId);
                }

                TempData["TwoFactorError"] = "Invalid or expired verification code. Please try again.";
                return RedirectToAction(nameof(LoginTwoFactor), new { token = model.Token });
            }

            await _userManager.ResetAccessFailedCountAsync(user);
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }
            if (!await _userManager.GetTwoFactorEnabledAsync(user))
            {
                await _userManager.SetTwoFactorEnabledAsync(user, true);
            }

            // Use the RememberMe value from the form if provided, otherwise fall back to state
            var rememberMe = model.RememberMe;
            await _signInManager.SignInAsync(user, rememberMe);

            var customUser = await _context.Users
                .Include(u => u.UserOrganizations)
                .FirstOrDefaultAsync(u => u.Email == user.Email);

            if (customUser != null)
            {
                await _trustedDeviceService.RegisterOrUpdateDeviceAsync(HttpContext, customUser, rememberMe);
                customUser.LastLoginDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            try
                    {
                        HttpContext.Session.SetString("UserId", user.Id);
                HttpContext.Session.SetString("AuthTime", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                HttpContext.Session.SetString("UserEmail", user.Email ?? state.Email);
                }
                catch (InvalidOperationException)
                {
                // Session unavailable - continue
                }

            await _twoFactorSessionStore.RemoveAsync(model.Token, HttpContext.RequestAborted);
            TempData.Remove("TwoFactorInfo");
            TempData.Remove("TwoFactorError");

            // Queue daily briefing for dashboard - fire and forget, non-blocking
            try
            {
                if (customUser != null && customUser.UserOrganizations.Any())
                {
                    var primaryOrg = customUser.UserOrganizations.FirstOrDefault(uo => uo.IsActive);
                    if (primaryOrg != null)
                    {
                        // Queue briefing generation (will be picked up by dashboard)
                        HttpContext.Session.SetString("QueueBriefing", "true");
                        HttpContext.Session.SetInt32("BriefingUserId", customUser.Id);
                        HttpContext.Session.SetInt32("BriefingOrgId", primaryOrg.OrganizationId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queueing login briefing for user {UserId}", user.Email);
                // Continue with login - briefing is non-critical
            }
                
                return RedirectToAction("Index", "Matter");
            }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendLoginTwoFactor(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Your verification session expired. Please sign in again.";
                return RedirectToAction("Index");
            }

            TwoFactorLoginState? state;
            try
            {
                state = await _twoFactorSessionStore.GetAsync(token, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve two-factor state for resend token {Token}", token);
                TempData["Error"] = "Two-factor verification is currently unavailable. Please sign in again.";
                return RedirectToAction("Index");
            }

            if (state is null)
            {
                TempData["Error"] = "Your verification session expired. Please sign in again.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.FindByIdAsync(state.UserId);
            if (user == null)
            {
                await _twoFactorSessionStore.RemoveAsync(token, HttpContext.RequestAborted);
                TempData["Error"] = "Your verification session expired. Please sign in again.";
                return RedirectToAction("Index");
            }

            var code = await _twoFactorService.GenerateVerificationCodeAsync();
            var protectedCode = _twoFactorService.ProtectCode(code);
            var expiry = DateTimeOffset.UtcNow.AddMinutes(10);

            var emailSent = await _twoFactorService.SendEmailVerificationAsync(state.Email, code);
            if (!emailSent)
            {
                TempData["TwoFactorError"] = "We couldn't deliver your verification code. Please try again.";
                return RedirectToAction(nameof(LoginTwoFactor), new { token });
            }

            try
            {
                var updatedState = state with
                {
                    ProtectedCode = protectedCode,
                    ExpiresAtUtc = expiry,
                    Attempts = 0
                };

                await _twoFactorSessionStore.UpdateAsync(token, updatedState, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update two-factor session for user {UserId}", state.UserId);
                TempData["Error"] = "We couldn't resend your verification code. Please sign in again.";
                return RedirectToAction("Index");
            }

            TempData["TwoFactorInfo"] = $"We sent a new verification code to {MaskEmail(state.Email)}.";
            return RedirectToAction(nameof(LoginTwoFactor), new { token });
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
                    }
                }
                else if (step.Value == 3)
                {
                    var email = TempData["RegistrationEmail"]?.ToString();
                    var phone = TempData["RegistrationPhone"]?.ToString();
                    if (!string.IsNullOrEmpty(email))
                    {
                        ViewBag.RegistrationEmail = email;
                        // Keep the data for the next request
                        TempData.Keep("RegistrationEmail");
                        TempData.Keep("VerificationCode");
                        TempData.Keep("CodeExpiry");
                    }
                }
                else if (step.Value == 4)
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
                    TempData.Keep("OrganizationType");
                    TempData.Keep("OrganizationName");
                    TempData.Keep("JoinCode");
                }
            }
            
            return View();
        }

        [Authorize]
        [HttpGet]
        public IActionResult AddPeople()
        {
            // Prefer org-scoped route: redirect to current primary organization if available
            var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            var primaryOrg = customUser?.GetPrimaryOrganization();
            if (primaryOrg != null)
            {
                return RedirectToAction("AddPeople", "Client", new { orgId = primaryOrg.OrganizationId });
            }

            // Fallback to existing view if no primary organization is set
            TempData["Error"] = "User must have a primary organization to add people.";
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

            // Determine invited type/role. If LawFirm, ignore provided role and use conservative default.
            var invitedUserType = model.UserType;
            var invitedRole = model.Role;
            if (string.Equals(invitedUserType, Certio.Domain.Users.UserTypes.LawFirm, StringComparison.OrdinalIgnoreCase))
            {
                invitedRole = Certio.Domain.Users.OrganizationRoles.Staff;
                // Ensure any model error for Role is cleared since it's not required
                ModelState.Remove(nameof(model.Role));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(invitedRole))
                {
                    ModelState.AddModelError(nameof(model.Role), "Role is required for this user type.");
                    return View(model);
                }
            }

            // Generate join code (1 use, 7 days TTL). teamName not used for now.
            var join = await _joinCodeService.GenerateAsync(
                organizationId: primaryOrg.OrganizationId,
                createdByUserId: customUser.Id,
                invitedUserType: invitedUserType,
                invitedRole: invitedRole,
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
        public async Task<IActionResult> StartRegistration(string email)
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

            // Store registration data in TempData with Keep() to persist across redirects
            TempData["RegistrationEmail"] = email;
            TempData.Keep("RegistrationEmail");

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
        /// Step 2: Select organization type
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SelectOrganization(string organizationType, string? organizationName, string? joinCode)
        {
            var storedEmail = TempData["RegistrationEmail"]?.ToString();
            
            if (string.IsNullOrEmpty(storedEmail))
            {
                TempData["Error"] = "Registration session expired. Please start over.";
                return RedirectToAction("Register");
            }

            // Validate organization type
            if (string.IsNullOrEmpty(organizationType) || 
                (organizationType != "client" && organizationType != "lawfirm" && organizationType != "eventplanner" && organizationType != "join"))
            {
                TempData["Error"] = "Please select a valid organization type.";
                return RedirectToAction("Register", new { step = 2 });
            }

            // Validate organization name for new organizations
            if ((organizationType == "client" || organizationType == "lawfirm" || organizationType == "eventplanner") && string.IsNullOrWhiteSpace(organizationName))
            {
                TempData["Error"] = "Please enter an organization name.";
                return RedirectToAction("Register", new { step = 2 });
            }

            // Validate join code for existing organizations
            if (organizationType == "join" && string.IsNullOrWhiteSpace(joinCode))
            {
                TempData["Error"] = "Please enter a join code.";
                return RedirectToAction("Register", new { step = 2 });
            }

            // Validate join code if provided
            if (organizationType == "join")
            {
                var joinSvc = HttpContext.RequestServices.GetService<Certio.Web.Services.IJoinCodeService>();
                if (joinSvc != null)
                {
                    var validJoin = await joinSvc.GetValidAsync(joinCode!);
                    if (validJoin == null)
                    {
                        TempData["Error"] = "Invalid or expired join code.";
                        return RedirectToAction("Register", new { step = 2 });
                    }
                }
            }

            // Store organization selection data
            TempData["OrganizationType"] = organizationType;
            if (!string.IsNullOrWhiteSpace(organizationName))
            {
                TempData["OrganizationName"] = organizationName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(joinCode))
            {
                TempData["JoinCode"] = joinCode.Trim();
            }
            
            TempData.Keep("RegistrationEmail");
            TempData.Keep("OrganizationType");
            TempData.Keep("OrganizationName");
            TempData.Keep("JoinCode");

            return RedirectToAction("Register", new { step = 3 });
        }

        /// <summary>
        /// Step 3: Verify 2FA code
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> VerifyTwoFactor(string verificationCode, string verificationMethod, string phoneNumber)
        {
            if (string.IsNullOrEmpty(verificationCode))
            {
                TempData["Error"] = "Please enter the verification code";
                return RedirectToAction("Register", new { step = 3 });
            }

            // Get stored verification data
            var storedEmail = TempData["RegistrationEmail"]?.ToString();
            var protectedCode = TempData["VerificationCode"]?.ToString();
            var codeExpiryStr = TempData["CodeExpiry"]?.ToString();

            if (string.IsNullOrEmpty(storedEmail) || string.IsNullOrEmpty(protectedCode) || string.IsNullOrEmpty(codeExpiryStr))
            {
                TempData["Error"] = "Verification session expired. Please start over.";
                return RedirectToAction("Register");
            }

            var codeExpiry = DateTime.Parse(codeExpiryStr);

            // Verify the code
            var isValid = _twoFactorService.VerifyProtectedCode(verificationCode, protectedCode, codeExpiry);
            if (!isValid)
            {
                TempData["Error"] = "Invalid or expired verification code. Please try again.";
                return RedirectToAction("Register", new { step = 3 });
            }

            // Store verified data for next step with Keep() to persist across redirects
            TempData["RegistrationEmail"] = storedEmail;
            TempData["RegistrationPhone"] = phoneNumber;
            TempData["IsVerified"] = "true";
            TempData.Keep("RegistrationEmail");
            TempData.Keep("RegistrationPhone");
            TempData.Keep("IsVerified");
            TempData.Keep("OrganizationType");
            TempData.Keep("OrganizationName");
            TempData.Keep("JoinCode");
            TempData.Remove("VerificationCode");
            TempData.Remove("CodeExpiry");

            return RedirectToAction("Register", new { step = 4 });
        }

        /// <summary>
        /// Generate and send 2FA code for step 3
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GenerateTwoFactorCode()
        {
            var storedEmail = TempData["RegistrationEmail"]?.ToString();
            
            if (string.IsNullOrEmpty(storedEmail))
            {
                TempData["Error"] = "Registration session expired. Please start over.";
                return RedirectToAction("Register");
            }

            // Generate verification code
            var verificationCode = await _twoFactorService.GenerateVerificationCodeAsync();
            var protectedCode = _twoFactorService.ProtectCode(verificationCode);
            
            // Send verification code via email
            var emailSent = await _twoFactorService.SendEmailVerificationAsync(storedEmail, verificationCode);
            
            if (!emailSent)
            {
                TempData["Error"] = "Failed to send verification code. Please try again.";
                return RedirectToAction("Register", new { step = 3 });
            }

            // Store verification data
            TempData["VerificationCode"] = protectedCode;
            TempData["CodeExpiry"] = DateTime.UtcNow.AddMinutes(10).ToString("O");
            TempData.Keep("RegistrationEmail");
            TempData.Keep("VerificationCode");
            TempData.Keep("CodeExpiry");
            TempData.Keep("OrganizationType");
            TempData.Keep("OrganizationName");
            TempData.Keep("JoinCode");

            return RedirectToAction("Register", new { step = 3 });
        }

        /// <summary>
        /// Step 4: Complete registration
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CompleteRegistration(string firstName, string lastName, string password, string confirmPassword, string email, string phoneNumber)
        {
            // Basic validation
            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || 
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                TempData["Error"] = "Please fill in all required fields";
                return RedirectToAction("Register", new { step = 4 });
            }

            if (password != confirmPassword)
            {
                TempData["Error"] = "Passwords do not match";
                return RedirectToAction("Register", new { step = 4 });
            }

            if (password.Length < 6)
            {
                TempData["Error"] = "Password must be at least 6 characters long";
                return RedirectToAction("Register", new { step = 4 });
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
                // Get organization selection data
                var organizationType = TempData["OrganizationType"]?.ToString();
                var organizationName = TempData["OrganizationName"]?.ToString();
                var joinCode = TempData["JoinCode"]?.ToString();
                
                Certio.Domain.Organizations.OrganizationJoinCode? validJoin = null;
                if (organizationType == "join" && !string.IsNullOrWhiteSpace(joinCode))
                {
                    var joinSvc = HttpContext.RequestServices.GetService<Certio.Web.Services.IJoinCodeService>();
                    if (joinSvc != null)
                    {
                        validJoin = await joinSvc.GetValidAsync(joinCode);
                    }
                }

                // Check for existing external user to migrate BEFORE creating Identity user
                Certio.Domain.Users.User? existingExternalUserToMigrate = null;
                if (validJoin != null && 
                    !string.IsNullOrWhiteSpace(validJoin.TeamName) && 
                    validJoin.TeamName.StartsWith("NEW_CLIENT_EXTERNAL_IDS:", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract external user ID from metadata (format: "NEW_CLIENT_EXTERNAL_IDS:userId")
                    var externalUserIdStr = validJoin.TeamName.Substring("NEW_CLIENT_EXTERNAL_IDS:".Length);
                    if (int.TryParse(externalUserIdStr, out var externalUserId))
                    {
                        // Check if an external user with this email exists (check by email, not just ID)
                        var normalizedEmail = storedEmail?.ToLowerInvariant().Trim();
                        existingExternalUserToMigrate = await _context.Users
                            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail && u.Id == externalUserId);
                        
                        // If not found by ID, try to find by email only (in case of mismatch)
                        if (existingExternalUserToMigrate == null)
                        {
                            existingExternalUserToMigrate = await _context.Users
                                .Include(u => u.UserOrganizations)
                                .ThenInclude(uo => uo.Organization)
                                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail &&
                                    u.UserOrganizations.Any(uo => uo.IsActive && 
                                                                  uo.UserType == Certio.Domain.Users.UserTypes.External &&
                                                                  uo.Organization != null &&
                                                                  uo.Organization.Name.ToLower().EndsWith("'s external contacts")));
                        }
                    }
                }

                // Create Identity user
                var identityUser = new IdentityUser 
                { 
                    UserName = storedEmail, 
                    Email = storedEmail,
                    PhoneNumber = phoneNumber,
                    EmailConfirmed = true,
                    TwoFactorEnabled = true
                };
                
                var result = await _userManager.CreateAsync(identityUser, password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    TempData["Error"] = $"Failed to create account: {errors}";
                    return RedirectToAction("Register", new { step = 4 });
                }

                // Determine custom User - migrate existing external user or use existing/create new
                Certio.Domain.Users.User customUser;
                if (existingExternalUserToMigrate != null)
                {
                    // Migrate existing external user instead of creating new one
                    existingExternalUserToMigrate.FirstName = firstName;
                    existingExternalUserToMigrate.LastName = lastName;
                    existingExternalUserToMigrate.PhoneNumber = phoneNumber;
                    existingExternalUserToMigrate.IsActive = true;
                    existingExternalUserToMigrate.Color = "#69848C"; // Client color
                    // Don't update CreatedAt - preserve original creation date
                    _context.Users.Update(existingExternalUserToMigrate);
                    await _context.SaveChangesAsync();
                    customUser = existingExternalUserToMigrate;
                }
                else
                {
                    // Check if custom User already exists with this email
                    var existingCustomUser = await _context.Users
                        .FirstOrDefaultAsync(u => u.Email == storedEmail);
                    
                    if (existingCustomUser != null)
                    {
                        // Update existing custom User with registration details
                        existingCustomUser.FirstName = firstName;
                        existingCustomUser.LastName = lastName;
                        existingCustomUser.PhoneNumber = phoneNumber;
                        existingCustomUser.IsActive = true;
                        // Don't update CreatedAt - preserve original creation date
                        // Color will be set based on organization type below
                        _context.Users.Update(existingCustomUser);
                        await _context.SaveChangesAsync();
                        customUser = existingCustomUser;
                    }
                    else
                    {
                        // Create new custom User record
                        customUser = new Certio.Domain.Users.User
                        {
                            FirstName = firstName,
                            LastName = lastName,
                            Email = storedEmail,
                            PhoneNumber = phoneNumber,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                            // Color will be set based on organization type below
                        };

                        _context.Users.Add(customUser);
                        await _context.SaveChangesAsync();
                    }
                }

                // Determine organization and role based on selection
                int organizationId = 0;
                var userType = Certio.Domain.Users.UserTypes.Client;
                var organizationRole = Certio.Domain.Users.OrganizationRoles.Member;

                if (validJoin != null)
                {
                    // Joining existing organization - DO NOT create default organization
                    organizationId = validJoin.OrganizationId;
                    userType = validJoin.InvitedUserType;
                    organizationRole = validJoin.InvitedRole;
                }
                else if (organizationType == "join")
                {
                    // Invalid join code - should not have reached here, but handle gracefully
                    TempData["Error"] = "Invalid or expired join code. Please check your join code and try again.";
                    return RedirectToAction("Register", new { step = 2 });
                }
                else if (organizationType == "client")
                {
                    // Create new client organization
                    var org = new Certio.Domain.Organizations.Organization
                    {
                        Name = organizationName ?? $"{firstName} {lastName}'s Organization",
                        Description = "Client Organization",
                        OwnerId = customUser.Id,
                        Type = Certio.Domain.Organizations.OrganizationType.Client,
                        IsPersonal = false,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Organizations.Add(org);
                    await _context.SaveChangesAsync();
                    organizationId = org.Id;
                    userType = Certio.Domain.Users.UserTypes.Client;
                    organizationRole = Certio.Domain.Users.OrganizationRoles.Owner;
                }
                else if (organizationType == "lawfirm")
                {
                    // Create new law firm organization
                    var org = new Certio.Domain.Organizations.Organization
                    {
                        Name = organizationName ?? $"{firstName} {lastName}'s Law Firm",
                        Description = "Law Firm Organization",
                        OwnerId = customUser.Id,
                        Type = Certio.Domain.Organizations.OrganizationType.LawFirm,
                        IsPersonal = false,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Organizations.Add(org);
                    await _context.SaveChangesAsync();
                    organizationId = org.Id;
                    userType = Certio.Domain.Users.UserTypes.LawFirm;
                    organizationRole = Certio.Domain.Users.OrganizationRoles.ManagingPartner; // Law firm creator is Managing Partner
                }
                else if (organizationType == "eventplanner")
                {
                    // Create new event planning organization
                    // NOTE: EventPlanner orgs use LawFirm UserType internally (aliasing approach)
                    // The OrganizationType.EventPlanner determines UI display (Managing Director, Director, etc.)
                    var org = new Certio.Domain.Organizations.Organization
                    {
                        Name = organizationName ?? $"{firstName} {lastName}'s Event Planning Company",
                        Description = "Event Planning Organization",
                        OwnerId = customUser.Id,
                        Type = Certio.Domain.Organizations.OrganizationType.EventPlanner,
                        IsPersonal = false,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Organizations.Add(org);
                    await _context.SaveChangesAsync();
                    organizationId = org.Id;
                    userType = Certio.Domain.Users.UserTypes.LawFirm; // Reuse LawFirm type (aliasing)
                    organizationRole = Certio.Domain.Users.OrganizationRoles.ManagingPartner; // Stored as ManagingPartner, displays as Managing Director
                }
                else
                {
                    // Default: Create client organization
                    var org = new Certio.Domain.Organizations.Organization
                    {
                        Name = $"{firstName} {lastName}'s Organization",
                        Description = "Client Organization",
                        OwnerId = customUser.Id,
                        Type = Certio.Domain.Organizations.OrganizationType.Client,
                        IsPersonal = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Organizations.Add(org);
                    await _context.SaveChangesAsync();
                    organizationId = org.Id;
                    userType = Certio.Domain.Users.UserTypes.Client;
                    organizationRole = Certio.Domain.Users.OrganizationRoles.Owner;

                    // Update user to reflect personal organization
                    customUser.IsPersonalOrganization = true;
                    _context.Users.Update(customUser);
                }

                // Add user to organization (check for existing membership first)
                var existingMembership = await _context.UserOrganizations
                    .FirstOrDefaultAsync(uo => uo.UserId == customUser.Id && uo.OrganizationId == organizationId);
                
                if (existingMembership == null)
                {
                    var userOrg = new Certio.Domain.Users.UserOrganization
                    {
                        UserId = customUser.Id,
                        OrganizationId = organizationId,
                        UserType = userType,
                        Role = organizationRole,
                        IsPrimary = !await _context.UserOrganizations.AnyAsync(uo => uo.UserId == customUser.Id && uo.IsPrimary), // Only set as primary if no other primary exists
                        IsActive = true,
                        JoinedAt = DateTime.UtcNow
                    };
                    _context.UserOrganizations.Add(userOrg);
                    await _context.SaveChangesAsync();
                }
                else if (!existingMembership.IsActive)
                {
                    // Reactivate existing membership if it was inactive
                    existingMembership.IsActive = true;
                    existingMembership.UserType = userType;
                    existingMembership.Role = organizationRole;
                    existingMembership.JoinedAt = DateTime.UtcNow;
                    _context.UserOrganizations.Update(existingMembership);
                    await _context.SaveChangesAsync();
                }

                // Set user color based on organization type (only if not already set)
                if (string.IsNullOrEmpty(customUser.Color))
                {
                    customUser.Color = GetColorForUserType(userType);
                    _context.Users.Update(customUser);
                    await _context.SaveChangesAsync();
                }

                // If joined via code, consume code and handle external user migration
                if (validJoin != null)
                {
                    var joinSvc = HttpContext.RequestServices.GetService<Certio.Web.Services.IJoinCodeService>();
                    if (joinSvc != null)
                    {
                        await joinSvc.ConsumeAsync(validJoin.Code);
                    }

                    // Check if this is a "New Client" join code with external user transfer
                    if (!string.IsNullOrWhiteSpace(validJoin.TeamName) && 
                        validJoin.TeamName.StartsWith("NEW_CLIENT_EXTERNAL_IDS:", StringComparison.OrdinalIgnoreCase))
                    {
                        // If we migrated an external user, deactivate their external organization memberships
                        if (existingExternalUserToMigrate != null)
                        {
                            // Find external organization memberships for this user
                            var externalMemberships = await _context.UserOrganizations
                                .Include(uo => uo.Organization)
                                .Where(uo => uo.UserId == customUser.Id &&
                                             uo.IsActive &&
                                             uo.UserType == Certio.Domain.Users.UserTypes.External &&
                                             uo.Organization != null &&
                                             uo.Organization.Name.ToLower().EndsWith("'s external contacts"))
                                .ToListAsync();

                            // Deactivate external memberships (soft remove)
                            foreach (var extMembership in externalMemberships)
                            {
                                extMembership.IsActive = false;
                                extMembership.LeftAt = DateTime.UtcNow;
                            }

                            await _context.SaveChangesAsync();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(validJoin.TeamName) && 
                        !validJoin.TeamName.StartsWith("NEW_CLIENT_EXTERNAL_IDS:", StringComparison.OrdinalIgnoreCase))
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

                await _userManager.SetTwoFactorEnabledAsync(identityUser, true);

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

                TempData["Success"] = "Account created successfully! Welcome to Notal.";
                
                // Determine redirect URL based on organization type
                if (organizationId > 0)
                {
                    var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
                    if (org != null && org.Type == Certio.Domain.Organizations.OrganizationType.Client && 
                        userType == Certio.Domain.Users.UserTypes.Client)
                    {
                        // Redirect to client dashboard for client organizations
                        return RedirectToAction("Dashboard", "Client", new { orgId = organizationId });
                    }
                }
                
                // Default to Matter dashboard for law firm organizations or new organizations
                return RedirectToAction("Index", "Matter");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registration error: {ex.Message}");
                TempData["Error"] = "An error occurred during registration. Please try again.";
                return RedirectToAction("Register", new { step = 3 });
            }
        }

        /// <summary>
        /// Get color for user avatar based on organization type
        /// </summary>
        private static string GetColorForUserType(string userType)
        {
            return userType switch
            {
                Certio.Domain.Users.UserTypes.LawFirm => "#3d1019",
                Certio.Domain.Users.UserTypes.Client => "#69848C",
                Certio.Domain.Users.UserTypes.External => "#aaaaaa",
                _ => "#69848C" // Default to client color
            };
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "your email";
            }

            var parts = email.Split('@');
            if (parts.Length != 2)
            {
                return email;
            }

            var local = parts[0];
            var domain = parts[1];

            string MaskSegment(string segment)
            {
                if (string.IsNullOrEmpty(segment))
                {
                    return segment;
                }

                if (segment.Length <= 2)
                {
                    return segment[0] + new string('*', Math.Max(1, segment.Length - 1));
                }

                return segment[0] + new string('*', segment.Length - 2) + segment[^1];
            }

            var maskedLocal = MaskSegment(local);

            var domainParts = domain.Split('.', 2);
            var maskedDomainFirst = MaskSegment(domainParts[0]);
            var maskedDomain = domainParts.Length == 2
                ? string.Join('.', maskedDomainFirst, domainParts[1])
                : maskedDomainFirst;

            return string.Join('@', maskedLocal, maskedDomain);
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
            var sampleOrgId = Guid.NewGuid();
            var documents = new List<Document>
            {
                new Document
                {
                    OrgId = sampleOrgId,
                    SourceType = DocumentSourceType.InternalUpload,
                    Title = "Morrison Industries - Service Agreement",
                    FileType = "application/pdf",
                    FileSizeBytes = 2400000,
                    Status = DocumentStatus.Final,
                    Category = "Contract",
                    IsPrivate = true,
                    CreatedAt = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc),
                    ModifiedAt = new DateTime(2024, 1, 15, 8, 30, 0, DateTimeKind.Utc),
                    Tags = new List<string> { "Contract", "Client", "Morrison" },
                    Metadata = new Dictionary<string, string?> { { "provider", "internal" } },
                    PreviewUrl = "https://docs.example.com/preview/1",
                    DownloadUrl = "https://docs.example.com/download/1",
                    EmbedUrl = "https://docs.example.com/embed/1",
                    Author = "Rachel Adams"
                },
                new Document
                {
                    OrgId = sampleOrgId,
                    SourceType = DocumentSourceType.GoogleDrive,
                    Title = "Compliance Audit Report Q4 2023",
                    FileType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    FileSizeBytes = 5100000,
                    Status = DocumentStatus.Published,
                    Category = "Compliance",
                    IsPrivate = false,
                    CreatedAt = new DateTime(2023, 12, 22, 15, 0, 0, DateTimeKind.Utc),
                    ModifiedAt = new DateTime(2024, 1, 12, 10, 0, 0, DateTimeKind.Utc),
                    Tags = new List<string> { "Compliance", "Audit", "Q4" },
                    Metadata = new Dictionary<string, string?>
                    {
                        { "provider", "google" },
                        { "driveFolder", "Audits" }
                    },
                    PreviewUrl = "https://drive.google.com/preview/abc",
                    DownloadUrl = "https://drive.google.com/download/abc",
                    EmbedUrl = "https://drive.google.com/embed/abc",
                    Author = "Compliance Team"
                },
                new Document
                {
                    OrgId = sampleOrgId,
                    SourceType = DocumentSourceType.InternalUpload,
                    Title = "Legal Research - AI Regulations",
                    FileType = "application/pdf",
                    FileSizeBytes = 1800000,
                    Status = DocumentStatus.Draft,
                    Category = "Research",
                    IsPrivate = true,
                    CreatedAt = new DateTime(2024, 1, 5, 9, 0, 0, DateTimeKind.Utc),
                    ModifiedAt = new DateTime(2024, 1, 10, 14, 45, 0, DateTimeKind.Utc),
                    Tags = new List<string> { "Research", "AI", "Regulations" },
                    Metadata = new Dictionary<string, string?> { { "provider", "internal" } },
                    PreviewUrl = "https://docs.example.com/preview/3",
                    DownloadUrl = "https://docs.example.com/download/3",
                    EmbedUrl = "https://docs.example.com/embed/3",
                    Author = "Legal Research Group"
                },
                new Document
                {
                    OrgId = sampleOrgId,
                    SourceType = DocumentSourceType.OneDrive,
                    Title = "Client Onboarding Presentation",
                    FileType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                    FileSizeBytes = 12300000,
                    Status = DocumentStatus.Review,
                    Category = "Presentation",
                    IsPrivate = false,
                    CreatedAt = new DateTime(2023, 12, 30, 16, 0, 0, DateTimeKind.Utc),
                    ModifiedAt = new DateTime(2024, 1, 8, 11, 15, 0, DateTimeKind.Utc),
                    Tags = new List<string> { "Presentation", "Onboarding" },
                    Metadata = new Dictionary<string, string?>
                    {
                        { "provider", "microsoft" },
                        { "sharepointSite", "ClientSuccess" }
                    },
                    PreviewUrl = "https://onedrive.live.com/preview/xyz",
                    DownloadUrl = "https://onedrive.live.com/download/xyz",
                    EmbedUrl = "https://onedrive.live.com/embed/xyz",
                    Author = "Client Success"
                },
                new Document
                {
                    OrgId = sampleOrgId,
                    SourceType = DocumentSourceType.InternalUpload,
                    Title = "Contract Template Library",
                    FileType = "application/zip",
                    FileSizeBytes = 8700000,
                    Status = DocumentStatus.Final,
                    Category = "Templates",
                    IsPrivate = false,
                    CreatedAt = new DateTime(2023, 11, 2, 18, 0, 0, DateTimeKind.Utc),
                    ModifiedAt = new DateTime(2024, 1, 5, 13, 20, 0, DateTimeKind.Utc),
                    Tags = new List<string> { "Templates", "Contracts" },
                    Metadata = new Dictionary<string, string?> { { "provider", "internal" } },
                    PreviewUrl = "https://docs.example.com/preview/5",
                    DownloadUrl = "https://docs.example.com/download/5",
                    EmbedUrl = "https://docs.example.com/embed/5",
                    Author = "Template Working Group"
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
                    Color = "#3d1019"
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
                    Color = "#3d1019"
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
                    Color = "#3d1019"
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
                    Color = "#3d1019"
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
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Communications()
        {
            // Get current organization context
            var clientContext = _clientContextAccessor.ClientContext;
            if (clientContext == null || !clientContext.OrganizationId.HasValue)
            {
                return RedirectToAction("Index");
            }

            var organizationId = clientContext.OrganizationId.Value;
            
            // Get current user
            var identityUser = await _userManager.GetUserAsync(User);
            var customUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == identityUser.Email);

            if (customUser == null)
            {
                return RedirectToAction("Index");
            }

            // Get current organization to check if it's a law firm
            var currentOrg = await _context.Organizations.FindAsync(organizationId);
            var isLawFirm = currentOrg?.Type is OrganizationType.LawFirm or OrganizationType.EventPlanner;

            List<ChannelCategory> channelCategories;

            if (isLawFirm)
            {
                // LAW FIRM VIEW - Hierarchical structure with firm channels at top and client organizations below
                channelCategories = new List<ChannelCategory>();

                // 1. Get law firm's own channels (general, urgent-matters, client-onboarding)
                var firmChannels = await _channelManagementService.GetOrganizationChannelsAsync(organizationId);
                var firmChannelsWithUnread = new List<Channel>();
                
                foreach (var channel in firmChannels)
                {
                    var unreadCount = await _channelManagementService.GetUnreadCountAsync(channel.Id, customUser.Id);
                    firmChannelsWithUnread.Add(new Channel
                    {
                        Id = channel.Id,
                        Name = channel.Title,
                        Unread = unreadCount,
                        Type = ChannelType.Text,
                        IsPrivate = channel.IsPrivateChannel,
                        MatterId = channel.MatterId,
                        MatterTitle = channel.Matter?.Title
                    });
                }

                // Separate firm's general channels from matter channels
                var firmGeneralChannels = firmChannelsWithUnread.Where(c => !c.MatterId.HasValue && !c.IsPrivate).ToList();
                var firmPrivateChannels = firmChannelsWithUnread.Where(c => c.IsPrivate).ToList();

                // Add law firm organization category at the top
                channelCategories.Add(new ChannelCategory
                {
                    Name = currentOrg?.Name?.ToUpperInvariant() ?? "LAW FIRM",
                    Channels = firmGeneralChannels
                });

                // 2. Get all client organizations and their channels
                var clientOrgChannelsDict = await _channelManagementService.GetClientOrganizationChannelsForLawFirmAsync(organizationId, customUser.Id);
                
                if (clientOrgChannelsDict.Any())
                {
                    var clientSubcategories = new List<ChannelSubcategory>();

                    foreach (var kvp in clientOrgChannelsDict)
                    {
                        var clientOrgId = kvp.Key;
                        var clientChannels = kvp.Value;

                        var clientOrg = await _context.Organizations.FindAsync(clientOrgId);
                        if (clientOrg == null) continue;

                        var clientChannelsWithUnread = new List<Channel>();
                        foreach (var channel in clientChannels)
                        {
                            var unreadCount = await _channelManagementService.GetUnreadCountAsync(channel.Id, customUser.Id);
                            clientChannelsWithUnread.Add(new Channel
                            {
                                Id = channel.Id,
                                Name = channel.Title,
                                Unread = unreadCount,
                                Type = ChannelType.Text,
                                IsPrivate = channel.IsPrivateChannel,
                                MatterId = channel.MatterId,
                                MatterTitle = channel.Matter?.Title
                            });
                        }

                        clientSubcategories.Add(new ChannelSubcategory
                        {
                            Name = clientOrg.Name,
                            OrganizationId = clientOrgId,
                            Channels = clientChannelsWithUnread
                        });
                    }

                    // Add Client Communications category with subcategories for each client
                    channelCategories.Add(new ChannelCategory
                    {
                        Name = "CLIENT COMMUNICATIONS",
                        Channels = new List<Channel>(),
                        Subcategories = clientSubcategories
                    });
                }

                // Add private channels if any exist
                if (firmPrivateChannels.Any())
                {
                    channelCategories.Add(new ChannelCategory
                    {
                        Name = "LEGAL TEAM",
                        Channels = firmPrivateChannels
                    });
                }

                // Add voice channels category (placeholder for future)
                channelCategories.Add(new ChannelCategory
                {
                    Name = "VOICE CHANNELS",
                    Channels = new List<Channel>
                    {
                        new Channel { Id = -1, Name = "Client Consultations", Unread = 0, Type = ChannelType.Voice, IsPrivate = false, Users = new List<string>() },
                        new Channel { Id = -2, Name = "Team Meetings", Unread = 0, Type = ChannelType.Voice, IsPrivate = true, Users = new List<string>() }
                    }
                });
            }
            else
            {
                // CLIENT VIEW - Original structure
                var channels = await _channelManagementService.GetOrganizationChannelsAsync(organizationId);
                
                // Get unread counts for each channel
                var channelsWithUnread = new List<Channel>();
                foreach (var channel in channels)
                {
                    var unreadCount = await _channelManagementService.GetUnreadCountAsync(channel.Id, customUser.Id);
                    channelsWithUnread.Add(new Channel
                    {
                        Id = channel.Id,
                        Name = channel.Title,
                        Unread = unreadCount,
                        Type = ChannelType.Text,
                        IsPrivate = channel.IsPrivateChannel,
                        MatterId = channel.MatterId,
                        MatterTitle = channel.Matter?.Title
                    });
                }

                // Organize channels into categories
                var publicChannels = channelsWithUnread.Where(c => !c.IsPrivate).ToList();
                var privateChannels = channelsWithUnread.Where(c => c.IsPrivate).ToList();

                channelCategories = new List<ChannelCategory>
                {
                    new ChannelCategory
                    {
                        Name = "CLIENT COMMUNICATIONS",
                        Channels = publicChannels
                    }
                };

                // Add private channels if any exist
                if (privateChannels.Any())
                {
                    channelCategories.Add(new ChannelCategory
                    {
                        Name = "LEGAL TEAM",
                        Channels = privateChannels
                    });
                }

                // Add voice channels category (placeholder for future)
                channelCategories.Add(new ChannelCategory
                {
                    Name = "VOICE CHANNELS",
                    Channels = new List<Channel>
                    {
                        new Channel { Id = -1, Name = "Client Consultations", Unread = 0, Type = ChannelType.Voice, IsPrivate = false, Users = new List<string>() },
                        new Channel { Id = -2, Name = "Team Meetings", Unread = 0, Type = ChannelType.Voice, IsPrivate = true, Users = new List<string>() }
                    }
                });
            }

            // NOTE: When integrating real channel messages from database, ensure to populate:
            // - UserId field for each message
            // - CreatedAt field for each message
            // This enables proper message grouping and current user detection on server-side
            
            // For demo purposes, set some messages as from the current user
            var baseTime = DateTime.Now.AddHours(-3);
            var demoUserId1 = 1001; // Sarah Johnson
            var demoUserId2 = 1002; // Mike Chen  
            var demoUserId3 = 1003; // Alex Rodriguez
            var demoUserId4 = 1004; // Emily Davis
            
            var messages = new List<Message>
            {
                new Message
                {
                    Id = 1,
                    UserId = demoUserId1,
                    User = "Sarah Johnson",
                    Avatar = "SJ",
                    Time = "9:42 AM",
                    CreatedAt = baseTime,
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
                    UserId = demoUserId2,
                    User = "Mike Chen",
                    Avatar = "MC",
                    Time = "9:45 AM",
                    CreatedAt = baseTime.AddMinutes(3),
                    Content = "Great work! Can you share the summary report in the contract-reviews channel?",
                    Reactions = new List<Reaction>()
                },
                new Message
                {
                    Id = 3,
                    UserId = demoUserId3,
                    User = "Alex Rodriguez",
                    Avatar = "AR",
                    Time = "10:15 AM",
                    CreatedAt = baseTime.AddMinutes(33),
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
            
            // Assign UserIds to messages for proper server-side rendering
            // Assign specific user IDs to simulate different users (for demo)
            var messageUserMapping = new Dictionary<int, int>
            {
                { 1, demoUserId1 }, // Sarah Johnson
                { 2, demoUserId2 }, // Mike Chen
                { 3, demoUserId3 }, // Alex Rodriguez
                { 4, demoUserId4 }, // Emily Davis
                { 5, demoUserId1 }, // Sarah Johnson
                { 6, customUser.Id }, // Current logged-in user
                { 7, customUser.Id }, // Current logged-in user (consecutive message)
                { 8, demoUserId4 }, // Emily Davis
                { 9, demoUserId1 }, // Sarah Johnson
                { 10, demoUserId2 }, // Mike Chen
                { 11, demoUserId3 }, // Alex Rodriguez
                { 12, demoUserId4 }, // Emily Davis
                { 13, demoUserId1 }, // Sarah Johnson
                { 14, demoUserId2 }  // Mike Chen
            };
            
            // Apply the UserIds and CreatedAt times
            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                if (messageUserMapping.ContainsKey(message.Id))
                {
                    message.UserId = messageUserMapping[message.Id];
                }
                // Set CreatedAt if not already set
                if (!message.CreatedAt.HasValue)
                {
                    message.CreatedAt = baseTime.AddMinutes(i * 15);
                }
                // Update user name and avatar for current user messages
                if (message.UserId == customUser.Id)
                {
                    message.User = $"{customUser.FirstName} {customUser.LastName}".Trim();
                    message.Avatar = $"{customUser.FirstName?.FirstOrDefault() ?? '?'}{customUser.LastName?.FirstOrDefault() ?? '?'}".ToUpper();
                }
            }

            // Get team members from current organization
            var orgUsers = await _context.UserOrganizations
                .Include(uo => uo.User)
                .Include(uo => uo.Organization)
                .Where(uo => uo.OrganizationId == organizationId && 
                             uo.IsActive && 
                             uo.UserId > 0 &&
                             uo.User != null && 
                             uo.User.IsActive && 
                             !uo.User.IsDeleted)
                .ToListAsync();

            Console.WriteLine($"[DEBUG] Found {orgUsers.Count} users in current org {organizationId}");

            // Get users from related organizations (if this is a law firm)
            var relatedOrgUsers = new List<(UserOrganization uo, int relatedOrgId, string relatedOrgName)>();
            
            if (isLawFirm)
            {
                // Get all client organizations related to this law firm
                var clientRelationships = await _context.OrganizationRelationships
                    .Include(or => or.TargetOrganization)
                    .Where(or => or.SourceOrganizationId == organizationId && 
                                 or.IsActive &&
                                 Certio.Domain.Organizations.RelationshipTypes.ServiceProviderClientTypes.Contains(or.RelationshipType))
                    .ToListAsync();

                Console.WriteLine($"[DEBUG] Found {clientRelationships.Count} client relationships");

                foreach (var relationship in clientRelationships)
                {
                    var clientOrgId = relationship.TargetOrganizationId;
                    var clientOrgName = relationship.TargetOrganization?.Name ?? "Unknown";
                    
                    // Get users from this client organization
                    var clientUsers = await _context.UserOrganizations
                        .Include(uo => uo.User)
                        .Include(uo => uo.Organization)
                        .Where(uo => uo.OrganizationId == clientOrgId && 
                                     uo.IsActive && 
                                     uo.UserId > 0 &&
                                     uo.User != null && 
                                     uo.User.IsActive && 
                                     !uo.User.IsDeleted)
                        .ToListAsync();

                    Console.WriteLine($"[DEBUG] Found {clientUsers.Count} users in client org {clientOrgId} ({clientOrgName})");
                    
                    foreach (var clientUser in clientUsers)
                    {
                        relatedOrgUsers.Add((clientUser, clientOrgId, clientOrgName));
                    }
                }
            }

            // Use UserPresenceService for real-time presence detection
            var userPresenceService = HttpContext.RequestServices.GetService<IUserPresenceService>();
            var onlineUserIds = userPresenceService?.GetOnlineUsersInOrganization(organizationId) ?? new List<int>();

            // Map current org users (can DM)
            var teamMembers = orgUsers.Select(uo =>
            {
                var isOnline = onlineUserIds.Contains(uo.UserId);
                var initials = string.IsNullOrEmpty(uo.User?.FirstName) && string.IsNullOrEmpty(uo.User?.LastName)
                    ? uo.User?.Email?.Substring(0, 2).ToUpper() ?? "??"
                    : $"{uo.User?.FirstName?.FirstOrDefault() ?? '?'}{uo.User?.LastName?.FirstOrDefault() ?? '?'}";

                var role = uo.Role ?? "Team Member";
                var roleIcon = role.ToLower() switch
                {
                    "partner" or "managing partner" => "fas fa-crown",
                    "associate" or "lawyer" => "fas fa-gavel",
                    "paralegal" => "fas fa-file-alt",
                    "admin" or "administrator" => "fas fa-cog",
                    _ => "fas fa-user"
                };
                var roleColor = role.ToLower() switch
                {
                    "partner" or "managing partner" => "text-warning",
                    "associate" or "lawyer" => "text-primary",
                    "paralegal" => "text-info",
                    "admin" or "administrator" => "text-secondary",
                    _ => "text-muted"
                };

                var actualUserId = uo.User?.Id ?? uo.UserId;
                var isExternalContacts = uo.Organization?.Name?.EndsWith("'s External Contacts", StringComparison.OrdinalIgnoreCase) ?? false;
                
                var member = new CommunicationsTeamMember
                {
                    UserId = actualUserId,
                    OrganizationId = uo.OrganizationId,
                    Name = $"{uo.User?.FirstName ?? ""} {uo.User?.LastName ?? ""}".Trim(),
                    Role = role,
                    Status = isExternalContacts ? "external" : (isOnline ? "online" : "offline"),
                    Avatar = initials,
                    Activity = isExternalContacts ? "External" : (isOnline ? "Online" : "Offline"),
                    RoleIcon = roleIcon,
                    RoleColor = roleColor,
                    CanDirectMessage = true, // Same org, can DM
                    OrganizationName = uo.Organization?.Name,
                    IsExternalContacts = isExternalContacts,
                    Color = uo.User?.Color ?? "#3d1019", // Use user's color, default to maroon
                    Email = uo.User?.Email
                };
                Console.WriteLine($"[DEBUG] Current org member: UserId={member.UserId}, Name={member.Name}, CanDM=true");
                return member;
            }).ToList();

            // Add related org users (CAN now DM via relationship)
            foreach (var (uo, relatedOrgId, relatedOrgName) in relatedOrgUsers)
            {
                var isOnline = onlineUserIds.Contains(uo.UserId);
                var initials = string.IsNullOrEmpty(uo.User?.FirstName) && string.IsNullOrEmpty(uo.User?.LastName)
                    ? uo.User?.Email?.Substring(0, 2).ToUpper() ?? "??"
                    : $"{uo.User?.FirstName?.FirstOrDefault() ?? '?'}{uo.User?.LastName?.FirstOrDefault() ?? '?'}";

                var role = uo.Role ?? "Team Member";
                var roleIcon = "fas fa-building"; // Client user icon
                var roleColor = "text-info"; // Client user color

                var actualUserId = uo.User?.Id ?? uo.UserId;
                var isExternalContacts = relatedOrgName?.EndsWith("'s External Contacts", StringComparison.OrdinalIgnoreCase) ?? false;
                
                var member = new CommunicationsTeamMember
                {
                    UserId = actualUserId,
                    OrganizationId = relatedOrgId,
                    Name = $"{uo.User?.FirstName ?? ""} {uo.User?.LastName ?? ""}".Trim(),
                    Role = $"{role} ({relatedOrgName})",
                    Status = isExternalContacts ? "external" : (isOnline ? "online" : "offline"),
                    Avatar = initials,
                    Activity = isExternalContacts ? "External" : (isOnline ? "Online" : "Offline"),
                    RoleIcon = roleIcon,
                    RoleColor = roleColor,
                    CanDirectMessage = true, // NOW ALLOWED via relationship
                    OrganizationName = relatedOrgName,
                    IsExternalContacts = isExternalContacts,
                    Color = uo.User?.Color ?? "#3d1019", // Use user's color, default to maroon
                    Email = uo.User?.Email
                };
                Console.WriteLine($"[DEBUG] Related org member: UserId={member.UserId}, Name={member.Name}, Org={relatedOrgName}, CanDM=true");
                teamMembers.Add(member);
            }
            
            Console.WriteLine($"[DEBUG] Final teamMembers count: {teamMembers.Count}");

            // Filter to only show users with active DirectMessage threads (except current user)
            var currentUserId = customUser.Id;
            var activeThreadUserIds = await _context.DirectThreads
                .Where(dt => dt.OrganizationId == organizationId && 
                            !dt.IsDeleted &&
                            ((dt.UserAId == currentUserId && dt.UserBId != currentUserId) ||
                             (dt.UserBId == currentUserId && dt.UserAId != currentUserId)))
                .Select(dt => dt.UserAId == currentUserId ? dt.UserBId : dt.UserAId)
                .Distinct()
                .ToListAsync();
            
            // Get users from active threads who aren't in teamMembers yet (e.g., from External Contacts org)
            var missingUserIds = activeThreadUserIds.Where(id => id != currentUserId && !teamMembers.Any(m => m.UserId == id)).ToList();
            if (missingUserIds.Any())
            {
                var missingUsers = await _context.Users
                    .Include(u => u.UserOrganizations)
                        .ThenInclude(uo => uo.Organization)
                    .Where(u => missingUserIds.Contains(u.Id))
                    .ToListAsync();
                
                foreach (var user in missingUsers)
                {
                    // Find which organization this user belongs to (prefer External Contacts if they're in multiple)
                    var userOrg = user.UserOrganizations
                        .FirstOrDefault(uo => uo.IsActive && uo.Organization?.Name?.EndsWith("'s External Contacts", StringComparison.OrdinalIgnoreCase) == true)
                        ?? user.UserOrganizations.FirstOrDefault(uo => uo.IsActive);
                    
                    if (userOrg != null)
                    {
                        var isOnline = onlineUserIds.Contains(user.Id);
                        var initials = string.IsNullOrEmpty(user.FirstName) && string.IsNullOrEmpty(user.LastName)
                            ? user.Email?.Substring(0, 2).ToUpper() ?? "??"
                            : $"{user.FirstName?.FirstOrDefault() ?? '?'}{user.LastName?.FirstOrDefault() ?? '?'}";
                        
                        var isExternalContacts = userOrg.Organization?.Name?.EndsWith("'s External Contacts", StringComparison.OrdinalIgnoreCase) ?? false;
                        
                        var member = new CommunicationsTeamMember
                        {
                            UserId = user.Id,
                            OrganizationId = userOrg.OrganizationId,
                            Name = $"{user.FirstName ?? ""} {user.LastName ?? ""}".Trim(),
                            Role = userOrg.Role ?? "Guest",
                            Status = isExternalContacts ? "external" : (isOnline ? "online" : "offline"),
                            Avatar = initials,
                            Activity = isExternalContacts ? "External" : (isOnline ? "Online" : "Offline"),
                            RoleIcon = "fas fa-user",
                            RoleColor = "text-muted",
                            CanDirectMessage = true,
                            OrganizationName = userOrg.Organization?.Name,
                            IsExternalContacts = isExternalContacts,
                            Color = user.Color ?? "#3d1019", // Use user's color, default to maroon
                            Email = user.Email
                        };
                        teamMembers.Add(member);
                    }
                }
            }
            
            // Always include current user, then filter others to only those with active threads
            teamMembers = teamMembers
                .Where(m => m.UserId == currentUserId || activeThreadUserIds.Contains(m.UserId))
                .ToList();

            Console.WriteLine($"[DEBUG] Filtered teamMembers count (with active threads): {teamMembers.Count}");

            // Sort team members with priority:
            // 1. Current user first
            // 2. Current organization users (not from relationships)
            // 3. Relationship users (clients) who are NOT external contacts
            // 4. External contacts
            // Within each group: online users first, then alphabetically
            teamMembers = teamMembers
                .OrderByDescending(m => m.UserId == customUser.Id) // Current user first
                .ThenByDescending(m => m.OrganizationId == organizationId && !m.IsExternalContacts) // Current org users (not external)
                .ThenByDescending(m => m.OrganizationId != organizationId && !m.IsExternalContacts) // Relationship users (not external)
                .ThenBy(m => m.IsExternalContacts) // External contacts last
                .ThenByDescending(m => m.Status == "online") // Then online users
                .ThenBy(m => m.Name) // Then alphabetically
                .ToList();

            // Set active channel to the first available channel
            var firstCategory = channelCategories.FirstOrDefault();
            var activeChannel = firstCategory?.Channels.FirstOrDefault()?.Name ?? 
                               firstCategory?.Subcategories.FirstOrDefault()?.Channels.FirstOrDefault()?.Name ?? 
                               "general";

            // Set user info for JavaScript
            ViewBag.CurrentUserId = customUser.Id;
            ViewBag.CurrentUserName = $"{customUser.FirstName} {customUser.LastName}".Trim();
            ViewBag.OrganizationId = organizationId;

            var viewModel = new CommunicationsViewModel
            {
                ChannelCategories = channelCategories,
                Messages = messages,
                TeamMembers = teamMembers,
                ActiveChannel = activeChannel,
                OnlineMembersCount = teamMembers.Count(m => m.Status == "online")
            };

            Console.WriteLine($"[DEBUG] ViewModel created with {viewModel.TeamMembers.Count} team members");
            foreach (var member in viewModel.TeamMembers)
            {
                Console.WriteLine($"[DEBUG] ViewModel member: UserId={member.UserId}, Name={member.Name}");
            }

            return View(viewModel);
        }

        private string GetRelativeTime(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;
            
            if (timeSpan.TotalMinutes < 1)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes > 1 ? "s" : "")} ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours > 1 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays > 1 ? "s" : "")} ago";
            
            return dateTime.ToString("MMM d");
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

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
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

                // Get the organization details to check its type
                var targetOrganization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Id == validJoin.OrganizationId);

                if (targetOrganization == null)
                {
                    return Json(new { success = false, message = "Organization not found." });
                }

                // Check if trying to join a LawFirm organization when user is already part of one
                if (targetOrganization.Type == Certio.Domain.Organizations.OrganizationType.LawFirm)
                {
                    var existingLawFirmMembership = await _context.UserOrganizations
                        .Include(uo => uo.Organization)
                        .FirstOrDefaultAsync(uo => uo.UserId == customUser.Id && 
                                                   uo.IsActive && 
                                                   uo.Organization.Type == Certio.Domain.Organizations.OrganizationType.LawFirm);

                    if (existingLawFirmMembership != null)
                    {
                        return Json(new { success = false, message = "You are already a member of a law firm organization. You can only be part of one law firm at a time." });
                    }
                }

                // Add user to the organization. If this is a Law Firm invite to a Client org, we set membership as LawFirm user type directly for the Client org (direct membership)
                var userOrg = new Certio.Domain.Users.UserOrganization
                {
                    UserId = customUser.Id,
                    OrganizationId = validJoin.OrganizationId,
                    UserType = validJoin.InvitedUserType,
                    Role = validJoin.InvitedRole,
                    JoinedAt = DateTime.UtcNow
                };
                _context.UserOrganizations.Add(userOrg);

                // Check if this is a "New Client" join code with external user transfer
                if (!string.IsNullOrWhiteSpace(validJoin.TeamName) && 
                    validJoin.TeamName.StartsWith("NEW_CLIENT_EXTERNAL_IDS:", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract external user ID from metadata (format: "NEW_CLIENT_EXTERNAL_IDS:userId")
                    var externalUserIdStr = validJoin.TeamName.Substring("NEW_CLIENT_EXTERNAL_IDS:".Length);
                    if (int.TryParse(externalUserIdStr, out var externalUserId))
                    {
                        // If the joining user is the external user, transfer their membership
                        if (externalUserId == customUser.Id)
                        {
                            // Find external organization memberships for this user
                            var externalMemberships = await _context.UserOrganizations
                                .Include(uo => uo.Organization)
                                .Where(uo => uo.UserId == customUser.Id &&
                                             uo.IsActive &&
                                             uo.UserType == Certio.Domain.Users.UserTypes.External &&
                                             uo.Organization != null &&
                                             uo.Organization.Name.ToLower().EndsWith("'s external contacts"))
                                .ToListAsync();

                            // Deactivate external memberships (soft remove)
                            foreach (var extMembership in externalMemberships)
                            {
                                extMembership.IsActive = false;
                                extMembership.LeftAt = DateTime.UtcNow;
                            }

                            // Update user color to client color (#A6D0DD - light blue)
                            customUser.Color = "#69848C";
                            _context.Users.Update(customUser);

                            await _context.SaveChangesAsync();
                        }
                    }
                }

                // If this is a Law Firm invitation, upsert the LawFirmClient relationship from the user's firm to the client and assign the user
                if (string.Equals(validJoin.InvitedUserType, Certio.Domain.Users.UserTypes.LawFirm, StringComparison.OrdinalIgnoreCase))
                {
                    // Try to locate the user's firm (if they already belong to one)
                    var userFirmMembership = await _context.UserOrganizations
                        .Include(uo => uo.Organization)
                        .FirstOrDefaultAsync(uo => uo.UserId == customUser.Id && uo.IsActive && uo.Organization.Type == Certio.Domain.Organizations.OrganizationType.LawFirm);

                    if (userFirmMembership != null)
                    {
                        // Upsert relationship
                        var existingRelationship = await _context.OrganizationRelationships
                            .FirstOrDefaultAsync(or =>
                                or.SourceOrganizationId == userFirmMembership.OrganizationId &&
                                or.TargetOrganizationId == validJoin.OrganizationId &&
                                or.RelationshipType == Certio.Domain.Organizations.RelationshipTypes.LawFirmClient);

                        if (existingRelationship == null)
                        {
                            existingRelationship = new Certio.Domain.Organizations.OrganizationRelationship
                            {
                                SourceOrganizationId = userFirmMembership.OrganizationId,
                                TargetOrganizationId = validJoin.OrganizationId,
                                RelationshipType = Certio.Domain.Organizations.RelationshipTypes.LawFirmClient,
                                AccessLevel = Certio.Domain.Organizations.AccessLevels.FullAccess,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow,
                                CreatedById = customUser.Id
                            };
                            _context.OrganizationRelationships.Add(existingRelationship);
                            await _context.SaveChangesAsync();
                        }

                        // Assign user to relationship if not already assigned
                        var alreadyAssigned = await _context.OrganizationRelationshipAssignedUsers
                            .AnyAsync(a => a.RelationshipId == existingRelationship.Id && a.UserId == customUser.Id);
                        if (!alreadyAssigned)
                        {
                            _context.OrganizationRelationshipAssignedUsers.Add(new Certio.Domain.Organizations.OrganizationRelationshipAssignedUser
                            {
                                RelationshipId = existingRelationship.Id,
                                UserId = customUser.Id,
                                AssignedAt = DateTime.UtcNow,
                                AssignedById = customUser.Id
                            });
                        }
                    }
                    else
                    {
                        // Defer role and assignment resolution until after the user joins/creates a firm
                        var resolver = HttpContext.RequestServices.GetService<Certio.Web.Services.ILawFirmRoleResolutionService>();
                        if (resolver != null)
                        {
                            await resolver.EnqueueResolveAsync(customUser.Id, validJoin.OrganizationId);
                        }
                    }
                }

                // Consume the join code and persist
                await _joinCodeService.ConsumeAsync(validJoin.Code);
                await _context.SaveChangesAsync();

                // Determine redirect URL based on organization type
                var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == validJoin.OrganizationId);
                string redirectUrl;
                
                if (org != null && org.Type == Certio.Domain.Organizations.OrganizationType.Client && 
                    validJoin.InvitedUserType == Certio.Domain.Users.UserTypes.Client)
                {
                    // Redirect to client dashboard for client organizations
                    redirectUrl = Url.Action("Dashboard", "Client", new { orgId = validJoin.OrganizationId });
                }
                else
                {
                    // Default to Matter dashboard for law firm organizations
                    redirectUrl = Url.Action("Index", "Matter");
                }

                return Json(new { 
                    success = true, 
                    message = "Successfully joined the organization!",
                    redirectUrl = redirectUrl
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
