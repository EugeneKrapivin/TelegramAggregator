using Microsoft.Extensions.Logging;
using Moq;
using TelegramAggregator.Services;
using Xunit;

namespace TelegramAggregator.Tests.Services;

/// <summary>
/// Unit tests for NormalizerService.
/// Tests text normalization, markup removal, URL normalization, and hash computation.
/// </summary>
public class NormalizerServiceTests
{
    private readonly NormalizerService _service;
    private readonly Mock<ILogger<NormalizerService>> _mockLogger;

    public NormalizerServiceTests()
    {
        _mockLogger = new Mock<ILogger<NormalizerService>>();
        _service = new NormalizerService(_mockLogger.Object);
    }

    [Fact]
    public async Task NormalizeTextAsync_WithPlainText_ReturnsNormalizedText()
    {
        // Arrange
        var input = "This is plain text without any markup";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(input, result.Normalized);
        Assert.Equal(input, result.OriginalText);
        Assert.NotEmpty(result.TextHash);
    }

    [Fact]
    public async Task NormalizeTextAsync_WithTelegramMarkup_RemovesMarkup()
    {
        // Arrange
        var input = "This **is** *bold* and __underline__ text";
        var expected = "This is bold and underline text";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.Equal(expected, result.Normalized);
        Assert.Equal(input, result.OriginalText);
    }

    [Fact]
    public async Task NormalizeTextAsync_WithStrikethrough_RemovesStrikethrough()
    {
        // Arrange
        var input = "This is ~~deleted~~ text";
        var expected = "This is deleted text";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.Equal(expected, result.Normalized);
    }

    [Fact]
    public async Task NormalizeTextAsync_WithUrls_NormalizesToDomain()
    {
        // Arrange
        var input = "Check this link https://github.com/user/repo?page=1&sort=name and this one http://example.com/path/to/page";
        var expected = "Check this link github.com and this one example.com";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.Equal(expected, result.Normalized);
    }

    [Fact]
    public async Task NormalizeTextAsync_WithExcessWhitespace_RemovesExtraSpaces()
    {
        // Arrange
        var input = "This   has    too    many     spaces";
        var expected = "This has too many spaces";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.Equal(expected, result.Normalized);
    }

    [Fact]
    public async Task NormalizeTextAsync_WithLeadingTrailingWhitespace_Trims()
    {
        // Arrange
        var input = "   text with leading and trailing spaces   ";
        var expected = "text with leading and trailing spaces";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.Equal(expected, result.Normalized);
    }

    [Fact]
    public async Task NormalizeTextAsync_WithCombinedMarkup_HandlesAll()
    {
        // Arrange
        var input = "Check **this link** https://github.com/repo and ~~old~~ *content*";
        // Expected: removes ** and *, ~~, normalizes URL
        var expected = "Check this link github.com and old content";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.Equal(expected, result.Normalized);
    }

    [Fact]
    public async Task NormalizeTextAsync_WithEmptyString_ReturnsEmpty()
    {
        // Arrange
        var input = "";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Normalized);
        Assert.Empty(result.OriginalText);
        Assert.NotEmpty(result.TextHash);
    }

    [Fact]
    public async Task NormalizeTextAsync_WithWhitespaceOnly_ReturnsEmpty()
    {
        // Arrange
        var input = "   \t  \n  ";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Normalized);
    }

    [Fact]
    public async Task NormalizeTextAsync_SameInputProducesSameHash()
    {
        // Arrange
        var input = "The quick brown fox jumps over the lazy dog";

        // Act
        var result1 = await _service.NormalizeTextAsync(input);
        var result2 = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.Equal(result1.TextHash, result2.TextHash);
    }

    [Fact]
    public async Task NormalizeTextAsync_DifferentInputProducesDifferentHash()
    {
        // Arrange
        var input1 = "The quick brown fox";
        var input2 = "The slow brown fox";

        // Act
        var result1 = await _service.NormalizeTextAsync(input1);
        var result2 = await _service.NormalizeTextAsync(input2);

        // Assert
        Assert.NotEqual(result1.TextHash, result2.TextHash);
    }

    [Fact]
    public async Task NormalizeTextAsync_HashIsValidSHA256()
    {
        // Arrange
        var input = "Test input";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        // SHA256 produces a 64-character hex string (256 bits / 4 bits per hex char = 64 chars)
        Assert.Equal(64, result.TextHash.Length);
        Assert.True(result.TextHash.All(c => "0123456789ABCDEF".Contains(c, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task NormalizeTextAsync_MarkupAndUrlsCombined_HandlesBoth()
    {
        // Arrange
        var input = "Read **my blog** at https://myblog.com/post?id=123&lang=en for more **details**";
        var expected = "Read my blog at myblog.com for more details";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.Equal(expected, result.Normalized);
    }

    [Fact]
    public async Task NormalizeTextAsync_MultilineText_HandlesNewlines()
    {
        // Arrange
        var input = "Line 1\nLine 2\r\nLine 3";
        // Should collapse newlines to single spaces and then normalize
        var expected = "Line 1 Line 2 Line 3";

        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.Equal(expected, result.Normalized);
    }

    [Theory]
    [InlineData("Hello World", "hello world")]
    [InlineData("UPPERCASE TEXT", "uppercase text")]
    [InlineData("MiXeD CaSe", "mixed case")]
    public async Task NormalizeTextAsync_DoesNotChangeCase(string input, string _)
    {
        // Note: Current implementation doesn't lowercase text
        // This test documents the current behavior
        // Act
        var result = await _service.NormalizeTextAsync(input);

        // Assert
        Assert.Equal(input, result.Normalized);
    }
}
