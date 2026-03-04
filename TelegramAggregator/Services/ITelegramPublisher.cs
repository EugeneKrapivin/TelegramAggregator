namespace TelegramAggregator.Services;

public interface ITelegramPublisher
{
    Task<long> PublishSummaryAsync(
        string headline,
        string digest,
        List<Guid> imageIds,
        List<string> sourceChannels,
        CancellationToken cancellationToken = default);
}
