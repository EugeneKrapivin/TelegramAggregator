# Integration Tests — Option 2: Infrastructure Smoke Tests

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Extend the `TelegramAggregator.IntegrationTests` project (from Option 1) with a second fixture that asserts Postgres comes up healthy, migrations exit with code 0, and the API's health endpoint returns 200.

**Architecture:** A second NUnit `[TestFixture]` class in the same project shares the existing `AppHostFixture` (same `DistributedApplication` instance, Docker starts once). The smoke tests use `ResourceNotificationService` to wait for and inspect resource states, and an HTTP call to `/health`.

**Tech Stack:** Same as Option 1. `KnownResourceStates` from `Aspire.Hosting.Testing`, `ResourceNotificationService`.

**Prerequisites:** Option 1 (`2026-03-05-integration-tests-option1-api-crud.md`) must be fully implemented and passing before starting this plan.

---

## Task 1: Verify `/health` endpoint is mapped on the API

The Aspire smoke test for `GET /health` only works if the API calls `MapHealthChecks`. The API's `Program.cs` uses `AddServiceDefaults()` via `builder.AddServiceDefaults()` — check whether it does.

**Step 1: Inspect the API's `Program.cs`**

Open `TelegramAggregator.Api/Program.cs` and check:
- Is `builder.AddServiceDefaults()` called?
- Is `app.MapHealthChecks("/health")` (or `app.MapDefaultEndpoints()`) called?

**Step 2a: If `AddServiceDefaults` and `MapDefaultEndpoints` are already present**

No change needed. The `/health` endpoint is already registered. Continue to Task 2.

**Step 2b: If they are NOT present**

Add to `TelegramAggregator.Api/Program.cs`:

After `var builder = WebApplication.CreateBuilder(args);`, add:
```csharp
builder.AddServiceDefaults();
```

After `var app = builder.Build();`, add:
```csharp
app.MapDefaultEndpoints();
```

`MapDefaultEndpoints()` is an extension method from `ServiceDefaults` that registers `/health`, `/alive`, and related Aspire health check routes.

**Step 3: Build the API to verify**

```bash
dotnet build TelegramAggregator.Api
```

Expected: `Build succeeded.`

**Step 4: If you modified the API, commit it**

```bash
git add TelegramAggregator.Api/Program.cs
git commit -m "feat: wire AddServiceDefaults and MapDefaultEndpoints in API"
```

---

## Task 2: Write `InfrastructureSmokeTests` fixture

**Files:**
- Create: `TelegramAggregator.IntegrationTests/InfrastructureSmokeTests.cs`

**Step 1: Create the file**

```csharp
using Aspire.Hosting;
using System.Net;

namespace TelegramAggregator.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class InfrastructureSmokeTests
{
    [Test]
    public async Task Postgres_BecomesHealthy()
    {
        // Arrange — AppHost is already running via AppHostFixture
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act — wait for postgres to report healthy
        await AppHostFixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(60));

        // Assert — reaching here means it's healthy; no exception = pass
        Assert.Pass("postgres resource is healthy");
    }

    [Test]
    public async Task Migrations_FinishWithExitCodeZero()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        // Wait for the migrations resource to reach Finished state
        // (WaitForCompletion in AppHost already ensures api waited, but we
        // assert independently here to get an explicit failure message)
        var notification = await AppHostFixture.App.ResourceNotifications
            .WaitForResourceAsync("migrations", KnownResourceStates.Finished, cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(90));

        // The exit code is available on the resource snapshot's exit code property
        Assert.That(notification.Snapshot.ExitCode, Is.EqualTo(0),
            "migrations service should exit 0 after applying all pending migrations");
    }

    [Test]
    public async Task Api_HealthEndpoint_ReturnsOk()
    {
        using var client = AppHostFixture.App.CreateHttpClient("api");

        var response = await client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Expected /health to return 200. Body: {await response.Content.ReadAsStringAsync()}");
    }
}
```

**Step 2: Build**

```bash
dotnet build TelegramAggregator.IntegrationTests
```

Expected: `Build succeeded.`

> **Note:** If `notification.Snapshot.ExitCode` doesn't compile, the `ResourceNotificationService` API shape may differ in the installed version. Check the `WaitForResourceAsync` return type — it may return `ResourceEvent` with a `Snapshot` property, or you may need to read the exit code differently. Consult the type using:
> ```bash
> grep -r "ExitCode" $(dotnet nuget locals global-packages -l | awk '{print $NF}')/aspire.hosting.testing/
> ```
> Adjust the assertion to match the actual property path.

---

## Task 3: Run smoke tests

**Step 1: Run only smoke tests**

```bash
dotnet test TelegramAggregator.IntegrationTests --filter "FullyQualifiedName~InfrastructureSmokeTests" -v normal
```

Expected:
```
Passed!  - Failed: 0, Passed: 3, Skipped: 0
```

If `Migrations_FinishWithExitCodeZero` fails with a compile or runtime error on the exit-code access path, see the note in Task 2 Step 2 and adjust.

**Step 2: Run all integration tests together (Docker starts once)**

```bash
dotnet test TelegramAggregator.IntegrationTests --filter "TestCategory=Integration" -v normal
```

Expected:
```
Passed!  - Failed: 0, Passed: 11, Skipped: 0
```
(8 CRUD tests + 3 smoke tests)

**Step 3: Confirm unit tests still pass**

```bash
dotnet test TelegramAggregator.Tests
```

Expected: `Passed! - Failed: 0, Passed: 72`

**Step 4: Commit**

```bash
git add TelegramAggregator.IntegrationTests/InfrastructureSmokeTests.cs
git commit -m "feat: add infrastructure smoke tests (postgres, migrations, health)"
```

---

## Known Limitations

- **`migrations` exit code API:** `ResourceNotificationService.WaitForResourceAsync` return type and `Snapshot.ExitCode` availability may vary by Aspire version. If the property doesn't exist, the smoke test for exit code should be skipped until the API stabilises.
- **Worker not tested:** The worker requires live Telegram credentials. It starts with stub secrets and its logic is stubbed — it won't crash, but no meaningful assertions can be made about it from outside.
- **Health check registration:** If the API does not call `MapDefaultEndpoints()` the health test will return 404. Task 1 handles this.
