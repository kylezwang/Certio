using Certio.Application.Interfaces;

namespace Certio.Web.Services;

/// <summary>
/// Polls for Change Notices that have Auto Reminders enabled and triggers a "Nudge" email
/// using the existing ChangeNoticeService send/nudge logic.
/// </summary>
public sealed class ChangeNoticeAutoReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChangeNoticeAutoReminderService> _logger;

    public ChangeNoticeAutoReminderService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ChangeNoticeAutoReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small startup delay to allow the app to fully boot.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var baseUrl = (_configuration["App:PublicBaseUrl"] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    // If not configured, don't spam logs every loop.
                    _logger.LogDebug("App:PublicBaseUrl not configured; skipping Change Notice auto reminders.");
                }
                else
                {
                    using var scope = _scopeFactory.CreateScope();
                    var changeNoticeService = scope.ServiceProvider.GetRequiredService<IChangeNoticeService>();

                    var nudged = await changeNoticeService.ProcessAutoRemindersAsync(baseUrl, stoppingToken);
                    if (nudged > 0)
                    {
                        _logger.LogInformation("Processed Change Notice auto reminders: nudged {Count} notice(s).", nudged);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Change Notice auto reminders.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}

