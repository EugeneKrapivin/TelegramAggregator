using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using TelegramAggregator.Api.AI;
using TelegramAggregator.Api.Background;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Common.Data.Entities;
using TelegramAggregator.Api.Config;
using TelegramAggregator.Api.Services;

namespace TelegramAggregator.Tests.Background;

[TestFixture]
public class SummaryBackgroundServiceTests
{
    private AppDbContext _dbContext;
    private ISemanticSummarizer _mockSummarizer;
    private ITelegramPublisher _mockPublisher;
    private IImageService _mockImageService;
    private SummaryBackgroundService _service;

    [SetUp]
    public void SetUp()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SummaryTestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(dbOptions);
        _mockSummarizer = Substitute.For<ISemanticSummarizer>();
        _mockPublisher = Substitute.For<ITelegramPublisher>();
        _mockImageService = Substitute.For<IImageService>();

        _mockSummarizer
            .SummarizeAsync(Arg.Any<List<PostSummary>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(("Test Headline", "Test Digest"));

        _mockPublisher
            .PublishSummaryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<Guid>>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(12345L);

        _service = new SummaryBackgroundService(
            Substitute.For<ILogger<SummaryBackgroundService>>(),
            Options.Create(new WorkerOptions()),
            _mockSummarizer,
            _mockPublisher,
            _mockImageService,
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

    private async Task<Post> SeedUnsummarizedPostAsync()
    {
        var channel = new Channel
        {
            TelegramChannelId = 1,
            Username = "testchannel",
            Title = "Test Channel",
            IsActive = true,
            AddedAt = DateTime.UtcNow
        };
        _dbContext.Channels.Add(channel);

        var post = new Post
        {
            TelegramMessageId = 1,
            Text = "Hello world",
            NormalizedTextHash = "hash",
            Fingerprint = "fp",
            PublishedAt = DateTime.UtcNow - TimeSpan.FromMinutes(5),
            IngestedAt = DateTime.UtcNow,
            IsSummarized = false,
            RawJson = "{}"
        };
        channel.Posts.Add(post);
        await _dbContext.SaveChangesAsync();
        return post;
    }

    [Test]
    public async Task ExecuteSummaryAsync_NoPosts_SkipsSummarizer()
    {
        await _service.ExecuteSummaryAsync(CancellationToken.None);

        await _mockSummarizer.DidNotReceive()
            .SummarizeAsync(Arg.Any<List<PostSummary>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteSummaryAsync_WithPosts_CallsSummarizer()
    {
        await SeedUnsummarizedPostAsync();

        await _service.ExecuteSummaryAsync(CancellationToken.None);

        await _mockSummarizer.Received(1)
            .SummarizeAsync(Arg.Is<List<PostSummary>>(p => p.Count == 1), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteSummaryAsync_WithPosts_CallsPublisher()
    {
        await SeedUnsummarizedPostAsync();

        await _service.ExecuteSummaryAsync(CancellationToken.None);

        await _mockPublisher.Received(1)
            .PublishSummaryAsync("Test Headline", "Test Digest", Arg.Any<List<Guid>>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteSummaryAsync_WithPosts_MarksPostsAsSummarized()
    {
        var post = await SeedUnsummarizedPostAsync();

        await _service.ExecuteSummaryAsync(CancellationToken.None);

        var updated = await _dbContext.Posts.FindAsync(post.Id);
        Assert.That(updated!.IsSummarized, Is.True);
    }

    [Test]
    public async Task ExecuteSummaryAsync_WithPosts_PersistsSummaryEntity()
    {
        await SeedUnsummarizedPostAsync();

        await _service.ExecuteSummaryAsync(CancellationToken.None);

        Assert.That(await _dbContext.Summaries.CountAsync(), Is.EqualTo(1));
        var summary = await _dbContext.Summaries.FirstAsync();
        Assert.That(summary.Headline, Is.EqualTo("Test Headline"));
        Assert.That(summary.SummaryText, Is.EqualTo("Test Digest"));
    }

    [Test]
    public async Task ExecuteSummaryAsync_WithPosts_ClearsImageContentAfterPublish()
    {
        await SeedUnsummarizedPostAsync();

        await _service.ExecuteSummaryAsync(CancellationToken.None);

        await _mockImageService.Received(1)
            .ClearContentBatchAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }
}
