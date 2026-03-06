# Architecture Documentation

## System Overview

TelegramAggregator is a news aggregation platform that monitors Telegram channels, deduplicates content, and generates AI-powered summaries. Built with .NET 10, Aspire, React, and PostgreSQL.

## High-Level Architecture

`
┌─────────────────────────────────────────────────────────────────┐
│                          Browser                                │
│                    (React Web UI)                               │
└────────────────┬────────────────────────────────────────────────┘
                 │ HTTP/REST
                 │
┌────────────────▼────────────────────────────────────────────────┐
│                    .NET 10 API Process                          │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              REST API (Minimal APIs)                     │  │
│  │  Endpoints: Channels, Posts, Images, Telegram Auth      │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │          Background Services (HostedServices)            │  │
│  │  • IngestionBackgroundService                            │  │
│  │  • SummaryBackgroundService                              │  │
│  │  • ImageCleanupBackgroundService                         │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              Business Logic (Singletons)                 │  │
│  │  • WTelegramClientAdapter  • TelegramPublisher           │  │
│  │  • NormalizerService        • ImageService               │  │
│  │  • DeduplicationService     • SemanticKernelSummarizer   │  │
│  └──────────────────────────────────────────────────────────┘  │
└────────────────┬────────────────────────────────────────────────┘
                 │ Npgsql/EF Core
┌────────────────▼────────────────────────────────────────────────┐
│                    PostgreSQL Database                          │
│  Tables: channels, posts, images, post_images, summaries       │
└─────────────────────────────────────────────────────────────────┘

External Integrations:
  • Telegram User Client (WTelegramClient) - Message ingestion
  • Telegram Bot API - Summary publishing
  • Azure OpenAI / OpenAI - AI summarization
`

See full documentation in docs/architecture.md for detailed component descriptions, data flows, and design patterns.
