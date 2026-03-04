using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TL;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Common.Data.Entities;
using TelegramAggregator.Config;

namespace TelegramAggregator.Services;

public class WTelegramClientAdapter
{
    private readonly ILogger<WTelegramClientAdapter> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IImageService _imageService;
    private readonly INormalizerService _normalizerService;
    private readonly IDeduplicationService _deduplicationService;
    private readonly TelegramOptions _options;

    private WTelegram.Client? _client;

    public WTelegramClientAdapter(
        ILogger<WTelegramClientAdapter> logger,
        AppDbContext dbContext,
        IImageService imageService,
        INormalizerService normalizerService,
        IDeduplicationService deduplicationService,
        IOptions<TelegramOptions>? telegramOptions = null)
    {
        _logger = logger;
        _dbContext = dbContext;
        _imageService = imageService;
        _normalizerService = normalizerService;
        _deduplicationService = deduplicationService;
        _options = telegramOptions?.Value ?? new TelegramOptions();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to Telegram user account: {Phone}", _options.UserPhoneNumber);

        _client = new WTelegram.Client(what => what switch
        {
            "api_id" => _options.ApiId,
            "api_hash" => _options.ApiHash,
            "phone_number" => _options.UserPhoneNumber,
            "session_pathname" => _options.SessionPath,
            _ => null
        });

        _client.OnUpdates += HandleUpdateAsync;

        var user = await _client.LoginUserIfNeeded();
        _logger.LogInformation("Connected as @{Username} (id={UserId})", user.username, user.id);
    }

    private async Task HandleUpdateAsync(IObject update)
    {
        var messages = update switch
        {
            Updates u => u.updates.OfType<UpdateNewChannelMessage>().Select(x => x.message),
            _ => Enumerable.Empty<MessageBase>()
        };

        foreach (var msg in messages.OfType<Message>())
        {
            try { await ReceiveAndProcessPostAsync(msg); }
            catch (Exception ex) { _logger.LogError(ex, "Error processing message {MessageId}", msg.id); }
        }
    }

    internal async Task ReceiveAndProcessPostAsync(Message msg, CancellationToken cancellationToken = default)
    {
        if (msg.peer_id is not PeerChannel peerChannel)
        {
            _logger.LogDebug("Ignoring non-channel message {MessageId}", msg.id);
            return;
        }

        var channel = await _dbContext.Channels
            .FirstOrDefaultAsync(c => c.TelegramChannelId == peerChannel.channel_id && c.IsActive, cancellationToken);

        if (channel is null)
        {
            _logger.LogDebug("Ignoring message from unmonitored channel {ChannelId}", peerChannel.channel_id);
            return;
        }

        var normalized = await _normalizerService.NormalizeTextAsync(msg.message ?? string.Empty, cancellationToken);

        var imageIds = new List<Guid>();
        var imageChecksums = new List<string>();

        if (msg.media is MessageMediaPhoto { photo: Photo photo } && _client is not null)
        {
            try
            {
                using var ms = new MemoryStream();
                await _client.DownloadFileAsync(photo, ms);
                var bytes = ms.ToArray();
                var size = photo.sizes.OfType<PhotoSize>().MaxBy(s => s.size);
                var imageId = await _imageService.FindOrCreateImageAsync(
                    bytes, "image/jpeg", size?.w ?? 0, size?.h ?? 0, cancellationToken);
                imageIds.Add(imageId);
                imageChecksums.Add(_imageService.ComputeSha256Hash(bytes));
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to process photo in message {MessageId}", msg.id); }
        }

        var fingerprint = _deduplicationService.ComputeFingerprint(normalized.TextHash, imageChecksums);

        if (await _deduplicationService.IsPostDuplicateAsync(fingerprint, channel.Id, cancellationToken))
        {
            _logger.LogInformation("Duplicate post skipped: message {MessageId}", msg.id);
            return;
        }

        var post = new Post
        {
            TelegramMessageId = msg.id,
            ChannelId = channel.Id,
            Text = normalized.Normalized,
            NormalizedTextHash = normalized.TextHash,
            Fingerprint = fingerprint,
            PublishedAt = msg.date,
            IngestedAt = DateTime.UtcNow,
            IsSummarized = false,
            RawJson = System.Text.Json.JsonSerializer.Serialize(new { msg.id, msg.message })
        };

        _dbContext.Posts.Add(post);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var imageId in imageIds)
            _dbContext.PostImages.Add(new PostImage { PostId = post.Id, ImageId = imageId });

        if (imageIds.Count > 0)
            await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Ingested post {PostId} from channel {ChannelId}", post.Id, channel.Id);
    }
}
