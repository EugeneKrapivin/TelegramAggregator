namespace TelegramAggregator.Api.Config;

public class WorkerOptions
{
    /// <summary>
    /// Interval for generating and posting summaries. Default is 10 minutes.
    /// </summary>
    public TimeSpan SummaryInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Interval for running the image cleanup job. Default is 1 hour.
    /// </summary>
    public TimeSpan ImageCleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Channel ID where summaries will be posted. Required.
    /// </summary>
    public long SummaryChannelId { get; set; } = 3814174631;

    /// <summary>
    /// Duration for which images are retained before being cleaned up. Default is 7 days.
    /// </summary>
    public TimeSpan ImageRetentionHours { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Hamming distance threshold for perceptual hash-based image deduplication. Lower values = stricter matching. Default is 8.
    /// </summary>
    public int PHashHammingThreshold { get; set; } = 8;
}
