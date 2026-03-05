# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Build release
dotnet build --configuration Release

# Run all tests
dotnet test

# Run a single test file
dotnet test --filter "FullyQualifiedName~NormalizerServiceTests"

# Run with Aspire (primary development entry point)
dotnet run --project TelegramAggregator.AppHost

# Run API + background services standalone
dotnet run --project TelegramAggregator.Api

# EF Core migrations
dotnet ef migrations add <MigrationName> -p TelegramAggregator.Common.Data -s TelegramAggregator.Api
dotnet ef database update -p TelegramAggregator.Common.Data -s TelegramAggregator.Api
```

## Architecture

This is a .NET 10 / Dotnet Aspire multi-project solution using `.slnx` format — projects are linked via `TelegramAggregator.AppHost`.

### Projects

| Project | Type | Role |
|---|---|---|
| `TelegramAggregator.AppHost` | Aspire AppHost | Orchestrates all services, wires secrets, provisions Postgres |
| `TelegramAggregator.Api` | Minimal API + Background Services | REST API for channel CRUD + ingestion pipeline + summarization workers |
| `TelegramAggregator.Common.Data` | Class Library | Shared EF Core `AppDbContext`, entities, and DTOs |
| `TelegramAggregator.ServiceDefaults` | Class Library | Shared Aspire service defaults (logging, health checks, telemetry) |
| `TelegramAggregator.Tests` | NUnit | Unit tests using NSubstitute and EF Core InMemory |

### Data Flow

**Ingestion** (not yet fully implemented):
`WTelegramClientAdapter` → `NormalizerService` → `ImageService` → `DeduplicationService` → `AppDbContext` (Postgres)

**Summarization** (every 10 minutes via `PeriodicTimer`):
`SummaryBackgroundService` → query unsummarized posts → `SemanticKernelSummarizer` (Azure OpenAI via Semantic Kernel) → `TelegramPublisher` (Bot API) → update DB state

**Maintenance**: `ImageCleanupBackgroundService` nullifies `Content` on images older than retention period, keeping `TelegramFileId` for reuse.

### Key Design Decisions

- **Single process architecture**: API and background services run in the same process for simplicity.
- **Two Telegram clients**: WTelegramClient (user-client) for ingestion; Telegram.Bot (bot API) for publishing to summary channel.
- **Image deduplication is two-stage**: exact SHA256 match first, then perceptual hash (pHash) Hamming distance if no exact match. Both hit paths avoid storing duplicate images.
- **Images stored as bytes in Postgres** temporarily; `Content` is nulled after `ImageRetentionHours` (default 7 days) while `TelegramFileId` is preserved for Telegram re-upload avoidance.
- **`AppDbContext` lives in `Common.Data`** — the Aspire connection name is `"telegram-new-aggregator"`.
- **All services are registered as `Singleton`** — be mindful of DbContext lifetime when adding scoped services (use `IServiceScopeFactory` in background services).
- **Namespaces**: `TelegramAggregator.Api.Services`, `TelegramAggregator.Api.Background`, `TelegramAggregator.Api.AI`, `TelegramAggregator.Api.Config`

### Configuration

Secrets flow through Aspire parameters (defined in `AppHost.cs`) as environment variables using double-underscore notation:

```
Telegram__BotToken
Telegram__ApiId
Telegram__ApiHash
Telegram__UserPhoneNumber
SemanticKernel__AzureOpenAI__Endpoint
SemanticKernel__AzureOpenAI__ApiKey
```

Worker behavior is controlled via `Worker` config section, bound to `WorkerOptions`:
- `SummaryInterval` (default 10 min)
- `ImageCleanupInterval` (default 1 hr)
- `ImageRetentionHours` (default 7 days)
- `PHashHammingThreshold` (default 8 — lower = stricter)
- `SummaryChannelId` (required — Telegram channel ID for posting summaries)

### Database

EF Core with Npgsql. Tables: `channels`, `posts`, `images`, `post_images` (junction), `summaries`.

`posts.RawJson` and `summaries.IncludedPostIds` use `jsonb` column type. `images.Content` uses `bytea`.

### Testing

Tests live in `TelegramAggregator.Tests`. Uses NUnit + NSubstitute + EF Core InMemory provider. Tests cover `NormalizerService`, `ImageService` (SHA256 + pHash paths), `DeduplicationService`, and background services.
