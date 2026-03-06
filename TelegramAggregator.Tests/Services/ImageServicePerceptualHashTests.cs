using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Api.Config;
using TelegramAggregator.Api.Services;

namespace TelegramAggregator.Tests.Services;

/// <summary>
/// Unit tests for perceptual hash functionality in ImageService.
/// Tests pHash computation and Hamming distance calculation.
/// </summary>
[TestFixture]
public class ImageServicePerceptualHashTests
{
    private AppDbContext _dbContext;
    private IServiceScopeFactory _mockScopeFactory;
    private ImageService _service;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        // Mock IServiceScopeFactory
        _mockScopeFactory = Substitute.For<IServiceScopeFactory>();
        var mockScope = Substitute.For<IServiceScope>();
        var mockServiceProvider = Substitute.For<IServiceProvider>();
        
        mockServiceProvider.GetService(typeof(AppDbContext)).Returns(_dbContext);
        mockScope.ServiceProvider.Returns(mockServiceProvider);
        _mockScopeFactory.CreateScope().Returns(mockScope);

        _service = new ImageService(
            Substitute.For<ILogger<ImageService>>(),
            _mockScopeFactory,
            Options.Create(new WorkerOptions { PHashHammingThreshold = 8 }));
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext?.Dispose();
    }

    // ========================
    // Hamming Distance Tests
    // ========================

    [Test]
    public void ComputeHammingDistance_IdenticalHashes_ReturnsZero()
    {
        ulong hash = 0b1010101010101010101010101010101010101010101010101010101010101010;

        int distance = _service.ComputeHammingDistance(hash, hash);

        Assert.That(distance, Is.EqualTo(0));
    }

    [Test]
    public void ComputeHammingDistance_CompleteDifferent_Returns64()
    {
        ulong hash1 = 0x0000000000000000;
        ulong hash2 = 0xFFFFFFFFFFFFFFFF;

        int distance = _service.ComputeHammingDistance(hash1, hash2);

        Assert.That(distance, Is.EqualTo(64));
    }

    [Test]
    public void ComputeHammingDistance_SingleBitDifference_ReturnsOne()
    {
        ulong hash1 = 0b1000000000000000000000000000000000000000000000000000000000000000;
        ulong hash2 = 0b0000000000000000000000000000000000000000000000000000000000000000;

        int distance = _service.ComputeHammingDistance(hash1, hash2);

        Assert.That(distance, Is.EqualTo(1));
    }

    [Test]
    public void ComputeHammingDistance_TwoBitsDifferent_ReturnsTwo()
    {
        ulong hash1 = 0b1100000000000000000000000000000000000000000000000000000000000000;
        ulong hash2 = 0b0000000000000000000000000000000000000000000000000000000000000000;

        int distance = _service.ComputeHammingDistance(hash1, hash2);

        Assert.That(distance, Is.EqualTo(2));
    }

    [Test]
    public void ComputeHammingDistance_IsSymmetric()
    {
        ulong hash1 = 0xAAAAAAAAAAAAAAAA;
        ulong hash2 = 0x5555555555555555;

        int distance1 = _service.ComputeHammingDistance(hash1, hash2);
        int distance2 = _service.ComputeHammingDistance(hash2, hash1);

        Assert.That(distance1, Is.EqualTo(distance2));
    }

    [Test]
    public void ComputeHammingDistance_WithSpecificPattern_ReturnsCorrectCount()
    {
        ulong hash1 = 0xAAAAAAAAAAAAAAAA; // 1010...1010
        ulong hash2 = 0x5555555555555555; // 0101...0101

        int distance = _service.ComputeHammingDistance(hash1, hash2);

        Assert.That(distance, Is.EqualTo(64));
    }

    [TestCase(0UL, 0UL, 0)]
    [TestCase(1UL, 1UL, 0)]
    [TestCase(1UL, 2UL, 2)]
    [TestCase(3UL, 3UL, 0)]
    [TestCase(255UL, 0UL, 8)]
    public void ComputeHammingDistance_WithVariousInputs_ReturnsExpectedDistance(ulong hash1, ulong hash2, int expected)
    {
        int distance = _service.ComputeHammingDistance(hash1, hash2);

        Assert.That(distance, Is.EqualTo(expected));
    }

    // ========================
    // Perceptual Hash Tests
    // ========================

    [Test]
    public async Task ComputePerceptualHashAsync_WithValidImageBytes_ReturnsValidHash()
    {
        var imageBytes = CreateSimpleTestImage(64, 64);

        var hash = await _service.ComputePerceptualHashAsync(imageBytes);

        Assert.That(hash, Is.Not.EqualTo(0UL));
    }

    [Test]
    public async Task ComputePerceptualHashAsync_SameImageBytes_ProducesSameHash()
    {
        var imageBytes = CreateSimpleTestImage(64, 64);

        var hash1 = await _service.ComputePerceptualHashAsync(imageBytes);
        var hash2 = await _service.ComputePerceptualHashAsync(imageBytes);

        Assert.That(hash1, Is.EqualTo(hash2));
    }

    [Test]
    public async Task ComputePerceptualHashAsync_SimilarImages_HaveSmallHammingDistance()
    {
        var image1 = CreateTestImageWithContent(64, 64, fillColor: true);
        var image2 = CreateTestImageWithContent(64, 64, fillColor: true);

        var hash1 = await _service.ComputePerceptualHashAsync(image1);
        var hash2 = await _service.ComputePerceptualHashAsync(image2);
        var distance = _service.ComputeHammingDistance(hash1, hash2);

        Assert.That(distance, Is.LessThanOrEqualTo(8), $"Expected distance <= 8, got {distance}");
    }

    [Test]
    public async Task ComputePerceptualHashAsync_DifferentImages_HaveLargeHammingDistance()
    {
        var image1 = CreateTestImageWithPattern(64, 64, pattern: false);
        var image2 = CreateTestImageWithPattern(64, 64, pattern: true);

        var hash1 = await _service.ComputePerceptualHashAsync(image1);
        var hash2 = await _service.ComputePerceptualHashAsync(image2);
        var distance = _service.ComputeHammingDistance(hash1, hash2);

        Assert.That(distance, Is.GreaterThan(10), $"Expected distance > 10, got {distance}");
    }

    [Test]
    public async Task ComputePerceptualHashAsync_VariousSizes_AllProduceValidHash()
    {
        var sizes = new[] { 32, 64, 128, 256, 512 };

        foreach (var size in sizes)
        {
            var imageBytes = CreateSimpleTestImage(size, size);
            var hash = await _service.ComputePerceptualHashAsync(imageBytes);

            Assert.That(hash, Is.Not.EqualTo(0UL));
        }
    }

    [Test]
    public async Task ComputePerceptualHashAsync_WithRotatedImage_ProducesSimilarHash()
    {
        var image = CreateTestImageWithContent(128, 128, fillColor: true);

        var hash1 = await _service.ComputePerceptualHashAsync(image);
        var hash2 = await _service.ComputePerceptualHashAsync(image);

        Assert.That(hash1, Is.EqualTo(hash2));
    }

    // ========================
    // Helper Methods
    // ========================

    private byte[] CreateSimpleTestImage(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                byte gray = (byte)((x + y) % 256);
                image[x, y] = new Rgba32(gray, gray, gray, 255);
            }
        using var memoryStream = new MemoryStream();
        image.SaveAsPng(memoryStream);
        return memoryStream.ToArray();
    }

    private byte[] CreateTestImageWithContent(int width, int height, bool fillColor)
    {
        using var image = new Image<Rgba32>(width, height);
        byte fillValue = fillColor ? (byte)255 : (byte)0;
        var fillColor32 = new Rgba32(fillValue, fillValue, fillValue, 255);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                image[x, y] = fillColor32;
        using var memoryStream = new MemoryStream();
        image.SaveAsPng(memoryStream);
        return memoryStream.ToArray();
    }

    private byte[] CreateTestImageWithPattern(int width, int height, bool pattern)
    {
        using var image = new Image<Rgba32>(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bool isCorner = (x < width / 2) != (y < height / 2);
                if (pattern)
                    isCorner = !isCorner;
                byte value = isCorner ? (byte)255 : (byte)0;
                image[x, y] = new Rgba32(value, value, value, 255);
            }
        using var memoryStream = new MemoryStream();
        image.SaveAsPng(memoryStream);
        return memoryStream.ToArray();
    }
}
