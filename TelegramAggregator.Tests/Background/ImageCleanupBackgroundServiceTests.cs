using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using TelegramAggregator.Background;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Common.Data.Entities;
using TelegramAggregator.Config;

namespace TelegramAggregator.Tests.Background;

[TestFixture]
public class ImageCleanupBackgroundServiceTests
{
    private AppDbContext _dbContext;
    private ImageCleanupBackgroundService _service;
    private WorkerOptions _options;

    [SetUp]
    public void SetUp()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CleanupTestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(dbOptions);

        _options = new WorkerOptions
        {
            ImageRetentionHours = TimeSpan.FromHours(24),
            ImageCleanupInterval = TimeSpan.FromHours(1)
        };

        _service = new ImageCleanupBackgroundService(
            Substitute.For<ILogger<ImageCleanupBackgroundService>>(),
            Options.Create(_options),
            CreateScopeFactory(_dbContext));
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private static IServiceScopeFactory CreateScopeFactory(AppDbContext dbContext)
    {
        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(AppDbContext)).Returns(dbContext);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(sp);

        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);
        return factory;
    }

    private async Task<Guid> SeedImageAsync(byte[]? content, DateTime? usedAt)
    {
        var image = new Image
        {
            Id = Guid.NewGuid(),
            ChecksumSha256 = Guid.NewGuid().ToString("N"),
            MimeType = "image/jpeg",
            Width = 100, Height = 100,
            SizeBytes = content?.Length ?? 0,
            Content = content,
            AddedAt = DateTime.UtcNow,
            UsedAt = usedAt
        };
        _dbContext.Images.Add(image);
        await _dbContext.SaveChangesAsync();
        return image.Id;
    }

    [Test]
    public async Task RunCleanupCycleAsync_StaleImageWithContent_ClearsContent()
    {
        var staleUsedAt = DateTime.UtcNow - _options.ImageRetentionHours - TimeSpan.FromMinutes(1);
        var imageId = await SeedImageAsync(new byte[] { 1, 2, 3 }, staleUsedAt);

        await _service.RunCleanupCycleAsync(_dbContext, CancellationToken.None);

        var image = await _dbContext.Images.FindAsync(imageId);
        Assert.That(image!.Content, Is.Null);
    }

    [Test]
    public async Task RunCleanupCycleAsync_RecentImage_PreservesContent()
    {
        var recentUsedAt = DateTime.UtcNow - TimeSpan.FromMinutes(5);
        var imageId = await SeedImageAsync(new byte[] { 1, 2, 3 }, recentUsedAt);

        await _service.RunCleanupCycleAsync(_dbContext, CancellationToken.None);

        var image = await _dbContext.Images.FindAsync(imageId);
        Assert.That(image!.Content, Is.Not.Null);
    }

    [Test]
    public async Task RunCleanupCycleAsync_ImageWithNullContent_DoesNotThrow()
    {
        var staleUsedAt = DateTime.UtcNow - _options.ImageRetentionHours - TimeSpan.FromMinutes(1);
        var imageId = await SeedImageAsync(content: null, staleUsedAt);

        Assert.DoesNotThrowAsync(() => _service.RunCleanupCycleAsync(_dbContext, CancellationToken.None));

        var image = await _dbContext.Images.FindAsync(imageId);
        Assert.That(image!.Content, Is.Null);
    }

    [Test]
    public async Task RunCleanupCycleAsync_ImageWithNullUsedAt_PreservesContent()
    {
        var imageId = await SeedImageAsync(new byte[] { 1, 2, 3 }, usedAt: null);

        await _service.RunCleanupCycleAsync(_dbContext, CancellationToken.None);

        var image = await _dbContext.Images.FindAsync(imageId);
        Assert.That(image!.Content, Is.Not.Null);
    }
}
