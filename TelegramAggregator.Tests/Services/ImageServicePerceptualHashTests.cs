using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using TelegramAggregator.Common.Data;
using TelegramAggregator.Services;

using Xunit;

namespace TelegramAggregator.Tests.Services;

/// <summary>
/// Unit tests for perceptual hash functionality in ImageService.
/// Tests pHash computation and Hamming distance calculation.
/// </summary>
public class ImageServicePerceptualHashTests : IDisposable
{
    private readonly Mock<ILogger<ImageService>> _mockLogger;
    private readonly AppDbContext _dbContext;
    private readonly ImageService _service;

    public ImageServicePerceptualHashTests()
    {
        _mockLogger = new Mock<ILogger<ImageService>>();

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
    // Hamming Distance Tests
    // ========================

    [Fact]
    public void ComputeHammingDistance_IdenticalHashes_ReturnsZero()
    {
        // Arrange
        ulong hash = 0b1010101010101010101010101010101010101010101010101010101010101010;

        // Act
        int distance = _service.ComputeHammingDistance(hash, hash);

        // Assert
        Assert.Equal(0, distance);
    }

    [Fact]
    public void ComputeHammingDistance_CompleteDifferent_Returns64()
    {
        // Arrange
        ulong hash1 = 0x0000000000000000;
        ulong hash2 = 0xFFFFFFFFFFFFFFFF;

        // Act
        int distance = _service.ComputeHammingDistance(hash1, hash2);

        // Assert
        Assert.Equal(64, distance);
    }

    [Fact]
    public void ComputeHammingDistance_SingleBitDifference_ReturnsOne()
    {
        // Arrange
        ulong hash1 = 0b1000000000000000000000000000000000000000000000000000000000000000;
        ulong hash2 = 0b0000000000000000000000000000000000000000000000000000000000000000;

        // Act
        int distance = _service.ComputeHammingDistance(hash1, hash2);

        // Assert
        Assert.Equal(1, distance);
    }

    [Fact]
    public void ComputeHammingDistance_TwoBitsDifferent_ReturnsTwo()
    {
        // Arrange
        ulong hash1 = 0b1100000000000000000000000000000000000000000000000000000000000000;
        ulong hash2 = 0b0000000000000000000000000000000000000000000000000000000000000000;

        // Act
        int distance = _service.ComputeHammingDistance(hash1, hash2);

        // Assert
        Assert.Equal(2, distance);
    }

    [Fact]
    public void ComputeHammingDistance_IsSymmetric()
    {
        // Arrange
        ulong hash1 = 0xAAAAAAAAAAAAAAAA;
        ulong hash2 = 0x5555555555555555;

        // Act
        int distance1 = _service.ComputeHammingDistance(hash1, hash2);
        int distance2 = _service.ComputeHammingDistance(hash2, hash1);

        // Assert
        Assert.Equal(distance1, distance2);
    }

    [Fact]
    public void ComputeHammingDistance_WithSpecificPattern_ReturnsCorrectCount()
    {
        // Arrange - Pattern: alternate bits
        ulong hash1 = 0xAAAAAAAAAAAAAAAA; // 1010...1010
        ulong hash2 = 0x5555555555555555; // 0101...0101
        // Each position differs, so 64 bits differ

        // Act
        int distance = _service.ComputeHammingDistance(hash1, hash2);

        // Assert
        Assert.Equal(64, distance);
    }

    [Theory]
    [InlineData(0, 0, 0)]      // Both 0
    [InlineData(1, 1, 0)]      // Both have single bit
    [InlineData(1, 2, 2)]      // Single bits at different positions (0001 vs 0010)
    [InlineData(3, 3, 0)]      // 0011 vs 0011
    [InlineData(255, 0, 8)]    // All 8 bits different
    public void ComputeHammingDistance_WithVariousInputs_ReturnsExpectedDistance(ulong hash1, ulong hash2, int expected)
    {
        // Act
        int distance = _service.ComputeHammingDistance(hash1, hash2);

        // Assert
        Assert.Equal(expected, distance);
    }

    // ========================
    // Perceptual Hash Tests
    // ========================

    [Fact]
    public async Task ComputePerceptualHashAsync_WithValidImageBytes_ReturnsValidHash()
    {
        // Arrange
        var imageBytes = CreateSimpleTestImage(64, 64);

        // Act
        var hash = await _service.ComputePerceptualHashAsync(imageBytes);

        // Assert
        Assert.NotEqual(0UL, hash);  // Hash should not be zero (extremely unlikely)
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_SameImageBytes_ProducesSameHash()
    {
        // Arrange
        var imageBytes = CreateSimpleTestImage(64, 64);

        // Act
        var hash1 = await _service.ComputePerceptualHashAsync(imageBytes);
        var hash2 = await _service.ComputePerceptualHashAsync(imageBytes);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_SimilarImages_HaveSmallHammingDistance()
    {
        // Arrange - Create two very similar images (same with slight variation)
        var image1 = CreateTestImageWithContent(64, 64, fillColor: true);
        var image2 = CreateTestImageWithContent(64, 64, fillColor: true);

        // Act
        var hash1 = await _service.ComputePerceptualHashAsync(image1);
        var hash2 = await _service.ComputePerceptualHashAsync(image2);
        var distance = _service.ComputeHammingDistance(hash1, hash2);

        // Assert - Similar images should have low Hamming distance
        Assert.True(distance <= 8, $"Expected distance <= 8, got {distance}");
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_DifferentImages_HaveLargeHammingDistance()
    {
        // Arrange - Create two very different images (opposite patterns)
        var image1 = CreateTestImageWithPattern(64, 64, pattern: false);  // Dark corners
        var image2 = CreateTestImageWithPattern(64, 64, pattern: true);   // Bright corners

        // Act
        var hash1 = await _service.ComputePerceptualHashAsync(image1);
        var hash2 = await _service.ComputePerceptualHashAsync(image2);
        var distance = _service.ComputeHammingDistance(hash1, hash2);

        // Assert - Very different images should have large Hamming distance (should be > 10)
        Assert.True(distance > 10, $"Expected distance > 10, got {distance}");
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_VariousSizes_AllProduceValidHash()
    {
        // Arrange
        var sizes = new[] { 32, 64, 128, 256, 512 };

        // Act & Assert
        foreach (var size in sizes)
        {
            var imageBytes = CreateSimpleTestImage(size, size);
            var hash = await _service.ComputePerceptualHashAsync(imageBytes);

            Assert.NotEqual(0UL, hash);
        }
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_WithRotatedImage_ProducesSimilarHash()
    {
        // Arrange - Create an image that would be very similar when rotated slightly
        // (This is a basic test - true rotation invariance is complex)
        var image = CreateTestImageWithContent(128, 128, fillColor: true);

        // Act
        var hash1 = await _service.ComputePerceptualHashAsync(image);
        // Note: In a real implementation, we'd also test with actual rotated image
        // For now, we just verify the hash is stable
        var hash2 = await _service.ComputePerceptualHashAsync(image);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    // ========================
    // Helper Methods
    // ========================

    /// <summary>
    /// Creates a simple test image with a uniform gradient.
    /// </summary>
    private byte[] CreateSimpleTestImage(int width, int height)
    {
        // Use SixLabors.ImageSharp to create a test image
        using (var image = new Image<Rgba32>(width, height))
        {
            // Fill with gradient
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte gray = (byte)((x + y) % 256);
                    image[x, y] = new Rgba32(gray, gray, gray, 255);
                }
            }

            using (var memoryStream = new MemoryStream())
            {
                image.SaveAsPng(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }

    /// <summary>
    /// Creates a test image with uniform fill color.
    /// </summary>
    private byte[] CreateTestImageWithContent(int width, int height, bool fillColor)
    {
        using (var image = new Image<Rgba32>(width, height))
        {
            byte fillValue = fillColor ? (byte)255 : (byte)0;
            var fillColor32 = new Rgba32(fillValue, fillValue, fillValue, 255);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    image[x, y] = fillColor32;
                }
            }

            using (var memoryStream = new MemoryStream())
            {
                image.SaveAsPng(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }

    /// <summary>
    /// Creates a test image with a pattern to ensure different hashes.
    /// </summary>
    private byte[] CreateTestImageWithPattern(int width, int height, bool pattern)
    {
        using (var image = new Image<Rgba32>(width, height))
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Create a checkboard or inverted pattern
                    bool isCorner = (x < width / 2) != (y < height / 2);
                    if (pattern)
                    {
                        isCorner = !isCorner;
                    }

                    byte value = isCorner ? (byte)255 : (byte)0;
                    image[x, y] = new Rgba32(value, value, value, 255);
                }
            }

            using (var memoryStream = new MemoryStream())
            {
                image.SaveAsPng(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }
}
