using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using TelegramAggregator.Background;
using TelegramAggregator.Config;
using TelegramAggregator.Services;
using TelegramAggregator.AI;
using TelegramAggregator.Common.Data;

var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (logging, health checks, telemetry, etc.)
builder.AddServiceDefaults();

// Add PostgreSQL database with EF Core via Aspire
// Registers AppDbContext with:
// - Connection string management from appsettings.json or environment
// - Database health checks
// - OpenTelemetry instrumentation and logging
// - Automatic retry and resilience policies
// - DbContext pooling
// Reads config from "Aspire:Npgsql:EntityFrameworkCore:PostgreSQL:AppDbContext" 
// or "Aspire:Npgsql:EntityFrameworkCore:PostgreSQL"
builder.AddNpgsqlDbContext<AppDbContext>("postgres");

// Configuration
builder.Services
    .Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));

// Register core services
builder.Services.AddSingleton<IImageService, ImageService>();
builder.Services.AddSingleton<ITelegramPublisher, TelegramPublisher>();
builder.Services.AddSingleton<ISemanticSummarizer, SemanticKernelSummarizer>();
builder.Services.AddSingleton<INormalizerService, NormalizerService>();
builder.Services.AddSingleton<IDeduplicationService, DeduplicationService>();

// Register background workers
builder.Services.AddHostedService<SummaryBackgroundService>();

var app = builder.Build();

await app.RunAsync();