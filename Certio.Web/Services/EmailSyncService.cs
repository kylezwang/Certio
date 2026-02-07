using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Services;

/// <summary>
/// Background service that refreshes email OAuth tokens.
/// Email sync is done on-demand when users visit their inbox (see EmailOAuthController.GetInbox).
/// </summary>
public class EmailSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailSyncService> _logger;
    private readonly TimeSpan _tokenRefreshInterval = TimeSpan.FromMinutes(5);

    public EmailSyncService(
        IServiceProvider serviceProvider,
        ILogger<EmailSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📧 Email Token Refresh Service started. Interval: {Interval} minutes", _tokenRefreshInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_tokenRefreshInterval, stoppingToken);
                await RefreshExpiringTokensAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in token refresh task");
            }
        }

        _logger.LogInformation("📧 Email Token Refresh Service stopped.");
    }

    private async Task RefreshExpiringTokensAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        try
        {
            var expiringAccounts = await context.EmailAccounts
                .Where(ea => ea.IsActive && 
                            ea.TokenExpiresAt.HasValue && 
                            ea.TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(30))
                .ToListAsync(ct);

            foreach (var account in expiringAccounts)
            {
                try
                {
                    await emailService.RefreshAccessTokenAsync(account.Id, ct);
                    _logger.LogDebug("Refreshed token for email account {AccountId}", account.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to refresh token for email account {AccountId}", account.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing expiring tokens");
        }
    }
}

