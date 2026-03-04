using Microsoft.Extensions.Logging;

using TelegramAggregator.Common.Data;

namespace TelegramAggregator.Services;

public class WTelegramClientAdapter
{
    private readonly ILogger<WTelegramClientAdapter> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IImageService _imageService;
    private readonly INormalizerService _normalizerService;
    private readonly IDeduplicationService _deduplicationService;

    public WTelegramClientAdapter(
        ILogger<WTelegramClientAdapter> logger,
        AppDbContext dbContext,
        IImageService imageService,
        INormalizerService normalizerService,
        IDeduplicationService deduplicationService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _imageService = imageService;
        _normalizerService = normalizerService;
        _deduplicationService = deduplicationService;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement WTelegramClient connection logic
        _logger.LogInformation("Connecting to Telegram user account");
        await Task.CompletedTask;
    }

    public async Task ReceiveAndProcessPostAsync(object telegramUpdate, CancellationToken cancellationToken = default)
    {
        // TODO: Implement post reception and processing logic
        // 1. Extract text and media from update
        // 2. Call normalizer
        // 3. Process images via ImageService
        // 4. Create Post entity
        // 5. Save to DB
        _logger.LogInformation("Processing Telegram post");
        await Task.CompletedTask;
    }
}
