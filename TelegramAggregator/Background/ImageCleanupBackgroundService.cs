using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Config;

namespace TelegramAggregator.Background;

public class ImageCleanupBackgroundService : BackgroundService
{
    private readonly ILogger<ImageCleanupBackgroundService> _logger;
    private readonly WorkerOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    public ImageCleanupBackgroundService(
        ILogger<ImageCleanupBackgroundService> logger,
        IOptions<WorkerOptions> options,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImageCleanupBackgroundService starting with interval: {Interval}", _options.ImageCleanupInterval);
        using var timer = new PeriodicTimer(_options.ImageCleanupInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await RunCleanupCycleAsync(dbContext, stoppingToken);
                }
                catch (Exception ex) { _logger.LogError(ex, "Error during image cleanup cycle"); }
            }
        }
        catch (OperationCanceledException) { _logger.LogInformation("ImageCleanupBackgroundService stopping"); }
    }

    internal async Task RunCleanupCycleAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - _options.ImageRetentionHours;

        var images = await dbContext.Images
            .Where(i => i.Content != null && i.UsedAt != null && i.UsedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (images.Count == 0)
        {
            _logger.LogDebug("No stale images to clean up");
            return;
        }

        foreach (var image in images)
            image.Content = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cleaned up content for {Count} stale images", images.Count);
    }
}
