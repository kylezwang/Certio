using System.Security.Cryptography;
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
        var webhookSecret = "test-webhook-secret";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmailIntegration:GmailVerificationToken", "secret-token" },
                { "EmailIntegration:WebhookSecret", webhookSecret }
            })
            .Build();

        var payload = new { };
        var controller = CreateController(configuration);
        SetupWebhookHeaders(controller.HttpContext, webhookSecret, payload);

        // Act
        var result = await controller.GmailWebhook(payload, CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GmailWebhook_WithValidToken_ShouldProcess()
    {
        // Arrange
        var webhookSecret = "test-webhook-secret";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmailIntegration:GmailVerificationToken", "secret-token" },
                { "EmailIntegration:WebhookSecret", webhookSecret }
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

        SetupWebhookHeaders(controller.HttpContext, webhookSecret, payload);

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
        var webhookSecret = "expected-secret";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmailIntegration:WebhookSecret", webhookSecret }
            })
            .Build();

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

        var controller = CreateController(configuration);
        SetupWebhookHeaders(controller.HttpContext, webhookSecret, payload);

        // Act
        var result = await controller.OutlookWebhook(payload);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task OutlookWebhook_ValidClientState_ShouldProcess()
    {
        // Arrange
        var webhookSecret = "expected-secret";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "EmailIntegration:WebhookSecret", webhookSecret }
            })
            .Build();

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
                        { "clientState", webhookSecret }
                    }
                }
            }
        };

        var controller = CreateController(configuration);
        SetupWebhookHeaders(controller.HttpContext, webhookSecret, payload);

        // Act
        var result = await controller.OutlookWebhook(payload);

        // Assert
        Assert.IsType<OkResult>(result);
        _emailServiceMock.Verify(s => s.SyncEmailsAsync(account.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private EmailWebhookController CreateController(IConfiguration configuration, Action<HttpContext>? httpContextSetup = null)
    {
        var cacheServiceMock = new Mock<Certio.Web.Services.ICacheService>();
        cacheServiceMock.Setup(c => c.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
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

    private static readonly JsonSerializerOptions WebhookJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static void SetupWebhookHeaders(HttpContext ctx, string webhookSecret, object payload)
    {
        var nonce = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var canonicalPayload = JsonSerializer.Serialize(payload, WebhookJsonOptions);

        var message = $"{nonce}.{timestamp}.{canonicalPayload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();

        ctx.Request.Headers["X-Webhook-Secret"] = webhookSecret;
        ctx.Request.Headers["X-Webhook-Nonce"] = nonce;
        ctx.Request.Headers["X-Webhook-Timestamp"] = timestamp;
        ctx.Request.Headers["X-Webhook-Signature"] = $"sha256={signature}";
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

