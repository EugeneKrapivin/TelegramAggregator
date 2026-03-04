using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TelegramAggregator.Services;

public class DeduplicationService : IDeduplicationService
{
    private readonly ILogger<DeduplicationService> _logger;

    public DeduplicationService(ILogger<DeduplicationService> logger)
    {
        _logger = logger;
    }

    public string ComputeFingerprint(string normalizedTextHash, List<string> imageChecksums)
    {
        var sortedChecksums = imageChecksums.OrderBy(c => c).ToList();
        var combined = normalizedTextHash + string.Join("", sortedChecksums);
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hashedBytes);
    }

    public async Task<bool> IsPostDuplicateAsync(string fingerprint, long channelId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual dedup check against DB
        // This is a stub for now
        _logger.LogDebug("Checking for duplicate post with fingerprint {Fingerprint} in channel {ChannelId}", fingerprint, channelId);
        return false;
    }
}
