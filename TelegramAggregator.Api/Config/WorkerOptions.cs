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
    /// MUST be negative number in format -100XXXXXXXXXX for channels/supergroups.
    /// Example: -1001234567890
    /// To find your channel ID: Add @raw_data_bot to channel, or use format -100 + channel_id from URL.
    /// </summary>
    public long SummaryChannelId { get; set; } = 0; // Must be configured - see docs/telegram-bot-configuration.md

    /// <summary>
    /// Duration for which images are retained before being cleaned up. Default is 7 days.
    /// </summary>
    public TimeSpan ImageRetentionHours { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Hamming distance threshold for perceptual hash-based image deduplication. Lower values = stricter matching. Default is 8.
    /// </summary>
    public int PHashHammingThreshold { get; set; } = 8;
}
