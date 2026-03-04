# Core Pipeline Implementation Design

**Date:** 2026-03-04
**Status:** Approved

## Overview

Implements the end-to-end ingestion and summarization pipeline for TelegramAggregator. All components are pure .NET logic against the existing DB and SDK packages already referenced — no new dependencies required.

## Components

### 1. `TelegramOptions` (new config class)

Binds the `"Telegram"` config section (injected by Aspire as `Telegram__*` environment variables):

```
BotToken         → Telegram__BotToken
ApiId            → Telegram__ApiId
ApiHash          → Telegram__ApiHash
UserPhoneNumber  → Telegram__UserPhoneNumber
```

Registered in `Program.cs` via `Configure<TelegramOptions>(config.GetSection("Telegram"))`.

### 2. `DeduplicationService.IsPostDuplicateAsync`

Inject `AppDbContext`. Query:

```
Posts.AnyAsync(p => p.Fingerprint == fingerprint && p.ChannelId == channelId)
```

Cross-channel dedup is intentionally excluded — the same story appearing verbatim in two channels counts as two distinct ingestion events.

### 3. `ImageService` — pHash wiring

After a SHA256 miss in `FindOrCreateImageAsync`:

1. Compute pHash via `ComputePerceptualHashAsync`
2. Load all images with `PerceptualHash != null` from DB
3. For each, parse hex → `ulong`, compute Hamming distance
4. If distance ≤ `WorkerOptions.PHashHammingThreshold`, return the existing image (update `UsedAt`)
5. Otherwise create new image, storing pHash as hex string alongside SHA256

Inject `IOptions<WorkerOptions>` for the threshold. In-memory Hamming scan is acceptable at current scale; can be optimised later with a DB-side approach.

### 4. `ImageCleanupBackgroundService` (new)

```
PeriodicTimer(WorkerOptions.ImageCleanupInterval)
  → IServiceScopeFactory → AppDbContext
  → Images WHERE Content != null AND UsedAt < now - ImageRetentionHours
  → ClearContentBatchAsync
```

Uses `IServiceScopeFactory` per cycle to avoid captive-dependency issues with the scoped `AppDbContext`.

### 5. `SummaryBackgroundService.ExecuteSummaryAsync`

Inject `IServiceScopeFactory`. Per cycle:

1. Query `Posts WHERE IsSummarized = false`, include `Channel` + `PostImages.Image`
2. If none, return early
3. Map to `PostSummary` DTOs (ChannelName, Text, PublishedAt)
4. Collect `imageIds` and `sourceChannels`
5. `await _summarizer.SummarizeAsync(posts)` → `(headline, digest)`
6. `await _publisher.PublishSummaryAsync(headline, digest, imageIds, sourceChannels)` → `telegramMessageId`
7. Persist `Summary` entity (WindowStart = earliest post, WindowEnd = now)
8. Mark all included posts `IsSummarized = true`
9. `await _imageService.ClearContentBatchAsync(imageIds)` — content cleared *after* publish

### 6. `IngestionBackgroundService` (new)

Minimal hosted service: calls `ConnectAsync`, then awaits cancellation. Its sole responsibility is keeping the WTelegramClient connection alive.

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await _adapter.ConnectAsync(stoppingToken);
    await Task.Delay(Timeout.Infinite, stoppingToken);
}
```

### 7. `WTelegramClientAdapter`

Inject `IOptions<TelegramOptions>`. Replace `object telegramUpdate` parameter with `TL.MessageBase`.

`ConnectAsync`:
1. Initialise `WTelegram.Client` with config callback (ApiId, ApiHash, phone)
2. Subscribe `client.OnUpdate += HandleUpdateAsync`
3. Call `client.LoginUserIfNeeded()`

`HandleUpdateAsync` (private):
- Extract `TL.Message` from any `TL.Updates` / `TL.UpdateNewChannelMessage`
- Query DB for active channels matching the message's peer ID
- If not a monitored channel, discard
- Call `ReceiveAndProcessPostAsync`

`ReceiveAndProcessPostAsync(TL.Message msg)`:
1. `NormalizeTextAsync(msg.message)`
2. For each photo in `msg.media`: download via WTelegramClient, `FindOrCreateImageAsync`
3. `ComputeFingerprint(normalizedHash, imageChecksums)`
4. `IsPostDuplicateAsync(fingerprint, channelId)` → skip if true
5. Save `Post` + `PostImage` junction rows

### 8. `TelegramPublisher`

Inject `IOptions<TelegramOptions>`, `IOptions<WorkerOptions>`, `AppDbContext`. Initialise `TelegramBotClient(botToken)` lazily.

`PublishSummaryAsync`:
1. Format message: `*{headline}*\n\n{digest}\n\n_{sourceChannels}_`
2. Fetch images from DB by `imageIds`
3. Build `InputMedia` list:
   - `Content != null` → `InputFile.FromStream` (upload bytes, store returned `file_id` as `TelegramFileId`)
   - `Content == null && TelegramFileId != null` → `InputFile.FromFileId` (re-use cached ID)
   - Both null → skip
4. If media list non-empty: `SendMediaGroupAsync` to `SummaryChannelId`; else `SendMessageAsync`
5. Return message ID

## Data Flow

```
WTelegramClient (MTProto, long-lived)
  IngestionBackgroundService  →  keeps client alive
  WTelegramClientAdapter      →  normalize → dedup → save Post

PeriodicTimer (10 min)
  SummaryBackgroundService    →  query → summarize → publish → mark done → clear images

PeriodicTimer (1 hr)
  ImageCleanupBackgroundService → clear stale image content
```

## Testing

- `DeduplicationServiceTests` — `IsPostDuplicateAsync` returns false for new fingerprint, true for existing
- `ImageServiceTests` — pHash path: near-duplicate returns existing ID; distinct image creates new; pHash stored on new images
- `ImageCleanupBackgroundServiceTests` — only images older than retention window are cleared
- `SummaryBackgroundServiceTests` — posts marked summarized after cycle; empty run skips summarizer call; `Summary` entity persisted; image content cleared post-publish
- `TelegramPublisherTests` — uses file ID when content null; uploads bytes when content present; skips image when both null
- `WTelegramClientAdapterTests` — non-monitored channel discarded; duplicate post skipped; `Post` + `PostImage` saved for new message

Integration / manual: `IngestionBackgroundService` wiring verified via Aspire dashboard logs.

## Files Touched

| File | Action |
|---|---|
| `Config/TelegramOptions.cs` | Create |
| `Program.cs` | Register `TelegramOptions`, `ImageCleanupBackgroundService`, `IngestionBackgroundService`, `WTelegramClientAdapter` |
| `Services/DeduplicationService.cs` | Inject `AppDbContext`, implement `IsPostDuplicateAsync` |
| `Services/ImageService.cs` | Inject `IOptions<WorkerOptions>`, wire pHash path |
| `Background/ImageCleanupBackgroundService.cs` | Create |
| `Background/SummaryBackgroundService.cs` | Inject `IServiceScopeFactory`, implement `ExecuteSummaryAsync` |
| `Background/IngestionBackgroundService.cs` | Create |
| `Services/WTelegramClientAdapter.cs` | Full implementation |
| `Services/TelegramPublisher.cs` | Full implementation |
| `Tests/Services/DeduplicationServiceTests.cs` | Create |
| `Tests/Services/ImageServicePHashTests.cs` | Create (or extend existing) |
| `Tests/Background/ImageCleanupBackgroundServiceTests.cs` | Create |
| `Tests/Background/SummaryBackgroundServiceTests.cs` | Create |
| `Tests/Services/TelegramPublisherTests.cs` | Create |
