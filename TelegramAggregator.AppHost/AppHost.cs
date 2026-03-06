using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

// Secrets
var telegramBotToken = builder.AddParameter("telegram-bot-token", secret: true)
    .WithDescription("Telegram bot token obtained from @BotFather");
var telegramApiId = builder.AddParameter("telegram-api-id", secret: true)
    .WithDescription("Telegram API ID from https://my.telegram.org/apps");
var telegramApiHash = builder.AddParameter("telegram-api-hash", secret: true)
    .WithDescription("Telegram API hash for user client authentication");
var telegramUserPhoneNumber = builder.AddParameter("telegram-user-phone-number", secret: true)
    .WithDescription("Phone number for Telegram user client login");
var azureOpenAiEndpoint = builder.AddParameter("azure-openai-endpoint", secret: true)
    .WithDescription("Azure OpenAI service endpoint URL");
var azureOpenAiApiKey = builder.AddParameter("azure-openai-api-key", secret: true)
    .WithDescription("API key for Azure OpenAI service");
var summaryChannelId = builder.AddParameter("worker-summary-channel-id")
    .WithDescription("Telegram channel ID for posting summaries (must be negative, format: -100XXXXXXXXXX)");

// PostgreSQL — Docker container in dev; supply connection string externally for any other target
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();
var database = postgres.AddDatabase("telegram-new-aggregator");

// Migration service — runs MigrateAsync() then exits; gates everything else
var migrations = builder.AddProject<Projects.TelegramAggregator_MigrationService>("migrations")
    .WithReference(postgres)
    .WithReference(database)
    .WaitFor(postgres);

// API (with background workers)
var api = builder.AddProject<Projects.TelegramAggregator_Api>("api")
    .WithReference(postgres)
    .WithReference(database)
    .WaitFor(postgres)
    .WaitForCompletion(migrations)
    .WithEnvironment("Telegram__BotToken", telegramBotToken)
    .WithEnvironment("Telegram__ApiId", telegramApiId)
    .WithEnvironment("Telegram__ApiHash", telegramApiHash)
    .WithEnvironment("Telegram__UserPhoneNumber", telegramUserPhoneNumber)
    .WithEnvironment("SemanticKernel__AzureOpenAI__Endpoint", azureOpenAiEndpoint)
    .WithEnvironment("SemanticKernel__AzureOpenAI__ApiKey", azureOpenAiApiKey)
    .WithEnvironment("Worker__SummaryChannelId", summaryChannelId);

// ── UI (Vite dev server) ──────────────────────────────────────────────────────
builder.AddViteApp("ui", "../TelegramAggregator.Web")
    .WithNpm(installCommand: "ci")
    .WithReference(api)
    .WithExternalHttpEndpoints();

// Scalar API docs
var scalar = builder.AddScalarApiReference();
scalar.WithApiReference(api, options =>
{
    options
       .AddDocument("v1", "Telegram Aggregator API")
       .WithOpenApiRoutePattern("/openapi/{documentName}.json")
       .WithTheme(ScalarTheme.Mars);
});

builder.Build().Run();
