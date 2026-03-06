using TelegramAggregator.Common.Data.Entities;
using TelegramAggregator.Common.Data.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Api.Config;
using TelegramAggregator.Api.Services;
using TelegramAggregator.Api.AI;
using TelegramAggregator.Api.Background;
using TelegramAggregator.Api.Endpoints;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add OpenAPI and Scalar for API documentation
builder.Services.AddOpenApi();

// Add database context (uses Aspire's Npgsql integration)
builder.AddNpgsqlDbContext<AppDbContext>("telegram-new-aggregator");

// Configuration
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));

// Register Telegram bot client
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
    new TelegramBotClient(sp.GetRequiredService<IOptions<TelegramOptions>>().Value.BotToken));

// Register core services (singletons - use IServiceScopeFactory for DbContext)
builder.Services.AddSingleton<IImageService, ImageService>();
builder.Services.AddSingleton<ITelegramPublisher, TelegramPublisher>();
builder.Services.AddSingleton<ISemanticSummarizer, SemanticKernelSummarizer>();
builder.Services.AddSingleton<INormalizerService, NormalizerService>();
builder.Services.AddSingleton<IDeduplicationService, DeduplicationService>();
builder.Services.AddSingleton<WTelegramClientAdapter>();

// Register Telegram authentication service
builder.Services.AddSingleton<TelegramAuthService>();

// Register background workers
builder.Services.AddHostedService<SummaryBackgroundService>();
builder.Services.AddHostedService<ImageCleanupBackgroundService>();
builder.Services.AddHostedService<IngestionBackgroundService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Channel Management Endpoints
var channelsGroup = app.MapGroup("/api/channels")
    .WithTags("Channels");

// GET /api/channels - Get all channels
channelsGroup.MapGet("/", GetChannels)
    .WithName("GetAllChannels")
    .WithSummary("Get all channels")
    .WithDescription("Retrieve a list of all channels");

// GET /api/channels/{id} - Get channel by ID
channelsGroup.MapGet("/{id}", GetChannelById)
    .WithName("GetChannelById")
    .WithSummary("Get channel by ID")
    .WithDescription("Retrieve a specific channel by its ID");

// POST /api/channels - Create new channel
channelsGroup.MapPost("/", CreateChannel)
    .WithName("CreateChannel")
    .WithSummary("Create new channel")
    .WithDescription("Add a new channel for monitoring");

// PUT /api/channels/{id} - Update channel
channelsGroup.MapPut("/{id}", UpdateChannel)
    .WithName("UpdateChannel")
    .WithSummary("Update channel")
    .WithDescription("Update channel details including active status");

// DELETE /api/channels/{id} - Delete channel
channelsGroup.MapDelete("/{id}", DeleteChannel)
    .WithName("DeleteChannel")
    .WithSummary("Delete channel")
    .WithDescription("Remove a channel from monitoring");

// GET /api/channels/{id}/posts/count
channelsGroup.MapGet("/{id}/posts/count", GetChannelPostCount)
    .WithName("GetChannelPostCount")
    .WithSummary("Get post count for channel")
    .WithDescription("Returns the total number of posts ingested for the channel.");

// Telegram Authentication Endpoints
app.MapTelegramAuthEndpoints();

// Posts & Images Endpoints
app.MapPostsEndpoints();
app.MapImagesEndpoints();

await app.RunAsync();

// Channel Endpoint Handlers
async Task<IResult> GetChannels(AppDbContext db)
{
    var channels = await db.Channels
        .Select(c => new ChannelDto(
            c.Id,
            c.TelegramChannelId,
            c.Username,
            c.Title,
            c.IsActive,
            c.AddedAt))
        .ToListAsync();
    return Results.Ok(channels);
}

async Task<IResult> GetChannelById(long id, AppDbContext db)
{
    var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == id);
    if (channel == null)
        return Results.NotFound();

    return Results.Ok(new ChannelDto(channel.Id,
        channel.TelegramChannelId,
        channel.Username,
        channel.Title,
        channel.IsActive,
        channel.AddedAt));
}

async Task<IResult> CreateChannel(CreateChannelRequest request, AppDbContext db)
{
    // Check if channel with same TelegramChannelId already exists
    var existingChannel = await db.Channels
        .FirstOrDefaultAsync(c => c.TelegramChannelId == request.TelegramChannelId);

    if (existingChannel != null)
        return Results.BadRequest("Channel with this Telegram ID already exists");

    var channel = new Channel
    {
        TelegramChannelId = request.TelegramChannelId,
        Username = request.Username,
        Title = request.Title,
        IsActive = true,
        AddedAt = DateTime.UtcNow
    };

    db.Channels.Add(channel);
    await db.SaveChangesAsync();

    return Results.Created($"/api/channels/{channel.Id}", 
        new ChannelDto(channel.Id, channel.TelegramChannelId, channel.Username, channel.Title, channel.IsActive, channel.AddedAt));
}

async Task<IResult> UpdateChannel(long id, UpdateChannelRequest request, AppDbContext db)
{
    var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == id);
    if (channel == null)
        return Results.NotFound();

    if (!string.IsNullOrEmpty(request.Username))
        channel.Username = request.Username;

    if (!string.IsNullOrEmpty(request.Title))
        channel.Title = request.Title;

    if (request.IsActive.HasValue)
        channel.IsActive = request.IsActive.Value;

    db.Channels.Update(channel);
    await db.SaveChangesAsync();

    return Results.Ok(new ChannelDto(channel.Id, channel.TelegramChannelId, channel.Username, channel.Title, channel.IsActive, channel.AddedAt));
}

async Task<IResult> DeleteChannel(long id, AppDbContext db)
{
    var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == id);
    if (channel == null)
        return Results.NotFound();

    db.Channels.Remove(channel);
    await db.SaveChangesAsync();

    return Results.NoContent();
}

async Task<IResult> GetChannelPostCount(long id, AppDbContext db)
{
    var exists = await db.Channels.AnyAsync(c => c.Id == id);
    if (!exists)
        return Results.NotFound();

    var count = await db.Posts.CountAsync(p => p.ChannelId == id);
    return Results.Ok(new { count });
}
