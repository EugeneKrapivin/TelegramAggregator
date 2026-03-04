namespace TelegramAggregator.Common.Data.Entities;

public class Image
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string? PerceptualHash { get; set; }
    public string MimeType { get; set; } = "image/jpeg";
    public int Width { get; set; }
    public int Height { get; set; }
    public long SizeBytes { get; set; }
    public string? ContentBase64 { get; set; }
    public string? TelegramFileId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }

    // Navigation
    public ICollection<PostImage> PostImages { get; set; } = new List<PostImage>();
}
