using System.Text;
using System.Text.Json;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Certio.Web.Controllers.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Threading;

namespace Certio.Tests.Controllers.Api;

public class EmailWebhookControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IEmailToDmService> _emailToDmServiceMock;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<EmailWebhookController>> _loggerMock;

    public EmailWebhookControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _emailServiceMock = new Mock<IEmailService>();
        _emailToDmServiceMock = new Mock<IEmailToDmService>();
        _loggerMock = new Mock<ILogger<EmailWebhookController>>();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
    }

    [Fact]
    public async Task GmailWebhook_MissingToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmailIntegration:GmailVerificationToken", "secret-token" }
            })
            .Build();

        var controller = CreateController(configuration);

        // Act
        var result = await controller.GmailWebhook(new { }, CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GmailWebhook_WithValidToken_ShouldProcess()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmailIntegration:GmailVerificationToken", "secret-token" }
            })
            .Build();

        var controller = CreateController(configuration, httpContextSetup: ctx =>
        {
            ctx.Request.Headers["X-Goog-Channel-Token"] = "secret-token";
        });

        var emailAccount = new Certio.Domain.Services.EmailAccount
        {
            Id = 1,
            EmailAddress = "user@example.com",
            Provider = "Gmail",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.EmailAccounts.Add(emailAccount);
        await _context.SaveChangesAsync();

        var notificationData = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "emailAddress", "user@example.com" }
        });

        var payload = new Dictionary<string, object?>
        {
            {
                "message", new Dictionary<string, object?>
                {
                    { "data", Convert.ToBase64String(Encoding.UTF8.GetBytes(notificationData)) },
                    { "attributes", new Dictionary<string, string?> { { "token", "secret-token" } } }
                }
            }
        };

        // Act
        var result = await controller.GmailWebhook(payload, CancellationToken.None);

        // Assert
        Assert.IsType<OkResult>(result);
        _emailServiceMock.Verify(s => s.SyncEmailsAsync(emailAccount.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OutlookWebhook_InvalidClientState_ShouldReturnUnauthorized()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmailIntegration:WebhookSecret", "expected-secret" }
            })
            .Build();

        var controller = CreateController(configuration);

        var payload = new Dictionary<string, object?>
        {
            {
                "value", new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        { "clientState", "invalid-secret" }
                    }
                }
            }
        };

        // Act
        var result = await controller.OutlookWebhook(payload);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task OutlookWebhook_ValidClientState_ShouldProcess()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmailIntegration:WebhookSecret", "expected-secret" }
            })
            .Build();

        var controller = CreateController(configuration);

        var account = new Certio.Domain.Services.EmailAccount
        {
            Id = 10,
            EmailAddress = "outlook@example.com",
            Provider = "Outlook",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.EmailAccounts.Add(account);
        await _context.SaveChangesAsync();

        var payload = new Dictionary<string, object?>
        {
            {
                "value", new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        { "clientState", "expected-secret" }
                    }
                }
            }
        };

        // Act
        var result = await controller.OutlookWebhook(payload);

        // Assert
        Assert.IsType<OkResult>(result);
        _emailServiceMock.Verify(s => s.SyncEmailsAsync(account.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private EmailWebhookController CreateController(IConfiguration configuration, Action<HttpContext>? httpContextSetup = null)
    {
        var cacheServiceMock = new Mock<Certio.Web.Services.ICacheService>();
        var environmentMock = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        environmentMock.Setup(e => e.EnvironmentName).Returns("Development");
        
        var controller = new EmailWebhookController(
            _context,
            _emailServiceMock.Object,
            _emailToDmServiceMock.Object,
            cacheServiceMock.Object,
            configuration,
            _loggerMock.Object,
            environmentMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContextSetup?.Invoke(httpContext);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

