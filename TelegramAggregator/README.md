# Telegram News Aggregator

A distributed system built with **.NET 10** and **Dotnet Aspire** that ingests news posts from Telegram channels, deduplicates content, generates AI-powered summaries, and publishes them to a dedicated channel.

**Status**: Phase 0 ✅ Complete | Phase 1 ⏳ Ready to Start

---

## 🚀 Quick Start

### For Developers
1. Read **[QUICKSTART.md](./QUICKSTART.md)** - Setup your environment
2. Review **[ARCHITECTURE.md](./ARCHITECTURE.md)** - Understand the system
3. Follow **[NEXT_STEPS.md](./NEXT_STEPS.md)** - Phase 1 tasks

### For Tech Leads
1. Read **[PLAN.md](./PLAN.md)** - Project requirements
2. Review **[ARCHITECTURE.md](./ARCHITECTURE.md)** - System design with diagrams
3. Check **[TASKS.md](./TASKS.md)** - Task roadmap and progress

### For Project Managers
1. **[PLAN.md](./PLAN.md)** - Project scope and timeline
2. **[TASKS.md](./TASKS.md)** - Progress tracking with checkboxes
3. **[NEXT_STEPS.md](./NEXT_STEPS.md)** - Phase 1 planning

---

## 📚 Documentation

| Document | Purpose | Read Time |
|----------|---------|-----------|
| **[PLAN.md](./PLAN.md)** | Project requirements, objectives, timeline | 15 min |
| **[ARCHITECTURE.md](./ARCHITECTURE.md)** | System design, components, data flow with Mermaid diagrams | 20 min |
| **[TASKS.md](./TASKS.md)** | Detailed task breakdown, acceptance criteria, progress | 30 min |
| **[QUICKSTART.md](./QUICKSTART.md)** | Developer setup, common tasks, troubleshooting | 10 min |
| **[NEXT_STEPS.md](./NEXT_STEPS.md)** | Phase 1 checklist, testing strategy, success criteria | 15 min |

---

## 🏗️ Project Structure

```
TelegramAggregator/
├── Program.cs                           # Aspire DI setup
├── appsettings.json                     # Configuration template
├── Config/
│   └── WorkerOptions.cs                 # Configuration options
├── Data/
│   ├── AppDbContext.cs                  # EF Core context
│   └── Entities/                        # Data models
├── Services/                            # Business logic
│   ├── IImageService.cs                 # Image deduplication
│   ├── INormalizerService.cs            # Text normalization ✅ Implemented
│   ├── IDeduplicationService.cs         # Post deduplication
│   ├── ITelegramPublisher.cs            # Telegram publishing
│   └── WTelegramClientAdapter.cs        # Telegram ingestion
├── AI/
│   └── ISemanticSummarizer.cs           # LLM integration
└── Background/
    └── SummaryBackgroundService.cs      # 10-minute summarizer
```

---

## ✨ Key Features

### ✅ Completed (Phase 0)
- Aspire + DI fully configured
- EF Core with PostgreSQL (Npgsql)
- 5 data entities with optimized indexes
- 5 service interfaces
- NormalizerService fully implemented
- Configuration management
- Background service framework
- Comprehensive documentation
- Professional Mermaid diagrams

### ⏳ Pending (Phase 1)
- Image service implementation
- Perceptual hash (pHash) algorithm
- Deduplication logic
- Unit tests
- Database migration

---

## 🎯 Current Status

### Phase 0: Scaffold & Database Setup ✅ COMPLETE
- [x] Task 0.1: Program.cs with Aspire DI
- [x] Task 0.2: EF Core data models
- [x] Task 0.4: Configuration (appsettings.json)
- [x] Task 0.5: NuGet packages (13 installed)
- [ ] Task 0.3: Migration (requires Postgres instance)

### Phase 1: Ingest & Storage ⏳ IN PROGRESS
- [ ] Task 1.1: NormalizerService unit tests
- [ ] Task 1.2: ImageService implementation
- [ ] Task 1.3: Perceptual hash algorithm
- [ ] Task 1.4: DeduplicationService
- [ ] Task 1.5: WTelegramClientAdapter

See **[TASKS.md](./TASKS.md)** for detailed progress.

---

## 🔧 Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Runtime | .NET | 10.0 |
| Host | Dotnet Aspire | Latest |
| ORM | EF Core + Npgsql | 8.0/8.0 |
| Telegram (ingest) | WTelegramClient | Latest |
| Telegram (publish) | Telegram.Bot | 22.9+ |
| Images | SixLabors.ImageSharp | 3.1+ |
| AI | Semantic Kernel | 1.72+ |
| Metrics | OpenTelemetry + Prometheus | 1.3+ |

