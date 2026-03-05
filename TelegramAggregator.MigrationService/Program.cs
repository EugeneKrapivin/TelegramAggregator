using TelegramAggregator.Common.Data;
using TelegramAggregator.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<AppDbContext>("postgres");

builder.Services.AddHostedService<Worker>();

var app = builder.Build();

await app.RunAsync();
