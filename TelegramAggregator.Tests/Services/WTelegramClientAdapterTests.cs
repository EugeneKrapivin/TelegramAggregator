using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using TL;
using TelegramAggregator.Common.Data;
using Channel = TelegramAggregator.Common.Data.Entities.Channel;
using TelegramAggregator.Api.Services;

namespace TelegramAggregator.Tests.Services;

[TestFixture]
public class WTelegramClientAdapterTests
{
    private AppDbContext _dbContext;
    private IServiceScopeFactory _mockScopeFactory;
    private IImageService _mockImageService;
    private INormalizerService _mockNormalizer;
    private IDeduplicationService _mockDedup;
    private WTelegramClientAdapter _adapter;
    private const long ChannelTelegramId = 12345L;

    [SetUp]
    public void SetUp()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdapterTestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(dbOptions);
        _mockImageService = Substitute.For<IImageService>();
        _mockNormalizer = Substitute.For<INormalizerService>();
        _mockDedup = Substitute.For<IDeduplicationService>();

        // Mock IServiceScopeFactory
        _mockScopeFactory = Substitute.For<IServiceScopeFactory>();
        var mockScope = Substitute.For<IServiceScope>();
        var mockServiceProvider = Substitute.For<IServiceProvider>();
        
        mockServiceProvider.GetService(typeof(AppDbContext)).Returns(_dbContext);
        mockScope.ServiceProvider.Returns(mockServiceProvider);
        _mockScopeFactory.CreateScope().Returns(mockScope);

        _mockNormalizer
            .NormalizeTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new NormalizedText
            {
                OriginalText = callInfo.Arg<string>(),
                Normalized = callInfo.Arg<string>().ToLower(),
                TextHash = "testhash"
            });

        _mockDedup
            .ComputeFingerprint(Arg.Any<string>(), Arg.Any<List<string>>())
            .Returns("testfingerprint");

        _mockDedup
            .IsPostDuplicateAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _adapter = new WTelegramClientAdapter(
            Substitute.For<ILogger<WTelegramClientAdapter>>(),
            _mockScopeFactory,
            _mockImageService,
            _mockNormalizer,
            _mockDedup);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private async Task<Channel> SeedActiveChannelAsync()
    {
        var channel = new Channel
        {
            TelegramChannelId = ChannelTelegramId,
            Username = "testchannel",
            Title = "Test Channel",
            IsActive = true,
            AddedAt = DateTime.UtcNow
        };
        _dbContext.Channels.Add(channel);
        await _dbContext.SaveChangesAsync();
        return channel;
    }

    private static Message BuildChannelMessage(long channelId, string text, int msgId = 1) =>
        new()
        {
            id = msgId,
            peer_id = new PeerChannel { channel_id = channelId },
            message = text,
            date = DateTime.UtcNow
        };

    [Test]
    public async Task ReceiveAndProcessPostAsync_NewPost_SavesPostToDb()
    {
        await SeedActiveChannelAsync();

        await _adapter.ReceiveAndProcessPostAsync(BuildChannelMessage(ChannelTelegramId, "Hello World"));

        Assert.That(await _dbContext.Posts.CountAsync(), Is.EqualTo(1));
        var post = await _dbContext.Posts.FirstAsync();
        Assert.That(post.Text, Is.EqualTo("hello world"));
        Assert.That(post.NormalizedTextHash, Is.EqualTo("testhash"));
    }

    [Test]
    public async Task ReceiveAndProcessPostAsync_UnmonitoredChannel_DoesNotSavePost()
    {
        await SeedActiveChannelAsync();

        await _adapter.ReceiveAndProcessPostAsync(BuildChannelMessage(channelId: 99999L, "Hello"));

        Assert.That(await _dbContext.Posts.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task ReceiveAndProcessPostAsync_DuplicatePost_DoesNotSavePost()
    {
        await SeedActiveChannelAsync();
        _mockDedup
            .IsPostDuplicateAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _adapter.ReceiveAndProcessPostAsync(BuildChannelMessage(ChannelTelegramId, "Dup"));

        Assert.That(await _dbContext.Posts.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task ReceiveAndProcessPostAsync_InactiveChannel_DoesNotSavePost()
    {
        _dbContext.Channels.Add(new Channel
        {
            TelegramChannelId = ChannelTelegramId,
            Username = "inactive",
            Title = "Inactive",
            IsActive = false,
            AddedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        await _adapter.ReceiveAndProcessPostAsync(BuildChannelMessage(ChannelTelegramId, "Hello"));

        Assert.That(await _dbContext.Posts.CountAsync(), Is.EqualTo(0));
    }
}
