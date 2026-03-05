namespace TelegramAggregator.Api.AI;

public interface ISemanticSummarizer
{
    Task<(string headline, string digest)> SummarizeAsync(
        List<PostSummary> posts,
        int maxTokens = 500,
        CancellationToken cancellationToken = default);
}

public class PostSummary
{
    public string ChannelName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
}
