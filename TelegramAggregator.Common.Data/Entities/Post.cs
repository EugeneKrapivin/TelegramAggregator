namespace TelegramAggregator.Common.Data.Entities;

public class Post
{
    public long Id { get; set; }
    public long TelegramMessageId { get; set; }
    public long ChannelId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string NormalizedTextHash { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;
    public bool IsSummarized { get; set; }
    public string RawJson { get; set; } = string.Empty;

    // Navigation
    public Channel? Channel { get; set; }
    public ICollection<PostImage> PostImages { get; set; } = new List<PostImage>();
}

public class PostImage
{
    public long PostId { get; set; }
    public Guid ImageId { get; set; }

    // Navigation
    public Post? Post { get; set; }
    public Image? Image { get; set; }
}

