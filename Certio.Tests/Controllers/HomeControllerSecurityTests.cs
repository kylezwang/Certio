using Certio.Infrastructure.Data;
using Certio.Web.Controllers;
using Certio.Web.Services;
using Certio.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading;

namespace Certio.Tests.Controllers;

public class HomeControllerSecurityTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public HomeControllerSecurityTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldEnforceTwoFactor()
    {
        // Arrange
        var user = new IdentityUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "security@example.com",
            UserName = "security@example.com",
            EmailConfirmed = true
        };

        var controllerSetup = CreateController(twoFactorServiceMockSetup: out var twoFactorServiceMock, userManagerMockSetup: out var userManagerMock, signInManagerMockSetup: out var signInManagerMock);
        var controller = controllerSetup.Controller;
        var httpContext = controllerSetup.HttpContext;

        userManagerMock.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        signInManagerMock.Setup(m => m.CheckPasswordSignInAsync(user, "CorrectHorseBatteryStaple", true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        twoFactorServiceMock.Setup(s => s.GenerateVerificationCodeAsync()).ReturnsAsync("123456");
        twoFactorServiceMock.Setup(s => s.ProtectCode("123456")).Returns("protected-123456");
        twoFactorServiceMock.Setup(s => s.SendEmailVerificationAsync(user.Email!, "123456"))
            .ReturnsAsync(true);

        // Act
        var result = await controller.Login(user.Email!, "CorrectHorseBatteryStaple", remember: true);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(HomeController.LoginTwoFactor), redirect.ActionName);

        Assert.Equal(user.Id, httpContext.Session.GetString("Auth:TwoFactorUserId"));
        Assert.Equal("protected-123456", httpContext.Session.GetString("Auth:TwoFactorCode"));
        Assert.Equal(user.Email, httpContext.Session.GetString("Auth:TwoFactorEmail"));
        Assert.Equal("0", httpContext.Session.GetString("Auth:TwoFactorAttempts"));

        twoFactorServiceMock.Verify(s => s.SendEmailVerificationAsync(user.Email!, "123456"), Times.Once);
        signInManagerMock.Verify(m => m.SignInAsync(It.IsAny<IdentityUser>(), It.IsAny<bool>(), null), Times.Never);
    }

    [Fact]
    public async Task VerifyLoginTwoFactor_WithValidCode_ShouldSignInAndClearSession()
    {
        // Arrange
        var user = new IdentityUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "mfa-user@example.com",
            UserName = "mfa-user@example.com",
            EmailConfirmed = false
        };

        var controllerSetup = CreateController(twoFactorServiceMockSetup: out var twoFactorServiceMock, userManagerMockSetup: out var userManagerMock, signInManagerMockSetup: out var signInManagerMock);
        var controller = controllerSetup.Controller;
        var httpContext = controllerSetup.HttpContext;

        var expiry = DateTime.UtcNow.AddMinutes(10);
        httpContext.Session.SetString("Auth:TwoFactorUserId", user.Id);
        httpContext.Session.SetString("Auth:TwoFactorCode", "protected-123456");
        httpContext.Session.SetString("Auth:TwoFactorExpiresAt", expiry.ToString("O"));
        httpContext.Session.SetString("Auth:TwoFactorRememberMe", "true");
        httpContext.Session.SetString("Auth:TwoFactorEmail", user.Email!);
        httpContext.Session.SetString("Auth:TwoFactorAttempts", "0");

        twoFactorServiceMock.Setup(s => s.VerifyProtectedCode("123456", "protected-123456", expiry))
            .Returns(true);

        userManagerMock.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        userManagerMock.Setup(m => m.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        userManagerMock.Setup(m => m.SetTwoFactorEnabledAsync(user, true)).ReturnsAsync(IdentityResult.Success);
        userManagerMock.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var model = new TwoFactorVerificationViewModel { VerificationCode = "123456" };

        // Act
        var result = await controller.VerifyLoginTwoFactor(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Matter", redirect.ControllerName);

        Assert.Null(httpContext.Session.GetString("Auth:TwoFactorUserId"));
        Assert.NotNull(httpContext.Session.GetString("UserId"));
        Assert.Equal(user.Id, httpContext.Session.GetString("UserId"));
        Assert.Equal(user.Email, httpContext.Session.GetString("UserEmail"));

        signInManagerMock.Verify(m => m.SignInAsync(user, true, null), Times.Once);
        userManagerMock.Verify(m => m.SetTwoFactorEnabledAsync(user, true), Times.Once);
        userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task VerifyLoginTwoFactor_InvalidCode_ShouldIncrementAttempts()
    {
        // Arrange
        var user = new IdentityUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "locked@example.com",
            UserName = "locked@example.com",
            EmailConfirmed = true
        };

        var controllerSetup = CreateController(twoFactorServiceMockSetup: out var twoFactorServiceMock, userManagerMockSetup: out var userManagerMock, signInManagerMockSetup: out _);
        var controller = controllerSetup.Controller;
        var httpContext = controllerSetup.HttpContext;

        var expiry = DateTime.UtcNow.AddMinutes(10);
        httpContext.Session.SetString("Auth:TwoFactorUserId", user.Id);
        httpContext.Session.SetString("Auth:TwoFactorCode", "protected-123456");
        httpContext.Session.SetString("Auth:TwoFactorExpiresAt", expiry.ToString("O"));
        httpContext.Session.SetString("Auth:TwoFactorAttempts", "0");

        userManagerMock.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        twoFactorServiceMock.Setup(s => s.VerifyProtectedCode("111111", "protected-123456", expiry)).Returns(false);

        var model = new TwoFactorVerificationViewModel { VerificationCode = "111111" };

        // Act
        var result = await controller.VerifyLoginTwoFactor(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(HomeController.LoginTwoFactor), redirect.ActionName);
        Assert.Equal("1", httpContext.Session.GetString("Auth:TwoFactorAttempts"));
        userManagerMock.Verify(m => m.AccessFailedAsync(It.IsAny<IdentityUser>()), Times.Never);
    }

    private ControllerSetup CreateController(out Mock<ITwoFactorService> twoFactorServiceMockSetup, out Mock<UserManager<IdentityUser>> userManagerMockSetup, out Mock<SignInManager<IdentityUser>> signInManagerMockSetup)
    {
        var userStore = new Mock<IUserStore<IdentityUser>>();
        var identityOptions = Options.Create(new IdentityOptions());
        var passwordHasher = new Mock<IPasswordHasher<IdentityUser>>();
        var userValidators = new List<IUserValidator<IdentityUser>> { new UserValidator<IdentityUser>() };
        var passwordValidators = new List<IPasswordValidator<IdentityUser>> { new PasswordValidator<IdentityUser>() };
        var lookupNormalizer = new Mock<ILookupNormalizer>();
        lookupNormalizer.Setup(n => n.NormalizeEmail(It.IsAny<string>())).Returns<string>(s => s);
        lookupNormalizer.Setup(n => n.NormalizeName(It.IsAny<string>())).Returns<string>(s => s);
        var userManagerLogger = new Mock<ILogger<UserManager<IdentityUser>>>();

        var userManagerMock = new Mock<UserManager<IdentityUser>>(
            userStore.Object,
            identityOptions,
            passwordHasher.Object,
            userValidators,
            passwordValidators,
            lookupNormalizer.Object,
            new IdentityErrorDescriber(),
            null!,
            userManagerLogger.Object);

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<IdentityUser>>();
        claimsFactory.Setup(f => f.CreateAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(new System.Security.Claims.ClaimsPrincipal(new ClaimsIdentity("Identity.Application")));

        var signInLogger = new Mock<ILogger<SignInManager<IdentityUser>>>();
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var confirmation = new Mock<IUserConfirmation<IdentityUser>>();
        confirmation.Setup(c => c.IsConfirmedAsync(userManagerMock.Object, It.IsAny<IdentityUser>()))
            .ReturnsAsync(true);

        var signInManagerMock = new Mock<SignInManager<IdentityUser>>(
            userManagerMock.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            identityOptions,
            signInLogger.Object,
            schemes.Object,
            confirmation.Object);

        var twoFactorServiceMock = new Mock<ITwoFactorService>();
        var briefingServiceMock = new Mock<Certio.Web.Services.IBriefingMessageService>();

        var joinCodeService = new Mock<IJoinCodeService>();
        var channelManagementService = new Mock<IChannelManagementService>();
        var clientContextAccessor = new Mock<IClientContextAccessor>();

        var controller = new HomeController(
            signInManagerMock.Object,
            userManagerMock.Object,
            twoFactorServiceMock.Object,
            briefingServiceMock.Object,
            _context,
            joinCodeService.Object,
            channelManagementService.Object,
            clientContextAccessor.Object);

        var httpContext = CreateHttpContextWithSession();
        contextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        twoFactorServiceMockSetup = twoFactorServiceMock;
        userManagerMockSetup = userManagerMock;
        signInManagerMockSetup = signInManagerMock;

        return new ControllerSetup(controller, httpContext);
    }

    private sealed record ControllerSetup(HomeController Controller, DefaultHttpContext HttpContext);

    private static DefaultHttpContext CreateHttpContextWithSession()
    {
        var context = new DefaultHttpContext();
        var session = new TestSession();
        context.Features.Set<ISessionFeature>(new SessionFeature { Session = session });
        return context;
    }

    private sealed class SessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = default!;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public IEnumerable<string> Keys => _store.Keys;
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsAvailable => true;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[]? value)
        {
            if (_store.TryGetValue(key, out var result))
            {
                value = result;
                return true;
            }
            value = null;
            return false;
        }
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

