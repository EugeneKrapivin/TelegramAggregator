# Telegram News Aggregator

A modern Telegram news aggregator built with .NET 10 and Aspire. Monitors multiple Telegram channels, deduplicates content using text and image fingerprints, generates AI-powered summaries using Semantic Kernel, and publishes results to a summary channel.

## Features

### Core Functionality
- **Multi-Channel Monitoring** - Subscribe to multiple Telegram channels as a user client
- **Smart Deduplication** - Eliminates duplicate posts using:
  - Text normalization and hashing
  - Image checksums (SHA256)
  - Perceptual image hashing with Hamming distance matching
- **AI-Powered Summaries** - Uses Semantic Kernel with Azure OpenAI for:
  - Headline generation (≤10 words)
  - Digest creation (≤150 words)
- **Background Workers** - Scheduled tasks:
  - Summary generation (every 1 minute)
  - Image cleanup (remove old image content after 7 days)

### Web Interface
- **Modern React UI** - Built with React 18 + TypeScript + Tailwind CSS
- **Channel Management** - Full CRUD operations with visual interface
- **Interactive Login** - Self-service Telegram authentication (no manual file copying)
- **Posts Viewer** - Sliding panel with infinite scroll for browsing channel posts
- **Image Gallery** - Lazy-loaded images with efficient caching

### API & Storage
- **RESTful API** - Minimal API with Scalar documentation for:
  - Channel management (CRUD operations)
  - Posts pagination with image support
  - Telegram authentication endpoints
- **PostgreSQL Storage** - Persistent storage for:
  - Channels, posts, images, summaries
  - Binary image content with automatic cleanup
- **Observability** - Built-in telemetry:
  - OpenTelemetry instrumentation
  - Aspire dashboard integration

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         React Web UI                            │
│         (Channel Management, Posts Viewer, Auth UI)            │
└────────────────────┬────────────────────────────────────────────┘
                     │ HTTP/REST
┌────────────────────▼────────────────────────────────────────────┐
│                   TelegramAggregator.Api                        │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │           REST API (Minimal APIs)                        │  │
│  │  • Channel CRUD      • Posts Pagination                  │  │
│  │  • Image Delivery    • Auth Endpoints                    │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │        Background Services (Hosted Services)             │  │
│  │  • IngestionBackgroundService   (Message monitoring)     │  │
│  │  • SummaryBackgroundService     (AI summarization)       │  │
│  │  • ImageCleanupBackgroundService (Storage cleanup)       │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              Business Logic Services                     │  │
│  │  • WTelegramClientAdapter  (User client ingestion)       │  │
│  │  • TelegramPublisher       (Bot API publishing)          │  │
│  │  • NormalizerService       (Text processing)             │  │
│  │  • ImageService            (Download & dedup)            │  │
│  │  • DeduplicationService    (Fingerprinting)              │  │
│  │  • SemanticKernelSummarizer (AI generation)              │  │
│  └──────────────────────────────────────────────────────────┘  │
└────────────────────┬────────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────────┐
│                    PostgreSQL Database                          │
│     (Channels, Posts, Images, Summaries, Post_Images)          │
└─────────────────────────────────────────────────────────────────┘

External Dependencies:
  • Telegram User Client (WTelegramClient) - Message ingestion
  • Telegram Bot API - Summary publishing
  • Azure OpenAI - Summary generation
