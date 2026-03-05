using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Common.Data.Entities;
using TelegramAggregator.Api.Config;
using TelegramAggregator.Api.Services;

namespace TelegramAggregator.Tests.Services;

[TestFixture]
public class TelegramPublisherTests
{
    private AppDbContext _dbContext;
    private ITelegramBotClient _mockBotClient;
    private TelegramPublisher _publisher;
    private const long SummaryChannelId = -1001234567890L;

    [SetUp]
    public void SetUp()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PublisherTestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(dbOptions);
        _mockBotClient = Substitute.For<ITelegramBotClient>();

        var textMsg = System.Text.Json.JsonSerializer.Deserialize<Message>("""{"message_id":99,"date":0,"chat":{"id":0,"type":"private"}}""")!;
        var mediaMsg = System.Text.Json.JsonSerializer.Deserialize<Message>("""{"message_id":100,"date":0,"chat":{"id":0,"type":"private"}}""")!;

        _mockBotClient
            .SendRequest(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(textMsg);

        _mockBotClient
            .SendRequest(Arg.Any<SendMediaGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new Message[] { mediaMsg });

        _publisher = new TelegramPublisher(
            Substitute.For<ILogger<TelegramPublisher>>(),
            _mockBotClient,
            Options.Create(new WorkerOptions { SummaryChannelId = SummaryChannelId }),
            _dbContext);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private async Task<Guid> SeedImageAsync(byte[]? content, string? telegramFileId = null)
    {
        var image = new TelegramAggregator.Common.Data.Entities.Image
        {
            Id = Guid.NewGuid(),
            ChecksumSha256 = Guid.NewGuid().ToString("N"),
            MimeType = "image/jpeg",
            Width = 100, Height = 100,
            SizeBytes = content?.Length ?? 0,
            Content = content,
            TelegramFileId = telegramFileId,
            AddedAt = DateTime.UtcNow
        };
        _dbContext.Images.Add(image);
        await _dbContext.SaveChangesAsync();
        return image.Id;
    }

    [Test]
    public async Task PublishSummaryAsync_NoImages_SendsTextMessage()
    {
        var msgId = await _publisher.PublishSummaryAsync("Headline", "Digest", [], ["ch1"], CancellationToken.None);

        Assert.That(msgId, Is.EqualTo(99L));
        await _mockBotClient.Received(1).SendRequest(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>());
        await _mockBotClient.DidNotReceive().SendRequest(Arg.Any<SendMediaGroupRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublishSummaryAsync_ImageWithContent_SendsMediaGroup()
    {
        var imageId = await SeedImageAsync(content: [0xFF, 0xD8, 0xFF]);

        await _publisher.PublishSummaryAsync("Headline", "Digest", [imageId], ["ch1"], CancellationToken.None);

        await _mockBotClient.Received(1).SendRequest(Arg.Any<SendMediaGroupRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublishSummaryAsync_ImageWithFileIdOnly_SendsMediaGroup()
    {
        var imageId = await SeedImageAsync(content: null, telegramFileId: "file-abc-123");

        await _publisher.PublishSummaryAsync("Headline", "Digest", [imageId], ["ch1"], CancellationToken.None);

        await _mockBotClient.Received(1).SendRequest(Arg.Any<SendMediaGroupRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublishSummaryAsync_ImageWithNoContentOrFileId_FallsBackToTextMessage()
    {
        var imageId = await SeedImageAsync(content: null, telegramFileId: null);

        var msgId = await _publisher.PublishSummaryAsync("Headline", "Digest", [imageId], ["ch1"], CancellationToken.None);

        Assert.That(msgId, Is.EqualTo(99L));
        await _mockBotClient.Received(1).SendRequest(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>());
        await _mockBotClient.DidNotReceive().SendRequest(Arg.Any<SendMediaGroupRequest>(), Arg.Any<CancellationToken>());
    }
}
