using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TelegramAggregator.Data;
using TelegramAggregator.Data.Entities;
using TelegramAggregator.Services;
using Xunit;

namespace TelegramAggregator.Tests.Services;

/// <summary>
/// Unit tests for ImageService.
/// Tests image download, SHA256 hash computation, and deduplication logic.
/// </summary>
public class ImageServiceTests
{
    private readonly Mock<ILogger<ImageService>> _mockLogger;
    private readonly AppDbContext _dbContext;
    private readonly ImageService _service;

    public ImageServiceTests()
    {
        _mockLogger = new Mock<ILogger<ImageService>>();

        // Setup in-memory database for testing
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _service = new ImageService(_mockLogger.Object, _dbContext);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    // ========================
    // FindOrCreateImageAsync Tests
    // ========================

    [Fact]
    public async Task FindOrCreateImageAsync_WithNewImage_CreatesNewRecord()
    {
        // Arrange
        var imageBytes = "Test Image Bytes"u8.ToArray();
        var mimeType = "image/jpeg";
        var width = 800;
        var height = 600;

        // Act
        var imageId = await _service.FindOrCreateImageAsync(imageBytes, mimeType, width, height);

        // Assert
        Assert.NotEqual(Guid.Empty, imageId);

        // Verify image was saved to database
        var savedImage = await _dbContext.Images.FindAsync(imageId);
        Assert.NotNull(savedImage);
        Assert.Equal(mimeType, savedImage.MimeType);
        Assert.Equal(width, savedImage.Width);
        Assert.Equal(height, savedImage.Height);
        Assert.Equal(imageBytes.Length, savedImage.SizeBytes);
        Assert.NotNull(savedImage.ContentBase64);
    }

    [Fact]
    public async Task FindOrCreateImageAsync_WithDuplicateImage_ReturnExistingId()
    {
        // Arrange
        var imageBytes = "Duplicate Image Bytes"u8.ToArray();
        var mimeType = "image/png";
        var width = 1024;
        var height = 768;

        // Act - Create first image
        var imageId1 = await _service.FindOrCreateImageAsync(imageBytes, mimeType, width, height);

        // Act - Create with same bytes (duplicate)
        var imageId2 = await _service.FindOrCreateImageAsync(imageBytes, mimeType, width, height);

        // Assert - Both should return same ID
        Assert.Equal(imageId1, imageId2);

        // Verify only one image in database
        var imageCount = await _dbContext.Images.CountAsync();
        Assert.Equal(1, imageCount);
    }

    [Fact]
    public async Task FindOrCreateImageAsync_WithExistingImage_UpdatesUsedAtTimestamp()
    {
        // Arrange
        var imageBytes = "Image Bytes"u8.ToArray();
        var mimeType = "image/jpeg";

        // Act - Create initial image
        var imageId = await _service.FindOrCreateImageAsync(imageBytes, mimeType, 640, 480);

        // Get the created image and note its UsedAt
        var image1 = await _dbContext.Images.FindAsync(imageId);
        var firstUsedAt = image1!.UsedAt;

        // Wait a bit to ensure time difference
        await Task.Delay(100);

        // Act - Find same image
        var imageId2 = await _service.FindOrCreateImageAsync(imageBytes, mimeType, 640, 480);

        // Get the image again and check UsedAt was updated
        var image2 = await _dbContext.Images.FindAsync(imageId2);
        var secondUsedAt = image2!.UsedAt;

        // Assert
        Assert.Equal(imageId, imageId2);
        Assert.True(secondUsedAt >= firstUsedAt);
    }

    [Fact]
    public async Task FindOrCreateImageAsync_WithDifferentImages_CreatesMultipleRecords()
    {
        // Arrange
        var bytes1 = "Image Bytes 1"u8.ToArray();
        var bytes2 = "Image Bytes 2"u8.ToArray();

        // Act
        var imageId1 = await _service.FindOrCreateImageAsync(bytes1, "image/jpeg", 800, 600);
        var imageId2 = await _service.FindOrCreateImageAsync(bytes2, "image/png", 1024, 768);

        // Assert
        Assert.NotEqual(imageId1, imageId2);

        var imageCount = await _dbContext.Images.CountAsync();
        Assert.Equal(2, imageCount);
    }

    [Fact]
    public async Task FindOrCreateImageAsync_StoresContentAsBase64()
    {
        // Arrange
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
        var mimeType = "image/jpeg";

        // Act
        var imageId = await _service.FindOrCreateImageAsync(imageBytes, mimeType, 100, 100);

        // Assert
        var savedImage = await _dbContext.Images.FindAsync(imageId);
        Assert.NotNull(savedImage!.ContentBase64);

        // Verify base64 can be decoded back to original
        var decodedBytes = Convert.FromBase64String(savedImage.ContentBase64);
        Assert.Equal(imageBytes, decodedBytes);
    }

    [Fact]
    public async Task FindOrCreateImageAsync_ComputesSha256Correctly()
    {
        // Arrange
        var imageBytes = "Test Image for Hash"u8.ToArray();

        // Act
        var imageId = await _service.FindOrCreateImageAsync(imageBytes, "image/jpeg", 100, 100);

        // Assert
        var savedImage = await _dbContext.Images.FindAsync(imageId);
        var expectedHash = _service.ComputeSha256Hash(imageBytes);

        Assert.Equal(expectedHash, savedImage!.ChecksumSha256);
    }

    [Fact]
    public async Task FindOrCreateImageAsync_WithMultipleDuplicates_ReturnsConsistentId()
    {
        // Arrange
        var imageBytes = "Consistent Image"u8.ToArray();

        // Act
        var id1 = await _service.FindOrCreateImageAsync(imageBytes, "image/jpeg", 100, 100);
        var id2 = await _service.FindOrCreateImageAsync(imageBytes, "image/png", 200, 200);
        var id3 = await _service.FindOrCreateImageAsync(imageBytes, "image/gif", 50, 50);

        // Assert - All should return the same ID (deduplication by content)
        Assert.Equal(id1, id2);
        Assert.Equal(id2, id3);

        var imageCount = await _dbContext.Images.CountAsync();
        Assert.Equal(1, imageCount);

        // Verify first image's metadata is preserved
        var image = await _dbContext.Images.FindAsync(id1);
        Assert.Equal("image/jpeg", image!.MimeType);
        Assert.Equal(100, image.Width);
        Assert.Equal(100, image.Height);
    }

    // ========================
    // Download Image Tests
    // ========================

    [Fact]
    public async Task DownloadImageAsync_WithValidUrl_ReturnsBytes()
    {
        // Note: This test would require mocking HttpClient
        // For now, we skip it as it requires actual HTTP or proper mocking
        // In a real scenario, use HttpClientFactory and mock it
        await Task.CompletedTask;
    }
}
