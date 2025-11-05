using System;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Certio.Web.Services;

public sealed class BackgroundEmbeddingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundEmbeddingWorker> _logger;

    public BackgroundEmbeddingWorker(IServiceProvider serviceProvider, ILogger<BackgroundEmbeddingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IEmbeddingJobQueue>();
        var indexer = scope.ServiceProvider.GetRequiredService<IDocumentIndexerService>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = await queue.DequeueAsync(stoppingToken);
                await indexer.ProcessIndexRequestAsync(request, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process embedding job");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}

