using TelegramAggregator.Data;
using TelegramAggregator.Common.Data.Entities;
using TelegramAggregator.Common.Data.Contracts;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add OpenAPI and Scalar for API documentation
builder.Services.AddOpenApi();

// Add database context (uses Aspire's Npgsql integration)
builder.AddNpgsqlDbContext<AppDbContext>("postgres");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Channel Management Endpoints
var channelsGroup = app.MapGroup("/api/channels")
    .WithTags("Channels")
    .WithOpenApi();

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

await app.RunAsync();

// Channel Endpoint Handlers
async Task<IResult> GetChannels(AppDbContext db)
{
    var channels = await db.Channels
        .Select(c => new ChannelDto(c.Id, c.TelegramChannelId, c.Username, c.Title, c.IsActive, c.AddedAt))
        .ToListAsync();
    return Results.Ok(channels);
}

async Task<IResult> GetChannelById(long id, AppDbContext db)
{
    var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == id);
    if (channel == null)
        return Results.NotFound();

    return Results.Ok(new ChannelDto(channel.Id, channel.TelegramChannelId, channel.Username, channel.Title, channel.IsActive, channel.AddedAt));
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
