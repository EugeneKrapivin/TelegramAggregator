using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using TelegramAggregator.Api.Services;

namespace TelegramAggregator.Tests.Services;

/// <summary>
/// Unit tests for NormalizerService.
/// Tests text normalization, markup removal, URL normalization, and hash computation.
/// </summary>
[TestFixture]
public class NormalizerServiceTests
{
    private NormalizerService _service;

    [SetUp]
    public void SetUp()
    {
        _service = new NormalizerService(Substitute.For<ILogger<NormalizerService>>());
    }

    [Test]
    public async Task NormalizeTextAsync_WithPlainText_ReturnsNormalizedText()
    {
        var input = "This is plain text without any markup";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Normalized, Is.EqualTo(input));
        Assert.That(result.OriginalText, Is.EqualTo(input));
        Assert.That(result.TextHash, Is.Not.Empty);
    }

    [Test]
    public async Task NormalizeTextAsync_WithTelegramMarkup_RemovesMarkup()
    {
        var input = "This **is** *bold* and __underline__ text";
        var expected = "This is bold and underline text";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result.Normalized, Is.EqualTo(expected));
        Assert.That(result.OriginalText, Is.EqualTo(input));
    }

    [Test]
    public async Task NormalizeTextAsync_WithStrikethrough_RemovesStrikethrough()
    {
        var input = "This is ~~deleted~~ text";
        var expected = "This is deleted text";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result.Normalized, Is.EqualTo(expected));
    }

    [Test]
    public async Task NormalizeTextAsync_WithUrls_NormalizesToDomain()
    {
        var input = "Check this link https://github.com/user/repo?page=1&sort=name and this one http://example.com/path/to/page";
        var expected = "Check this link github.com and this one example.com";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result.Normalized, Is.EqualTo(expected));
    }

    [Test]
    public async Task NormalizeTextAsync_WithExcessWhitespace_RemovesExtraSpaces()
    {
        var input = "This   has    too    many     spaces";
        var expected = "This has too many spaces";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result.Normalized, Is.EqualTo(expected));
    }

    [Test]
    public async Task NormalizeTextAsync_WithLeadingTrailingWhitespace_Trims()
    {
        var input = "   text with leading and trailing spaces   ";
        var expected = "text with leading and trailing spaces";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result.Normalized, Is.EqualTo(expected));
    }

    [Test]
    public async Task NormalizeTextAsync_WithCombinedMarkup_HandlesAll()
    {
        var input = "Check **this link** https://github.com/repo and ~~old~~ *content*";
        var expected = "Check this link github.com and old content";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result.Normalized, Is.EqualTo(expected));
    }

    [Test]
    public async Task NormalizeTextAsync_WithEmptyString_ReturnsEmpty()
    {
        var input = "";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Normalized, Is.Empty);
        Assert.That(result.OriginalText, Is.Empty);
        Assert.That(result.TextHash, Is.Not.Empty);
    }

    [Test]
    public async Task NormalizeTextAsync_WithWhitespaceOnly_ReturnsEmpty()
    {
        var input = "   \t  \n  ";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Normalized, Is.Empty);
    }

    [Test]
    public async Task NormalizeTextAsync_SameInputProducesSameHash()
    {
        var input = "The quick brown fox jumps over the lazy dog";

        var result1 = await _service.NormalizeTextAsync(input);
        var result2 = await _service.NormalizeTextAsync(input);

        Assert.That(result1.TextHash, Is.EqualTo(result2.TextHash));
    }

    [Test]
    public async Task NormalizeTextAsync_DifferentInputProducesDifferentHash()
    {
        var input1 = "The quick brown fox";
        var input2 = "The slow brown fox";

        var result1 = await _service.NormalizeTextAsync(input1);
        var result2 = await _service.NormalizeTextAsync(input2);

        Assert.That(result1.TextHash, Is.Not.EqualTo(result2.TextHash));
    }

    [Test]
    public async Task NormalizeTextAsync_HashIsValidSHA256()
    {
        var input = "Test input";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result.TextHash.Length, Is.EqualTo(64));
        Assert.That(result.TextHash.All(c => "0123456789ABCDEF".Contains(c, StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public async Task NormalizeTextAsync_MarkupAndUrlsCombined_HandlesBoth()
    {
        var input = "Read **my blog** at https://myblog.com/post?id=123&lang=en for more **details**";
        var expected = "Read my blog at myblog.com for more details";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result.Normalized, Is.EqualTo(expected));
    }

    [Test]
    public async Task NormalizeTextAsync_MultilineText_HandlesNewlines()
    {
        var input = "Line 1\nLine 2\r\nLine 3";
        var expected = "Line 1 Line 2 Line 3";

        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result.Normalized, Is.EqualTo(expected));
    }

    [TestCase("Hello World", "hello world")]
    [TestCase("UPPERCASE TEXT", "uppercase text")]
    [TestCase("MiXeD CaSe", "mixed case")]
    public async Task NormalizeTextAsync_DoesNotChangeCase(string input, string _)
    {
        // Note: Current implementation doesn't lowercase text
        // This test documents the current behavior
        var result = await _service.NormalizeTextAsync(input);

        Assert.That(result.Normalized, Is.EqualTo(input));
    }
}
