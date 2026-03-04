# Telegram News Aggregator - Implementation Plan

## Overall Objective
Build a Dotnet Aspire-hosted service that:
- Reads posts from multiple Telegram channels using a user-client
- Deduplicates text and images (exact checksums + perceptual hashing)
- Stores posts and images in Postgres (images as base64)
- Generates AI-powered summaries every 10 minutes using Semantic Kernel
- Posts summaries to a dedicated summary channel using `Telegram.Bot`
- Runs a cleanup job to remove base64 image blobs after they are used

## High-Level Architecture

### Components
- **Aspire Bootstrap**: Host, configuration, secrets, logging, OpenTelemetry, service registration
- **Ingest Adapter**: `WTelegramClientAdapter` — subscribes to channel posts as a user and persists `Post` + image metadata
- **Data Access**: EF Core `AppDbContext` using `Npgsql.EntityFrameworkCore.PostgreSQL`
- **Image Service**: `ImageService` — downloads images, computes SHA256, computes pHash (perceptual hash), dedupes, stores base64, metadata, and `TelegramFileId`
- **Deduplicator/Normalizer**: `NormalizerService` and `DeduplicationService` for text + image fingerprinting
- **Summarizer**: `SemanticKernelSummarizer` — extractive pre-pass then Semantic Kernel abstractive refinement
- **Publisher**: `TelegramPublisher` — posts to summary channel using `Telegram.Bot`, uploads images when necessary and saves returned `file_id`
- **Background Workers**:
  - `SummaryBackgroundService` — PeriodicTimer, 10-minute window, orchestrates summarization + publish
  - `ImageCleanupBackgroundService` — PeriodicTimer (hourly/daily), cleanup that nulls/removes `ContentBase64` for used images older than retention
- **Observability**: `Microsoft.Extensions.Logging` + OpenTelemetry metrics/prometheus exporter

## Data Model

### Core Tables/Entities

#### Channels
- `Id` (PK, long)
- `TelegramChannelId` (long, unique)
- `Username` (string)
- `Title` (string)
- `AddedAt` (datetime)

#### Posts
- `Id` (PK, long)
- `TelegramMessageId` (long)
- `ChannelId` (FK)
- `Text` (text)
- `NormalizedTextHash` (string, hash of normalized text)
- `Fingerprint` (string, combined hash for dedup)
- `PublishedAt` (datetime)
- `IngestedAt` (datetime)
- `IsSummarized` (bool)
- `RawJson` (jsonb, for preserving original Telegram metadata)

#### Images
- `Id` (PK, guid)
- `ChecksumSha256` (string, indexed)
- `PerceptualHash` (string/bigint, indexed)
- `MimeType` (string)
- `Width` (int)
- `Height` (int)
- `SizeBytes` (long)
- `ContentBase64` (text, nullable)
- `TelegramFileId` (string, nullable)
- `AddedAt` (datetime)
- `UsedAt` (datetime, nullable)

#### Summaries
- `Id` (PK, guid)
- `WindowStart` (datetime)
- `WindowEnd` (datetime)
- `SummaryText` (text)
- `Headline` (string)
- `PublishedAt` (datetime)
- `IncludedPostIds` (jsonb, array of included post IDs)

## Image Deduplication Algorithm

### Two-Layer Approach

1. **Exact-Match Layer (Fast)**
   - Compute SHA256 of downloaded image bytes
   - Query DB by `ChecksumSha256`
   - If found → reuse that image record (no storing new base64)

2. **Perceptual-Match Layer (Near-Duplicates)**
   - Compute 64-bit perceptual hash (pHash) using ImageSharp + `CoenM.ImageHash`
   - Normalize image (32x32 or 64x64 for hashing)
   - Query candidates (recent images or size-similar)
   - Compute Hamming distance between hashes
   - If distance ≤ threshold (start with 8) → treat as duplicate, reuse existing image record
   - Otherwise → insert new image record with `ContentBase64`

### Thresholds
- Hamming threshold: 8 (tunable, adjust based on false positives)
- Candidate filtering: time window (recent days) or size similarity to reduce scan time

## Ingest Flow (Sequence)

1. `WTelegramClientAdapter` receives a channel post
2. `NormalizerService` extracts text, removes markup, normalizes links → produces `NormalizedTextHash`
3. For each media item:
   - Download bytes
   - `ImageService` computes SHA256 and pHash
   - Check DB for exact match (SHA256) or perceptual match (pHash + Hamming distance)
   - If new image: insert `Images` with `ContentBase64`
   - Return `ImageId` (existing or new)
