# Aspire Integration Tests Design

**Date:** 2026-03-05
**Status:** Approved

---

## Context

The solution has 72 unit tests covering `NormalizerService`, `ImageService`, and related services using EF Core InMemory. These are fast and have no external dependencies.

With the addition of the `MigrationService` and real Postgres provisioning in AppHost, there is now a meaningful integration surface worth testing:

- The API's 5 channel CRUD endpoints against a real Postgres DB
- The infrastructure startup sequence (postgres → migrations → api)

Aspire's `Aspire.Hosting.Testing` package provides `DistributedApplicationTestingBuilder`, which launches the full AppHost in a background thread and manages its lifecycle. It does **not** support DI mocking — services run in separate processes; tests communicate only through external channels (HTTP, connection strings).

---

## Project

**Name:** `TelegramAggregator.IntegrationTests`
**Type:** NUnit class library (`Microsoft.NET.Sdk`, `IsTestProject=true`)
**Framework:** net10.0
**Packages:** `Aspire.Hosting.Testing`, `Microsoft.NET.Test.Sdk`, `NUnit`, `NUnit3TestAdapter`
**Reference:** `TelegramAggregator.AppHost`

**CI note:** Tests require Docker. All test classes carry `[Category("Integration")]` so they can be excluded without Docker:
```
dotnet test --filter "TestCategory!=Integration"
```

---

## Option 1: API Endpoint Integration Tests

### Scope

Tests the 5 channel CRUD endpoints (`/api/channels`) end-to-end against a real migrated Postgres database.

### AppHost Lifetime

A single `[SetUpFixture]` at the assembly level starts the AppHost once per `dotnet test` run and disposes it after all tests complete. This avoids spinning up Docker containers per fixture class.

```csharp
[SetUpFixture]
public class AppHostFixture
{
    public static DistributedApplication App { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TelegramAggregator_AppHost>();
        App = await appHost.BuildAsync();
        await App.StartAsync();
        await App.ResourceNotifications
            .WaitForResourceHealthyAsync("api")
            .WaitAsync(TimeSpan.FromSeconds(60));
    }

    [OneTimeTearDown]
    public async Task StopAsync() => await App.DisposeAsync();
}
```

Because `WaitForCompletion(migrations)` is already wired in AppHost, the API only becomes healthy after migrations have run — no additional waiting needed in tests.

### Test Cases

**Fixture:** `ChannelsApiTests`

| Test | Endpoint | Expected |
|---|---|---|
| `GetAll_EmptyDb_ReturnsOkAndEmptyList` | `GET /api/channels` | 200 `[]` |
| `Create_ValidRequest_Returns201WithBody` | `POST /api/channels` | 201 + `ChannelDto` |
| `Create_DuplicateTelegramId_Returns400` | `POST /api/channels` | 400 |
| `GetById_ExistingId_Returns200WithBody` | `GET /api/channels/{id}` | 200 + `ChannelDto` |
| `GetById_UnknownId_Returns404` | `GET /api/channels/{id}` | 404 |
| `Update_ExistingId_Returns200WithUpdatedData` | `PUT /api/channels/{id}` | 200 + updated `ChannelDto` |
| `Delete_ExistingId_Returns204` | `DELETE /api/channels/{id}` | 204 |
| `Delete_ThenGet_Returns404` | `DELETE` then `GET` | 404 |

### Resource Access

```csharp
var client = AppHostFixture.App.CreateHttpClient("api");
```

---

## Option 2: API + Infrastructure Smoke Tests

Everything in Option 1, plus a second fixture class in the same project sharing the same `AppHostFixture`:

### Additional Test Cases

**Fixture:** `InfrastructureSmokeTests`

| Test | What it asserts |
|---|---|
| `Postgres_BecomesHealthy` | `WaitForResourceHealthyAsync("postgres")` completes within timeout — Docker container came up and is accepting connections |
| `Migrations_ExitCodeZero` | `WaitForResourceAsync("migrations", KnownResourceStates.Finished)` completes and exit code is 0 — `MigrateAsync()` succeeded |
| `Api_HealthEndpoint_ReturnsOk` | `GET /health` returns 200 — Aspire `ServiceDefaults` health check is wired correctly |

### Notes

- The `migrations` exit code assertion requires reading the resource's exit code from `ResourceNotifications` state after it finishes — available via `DistributedApplication` resource state inspection.
- `GET /health` is registered automatically by `AddServiceDefaults()` in the API via `.MapHealthChecks("/health")`.
- Both fixture classes share the single `AppHostFixture` instance — Docker starts once, all tests run, Docker stops.

---

## What Is Not Covered

- Worker ingestion/summarization logic — requires live Telegram credentials unavailable in CI
- pHash deduplication, image cleanup — covered by existing unit tests
- Concurrent request behaviour — out of scope for now
