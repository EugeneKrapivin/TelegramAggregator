using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Common.Data.Entities;
using TelegramAggregator.Services;

namespace TelegramAggregator.Tests.Services;

[TestFixture]
public class DeduplicationServiceTests
{
    private AppDbContext _dbContext;
    private DeduplicationService _service;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DeduplicationTestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);
        _service = new DeduplicationService(Substitute.For<ILogger<DeduplicationService>>(), _dbContext);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private async Task<Channel> SeedChannelAsync(long telegramId = 100)
    {
        var channel = new Channel
        {
            TelegramChannelId = telegramId,
            Username = "testchannel",
            Title = "Test Channel",
            IsActive = true,
            AddedAt = DateTime.UtcNow
        };
        _dbContext.Channels.Add(channel);
        await _dbContext.SaveChangesAsync();
        return channel;
    }

    private async Task SeedPostAsync(long channelId, string fingerprint)
    {
        _dbContext.Posts.Add(new Post
        {
            TelegramMessageId = Random.Shared.NextInt64(1, 9999),
            ChannelId = channelId,
            Text = "text",
            NormalizedTextHash = "hash",
            Fingerprint = fingerprint,
            PublishedAt = DateTime.UtcNow,
            IngestedAt = DateTime.UtcNow,
            RawJson = "{}"
        });
        await _dbContext.SaveChangesAsync();
    }

    [Test]
    public async Task IsPostDuplicateAsync_NewFingerprint_ReturnsFalse()
    {
        var channel = await SeedChannelAsync();
        var result = await _service.IsPostDuplicateAsync("brand-new-fingerprint", channel.Id);
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task IsPostDuplicateAsync_ExistingFingerprint_ReturnsTrue()
    {
        var channel = await SeedChannelAsync();
        await SeedPostAsync(channel.Id, "fp-abc");

        var result = await _service.IsPostDuplicateAsync("fp-abc", channel.Id);
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task IsPostDuplicateAsync_FingerprintInDifferentChannel_ReturnsFalse()
    {
        var ch1 = await SeedChannelAsync(telegramId: 1);
        var ch2 = await SeedChannelAsync(telegramId: 2);
        await SeedPostAsync(ch1.Id, "shared-fp");

        var result = await _service.IsPostDuplicateAsync("shared-fp", ch2.Id);
        Assert.That(result, Is.False);
    }

    [Test]
    public void ComputeFingerprint_ImageOrderIndependent_ReturnsSameHash()
    {
        var fp1 = _service.ComputeFingerprint("texthash", ["sha1", "sha2"]);
        var fp2 = _service.ComputeFingerprint("texthash", ["sha2", "sha1"]);
        Assert.That(fp1, Is.EqualTo(fp2));
    }

    [Test]
    public void ComputeFingerprint_DifferentText_ReturnsDifferentHash()
    {
        var fp1 = _service.ComputeFingerprint("hash1", []);
        var fp2 = _service.ComputeFingerprint("hash2", []);
        Assert.That(fp1, Is.Not.EqualTo(fp2));
    }
}
