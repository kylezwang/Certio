using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Services;

/// <summary>
/// Background service that periodically syncs emails and refreshes tokens
/// </summary>
public class EmailSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailSyncService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(15); // Sync every 15 minutes
    private readonly TimeSpan _tokenRefreshInterval = TimeSpan.FromMinutes(5); // Check tokens every 5 minutes

    public EmailSyncService(
        IServiceProvider serviceProvider,
        ILogger<EmailSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📧 Email Sync Service started. Sync interval: {SyncInterval} minutes", _syncInterval.TotalMinutes);

        // Start token refresh task
        var tokenRefreshTask = Task.Run(async () =>
        {
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
        }, stoppingToken);

        // Start email sync task
        var emailSyncTask = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_syncInterval, stoppingToken);
                    await SyncAllActiveEmailAccountsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in email sync task");
                }
            }
        }, stoppingToken);

        // Wait for both tasks
        await Task.WhenAll(tokenRefreshTask, emailSyncTask);

        _logger.LogInformation("📧 Email Sync Service stopped.");
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
                            ea.TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(30)) // Refresh if expires in 30 min
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

    private async Task SyncAllActiveEmailAccountsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        try
        {
            var activeAccounts = await context.EmailAccounts
                .Where(ea => ea.IsActive)
                .ToListAsync(ct);

            _logger.LogInformation("Syncing {Count} active email accounts", activeAccounts.Count);

            foreach (var account in activeAccounts)
            {
                try
                {
                    await emailService.SyncEmailsAsync(account.Id, ct);
                    _logger.LogDebug("Synced emails for account {AccountId} ({Provider})", account.Id, account.Provider);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync emails for account {AccountId}", account.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing email accounts");
        }
    }
}