```

### Project Structure

- **TelegramAggregator.Api** - REST API + Background Services
  - Channel management endpoints
  - Posts and images delivery
  - Telegram authentication endpoints
  - Background workers for ingestion, summarization, and cleanup
  - Business logic services (Singleton lifetime with IServiceScopeFactory)
  
- **TelegramAggregator.Web** - React Frontend
  - Channel management UI with CRUD operations
  - Interactive Telegram login wizard
  - Posts viewer with infinite scroll
  - Built with Vite, TypeScript, Tailwind CSS
  
- **TelegramAggregator.Common.Data** - Shared Data Layer
  - Entities (Channel, Post, Image, Summary)
  - DTOs (Data Transfer Objects)
  - EF Core DbContext
  
- **TelegramAggregator.MigrationService** - Database Migrations
  - Runs EF migrations on startup
  - Gates other services via Aspire WaitForCompletion
  
- **TelegramAggregator.AppHost** - Aspire Orchestration
  - Service configuration and dependencies
  - PostgreSQL provisioning with pgAdmin
  - Secrets management via parameters
  - Vite dev server integration
  
- **TelegramAggregator.ServiceDefaults** - Shared Configuration
  - Logging and telemetry defaults
  - Health check configuration

## Prerequisites

- .NET 10 SDK
- Node.js 18+ and npm (for Web UI)
- Docker & Docker Compose (for PostgreSQL)
- Telegram account for user client authentication
- API credentials for:
  - Telegram Bot Token (@BotFather)
  - Telegram API credentials (api_id, api_hash from https://my.telegram.org/apps)
  - Azure OpenAI or OpenAI API keys

## Getting Started

### 1. Clone and Setup

```bash
git clone <repository>
cd TelegramAggregator
dotnet restore
cd TelegramAggregator.Web
npm install
cd ..
```

### 2. Configure Secrets (Aspire)

Copy the example configuration:
```bash
cp TelegramAggregator.AppHost/appsettings.Development.json.example TelegramAggregator.AppHost/appsettings.Development.json
```

Edit `appsettings.Development.json` and fill in your values:
- `telegram-bot-token` - From @BotFather
- `telegram-api-id` - From https://my.telegram.org/apps
- `telegram-api-hash` - From https://my.telegram.org/apps
- `telegram-user-phone-number` - Your account number (e.g., +1234567890)
- `azure-openai-endpoint` - Azure OpenAI service URL
- `azure-openai-api-key` - Azure OpenAI API key
- `worker-summary-channel-id` - Target channel ID (see [Bot Configuration Guide](docs/telegram-bot-configuration.md))

### 3. Run with Aspire

```bash
dotnet run --project TelegramAggregator.AppHost
```

Or use the Aspire CLI (recommended):
```bash
aspire run
```

The Aspire dashboard will open at `http://localhost:15000` showing:
- **API** - Backend services (health, logs, metrics)
- **UI** - React frontend dev server
- **postgres** - PostgreSQL container
- **pgadmin** - Database admin UI
- **migrations** - Database migration service

### 4. Access the Application

- **Web UI**: http://localhost:5173
- **API**: http://localhost:5000 (HTTPS: https://localhost:5001)
- **Scalar API Docs**: http://localhost:5000/scalar/v1
- **Aspire Dashboard**: http://localhost:15000

### 5. First-Time Setup

1. **Navigate to Telegram Login** - Click "Telegram Login" in the web UI header
2. **Authenticate** - Follow the wizard:
   - Enter phone number → receive verification code
   - Submit code → if 2FA enabled, enter password
   - Success → session saved, auto-login on restart
3. **Add Channels** - Use the Channels page to add Telegram channels to monitor
4. **View Posts** - Click any channel row to see posts in sliding panel

## Configuration

### Aspire Parameters (Recommended)

Edit `TelegramAggregator.AppHost/appsettings.Development.json`:

```json
{
  "Parameters": {
    "telegram-bot-token": "YOUR_BOT_TOKEN",
    "telegram-api-id": "YOUR_API_ID",
    "telegram-api-hash": "YOUR_API_HASH",
    "telegram-user-phone-number": "+1234567890",
    "azure-openai-endpoint": "https://your-instance.openai.azure.com/",
    "azure-openai-api-key": "YOUR_API_KEY",
    "worker-summary-channel-id": "-1001234567890"
  }
}
```

**Important:** See [Telegram Bot Configuration Guide](docs/telegram-bot-configuration.md) for:
- How to find your channel ID (must be negative with `-100` prefix)
- Adding bot as channel admin
- Verification troubleshooting