4. Create `Post` with references to found/created `ImageId`s and computed `Fingerprint`
5. Check for text/image fingerprint duplicates; drop or link depending on policy
6. Commit to DB

## Summarization/Publish Flow (Every 10 Minutes)

1. `SummaryBackgroundService` wakes (PeriodicTimer)
2. Optionally acquire leader lock (for multi-instance scenarios)
3. Query `Posts` with `IngestedAt` within last 10-minute window and `IsSummarized = false`
4. Filter low-value items (too short, spam heuristics, etc.)
5. Aggregate texts and include metadata (source channel, timestamp)
6. Extractive pass: pick representative sentences (TF-IDF or simple heuristics) and representative images
7. Invoke `SemanticKernelSummarizer` to produce concise digest + headline
8. `TelegramPublisher` posts to summary channel:
   - Headline + digest + inline source references
   - Attach images (reuse `TelegramFileId` if present; otherwise upload base64 and store returned `file_id`)
9. Mark included `Posts.IsSummarized = true`
10. Set `Images.UsedAt` for used images
11. Save `Summaries` record
12. Emit metrics and logs

## Image Cleanup Flow

1. `ImageCleanupBackgroundService` runs periodically (configurable, e.g., hourly/daily)
2. Find `Images` with:
   - `UsedAt != null`
   - `UsedAt < (now - ImageRetentionHours)`
   - `ContentBase64 != NULL`
3. Delete `ContentBase64` (set to null) but keep `TelegramFileId` and metadata for future reuse
4. Optionally delete entire row if `TelegramFileId` is null and older than aggressive retention threshold
5. Emit cleanup metrics (count deleted, bytes freed, etc.)

## Configuration & Secrets

### Environment Variables / Aspire Config
- `POSTGRES_CONNECTION` — connection string
- `TG_API_ID`, `TG_API_HASH`, `TG_USER_SESSION` — user-client credentials/session
- `TG_BOT_TOKEN` — publishing bot token
- `SUMMARY_CHANNEL_ID` — target channel id (long)
- `SUMMARY_INTERVAL_MINUTES` — default 10
- `IMAGE_RETENTION_HOURS` — default 168 (7 days)
- `PHASH_HAMMING_THRESHOLD` — default 8
- Semantic Kernel provider config (Azure/OpenAI keys & endpoints)
- Logging/OTel exporter config

