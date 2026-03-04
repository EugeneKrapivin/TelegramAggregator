# Telegram News Aggregator

A distributed Telegram news aggregator built with .NET 10 and Aspire. Monitors multiple Telegram channels, deduplicates content using text and image fingerprints, generates AI-powered summaries using Semantic Kernel, and publishes results to a summary channel.

## Features

- **Multi-Channel Monitoring** - Subscribe to multiple Telegram channels as a user client
- **Smart Deduplication** - Eliminates duplicate posts using:
  - Text normalization and hashing
  - Image checksums (SHA256)
  - Perceptual image hashing with Hamming distance matching
- **AI-Powered Summaries** - Uses Semantic Kernel with configurable LLM providers:
  - Azure OpenAI (primary)
  - OpenAI (fallback)
- **RESTful API** - Minimal API with Scalar documentation for:
  - Channel management (CRUD operations)
  - Enable/disable channels dynamically
- **PostgreSQL Storage** - Persistent storage for:
  - Channels, posts, images, summaries
  - Base64 encoded image content with cleanup jobs
- **Background Workers** - Scheduled tasks:
  - Summary generation (every 10 minutes)
  - Image cleanup (remove old base64 content)
- **Observability** - Built-in telemetry:
  - OpenTelemetry instrumentation
  - Prometheus metrics export

## Architecture

```
Telegram Channels
       ↓
WTelegramClientAdapter (User Client)
       ↓
NormalizerService (Text Processing)
       ↓
ImageService (Download & Dedup)
       ↓
DeduplicationService (Fingerprinting)
       ↓
PostgreSQL Database
       ↓
SummaryBackgroundService (10-min interval)
       ↓
SemanticKernelSummarizer (AI Generation)
       ↓
TelegramPublisher (Bot API)
       ↓
Summary Channel
```

### Project Structure

- **TelegramAggregator** - Console/Worker service
  - Background workers for summarization and cleanup
  - Telegram client integration
  - Business logic services
  
- **TelegramAggregator.Api** - REST API (Minimal APIs)
  - Channel management endpoints
  - Scalar OpenAPI documentation
  
- **TelegramAggregator.Common.Data** - Shared layer
  - Entities (Channel, Post, Image, Summary)
  - DTOs (Data Transfer Objects)
  
- **TelegramAggregator.AppHost** - Aspire orchestration
  - Service configuration
  - Database setup
  - Secrets management
  
- **TelegramAggregator.ServiceDefaults** - Service configuration
  - Logging and telemetry defaults

## Prerequisites

- .NET 10 SDK
- Docker & Docker Compose (for PostgreSQL)
- Telegram account for user client authentication
- API credentials for:
  - Telegram Bot Token (@BotFather)
  - Telegram API Hash (for user client)
  - Azure OpenAI or OpenAI API keys

## Getting Started

### 1. Clone and Setup

```bash
git clone <repository>
cd TelegramAggregator
dotnet restore
```

### 2. Start PostgreSQL

```bash
docker-compose up -d postgres
```

### 3. Configure Secrets (Aspire)

When running via Aspire dashboard, you'll be prompted to enter:
- `telegram-bot-token` - From @BotFather
- `telegram-api-hash` - From Telegram API Console
- `telegram-user-phone-number` - Your account number
- `azure-openai-endpoint` - Azure OpenAI service URL
- `azure-openai-api-key` - Azure OpenAI API key
- `openai-api-key` - OpenAI API key (fallback)

### 4. Run with Aspire

```bash
dotnet run --project TelegramAggregator.AppHost
```

Open the dashboard at `http://localhost:15000`

### 5. (Optional) Run Individual Services

**Worker Service:**
```bash
dotnet run --project TelegramAggregator
```

**API Service:**
```bash
dotnet run --project TelegramAggregator.Api
```

Access Scalar API documentation at `http://localhost:5003/scalar/v1`

## Configuration

### appsettings.json

```json
{
  "Worker": {
    "SummaryInterval": "00:10:00",     // 10 minutes
    "ImageCleanupInterval": "01:00:00", // 1 hour
    "ImageRetentionHours": "168:00:00", // 7 days
    "PHashHammingThreshold": 8          // Image similarity threshold
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Prometheus": {
    "Port": 9090,
    "Enabled": true
  }
}
```

## API Endpoints

### Channel Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/channels` | Get all channels |
| GET | `/api/channels/{id}` | Get channel by ID |
| POST | `/api/channels` | Create new channel |
| PUT | `/api/channels/{id}` | Update channel |
| DELETE | `/api/channels/{id}` | Delete channel |

### Request Examples

**Create Channel:**
```bash
POST /api/channels
Content-Type: application/json

{
  "telegramChannelId": 1234567890,
  "username": "channel_name",
  "title": "My News Channel"
}
```

**Update Channel (disable):**
```bash
PUT /api/channels/1
Content-Type: application/json

{
  "isActive": false
}
```

## Database Schema

### Tables
- `channels` - Monitored Telegram channels
- `posts` - Aggregated posts with normalized text
- `images` - Deduplicated images (SHA256 + pHash)
- `post_images` - Junction table linking posts to images
- `summaries` - Generated summaries with included posts

## Development

### Running Tests

```bash
dotnet test
```

### Building

```bash
dotnet build
```

### Entity Framework Migrations

```bash
# Add migration
dotnet ef migrations add MigrationName -p TelegramAggregator

# Apply migrations
dotnet ef database update -p TelegramAggregator
```

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
- Verify PostgreSQL is running: `docker ps`
- Check connection string in appsettings.json
- Ensure migrations are applied: `dotnet ef database update`

### Telegram Authentication
- Phone number must include country code (e.g., +1234567890)
- API Hash must match your Telegram application credentials
- Session file is cached; delete to re-authenticate

### Summarizer Issues
- Verify API keys are valid
- Check rate limits on LLM providers
- Review semantic kernel configuration in appsettings.json

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