### appsettings.json (Worker Behavior)

```json
{
  "Worker": {
    "SummaryInterval": "00:01:00",     // 1 minute
    "ImageCleanupInterval": "01:00:00", // 1 hour
    "ImageRetentionHours": "168:00:00", // 7 days
    "PHashHammingThreshold": 8,         // Image similarity (lower = stricter)
    "SummaryChannelId": 0               // Set via Aspire parameter (required)
  }
}
```

### Environment Variables (Production)

Alternatively, use environment variables with double-underscore notation:

```bash
export Telegram__BotToken="your-token"
export Telegram__ApiId="12345678"
export Telegram__ApiHash="abcd1234..."
export Telegram__UserPhoneNumber="+1234567890"
export SemanticKernel__AzureOpenAI__Endpoint="https://..."
export SemanticKernel__AzureOpenAI__ApiKey="your-key"
export Worker__SummaryChannelId="-1001234567890"
```

## API Endpoints

### Channel Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/channels` | Get all channels with post counts |
| GET | `/api/channels/{id}` | Get channel by ID |
| POST | `/api/channels` | Create new channel |
| PUT | `/api/channels/{id}` | Update channel (title, isActive) |
| DELETE | `/api/channels/{id}` | Delete channel and related data |

### Posts & Images

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/channels/{id}/posts?skip=0&take=20` | Get paginated posts for channel |
| GET | `/api/images/{id}` | Get image by ID (returns binary image data) |

### Telegram Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/telegram/auth/start` | Begin login (sends verification code) |
| GET | `/api/telegram/auth/status` | Get current auth state |
| POST | `/api/telegram/auth/submit` | Submit verification code or 2FA password |
| POST | `/api/telegram/auth/reset` | Reset auth state (debugging) |

### Request Examples

**Create Channel:**
```bash
POST /api/channels
Content-Type: application/json

{
  "telegramChannelId": 1234567890,
  "username": "channel_name",
  "title": "My News Channel",
  "isActive": true
}
```

**Update Channel (disable):**
```bash
PUT /api/channels/1
Content-Type: application/json

{
  "title": "Updated Title",
  "isActive": false
}
```

**Get Posts with Pagination:**
```bash
GET /api/channels/1/posts?skip=0&take=20
```

Returns:
```json
{
  "posts": [
    {
      "id": 123,
      "text": "Post content...",
      "publishedAt": "2026-03-06T09:00:00Z",
      "imageIds": [45, 46]
    }
  ],
  "totalCount": 150,
  "hasMore": true
}
```

## Web UI

### Features

- **Channel Management** - Visual table with CRUD operations
  - Create, edit, delete channels
  - Toggle active/inactive status
  - View post counts in real-time
  
- **Telegram Login** - Interactive authentication wizard
  - Phone number entry
  - Verification code submission
  - 2FA password support
  - Auto-login on subsequent starts
  
- **Posts Viewer** - Sliding panel with rich features
  - Click any channel row to open
  - Infinite scroll pagination
  - Lazy-loaded images
  - Click outside to close
  - Switch channels without closing

### Technology Stack

- **React 18** - UI framework
- **TypeScript** - Type safety
- **Tailwind CSS** - Styling
- **Vite** - Build tool and dev server
- **React Router** - Client-side routing
- **date-fns** - Date formatting

## Database Schema

### Tables
- `channels` - Monitored Telegram channels (id, telegram_channel_id, username, title, is_active)
- `posts` - Aggregated posts with normalized text (id, channel_id, telegram_message_id, text, published_at, raw_json)
- `images` - Deduplicated images (id, checksum_sha256, perceptual_hash, telegram_file_id, content, width, height, size_bytes)
- `post_images` - Junction table linking posts to images (post_id, image_id)
- `summaries` - Generated summaries (id, headline, digest, included_post_ids, created_at)