### appsettings.json Template
```json
{
  "Worker": {
    "SummaryIntervalMinutes": 10,
    "SummaryChannelId": 0,
    "ImageRetentionHours": 168
  },
  "ConnectionStrings": {
    "postgres": "Host=localhost;Database=telegram_aggregator;..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## Libraries to Use

- **Aspire**: Dotnet Aspire for hosting/config
- **Telegram**: `WTelegramClient` (user-client ingestion), `Telegram.Bot` (publishing)
- **Data**: `Microsoft.EntityFrameworkCore` + `Npgsql.EntityFrameworkCore.PostgreSQL`
- **Images**: `SixLabors.ImageSharp` + `CoenM.ImageHash` (or custom pHash)
- **AI**: `Microsoft.SemanticKernel`
- **Logging**: `Microsoft.Extensions.Logging` + OpenTelemetry + Prometheus exporter
- **Optional**: `pgvector` + `dotnet-pgvector` for advanced embeddings (future)

## Security & Compliance

- Keep secrets in environment variables or Aspire secret providers (no hardcoding)
- Sanitize content before sending to Semantic Kernel (PII removal policy)
- Respect channel copyrights; include attribution in summaries
- Monitor user-client account health (avoid abuse/TOS violations)
- Enforce DB retention policies to avoid unbounded storage growth

## Resilience & Scaling Notes

- Use retries + exponential backoff for network calls (Telegram API, LLM)
- Use Postgres advisory lock for single active summarizer in multi-instance runs
- Monitor DB growth carefully due to base64 image storage; cleanup must be enforced
- For large scale, move image storage to S3 and keep only references in DB
- Keep `TelegramFileId` to avoid re-uploading previously posted images

## Implementation Milestones

### Phase 0: Scaffold
1. Update `Program.cs` to register Aspire modules and DI services
2. Create EF Core `AppDbContext` + entities (`Channel`, `Post`, `Image`, `Summary`)
3. Create initial migrations and test local Postgres connection
4. Create `appsettings.json` template with placeholder secrets

### Phase 1: Ingest & Storage
5. Implement `WTelegramClientAdapter` (skeleton: connect to user account, receive posts)
6. Implement `ImageService` with SHA256 + pHash + DB dedup logic
7. Implement `NormalizerService` (text extraction, normalization, hashing)
8. Implement `DeduplicationService` (fingerprint-based post dedup)
9. Add unit tests for checksum + pHash + Hamming distance logic

### Phase 2: Summarizer & Publisher
10. Implement `SemanticKernelSummarizer` wrapper (extractive + Semantic Kernel refinement)
11. Implement `TelegramPublisher` (post to summary channel using `Telegram.Bot`)
12. Implement `SummaryBackgroundService` (PeriodicTimer, orchestrate ingest→summarize→publish)
13. Register with Aspire and test with sample data

### Phase 3: Cleanup & Observability
14. Implement `ImageCleanupBackgroundService`
15. Add OpenTelemetry metrics (ingestion rate, dedupe rate, summary latency, publish failures, cleanup count)
16. Add structured logging for all major operations
17. Test metrics export to Prometheus

### Phase 4: Testing & Hardening
18. Integration tests (local Postgres) for full ingest→summarize→publish→cleanup flow
19. Tune pHash Hamming threshold and retention settings
20. Add error handling and retry logic
21. Optional: Add leader-election for multi-instance (Postgres advisory lock)

### Phase 5: Optional Improvements (Future)
22. Add CLIP/embeddings + `pgvector` for better image similarity search
23. Add LLM prompt tuning, rate-limits, and safety filters
24. Add Dockerfile, CI pipeline, and deployment guidance
25. Add admin UI or CLI for managing channels and summaries

## Suggested File Structure

```
TelegramAggregator/
├── Program.cs
├── appsettings.json
├── PLAN.md (this file)
├── Config/
│   └── WorkerOptions.cs
├── Data/
│   ├── AppDbContext.cs
│   └── Entities/
│       ├── Channel.cs
│       ├── Post.cs
│       ├── Image.cs
│       └── Summary.cs
├── Services/
│   ├── IImageService.cs
│   ├── ImageService.cs
│   ├── INormalizerService.cs
│   ├── NormalizerService.cs
│   ├── IDeduplicationService.cs
│   ├── DeduplicationService.cs
│   ├── ITelegramPublisher.cs
│   ├── TelegramPublisher.cs
│   └── WTelegramClientAdapter.cs
├── AI/
│   ├── ISemanticSummarizer.cs
│   └── SemanticKernelSummarizer.cs
├── Background/
│   ├── SummaryBackgroundService.cs
│   └── ImageCleanupBackgroundService.cs
└── Tests/
    ├── ImageServiceTests.cs
    ├── DeduplicationServiceTests.cs
    └── IntegrationTests.cs
```

## Testing Checklist

- [ ] Unit tests: SHA256 checksum, pHash computation, Hamming distance, dedup logic
- [ ] Unit tests: Text normalization and fingerprinting
- [ ] Integration tests: Ingest sample messages with images, verify DB storage
- [ ] Integration tests: Summarization with mocked Semantic Kernel
- [ ] Integration tests: Publishing to a test Telegram channel (requires test bot/channel)
- [ ] E2E flow: ingest→summarize→publish→cleanup on local Postgres
- [ ] Metrics export: verify metrics appear in Prometheus
- [ ] Performance: measure ingest rate, summarizer latency, DB query performance

## Notes & Constraints

- **User-Client Requirement**: We use a user-client (`WTelegramClient`) because the bot cannot be added as admin to source channels. This requires managing a user account session and complying with Telegram TOS.
- **Base64 Storage**: Storing images as base64 in DB simplifies deployment but increases DB size. Cleanup job must be enforced. For large scale, migrate to S3-like object storage.
- **Publishing Bot**: Ensure the publishing bot (used by `TelegramPublisher`) is added to the summary channel as admin (or at least can post messages).
- **Semantic Kernel**: Requires external LLM provider (Azure OpenAI, OpenAI, or compatible). Manage token usage and costs.
- **Single Instance Initially**: Scheduler assumes single active instance. For multi-instance, use Postgres advisory lock for leader election.
