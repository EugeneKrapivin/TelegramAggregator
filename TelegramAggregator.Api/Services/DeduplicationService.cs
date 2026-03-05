using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramAggregator.Common.Data;

namespace TelegramAggregator.Api.Services;

public class DeduplicationService : IDeduplicationService
{
    private readonly ILogger<DeduplicationService> _logger;
    private readonly AppDbContext _dbContext;

    public DeduplicationService(ILogger<DeduplicationService> logger, AppDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
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
        _logger.LogDebug("Checking for duplicate post with fingerprint {Fingerprint} in channel {ChannelId}", fingerprint, channelId);
        return await _dbContext.Posts.AnyAsync(
            p => p.Fingerprint == fingerprint && p.ChannelId == channelId,
            cancellationToken);
    }
}
