namespace TelegramAggregator.Api.Services;

public interface INormalizerService
{
    Task<NormalizedText> NormalizeTextAsync(string text, CancellationToken cancellationToken = default);
}

public class NormalizedText
{
    public string OriginalText { get; set; } = string.Empty;
    public string Normalized { get; set; } = string.Empty;
    public string TextHash { get; set; } = string.Empty;
}
