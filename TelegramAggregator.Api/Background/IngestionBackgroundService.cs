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
            // Connect and authenticate
            await _adapter.ConnectAsync(stoppingToken);
            
            // Start polling loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _adapter.PollChannelsAsync(stoppingToken);
                    
                    // Wait 5 minutes before next poll
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // Normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during channel polling, will retry in 30 seconds");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
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
