# Quick Start Guide

## Project Overview
Telegram News Aggregator: Reads posts from multiple Telegram channels, deduplicates, summarizes with AI (Semantic Kernel), and posts summaries to a dedicated channel.

**Tech Stack**: .NET 10 + Dotnet Aspire + Postgres + Semantic Kernel + Telegram Bot APIs

## Development Setup

### Prerequisites
- .NET 10 SDK
- Postgres (14+) or Docker
- Telegram account + API credentials (https://my.telegram.org)
- Optional: Azure OpenAI or OpenAI API key (for LLM)

### 1. Clone & Install Dependencies
```bash
cd TelegramAggregator
dotnet restore
```

### 2. Set Up Database
Option A: Docker
```bash
docker run -d --name postgres \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 \
  postgres:15
```

Option B: Local Postgres
```bash
# Create database
createdb telegram_aggregator
```

### 3. Create Database Schema
```bash
dotnet ef migrations add InitialSchema
dotnet ef database update
```

### 4. Configure Secrets
Create environment variables (or update `appsettings.json`):
```bash
export POSTGRES_CONNECTION="Host=localhost;Database=telegram_aggregator;Username=postgres;Password=postgres"
export TELEGRAM__BOTTOKEN="your_bot_token_here"
export TELEGRAM__APIID="your_api_id"
export TELEGRAM__APIHASH="your_api_hash"
export SEMANTICKERNEL__PROVIDER="AzureOpenAI"  # or "OpenAI"
export SEMANTICKERNEL__AZUREOPENAI__APIKEY="key"
export SEMANTICKERNEL__AZUREOPENAI__ENDPOINT="https://..."
```

### 5. Build & Run
```bash
dotnet build                # Verify compilation
dotnet run                  # Start service (will attempt connection to Telegram + LLM)
```

## Project Structure

```
TelegramAggregator/
├── Program.cs                         # Entry point + DI setup
├── appsettings.json                   # Configuration template
├── PLAN.md                            # High-level design
├── ARCHITECTURE.md                    # Detailed architecture
├── TASKS.md                           # Implementation roadmap + status
├── PHASE0_SUMMARY.md                  # Phase 0 completion summary
│
├── Config/
│   └── WorkerOptions.cs               # Configuration options class
│
├── Data/
│   ├── AppDbContext.cs                # EF Core DbContext
│   └── Entities/
│       ├── Channel.cs
│       ├── Post.cs                    # Includes PostImage junction
│       ├── Image.cs
│       └── Summary.cs
│
├── Services/
│   ├── INormalizerService.cs          # Text normalization interface
│   ├── NormalizerService.cs           # Implementation (ready)
│   ├── IImageService.cs               # Image dedup interface
│   ├── ImageService.cs                # Stub (TODO: pHash logic)
│   ├── IDeduplicationService.cs       # Post dedup interface
│   ├── DeduplicationService.cs        # Stub (TODO: DB queries)
│   ├── ITelegramPublisher.cs          # Publishing interface
│   ├── TelegramPublisher.cs           # Stub (TODO: Telegram.Bot)
│   └── WTelegramClientAdapter.cs      # User-client skeleton
│
├── AI/
│   ├── ISemanticSummarizer.cs         # Summarization interface
│   └── SemanticKernelSummarizer.cs    # Skeleton (TODO: LLM integration)
│
└── Background/
    └── SummaryBackgroundService.cs    # 10-minute timer orchestrator
```

## Key Classes & Interfaces

### Configuration
- **`WorkerOptions`**: Bind from config section `Worker`
  - `SummaryIntervalMinutes` (default 10)
  - `ImageRetentionHours` (default 168)
  - `PHashHammingThreshold` (default 8)

### Data Access
- **`AppDbContext`**: EF Core, Postgres via Npgsql
  - DbSets: `Channels`, `Posts`, `Images`, `Summaries`, `PostImages`

### Services
- **`INormalizerService`**: Extract & hash text (✅ Ready to test)
- **`IImageService`**: Download & dedup images (⏳ Skeleton)
- **`IDeduplicationService`**: Compute fingerprints (⏳ Skeleton)
- **`ITelegramPublisher`**: Post to summary channel (⏳ Skeleton)
- **`ISemanticSummarizer`**: Generate summaries (⏳ Skeleton)

### Background Workers
- **`SummaryBackgroundService`**: Runs every 10 minutes
  - Queries unsummarized posts
  - Calls summarizer & publisher
  - Updates DB state
  - (TODO: Full implementation)

## Common Tasks

### Run Tests (Phase 1+)
```bash
dotnet test
```

### Create a Migration
```bash
dotnet ef migrations add <MigrationName>
```

### View Database
```bash
psql telegram_aggregator -U postgres
\dt                          # List tables
SELECT * FROM posts LIMIT 5; # Query posts
```

### Enable Detailed Logging
Update `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "TelegramAggregator": "Debug"
    }
  }
}
```

### Check Metrics (Phase 3+)
Prometheus exporter (once implemented):
```bash
curl http://localhost:9090/metrics
```

## Development Workflow

### Phase 1: Implement Service Tests
1. Task 1.1: NormalizerService tests
2. Task 1.2-1.3: ImageService (dedup logic)
3. Task 1.4-1.5: DeduplicationService & adapter

**File**: `TASKS.md` for detailed acceptance criteria

### Phase 2: Summarization & Publishing
1. Task 2.1-2.3: SemanticKernelSummarizer
2. Task 2.4-2.5: TelegramPublisher + SummaryBackgroundService

### Phase 3-5: Testing, Observability, Optional Features

## Debugging Tips

### Enable SQL Query Logging
```csharp
// In Program.cs or appsettings.json
optionsBuilder.LogTo(Console.WriteLine).EnableSensitiveDataLogging();
```

### Check DI Container
- All services registered in `Program.cs`
- If missing, add to `builder.Services.AddXxx()`

### Database Connection Issues
- Ensure Postgres is running: `psql -l`
- Check connection string in `appsettings.json` or env var
- Run migrations: `dotnet ef database update`

### Telegram API Issues
- Verify API credentials from https://my.telegram.org
- Check rate limits (Telegram throttles requests)
- Use `ILogger<T>` to debug messages

### Semantic Kernel Issues
- Verify LLM provider credentials in config
- Check token limits (default 500, configurable)
- Handle gracefully if LLM offline (fallback to extractive)

## Resources

- **Documentation**: See `ARCHITECTURE.md`, `PLAN.md`
- **Roadmap**: See `TASKS.md` with completion status
- **Config Template**: `appsettings.json`
- **EF Core**: https://learn.microsoft.com/en-us/ef/core/
- **Semantic Kernel**: https://github.com/microsoft/semantic-kernel
- **Telegram Bot API**: https://core.telegram.org/bots/api

## Contact & Support

For issues or questions:
1. Check `ARCHITECTURE.md` for design decisions
2. Review `TASKS.md` for current progress
3. Check logs with `ILogger<T>`
4. Refer to referenced library docs

---

**Status**: Phase 0 ✅ Complete | Phase 1 ⏳ In Progress
