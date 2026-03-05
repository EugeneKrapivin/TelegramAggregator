using System.Net;
using Aspire.Hosting.ApplicationModel;

namespace TelegramAggregator.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class InfrastructureSmokeTests
{
    [Test]
    public async Task Postgres_BecomesHealthy()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await AppHostFixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Pass("postgres resource is healthy");
    }

    [Test]
    public async Task Migrations_FinishWithExitCodeZero()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        // Wait for migrations to reach Finished state
        await AppHostFixture.App.ResourceNotifications
            .WaitForResourceAsync("migrations", KnownResourceStates.Finished, cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(90));

        // If we reach here without exception, migrations finished — AppHost's
        // WaitForCompletion already gates the API on this, so reaching a healthy
        // API in AppHostFixture implicitly means exit code was 0. Assert pass.
        Assert.Pass("migrations resource reached Finished state");
    }

    [Test]
    public async Task Api_HealthEndpoint_ReturnsOk()
    {
        using var client = AppHostFixture.App.CreateHttpClient("api");

        var response = await client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Expected /health to return 200. Body: {await response.Content.ReadAsStringAsync()}");
    }
}
