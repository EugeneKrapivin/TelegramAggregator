namespace TelegramAggregator.Common.Data.Entities;

public class Channel
{
    public long Id { get; set; }
    public long TelegramChannelId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}