### Image Storage Strategy

Images follow a lifecycle to optimize storage:
1. **Download** - Content stored as `bytea` in `images.content`
2. **Retention** - Content kept for `ImageRetentionHours` (default 7 days)
3. **Cleanup** - `ImageCleanupBackgroundService` nullifies `content` after retention period
4. **Reuse** - `telegram_file_id` persists for Telegram re-upload without re-downloading

## Development

### Running Tests

```bash
dotnet test
```

Tests use NUnit + NSubstitute + EF Core InMemory provider.

### Building

```bash
# Backend
dotnet build

# Frontend
cd TelegramAggregator.Web
npm run build
```

### Frontend Development

```bash
cd TelegramAggregator.Web

# Install dependencies
npm install

# Run dev server (standalone)
npm run dev

# Lint
npm run lint

# Type check
npm run type-check
```

**Note:** When running via Aspire, Vite dev server starts automatically.

### Entity Framework Migrations

```bash
# Add migration
dotnet ef migrations add MigrationName -p TelegramAggregator.Common.Data -s TelegramAggregator.Api

# Apply migrations
dotnet ef database update -p TelegramAggregator.Common.Data -s TelegramAggregator.Api
```

**Note:** In production, `TelegramAggregator.MigrationService` applies migrations automatically on startup.

## Configuration Management

### Using User Secrets (Local Development)

```bash
dotnet user-secrets init --project TelegramAggregator
dotnet user-secrets set "Telegram:BotToken" "your-token" --project TelegramAggregator
```

### Using Environment Variables (Production)

```bash
export TELEGRAM__BOTTOKEN="your-token"
export SEMANTICKERNEL__AZUREOPENAI__ENDPOINT="https://..."
```

## Performance Tuning

### Image Deduplication
- **Hamming Threshold**: Controls perceptual hash matching sensitivity
  - Lower values (4-6): Stricter, fewer false positives
  - Higher values (10-12): Looser, more aggressive deduplication
- **Candidate Filtering**: Uses time window and size similarity to reduce scan time

### Database Optimization
- Indexes on `TelegramChannelId`, `ChecksumSha256`, `PerceptualHash`
- Partitioning recommendations for large datasets
- Cleanup jobs to manage `ContentBase64` size

## Troubleshooting

### Database Connection Issues
- Verify PostgreSQL is running: Check Aspire dashboard or `docker ps`
- Check connection string in appsettings.json
- Ensure migrations are applied: Check migrations service in Aspire dashboard

### Telegram Authentication
- **Phone number** must include country code (e.g., +1234567890)
- **API Hash** must match your Telegram application credentials
- **Session file** is cached at `data/wtelegram.session`; delete to re-authenticate
- Use the Web UI login wizard for interactive authentication

### Bot API "Chat Not Found" Error
- **Channel ID** must be negative with `-100` prefix (e.g., `-1001234567890`)
- **Bot must be admin** in the target channel with "Post Messages" permission
- See [Telegram Bot Configuration Guide](docs/telegram-bot-configuration.md)

### Summarizer Issues
- Verify API keys are valid in Aspire parameters
- Check rate limits on LLM providers
- Review semantic kernel configuration in appsettings.json

### Images Not Displaying
- Check that posts have associated images in database
- Verify `/api/images/{id}` endpoint returns image data
- Check browser console for 404 or CORS errors
- Confirm `ImageRetentionHours` hasn't expired image content

## Contributing

1. Create a feature branch: `git checkout -b feature/name`
2. Make changes and test: `dotnet test`
3. Commit with descriptive messages
4. Push and create a Pull Request

## License

This project is licensed under the MIT License.

## References

- [.NET 10 Documentation](https://learn.microsoft.com/en-us/dotnet/)
- [Aspire Documentation](https://aspire.dev/)
- [Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [Telegram Bot API](https://core.telegram.org/bots/api)
- [WTelegramClient](https://github.com/wiz0u/WTelegramClient)
