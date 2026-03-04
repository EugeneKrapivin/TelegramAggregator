using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TelegramAggregator.Services;

public class NormalizerService : INormalizerService
{
    private readonly ILogger<NormalizerService> _logger;

    public NormalizerService(ILogger<NormalizerService> logger)
    {
        _logger = logger;
    }

    public Task<NormalizedText> NormalizeTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            var emptyResult = new NormalizedText
            {
                OriginalText = text,
                Normalized = string.Empty,
                TextHash = ComputeHash(string.Empty)
            };
            return Task.FromResult(emptyResult);
        }

        // Remove Telegram markup (simplified)
        var normalized = text
            .Replace("**", "") // bold
            .Replace("*", "")  // italic
            .Replace("__", "") // underline
            .Replace("~~", ""); // strikethrough

        // Normalize URLs (replace with domain only)
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"https?://[^\s]+",
            m => new Uri(m.Value).Host
        );

        // Remove excess whitespace
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();

        var result = new NormalizedText
        {
            OriginalText = text,
            Normalized = normalized,
            TextHash = ComputeHash(normalized)
        };

        _logger.LogDebug("Normalized text from {OriginalLength} to {NormalizedLength} chars", text.Length, normalized.Length);

        return Task.FromResult(result);
    }

    private static string ComputeHash(string text)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hashedBytes);
    }
}
