using Aspire.Hosting.Testing;

namespace TelegramAggregator.IntegrationTests;

[SetUpFixture]
public class AppHostFixture
{
    public static DistributedApplication App { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TelegramAggregator_AppHost>();

        // Provide stub values for required secret parameters so the worker
        // starts without crashing (its Telegram logic is stubbed anyway)
        appHost.Configuration["Parameters:telegram-bot-token"] = "dummy";
        appHost.Configuration["Parameters:telegram-api-id"] = "12345";
        appHost.Configuration["Parameters:telegram-api-hash"] = "dummy-hash";
        appHost.Configuration["Parameters:telegram-user-phone-number"] = "+1234567890";
        appHost.Configuration["Parameters:azure-openai-endpoint"] = "https://dummy.openai.azure.com/";
        appHost.Configuration["Parameters:azure-openai-api-key"] = "dummy-key";

        App = await appHost.BuildAsync();
        await App.StartAsync();

        // Wait for the API to be healthy (migrations will have run first
        // because of WaitForCompletion(migrations) wired in AppHost)
        await App.ResourceNotifications
            .WaitForResourceHealthyAsync("api")
            .WaitAsync(TimeSpan.FromSeconds(120));
    }

    [OneTimeTearDown]
    public async Task StopAsync() => await App.DisposeAsync();
}
