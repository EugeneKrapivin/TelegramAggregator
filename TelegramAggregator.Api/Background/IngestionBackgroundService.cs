using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TelegramAggregator.Api.Services;

namespace TelegramAggregator.Api.Background;

public class IngestionBackgroundService : BackgroundService
{
    private readonly ILogger<IngestionBackgroundService> _logger;
    private readonly WTelegramClientAdapter _adapter;

    public IngestionBackgroundService(ILogger<IngestionBackgroundService> logger, WTelegramClientAdapter adapter)
    {
        _logger = logger;
        _adapter = adapter;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IngestionBackgroundService starting");
        try
        {
            await _adapter.ConnectAsync(stoppingToken);
            // TODO: should I replace Task.Delay with a TaskCompletionSource?
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) 
        { 
            _logger.LogInformation("IngestionBackgroundService stopping"); 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IngestionBackgroundService fatal error");
            throw;
        }
    }
}
