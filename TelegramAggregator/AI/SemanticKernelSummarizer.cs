using Microsoft.Extensions.Logging;

namespace TelegramAggregator.AI;

public class SemanticKernelSummarizer : ISemanticSummarizer
{
    private readonly ILogger<SemanticKernelSummarizer> _logger;

    public SemanticKernelSummarizer(ILogger<SemanticKernelSummarizer> logger)
    {
        _logger = logger;
    }

    public async Task<(string headline, string digest)> SummarizeAsync(
        List<PostSummary> posts,
        int maxTokens = 500,
        CancellationToken cancellationToken = default)
    {
        if (posts.Count == 0)
        {
            return ("No posts to summarize", string.Empty);
        }

        // TODO: Implement Semantic Kernel integration
        _logger.LogInformation("Summarizing {PostCount} posts", posts.Count);

        var headline = $"Summary of {posts.Count} posts";
        var digest = string.Join("; ", posts.Select(p => $"{p.ChannelName}: {p.Text.Substring(0, Math.Min(50, p.Text.Length))}..."));

        return await Task.FromResult((headline, digest));
    }
}
