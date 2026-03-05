using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using SixLabors.ImageSharp;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Api.Config;
using TelegramAggregator.Api.Services;

namespace TelegramAggregator.Tests.Services;

/// <summary>
/// Unit tests for ImageService.
/// Tests image download, SHA256 hash computation, and deduplication logic.
/// </summary>
[TestFixture]
public class ImageServiceTests
{
    private ILogger<ImageService> _mockLogger;
    private AppDbContext _dbContext;
    private ImageService _service;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = Substitute.For<ILogger<ImageService>>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _service = new ImageService(_mockLogger, _dbContext, Options.Create(new WorkerOptions { PHashHammingThreshold = 8 }));
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext?.Dispose();
    }

    // ========================
    // FindOrCreateImageAsync Tests
    // ========================

    [Test]
    public async Task FindOrCreateImageAsync_WithNewImage_CreatesNewRecord()
    {
        var imageBytes = "Test Image Bytes"u8.ToArray();
        var mimeType = "image/jpeg";
        var width = 800;
        var height = 600;

        var imageId = await _service.FindOrCreateImageAsync(imageBytes, mimeType, width, height);

        Assert.That(imageId, Is.Not.EqualTo(Guid.Empty));

        var savedImage = await _dbContext.Images.FindAsync(imageId);
        Assert.That(savedImage, Is.Not.Null);
        Assert.That(savedImage!.MimeType, Is.EqualTo(mimeType));
        Assert.That(savedImage.Width, Is.EqualTo(width));
        Assert.That(savedImage.Height, Is.EqualTo(height));
        Assert.That(savedImage.SizeBytes, Is.EqualTo(imageBytes.Length));
        Assert.That(savedImage.Content, Is.Not.Null);
    }

    [Test]
    public async Task FindOrCreateImageAsync_WithDuplicateImage_ReturnExistingId()
    {
        var imageBytes = "Duplicate Image Bytes"u8.ToArray();
        var mimeType = "image/png";
        var width = 1024;
        var height = 768;

        var imageId1 = await _service.FindOrCreateImageAsync(imageBytes, mimeType, width, height);
        var imageId2 = await _service.FindOrCreateImageAsync(imageBytes, mimeType, width, height);

        Assert.That(imageId1, Is.EqualTo(imageId2));

        var imageCount = await _dbContext.Images.CountAsync();
        Assert.That(imageCount, Is.EqualTo(1));
    }

    [Test]
    public async Task FindOrCreateImageAsync_WithExistingImage_UpdatesUsedAtTimestamp()
    {
        var imageBytes = "Image Bytes"u8.ToArray();
        var mimeType = "image/jpeg";

        var imageId = await _service.FindOrCreateImageAsync(imageBytes, mimeType, 640, 480);

        var image1 = await _dbContext.Images.FindAsync(imageId);
        var firstUsedAt = image1!.UsedAt;

        await Task.Delay(100);

        var imageId2 = await _service.FindOrCreateImageAsync(imageBytes, mimeType, 640, 480);

        var image2 = await _dbContext.Images.FindAsync(imageId2);
        var secondUsedAt = image2!.UsedAt;

        Assert.That(imageId, Is.EqualTo(imageId2));
        Assert.That(secondUsedAt, Is.GreaterThanOrEqualTo(firstUsedAt));
    }

    [Test]
    public async Task FindOrCreateImageAsync_WithDifferentImages_CreatesMultipleRecords()
    {
        var bytes1 = "Image Bytes 1"u8.ToArray();
        var bytes2 = "Image Bytes 2"u8.ToArray();

        var imageId1 = await _service.FindOrCreateImageAsync(bytes1, "image/jpeg", 800, 600);
        var imageId2 = await _service.FindOrCreateImageAsync(bytes2, "image/png", 1024, 768);

        Assert.That(imageId1, Is.Not.EqualTo(imageId2));

        var imageCount = await _dbContext.Images.CountAsync();
        Assert.That(imageCount, Is.EqualTo(2));
    }

    [Test]
    public async Task FindOrCreateImageAsync_StoresRawContent()
    {
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
        var mimeType = "image/jpeg";

        var imageId = await _service.FindOrCreateImageAsync(imageBytes, mimeType, 100, 100);

        var savedImage = await _dbContext.Images.FindAsync(imageId);
        Assert.That(savedImage!.Content, Is.Not.Null);
        Assert.That(savedImage.Content, Is.EqualTo(imageBytes));
    }

    [Test]
    public async Task FindOrCreateImageAsync_ComputesSha256Correctly()
    {
        var imageBytes = "Test Image for Hash"u8.ToArray();

        var imageId = await _service.FindOrCreateImageAsync(imageBytes, "image/jpeg", 100, 100);

        var savedImage = await _dbContext.Images.FindAsync(imageId);
        var expectedHash = _service.ComputeSha256Hash(imageBytes);

        Assert.That(savedImage!.ChecksumSha256, Is.EqualTo(expectedHash));
    }

    [Test]
    public async Task FindOrCreateImageAsync_WithMultipleDuplicates_ReturnsConsistentId()
    {
        var imageBytes = "Consistent Image"u8.ToArray();

        var id1 = await _service.FindOrCreateImageAsync(imageBytes, "image/jpeg", 100, 100);
        var id2 = await _service.FindOrCreateImageAsync(imageBytes, "image/png", 200, 200);
        var id3 = await _service.FindOrCreateImageAsync(imageBytes, "image/gif", 50, 50);

        Assert.That(id1, Is.EqualTo(id2));
        Assert.That(id2, Is.EqualTo(id3));

        var imageCount = await _dbContext.Images.CountAsync();
        Assert.That(imageCount, Is.EqualTo(1));

        var image = await _dbContext.Images.FindAsync(id1);
        Assert.That(image!.MimeType, Is.EqualTo("image/jpeg"));
        Assert.That(image.Width, Is.EqualTo(100));
        Assert.That(image.Height, Is.EqualTo(100));
    }

    // ========================
    // ClearContentAsync Tests
    // ========================

    [Test]
    public async Task ClearContentAsync_WithExistingImage_SetsContentToNull()
    {
        var imageBytes = "Clear Me"u8.ToArray();
        var imageId = await _service.FindOrCreateImageAsync(imageBytes, "image/jpeg", 100, 100);

        var before = await _dbContext.Images.FindAsync(imageId);
        Assert.That(before!.Content, Is.Not.Null);

        await _service.ClearContentAsync(imageId);

        var after = await _dbContext.Images.FindAsync(imageId);
        Assert.That(after!.Content, Is.Null);
    }

    [Test]
    public async Task ClearContentAsync_WithNonExistentImage_DoesNotThrow()
    {
        Assert.DoesNotThrowAsync(() => _service.ClearContentAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task ClearContentBatchAsync_ClearsMultipleImages()
    {
        var bytes1 = "Batch Image 1"u8.ToArray();
        var bytes2 = "Batch Image 2"u8.ToArray();
        var id1 = await _service.FindOrCreateImageAsync(bytes1, "image/jpeg", 100, 100);
        var id2 = await _service.FindOrCreateImageAsync(bytes2, "image/png", 200, 200);

        await _service.ClearContentBatchAsync([id1, id2]);

        var image1 = await _dbContext.Images.FindAsync(id1);
        var image2 = await _dbContext.Images.FindAsync(id2);
        Assert.That(image1!.Content, Is.Null);
        Assert.That(image2!.Content, Is.Null);
    }

    [Test]
    public async Task ClearContentBatchAsync_SkipsAlreadyClearedImages()
    {
        var bytes = "Already Cleared"u8.ToArray();
        var imageId = await _service.FindOrCreateImageAsync(bytes, "image/jpeg", 100, 100);

        await _service.ClearContentAsync(imageId);
        await _service.ClearContentBatchAsync([imageId]);

        var image = await _dbContext.Images.FindAsync(imageId);
        Assert.That(image!.Content, Is.Null);
    }

    [Test]
    public async Task DownloadImageAsync_WithValidUrl_ReturnsBytes()
    {
        // Note: This test would require mocking HttpClient
        // For now, we skip it as it requires actual HTTP or proper mocking
        await Task.CompletedTask;
    }

    // ========================
    // pHash Deduplication Tests
    // ========================

    /// <summary>
    /// Creates a 16×16 PNG where the top 8 rows are <paramref name="topColor"/>
    /// and the bottom 8 rows are <paramref name="bottomColor"/>.
    /// After pHash resize to 8×8, the upper 4 rows and lower 4 rows produce a deterministic hash.
    /// </summary>
    private static byte[] CreateTopBottomBicolorPng(SixLabors.ImageSharp.PixelFormats.Rgba32 topColor, SixLabors.ImageSharp.PixelFormats.Rgba32 bottomColor)
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(16, 16);
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
                image[x, y] = y < 8 ? topColor : bottomColor;
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Test]
    public async Task FindOrCreateImageAsync_NearDuplicateImage_ReturnsExistingId()
    {
        // imageA: top white (255), bottom black (0)
        var imageA = CreateTopBottomBicolorPng(new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 255, 255), new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 0));
        // imageB: top light gray (220), bottom dark gray (30) — different SHA256, identical pHash structure
        var imageB = CreateTopBottomBicolorPng(new SixLabors.ImageSharp.PixelFormats.Rgba32(220, 220, 220), new SixLabors.ImageSharp.PixelFormats.Rgba32(30, 30, 30));

        var idA = await _service.FindOrCreateImageAsync(imageA, "image/png", 16, 16);
        var idB = await _service.FindOrCreateImageAsync(imageB, "image/png", 16, 16);

        Assert.That(idA, Is.EqualTo(idB));
        Assert.That(await _dbContext.Images.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task FindOrCreateImageAsync_VisuallyDifferentImage_CreatesNewRecord()
    {
        // imageA: top white, bottom black
        var imageA = CreateTopBottomBicolorPng(new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 255, 255), new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 0));
        // imageB: top black, bottom white — inverted, maximum Hamming distance
        var imageB = CreateTopBottomBicolorPng(new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 0), new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 255, 255));

        var idA = await _service.FindOrCreateImageAsync(imageA, "image/png", 16, 16);
        var idB = await _service.FindOrCreateImageAsync(imageB, "image/png", 16, 16);

        Assert.That(idA, Is.Not.EqualTo(idB));
        Assert.That(await _dbContext.Images.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task FindOrCreateImageAsync_NewImage_StoresPerceptualHash()
    {
        var imageBytes = CreateTopBottomBicolorPng(new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 255, 255), new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 0));

        var imageId = await _service.FindOrCreateImageAsync(imageBytes, "image/png", 16, 16);

        var saved = await _dbContext.Images.FindAsync(imageId);
        Assert.That(saved!.PerceptualHash, Is.Not.Null);
        Assert.That(saved.PerceptualHash, Is.Not.Empty);
    }
}
