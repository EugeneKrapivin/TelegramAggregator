namespace TelegramAggregator.Common.Data.Entities;

public class Summary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public string IncludedPostIds { get; set; } = "[]"; // JSON array
}
