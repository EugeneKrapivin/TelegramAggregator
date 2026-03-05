using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Telegram.Bot;

using TelegramAggregator.Background;
using TelegramAggregator.Config;
using TelegramAggregator.Services;
using TelegramAggregator.AI;
using TelegramAggregator.Common.Data;

var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (logging, health checks, telemetry, etc.)
builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<AppDbContext>("postgres");

// Configuration
builder.Services
    .Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));

// Register Telegram bot client
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
    new TelegramBotClient(sp.GetRequiredService<IOptions<TelegramOptions>>().Value.BotToken));

// Register core services
builder.Services.AddSingleton<IImageService, ImageService>();
builder.Services.AddSingleton<ITelegramPublisher, TelegramPublisher>();
builder.Services.AddSingleton<ISemanticSummarizer, SemanticKernelSummarizer>();
builder.Services.AddSingleton<INormalizerService, NormalizerService>();
builder.Services.AddSingleton<IDeduplicationService, DeduplicationService>();
builder.Services.AddSingleton<WTelegramClientAdapter>();

// Register background workers
builder.Services.AddHostedService<SummaryBackgroundService>();
builder.Services.AddHostedService<ImageCleanupBackgroundService>();
builder.Services.AddHostedService<IngestionBackgroundService>();

var app = builder.Build();

await app.RunAsync();