# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build all projects
dotnet build

# Build release
dotnet build --configuration Release

# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~NormalizerServiceTests"

# Run with Aspire (primary development entry point)
dotnet run --project TelegramAggregator.AppHost

# Or use Aspire CLI
aspire run

# Run API + background services standalone (for debugging)
dotnet run --project TelegramAggregator.Api

# Frontend development (standalone, without Aspire)
cd TelegramAggregator.Web
npm install
npm run dev

# EF Core migrations (Common.Data is migration project, Api is startup project)
dotnet ef migrations add <MigrationName> -p TelegramAggregator.Common.Data -s TelegramAggregator.Api
dotnet ef database update -p TelegramAggregator.Common.Data -s TelegramAggregator.Api
```

## Architecture

This is a .NET 10 / Dotnet Aspire multi-project solution using `.slnx` format — projects are linked via `TelegramAggregator.AppHost`.

### Projects

| Project | Type | Role |
|---|---|---|
| `TelegramAggregator.AppHost` | Aspire AppHost | Orchestrates all services, wires secrets, provisions Postgres + pgAdmin |
| `TelegramAggregator.Api` | Minimal API + Background Services | REST API (channels, posts, images, auth) + ingestion + summarization + cleanup workers |
| `TelegramAggregator.Web` | Vite + React | Frontend UI (channel management, posts viewer, Telegram login wizard) |
| `TelegramAggregator.Common.Data` | Class Library | Shared EF Core `AppDbContext`, entities, and DTOs |
| `TelegramAggregator.MigrationService` | Console App | Runs EF migrations on startup, gates other services |
| `TelegramAggregator.ServiceDefaults` | Class Library | Shared Aspire service defaults (logging, health checks, telemetry) |
| `TelegramAggregator.Tests` | NUnit | Unit tests using NSubstitute and EF Core InMemory |

### Data Flow

**Ingestion** (manual trigger via Telegram client):
`WTelegramClientAdapter` → `NormalizerService` → `ImageService` → `DeduplicationService` → `AppDbContext` (Postgres)

**Summarization** (every 1 minute via `PeriodicTimer`):
`SummaryBackgroundService` → query unsummarized posts → `SemanticKernelSummarizer` (Azure OpenAI via Semantic Kernel) → `TelegramPublisher` (Bot API) → update DB state

**Maintenance**: 
- `ImageCleanupBackgroundService` nullifies `Content` on images older than retention period (default 7 days), keeping `TelegramFileId` for reuse.
- `IngestionBackgroundService` monitors Telegram channels for new messages (if logged in).

**Frontend**:
- Channels page: CRUD operations on channels
- Posts viewer: Click channel → sliding panel with infinite scroll → lazy-load images via `/api/images/{id}`
- Telegram login: Interactive wizard for WTelegram authentication (phone → code → 2FA)

### Key Design Decisions

- **Single process architecture**: Worker services merged into API project for simpler deployment and service-to-service communication.
- **Two Telegram clients**: WTelegramClient (user-client) for ingestion; Telegram.Bot (bot API) for publishing to summary channel.
- **Image deduplication is two-stage**: exact SHA256 match first, then perceptual hash (pHash) Hamming distance if no exact match. Both hit paths avoid storing duplicate images.
- **Images stored as bytes in Postgres** temporarily; `Content` is nulled after `ImageRetentionHours` (default 7 days) while `TelegramFileId` is preserved for Telegram re-upload avoidance.
- **`AppDbContext` lives in `Common.Data`** — the Aspire connection name is `"telegram-new-aggregator"`.
- **All services are registered as `Singleton`** — be mindful of DbContext lifetime when adding scoped services (use `IServiceScopeFactory` in background services).
- **React StrictMode removed** — Caused duplicate API calls during development (double-effect in dev mode).
- **Infinite scroll pattern**: Uses Intersection Observer with `loadingRef` (useRef) to prevent concurrent API calls.
- **Session file persistence**: WTelegram session at `data/wtelegram.session`, encrypted with api_hash, survives restarts.
- **Namespaces**: 
  - Backend: `TelegramAggregator.Api.Services`, `TelegramAggregator.Api.Background`, `TelegramAggregator.Api.AI`, `TelegramAggregator.Api.Config`, `TelegramAggregator.Api.Endpoints`
  - Frontend: `TelegramAggregator.Web` (React components, hooks, API clients)

### Configuration

Secrets flow through Aspire parameters (defined in `AppHost.cs`) as environment variables using double-underscore notation:

```
Telegram__BotToken
Telegram__ApiId
Telegram__ApiHash
Telegram__UserPhoneNumber
SemanticKernel__AzureOpenAI__Endpoint
SemanticKernel__AzureOpenAI__ApiKey
Worker__SummaryChannelId
```

Worker behavior is controlled via `Worker` config section, bound to `WorkerOptions`:
- `SummaryInterval` (default 1 min)
- `ImageCleanupInterval` (default 1 hr)
- `ImageRetentionHours` (default 7 days)
- `PHashHammingThreshold` (default 8 — lower = stricter)
- `SummaryChannelId` (required — Telegram channel ID for posting summaries, must be negative with `-100` prefix)

See `docs/telegram-bot-configuration.md` for bot setup guide.

### Database

EF Core with Npgsql. Tables: `channels`, `posts`, `images`, `post_images` (junction), `summaries`.

`posts.RawJson` and `summaries.IncludedPostIds` use `jsonb` column type. `images.Content` uses `bytea` (binary).

Migrations live in `TelegramAggregator.Common.Data/Migrations`.

### Testing

Tests live in `TelegramAggregator.Tests`. Uses NUnit + NSubstitute + EF Core InMemory provider. Tests cover `NormalizerService`, `ImageService` (SHA256 + pHash paths), `DeduplicationService`, and background services.

### Frontend Architecture

- **React 18 + TypeScript** - Type-safe component development
- **Vite** - Fast dev server with HMR
- **Tailwind CSS** - Utility-first styling
- **React Router** - Client-side routing (`/` for channels, `/settings/telegram-login` for auth)
- **Custom Hooks**:
  - `useInfiniteScroll` - Intersection Observer for bottom detection
  - `useClickOutside` - Click-outside-to-close behavior with 100ms delay
- **API Client** - `src/api.ts` and `src/api/posts.ts` for backend communication
- **Key Components**:
  - `ChannelsPage` - Main dashboard with channel table
  - `ChannelTable` - CRUD operations, post counts, row click handler
  - `ChannelPostsPanel` - Sliding panel with infinite scroll
  - `PostCard` - Individual post display with lazy-loaded images
  - `TelegramLoginPage` - Step wizard for authentication
  - `ChannelModal` - Create/edit channel form

**Important Pattern**: `loadingRef` (useRef) prevents race conditions in infinite scroll by providing synchronous guard before async state updates.