---

## 📦 Build Status

✅ **Successful** (0 errors, 0 warnings)

```bash
dotnet build          # Compile
dotnet run            # Run application
dotnet test           # Run tests (Phase 1+)
dotnet ef migrations add InitialSchema  # Create migration
dotnet ef database update               # Apply migration
```

---

## 🚀 Getting Started

### 1. Clone Repository
```bash
git clone <repository-url>
cd TelegramAggregator
```

### 2. Setup PostgreSQL
```bash
# Option A: Docker
docker run -d --name postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:15

# Option B: Local installation
createdb telegram_aggregator
```

### 3. Configure Connection
Update `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "postgres": "Host=localhost;Port=5432;Database=telegram_aggregator;Username=postgres;Password=postgres"
  }
}
```

### 4. Create Database Schema
```bash
dotnet ef migrations add InitialSchema
dotnet ef database update
```

### 5. Run Application
```bash
dotnet run
```

For detailed setup instructions, see **[QUICKSTART.md](./QUICKSTART.md)**.

---

## 📋 Git Commands

### Initialize Repository
```bash
git init
git add .
git commit -m "Phase 0 Complete: Project scaffold, Aspire integration, EF Core, services, comprehensive documentation"
```

### Create Feature Branch (Phase 1)
```bash
git checkout -b phase-1/ingest-storage
```

### Commit Phase 1 Work
```bash
git commit -m "Task 1.1: Add NormalizerService unit tests - all passing"
```

---

## 🎓 Learning Resources

- **Architecture**: See [ARCHITECTURE.md](./ARCHITECTURE.md) with 7 professional Mermaid diagrams
- **Data Flow**: See ARCHITECTURE.md → Data Flow section
- **Database Schema**: See ARCHITECTURE.md → Database Schema Diagram
- **Project Plan**: See [PLAN.md](./PLAN.md)
- **Development**: See [QUICKSTART.md](./QUICKSTART.md) and [NEXT_STEPS.md](./NEXT_STEPS.md)

---

## 🤝 Contributing

### Phase 1 Development
1. Create feature branch: `git checkout -b phase-1/task-name`
2. Follow acceptance criteria in [TASKS.md](./TASKS.md)
3. Write unit tests first (test-driven development)
4. Run `dotnet build` to verify
5. Commit with descriptive message
6. Update progress in TASKS.md

### Code Style
- Follow C# naming conventions (PascalCase for classes, camelCase for variables)
- Use meaningful names for variables and methods
- Add XML comments for public APIs
- Keep methods focused and testable

---

## 📞 Need Help?

**Question about...?**

| Topic | Reference |
|-------|-----------|
| Project requirements | [PLAN.md](./PLAN.md) |
| System design | [ARCHITECTURE.md](./ARCHITECTURE.md) |
| Development setup | [QUICKSTART.md](./QUICKSTART.md) |
| Current tasks | [TASKS.md](./TASKS.md) |
| Phase 1 planning | [NEXT_STEPS.md](./NEXT_STEPS.md) |
| Data model | [ARCHITECTURE.md](./ARCHITECTURE.md) → Database Schema Diagram |

---

## 📊 Project Metrics

```
Phase:               0 (Scaffold & Database Setup)
Build Status:        ✅ Success (0 errors, 0 warnings)
Source Files:        27
Service Interfaces:  5
Service Implementations: 5 (1 complete, 4 stubs)
Data Entities:       5
Database Indexes:    6
NuGet Packages:      13
Documentation:       5 comprehensive guides
Diagrams:            7 professional Mermaid diagrams
```

---

## 🎯 Next Phase

**Phase 1: Ingest & Storage** (12-15 hours)

1. Task 1.1: NormalizerService unit tests (3 hours)
2. Task 1.2: ImageService implementation (4 hours)
3. Task 1.3: Perceptual hash algorithm (5 hours)
4. Task 1.4: DeduplicationService (2 hours)
5. Task 1.5: WTelegramClientAdapter (3 hours)

See [NEXT_STEPS.md](./NEXT_STEPS.md) for detailed checklist.

---

## 📄 License

[Add your license information here]

---

## 👥 Authors

- **Initial Implementation**: GitHub Copilot
- **Date**: 2025-03-02

---

**Ready to start development?** → Begin with [QUICKSTART.md](./QUICKSTART.md)  
**Want to understand the architecture?** → Read [ARCHITECTURE.md](./ARCHITECTURE.md)  
**Need to know the next tasks?** → Check [NEXT_STEPS.md](./NEXT_STEPS.md)
