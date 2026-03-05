using Microsoft.EntityFrameworkCore;
using TelegramAggregator.Common.Data;

namespace TelegramAggregator.MigrationService;

public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        logger.LogInformation("Applying pending EF Core migrations...");
        await dbContext.Database.MigrateAsync(stoppingToken);
        logger.LogInformation("Migrations applied successfully.");

        hostApplicationLifetime.StopApplication();
    }
}
