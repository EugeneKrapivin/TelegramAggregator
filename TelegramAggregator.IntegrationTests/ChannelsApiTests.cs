using System.Net;
using System.Net.Http.Json;
using TelegramAggregator.Common.Data.Contracts;

namespace TelegramAggregator.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class ChannelsApiTests
{
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _client = AppHostFixture.App.CreateHttpClient("api");
    }

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    public async Task GetAll_ReturnsOkWithChannelList()
    {
        // Create a channel first so we have something to assert
        var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
        var create = new CreateChannelRequest(telegramId, $"@test_{telegramId}", $"Test {telegramId}");
        var post = await _client.PostAsJsonAsync("/api/channels", create);
        Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var response = await _client.GetAsync("/api/channels");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var channels = await response.Content.ReadFromJsonAsync<List<ChannelDto>>();
        Assert.That(channels, Is.Not.Null);
        Assert.That(channels!.Any(c => c.TelegramChannelId == telegramId), Is.True);
    }

    [Test]
    public async Task Create_ValidRequest_Returns201WithBody()
    {
        var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
        var request = new CreateChannelRequest(telegramId, $"@chan_{telegramId}", $"Channel {telegramId}");

        var response = await _client.PostAsJsonAsync("/api/channels", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var dto = await response.Content.ReadFromJsonAsync<ChannelDto>();
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.TelegramChannelId, Is.EqualTo(telegramId));
        Assert.That(dto.Username, Is.EqualTo(request.Username));
        Assert.That(dto.IsActive, Is.True);
    }

    [Test]
    public async Task Create_DuplicateTelegramId_Returns400()
    {
        var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
        var request = new CreateChannelRequest(telegramId, $"@dup_{telegramId}", $"Dup {telegramId}");

        // First create should succeed
        var first = await _client.PostAsJsonAsync("/api/channels", request);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Second create with same TelegramChannelId should fail
        var second = await _client.PostAsJsonAsync("/api/channels", request);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetById_ExistingId_Returns200WithBody()
    {
        var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
        var created = await CreateChannelAsync(telegramId);

        var response = await _client.GetAsync($"/api/channels/{created.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<ChannelDto>();
        Assert.That(dto!.Id, Is.EqualTo(created.Id));
        Assert.That(dto.TelegramChannelId, Is.EqualTo(telegramId));
    }

    [Test]
    public async Task GetById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/channels/999999999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Update_ExistingId_Returns200WithUpdatedData()
    {
        var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
        var created = await CreateChannelAsync(telegramId);

        var updateRequest = new UpdateChannelRequest(null, "Updated Title", false);
        var response = await _client.PutAsJsonAsync($"/api/channels/{created.Id}", updateRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<ChannelDto>();
        Assert.That(dto!.Title, Is.EqualTo("Updated Title"));
        Assert.That(dto.IsActive, Is.False);
    }

    [Test]
    public async Task Delete_ExistingId_Returns204()
    {
        var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
        var created = await CreateChannelAsync(telegramId);

        var response = await _client.DeleteAsync($"/api/channels/{created.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Delete_ThenGet_Returns404()
    {
        var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
        var created = await CreateChannelAsync(telegramId);

        await _client.DeleteAsync($"/api/channels/{created.Id}");
        var response = await _client.GetAsync($"/api/channels/{created.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private async Task<ChannelDto> CreateChannelAsync(long telegramId)
    {
        var request = new CreateChannelRequest(telegramId, $"@h_{telegramId}", $"Helper {telegramId}");
        var response = await _client.PostAsJsonAsync("/api/channels", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChannelDto>())!;
    }
}
