using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TL;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Common.Data.Entities;
using TelegramAggregator.Api.Config;

namespace TelegramAggregator.Api.Services;

public class WTelegramClientAdapter
{
    private readonly ILogger<WTelegramClientAdapter> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IImageService _imageService;
    private readonly INormalizerService _normalizerService;
    private readonly IDeduplicationService _deduplicationService;
    private readonly TelegramOptions _options;
    private WTelegram.Client? _client;
    private bool _loginAttempted;

    // Public accessor for TelegramAuthService to use during login
    public WTelegram.Client Client
    {
        get
        {
            if (_client == null)
            {
                _client = new WTelegram.Client(Config);
                _client.OnUpdates += HandleUpdateAsync;
            }
            return _client;
        }
    }

    public WTelegramClientAdapter(
        ILogger<WTelegramClientAdapter> logger,
        IServiceScopeFactory scopeFactory,
        IImageService imageService,
        INormalizerService normalizerService,
        IDeduplicationService deduplicationService,
        IOptions<TelegramOptions>? telegramOptions = null)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _imageService = imageService;
        _normalizerService = normalizerService;
        _deduplicationService = deduplicationService;
        _options = telegramOptions?.Value ?? new TelegramOptions();
    }

    private async Task TryAutoLoginAsync()
    {
        try
        {
            // Check if session file exists before attempting login
            var sessionPath = Path.Combine("data", "wtelegram.session");
            if (!File.Exists(sessionPath))
            {
                _logger.LogInformation("No session file found - manual login required via /settings/telegram-login");
                return;
            }

            // If session file exists, try to auto-login
            var user = await Client.LoginUserIfNeeded();
            _logger.LogInformation("Auto-login successful: @{Username} (id={UserId})", user.username, user.id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-login failed - manual login required via /settings/telegram-login");
        }
    }

    private string? Config(string what)
    {
        return what switch
        {
            "api_id" => _options.ApiId,
            "api_hash" => _options.ApiHash,
            "phone_number" => _options.UserPhoneNumber,
            "session_pathname" => Path.Combine("data", "wtelegram.session"),
            "verification_code" => null,  // Will be provided via UI
            "password" => null,           // Will be provided via UI
            _ => null
        };
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Telegram client connection");

        // Initialize client but don't trigger auto-login yet
        _ = Client;

        // Only attempt auto-login if session file exists
        var sessionPath = Path.Combine("data", "wtelegram.session");
        if (!File.Exists(sessionPath))
        {
            _logger.LogInformation("No Telegram session found. Please login via /settings/telegram-login");
            return;
        }

        if (!_loginAttempted)
        {
            _loginAttempted = true;
            await TryAutoLoginAsync();
        }

        if (Client.User != null)
        {
            _logger.LogInformation("Connected as @{Username} (id={UserId})", Client.User.username, Client.User.id);
        }
        else
        {
            _logger.LogWarning("Telegram client initialized but not logged in. Use /settings/telegram-login to authenticate.");
        }
    }

    private async Task HandleUpdateAsync(IObject update)
    {
        var messages = update switch
        {
            Updates u => u.updates.OfType<UpdateNewChannelMessage>().Select(x => x.message),
            _ => []
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

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var channel = await dbContext.Channels
            .FirstOrDefaultAsync(c => 
                c.TelegramChannelId == peerChannel.channel_id && c.IsActive, 
                cancellationToken);

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
            catch (Exception ex) 
            { 
                _logger.LogWarning(ex, "Failed to process photo in message {MessageId}", msg.id); 
            }
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

        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var imageId in imageIds)
        {
            dbContext.PostImages.Add(new PostImage { PostId = post.Id, ImageId = imageId });
        }

        if (imageIds.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Ingested post {PostId} from channel {ChannelId}", post.Id, channel.Id);
    }
}
