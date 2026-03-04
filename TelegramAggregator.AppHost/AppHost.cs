using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

// Add external parameters for secrets
var telegramBotToken = builder
    .AddParameter("telegram-bot-token", secret: true)
    .WithDescription("Telegram bot token obtained from @BotFather");
var telegramApiId = builder
    .AddParameter("telegram-api-id", secret: true)
    .WithDescription("Telegram API ID from https://my.telegram.org/apps");
var telegramApiHash = builder
    .AddParameter("telegram-api-hash", secret: true)
    .WithDescription("Telegram API hash for user client authentication");
var telegramUserPhoneNumber = builder
    .AddParameter("telegram-user-phone-number", secret: true)
    .WithDescription("Phone number for Telegram user client login");
var azureOpenAiEndpoint = builder
    .AddParameter("azure-openai-endpoint", secret: true)
    .WithDescription("Azure OpenAI service endpoint URL");
var azureOpenAiApiKey = builder
    .AddParameter("azure-openai-api-key", secret: true)
    .WithDescription("API key for Azure OpenAI service");

// Add API project
var api = builder.AddProject<Projects.TelegramAggregator_Api>("api");

builder.AddProject<Projects.TelegramAggregator>("telegramaggregator")
    .WithEnvironment("Telegram__BotToken", telegramBotToken)
    .WithEnvironment("Telegram__ApiId", telegramApiId)
    .WithEnvironment("Telegram__ApiHash", telegramApiHash)
    .WithEnvironment("Telegram__UserPhoneNumber", telegramUserPhoneNumber)
    .WithEnvironment("SemanticKernel__AzureOpenAI__Endpoint", azureOpenAiEndpoint)
    .WithEnvironment("SemanticKernel__AzureOpenAI__ApiKey", azureOpenAiApiKey);

var scalar = builder.AddScalarApiReference();
scalar.WithApiReference(api, options =>
{
    options
       .AddDocument("v1", "Telegram Aggregator API")
       .WithOpenApiRoutePattern("/api-documentation/{documentName}.json")
       .WithTheme(ScalarTheme.Mars);
});

builder.Build()
    .Run();
