using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Api.Config;

namespace TelegramAggregator.Api.Services;

public class TelegramPublisher : ITelegramPublisher
{
    private readonly ILogger<TelegramPublisher> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly long _summaryChannelId;
    private readonly IServiceScopeFactory _scopeFactory;

    public TelegramPublisher(
        ILogger<TelegramPublisher> logger,
        ITelegramBotClient botClient,
        IOptions<WorkerOptions> workerOptions,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _botClient = botClient;
        _summaryChannelId = workerOptions.Value.SummaryChannelId;
        _scopeFactory = scopeFactory;
    }

    public async Task<long> PublishSummaryAsync(
        string headline,
        string digest,
        List<Guid> imageIds,
        List<string> sourceChannels,
        CancellationToken cancellationToken = default)
    {
        var text = $"*{headline}*\n\n{digest}\n\n_{string.Join(", ", sourceChannels)}_";
        var chatId = new ChatId(_summaryChannelId);

        var media = await BuildMediaListAsync(imageIds, text, cancellationToken);

        if (media.Count > 0)
        {
            _logger.LogInformation("Publishing summary with {Count} images to channel {ChannelId}", media.Count, _summaryChannelId);
            var messages = await _botClient.SendMediaGroup(chatId, media, cancellationToken: cancellationToken);
            
            return messages[0].MessageId;
        }

        _logger.LogInformation("Publishing text-only summary to channel {ChannelId}", _summaryChannelId);
        var message = await _botClient.SendMessage(chatId, text, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
        
        return message.MessageId;
    }

    private async Task<List<IAlbumInputMedia>> BuildMediaListAsync(
        List<Guid> imageIds,
        string caption,
        CancellationToken cancellationToken)
    {
        if (imageIds.Count == 0)
        {
            return [];
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var images = await dbContext.Images
            .Where(i => imageIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        var media = new List<IAlbumInputMedia>();
        var isFirst = true;

        foreach (var image in images)
        {
            InputFile? inputFile;

            if (image.Content is not null)
            {
                inputFile = InputFile.FromStream(new MemoryStream(image.Content), "image.jpg");
            }
            else if (image.TelegramFileId is not null)
            {
                inputFile = InputFile.FromFileId(image.TelegramFileId);
            }
            else
            {
                _logger.LogWarning("Image {ImageId} has no content or file ID — skipping", image.Id);
                continue;
            }

            var photo = new InputMediaPhoto(inputFile);
            if (isFirst)
            {
                photo.Caption = caption;
                photo.ParseMode = ParseMode.Markdown;
            }
            media.Add(photo);
            isFirst = false;
        }

        return media;
    }
}
