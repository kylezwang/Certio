using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Certio.Infrastructure.Data;
using Certio.Domain.Organizations;
using Certio.Domain.Users;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Controllers
{
    /// <summary>
    /// Handles OAuth external login flow for Google and Microsoft.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;
        private readonly IConfiguration _configuration;

        public AccountController(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context,
            ILogger<AccountController> logger,
            IConfiguration configuration)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// API endpoint to check if OAuth providers are available
        /// </summary>
        [HttpGet]
        [Route("api/auth/providers")]
        public async Task<IActionResult> GetAvailableProviders()
        {
            var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
            var providers = schemes.Select(s => s.Name).ToList();
            
            return Ok(new { 
                success = true, 
                providers = providers,
                hasGoogle = providers.Contains("Google"),
                hasMicrosoft = providers.Contains("Microsoft")
            });
        }

        /// <summary>
        /// API endpoint to get OAuth authorization URL for popup flow
        /// </summary>
        [HttpGet]
        [Route("api/auth/oauth-url")]
        public IActionResult GetOAuthUrl(string provider, string? returnUrl = null)
        {
            // Check if provider is configured
            var schemes = _signInManager.GetExternalAuthenticationSchemesAsync().Result;
            if (!schemes.Any(s => s.Name.Equals(provider, StringComparison.OrdinalIgnoreCase)))
            {
                return Ok(new { 
                    success = false, 
                    error = $"{provider} authentication is not configured. Please contact your administrator." 
                });
            }

            // Build the OAuth URL - this will be the Challenge URL
            var callbackUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl, popup = true }, Request.Scheme);
            
            return Ok(new { 
                success = true, 
                // The actual OAuth flow starts at ExternalLogin action
                authorizationUrl = Url.Action(nameof(ExternalLogin), "Account", new { provider, returnUrl, popup = true }, Request.Scheme)
            });
        }

        /// <summary>
        /// Initiates external login (redirects to Google/Microsoft)
        /// </summary>
        [HttpGet]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null, bool popup = false)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl, popup });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        /// <summary>
        /// Handles the callback from external OAuth provider
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null, bool popup = false)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                _logger.LogWarning("External login error: {Error}", remoteError);
                if (popup)
                {
                    return View("OAuthPopupResult", new OAuthPopupResult { 
                        Success = false, 
                        Error = $"Error from external provider: {remoteError}" 
                    });
                }
                TempData["Error"] = $"Error from external provider: {remoteError}";
                return RedirectToAction("Index", "Home");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogWarning("External login info was null");
                if (popup)
                {
                    return View("OAuthPopupResult", new OAuthPopupResult { 
                        Success = false, 
                        Error = "Error loading external login information." 
                    });
                }
                TempData["Error"] = "Error loading external login information.";
                return RedirectToAction("Index", "Home");
            }

            // Try to sign in the user with this external login provider
            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, 
                info.ProviderKey, 
                isPersistent: false, 
                bypassTwoFactor: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in with {Provider}", info.LoginProvider);
                
                // Get the user and redirect to their dashboard
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                string? redirectTo = null;
                
                if (!string.IsNullOrEmpty(email))
                {
                    var customUser = await _context.Users
                        .Include(u => u.UserOrganizations)
                        .ThenInclude(uo => uo.Organization)
                        .FirstOrDefaultAsync(u => u.Email == email);

                    if (customUser?.UserOrganizations?.Any(uo => uo.IsActive) == true)
                    {
                        var orgId = customUser.UserOrganizations.First(uo => uo.IsActive).OrganizationId;
                        redirectTo = Url.Action("Dashboard", "Client", new { orgId }, Request.Scheme);
                    }
                }

                if (popup)
                {
                    return View("OAuthPopupResult", new OAuthPopupResult { 
                        Success = true, 
                        RedirectUrl = redirectTo ?? Url.Content("~/"),
                        Email = email
                    });
                }
                
                return redirectTo != null ? Redirect(redirectTo) : LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                if (popup)
                {
                    return View("OAuthPopupResult", new OAuthPopupResult { 
                        Success = false, 
                        Error = "Your account is locked out. Please try again later." 
                    });
                }
                TempData["Error"] = "Your account is locked out. Please try again later.";
                return RedirectToAction("Index", "Home");
            }

            // User does not have an account - auto-create account with OAuth info
            var userEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
            var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "";
            var lastName = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? "";
            var fullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? "";

            // If we don't have separate first/last names, try to split the full name
            if (string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(fullName))
            {
                var nameParts = fullName.Split(' ', 2);
                firstName = nameParts[0];
                lastName = nameParts.Length > 1 ? nameParts[1] : "";
            }

            if (string.IsNullOrEmpty(userEmail))
            {
                _logger.LogError("OAuth email is null for provider {Provider}", info.LoginProvider);
                if (popup)
                {
                    return View("OAuthPopupResult", new OAuthPopupResult { 
                        Success = false, 
                        Error = "Email not provided by OAuth provider." 
                    });
                }
                TempData["Error"] = "Email not provided by OAuth provider.";
                return RedirectToAction("Index", "Home");
            }

            _logger.LogInformation("Auto-creating account for OAuth user {Email}", userEmail);

            // Create new Identity user (no password for OAuth users)
            var newIdentityUser = new IdentityUser
            {
                UserName = userEmail,
                Email = userEmail,
                EmailConfirmed = true // OAuth providers verify email
            };

            var createUserResult = await _userManager.CreateAsync(newIdentityUser);
            if (!createUserResult.Succeeded)
            {
                var errors = string.Join(", ", createUserResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create Identity user for OAuth: {Errors}", errors);
                if (popup)
                {
                    return View("OAuthPopupResult", new OAuthPopupResult { 
                        Success = false, 
                        Error = $"Failed to create account: {errors}" 
                    });
                }
                TempData["Error"] = $"Failed to create account: {errors}";
                return RedirectToAction("Index", "Home");
            }

            // Link OAuth login to the new user
            var addLoginResult = await _userManager.AddLoginAsync(newIdentityUser, info);
            if (!addLoginResult.Succeeded)
            {
                await _userManager.DeleteAsync(newIdentityUser);
                var errors = string.Join(", ", addLoginResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to link OAuth login: {Errors}", errors);
                if (popup)
                {
                    return View("OAuthPopupResult", new OAuthPopupResult { 
                        Success = false, 
                        Error = "Failed to link OAuth login." 
                    });
                }
                TempData["Error"] = "Failed to link OAuth login.";
                return RedirectToAction("Index", "Home");
            }

            // Create custom User record
            var newCustomUser = new User
            {
                Email = userEmail,
                FirstName = firstName,
                LastName = lastName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newCustomUser);
            await _context.SaveChangesAsync();

            // Sign in the user
            await _signInManager.SignInAsync(newIdentityUser, isPersistent: false);

            _logger.LogInformation("OAuth account created successfully for {Email}, redirecting to organization setup", userEmail);

            // Redirect to organization setup (step 2)
            var orgSetupUrl = Url.Action("Register", "Home", new { step = 2, oauth = true }, Request.Scheme);
            
            // Store user info in TempData for organization setup
            TempData["RegistrationEmail"] = userEmail;
            TempData["OAuthFirstName"] = firstName;
            TempData["OAuthLastName"] = lastName;
            TempData["IsVerified"] = true;

            if (popup)
            {
                return View("OAuthPopupResult", new OAuthPopupResult { 
                    Success = true, 
                    NeedsRegistration = true,
                    RedirectUrl = orgSetupUrl,
                    Email = userEmail,
                    FirstName = firstName,
                    LastName = lastName,
                    Provider = info.LoginProvider
                });
            }

            return Redirect(orgSetupUrl!);
        }
        
        /// <summary>
        /// Result model for OAuth popup
        /// </summary>
        public class OAuthPopupResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? RedirectUrl { get; set; }
            public string? Email { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Provider { get; set; }
            public bool NeedsRegistration { get; set; }
        }

        /// <summary>
        /// Completes OAuth registration (creates account and links external login)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteOAuthRegistration(
            string email,
            string firstName,
            string lastName,
            string organizationType,
            string? organizationName,
            string? joinCode)
        {
            // Get OAuth info from session
            var provider = HttpContext.Session.GetString("OAuthProvider");
            var providerKey = HttpContext.Session.GetString("OAuthProviderKey");

            if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(providerKey))
            {
                TempData["Error"] = "OAuth session expired. Please try signing in again.";
                return RedirectToAction("Index", "Home");
            }

            // Check if email already exists
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                // Link the OAuth login to existing account
                var addLoginResult = await _userManager.AddLoginAsync(existingUser, 
                    new UserLoginInfo(provider, providerKey, provider));
                
                if (addLoginResult.Succeeded)
                {
                    await _signInManager.SignInAsync(existingUser, isPersistent: false);
                    ClearOAuthSession();
                    
                    // Find user's organization
                    var customUser = await _context.Users
                        .Include(u => u.UserOrganizations)
                        .FirstOrDefaultAsync(u => u.Email == email);
                    
                    if (customUser?.UserOrganizations?.Any(uo => uo.IsActive) == true)
                    {
                        return RedirectToAction("Dashboard", "Client", 
                            new { orgId = customUser.UserOrganizations.First(uo => uo.IsActive).OrganizationId });
                    }
                }
                
                TempData["Error"] = "Failed to link account. Please try again.";
                return RedirectToAction("Index", "Home");
            }

            // Create new Identity user (no password for OAuth users)
            var identityUser = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true // OAuth providers verify email
            };

            var createResult = await _userManager.CreateAsync(identityUser);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create Identity user: {Errors}", errors);
                TempData["Error"] = $"Failed to create account: {errors}";
                return RedirectToAction("Register", "Home");
            }

            // Link OAuth login to the new user
            var loginResult = await _userManager.AddLoginAsync(identityUser, 
                new UserLoginInfo(provider, providerKey, provider));
            
            if (!loginResult.Succeeded)
            {
                await _userManager.DeleteAsync(identityUser);
                TempData["Error"] = "Failed to link OAuth login. Please try again.";
                return RedirectToAction("Register", "Home");
            }

            // Create custom User record
            var customNewUser = new User
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(customNewUser);
            await _context.SaveChangesAsync();

            // Handle organization setup
            if (organizationType == "join" && !string.IsNullOrEmpty(joinCode))
            {
                // Join existing organization with code
                var invitation = await _context.OrganizationJoinCodes
                    .Include(i => i.Organization)
                    .FirstOrDefaultAsync(i => i.Code == joinCode && 
                                              i.IsActive && 
                                              i.UsesRemaining > 0 &&
                                              i.ExpiresAt > DateTime.UtcNow);

                if (invitation != null)
                {
                    var userOrg = new UserOrganization
                    {
                        OrganizationId = invitation.OrganizationId,
                        UserId = customNewUser.Id,
                        Role = invitation.InvitedRole ?? "Member",
                        UserType = invitation.InvitedUserType ?? UserTypes.LawFirm,
                        JoinedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    _context.UserOrganizations.Add(userOrg);
                    invitation.UsesRemaining--;
                    invitation.LastUsedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                else
                {
                    TempData["Error"] = "Invalid or expired invitation code.";
                    return RedirectToAction("Register", "Home");
                }
            }
            else if (!string.IsNullOrEmpty(organizationName))
            {
                // Create new organization
                var orgType = OrganizationType.LawFirm;
                
                var organization = new Organization
                {
                    Name = organizationName,
                    Type = orgType,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Organizations.Add(organization);
                await _context.SaveChangesAsync();

                // Add user as admin of the organization
                var userOrg = new UserOrganization
                {
                    OrganizationId = organization.Id,
                    UserId = customNewUser.Id,
                    Role = "Admin",
                    UserType = UserTypes.LawFirm,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.UserOrganizations.Add(userOrg);
                await _context.SaveChangesAsync();
            }

            // Sign in the user
            await _signInManager.SignInAsync(identityUser, isPersistent: false);
            ClearOAuthSession();

            _logger.LogInformation("OAuth user {Email} completed registration with {Provider}", email, provider);

            // Redirect to dashboard
            var userMembership = await _context.UserOrganizations
                .FirstOrDefaultAsync(m => m.UserId == customNewUser.Id && m.IsActive);

            if (userMembership != null)
            {
                return RedirectToAction("Dashboard", "Client", new { orgId = userMembership.OrganizationId });
            }

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Gets available external authentication providers
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetExternalProviders()
        {
            var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
            var providers = schemes.Select(s => new { s.Name, s.DisplayName }).ToList();
            return Json(providers);
        }

        private void ClearOAuthSession()
        {
            HttpContext.Session.Remove("OAuthProvider");
            HttpContext.Session.Remove("OAuthProviderKey");
            HttpContext.Session.Remove("OAuthEmail");
            HttpContext.Session.Remove("OAuthFirstName");
            HttpContext.Session.Remove("OAuthLastName");
        }
    }
}
