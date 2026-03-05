using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Text.Json;

using TelegramAggregator.Api.AI;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Common.Data.Entities;
using TelegramAggregator.Api.Config;
using TelegramAggregator.Api.Services;

namespace TelegramAggregator.Api.Background;

public class SummaryBackgroundService : BackgroundService
{
    private readonly ILogger<SummaryBackgroundService> _logger;
    private readonly WorkerOptions _options;
    private readonly ISemanticSummarizer _summarizer;
    private readonly ITelegramPublisher _publisher;
    private readonly IImageService _imageService;
    private readonly IServiceScopeFactory _scopeFactory;

    public SummaryBackgroundService(
        ILogger<SummaryBackgroundService> logger,
        IOptions<WorkerOptions> options,
        ISemanticSummarizer summarizer,
        ITelegramPublisher publisher,
        IImageService imageService,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _summarizer = summarizer;
        _publisher = publisher;
        _imageService = imageService;
        _scopeFactory = scopeFactory;
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

    internal async Task ExecuteSummaryAsync(CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting summary cycle at {StartTime}", startTime);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var posts = await dbContext.Posts
            .Where(p => !p.IsSummarized)
            .Include(p => p.Channel)
            .Include(p => p.PostImages)
            .ToListAsync(cancellationToken);

        if (posts.Count == 0)
        {
            _logger.LogInformation("No unsummarized posts — skipping cycle");
            return;
        }

        var postSummaries = posts.Select(p => new PostSummary
        {
            ChannelName = p.Channel?.Title ?? "Unknown",
            Text = p.Text,
            PublishedAt = p.PublishedAt
        }).ToList();

        var imageIds = posts
            .SelectMany(p => p.PostImages)
            .Select(pi => pi.ImageId)
            .Distinct()
            .ToList();

        var sourceChannels = posts
            .Select(p => p.Channel?.Username ?? "unknown")
            .Distinct()
            .ToList();

        var (headline, digest) = await _summarizer.SummarizeAsync(postSummaries, cancellationToken: cancellationToken);
        var telegramMessageId = await _publisher.PublishSummaryAsync(headline, digest, imageIds, sourceChannels, cancellationToken);

        dbContext.Summaries.Add(new Summary
        {
            Id = Guid.CreateVersion7(),
            WindowStart = posts.Min(p => p.PublishedAt),
            WindowEnd = startTime,
            Headline = headline,
            SummaryText = digest,
            PublishedAt = DateTime.UtcNow,
            IncludedPostIds = JsonSerializer.Serialize(posts.Select(p => p.Id).ToList())
        });

        foreach (var post in posts)
        {
            post.IsSummarized = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await _imageService.ClearContentBatchAsync(imageIds, cancellationToken);

        _logger.LogInformation("Summary cycle complete — headline: {Headline}, messageId: {MessageId}", headline, telegramMessageId);
    }
}
