using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramAggregator.Config;
using TelegramAggregator.Services;
using TelegramAggregator.AI;

namespace TelegramAggregator.Background;

public class SummaryBackgroundService : BackgroundService
{
    private readonly ILogger<SummaryBackgroundService> _logger;
    private readonly WorkerOptions _options;
    private readonly ISemanticSummarizer _summarizer;
    private readonly ITelegramPublisher _publisher;

    public SummaryBackgroundService(
        ILogger<SummaryBackgroundService> logger,
        IOptions<WorkerOptions> options,
        ISemanticSummarizer summarizer,
        ITelegramPublisher publisher)
    {
        _logger = logger;
        _options = options.Value;
        _summarizer = summarizer;
        _publisher = publisher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SummaryBackgroundService starting with interval: {Interval}", _options.SummaryInterval);

        using var timer = new PeriodicTimer(_options.SummaryInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ExecuteSummaryAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing summary cycle");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SummaryBackgroundService stopping");
        }
    }

    private async Task ExecuteSummaryAsync(CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting summary cycle at {StartTime}", startTime);

        // TODO: Implement full summary logic:
        // 1. Query unsummarized posts from last window
        // 2. Call summarizer
        // 3. Call publisher
        // 4. Update DB state
        
        _logger.LogInformation("Summary cycle completed");
    }
}
