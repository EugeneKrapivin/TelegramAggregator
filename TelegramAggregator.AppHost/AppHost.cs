var builder = DistributedApplication.CreateBuilder(args);

// Add external parameters for secrets
var telegramBotToken = builder
    .AddParameter("telegram-bot-token", secret: true)
    .WithDescription("Telegram bot token obtained from @BotFather");
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
var openAiApiKey = builder
    .AddParameter("openai-api-key", secret: true)
    .WithDescription("API key for OpenAI service (fallback provider)");

// Add API project
builder.AddProject<Projects.TelegramAggregator_Api>("api");

builder.AddProject<Projects.TelegramAggregator>("telegramaggregator")
    .WithEnvironment("Telegram__BotToken", telegramBotToken)
    .WithEnvironment("Telegram__ApiHash", telegramApiHash)
    .WithEnvironment("Telegram__UserPhoneNumber", telegramUserPhoneNumber)
    .WithEnvironment("SemanticKernel__AzureOpenAI__Endpoint", azureOpenAiEndpoint)
    .WithEnvironment("SemanticKernel__AzureOpenAI__ApiKey", azureOpenAiApiKey)
    .WithEnvironment("SemanticKernel__OpenAI__ApiKey", openAiApiKey);

builder.Build().Run();
