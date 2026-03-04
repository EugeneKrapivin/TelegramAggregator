namespace TelegramAggregator.Services;

public interface IDeduplicationService
{
    Task<bool> IsPostDuplicateAsync(string fingerprint, long channelId, CancellationToken cancellationToken = default);
    string ComputeFingerprint(string normalizedTextHash, List<string> imageChecksums);
}
