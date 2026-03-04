# Integration Tests — Option 1: API CRUD Tests

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a `TelegramAggregator.IntegrationTests` NUnit project that spins up the full Aspire AppHost once per test run and exercises all 5 channel CRUD endpoints against a real Postgres database.

**Architecture:** A NUnit `[SetUpFixture]` (assembly-level) starts `DistributedApplicationTestingBuilder` once, waits for the `api` resource to become healthy, exposes a static `DistributedApplication` to all test classes, then disposes on teardown. Each test creates its own data using unique `TelegramChannelId` values so tests are fully independent of each other and run order.

**Tech Stack:** .NET 10, NUnit 4, `Aspire.Hosting.Testing` 13.1.x, `System.Net.Http.Json`, `TelegramAggregator.AppHost` (project reference), `TelegramAggregator.Common.Data` (for DTO types)

**Prerequisites:** The `feat/migration-service` branch must be merged to master before this plan is executed — it provides the postgres resource and migration service that these tests depend on.

---

## Task 1: Add `Aspire.Hosting.Testing` to central package management

**Files:**
- Modify: `Directory.Packages.props`

**Step 1: Add the package version entry**

Open `Directory.Packages.props` and add one line inside the `<!-- Testing -->` group:

```xml
<PackageVersion Include="Aspire.Hosting.Testing" Version="13.1.2" />
```

Result (testing section should look like):

```xml
<!-- Testing -->
<PackageVersion Include="Aspire.Hosting.Testing" Version="13.1.2" />
<PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.3.0" />
<PackageVersion Include="NUnit" Version="4.3.2" />
<PackageVersion Include="NUnit3TestAdapter" Version="4.6.0" />
<PackageVersion Include="NSubstitute" Version="5.3.0" />
```

**Step 2: Verify the file is valid XML**

```bash
dotnet build Directory.Packages.props 2>&1 | head -5
```

Expected: no XML parse errors (build will fail on missing project, that's fine).

---

## Task 2: Create the `TelegramAggregator.IntegrationTests` project

**Files:**
- Create: `TelegramAggregator.IntegrationTests/TelegramAggregator.IntegrationTests.csproj`

**Step 1: Create the project directory**

```bash
mkdir TelegramAggregator.IntegrationTests
```

**Step 2: Create the `.csproj`**

Create `TelegramAggregator.IntegrationTests/TelegramAggregator.IntegrationTests.csproj` with this exact content:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>TelegramAggregator.IntegrationTests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.Testing" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NUnit" />
    <PackageReference Include="NUnit3TestAdapter" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\TelegramAggregator.AppHost\TelegramAggregator.AppHost.csproj" />
    <ProjectReference Include="..\TelegramAggregator.Common.Data\TelegramAggregator.Common.Data.csproj" />
  </ItemGroup>

</Project>
```

**Step 3: Verify the project restores**

```bash
dotnet restore TelegramAggregator.IntegrationTests
```

Expected: `Restore succeeded.`

---

## Task 3: Wire the project into AppHost so `Projects.TelegramAggregator_IntegrationTests` is discoverable

The `Aspire.Hosting.Testing` package requires the AppHost to have a project reference to the test project so the test can call `DistributedApplicationTestingBuilder.CreateAsync<Projects.TelegramAggregator_AppHost>()`. But actually the reference direction is the other way: the **test project** references the **AppHost**. No AppHost change needed.

However the AppHost `.csproj` must NOT reference the integration tests project (that would create a circular reference). The test project references the AppHost only.

**Step 1: Verify no circular reference**

Open `TelegramAggregator.AppHost/TelegramAggregator.AppHost.csproj` and confirm `TelegramAggregator.IntegrationTests` is NOT listed. No changes needed.

---

## Task 4: Write `AppHostFixture` — assembly-level AppHost lifecycle

**Files:**
- Create: `TelegramAggregator.IntegrationTests/AppHostFixture.cs`

**Step 1: Create the file**

```csharp
using Aspire.Hosting.Testing;

namespace TelegramAggregator.IntegrationTests;

[SetUpFixture]
public class AppHostFixture
{
    public static DistributedApplication App { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TelegramAggregator_AppHost>();

        // Provide stub values for required secret parameters so the worker
        // starts without crashing (its Telegram logic is stubbed anyway)
        appHost.Configuration["Parameters:telegram-bot-token"] = "dummy";
        appHost.Configuration["Parameters:telegram-api-id"] = "12345";
        appHost.Configuration["Parameters:telegram-api-hash"] = "dummy-hash";
        appHost.Configuration["Parameters:telegram-user-phone-number"] = "+1234567890";
        appHost.Configuration["Parameters:azure-openai-endpoint"] = "https://dummy.openai.azure.com/";
        appHost.Configuration["Parameters:azure-openai-api-key"] = "dummy-key";

        App = await appHost.BuildAsync();
        await App.StartAsync();

        // Wait for the API to be healthy (migrations will have run first
        // because of WaitForCompletion(migrations) wired in AppHost)
        await App.ResourceNotifications
            .WaitForResourceHealthyAsync("api")
            .WaitAsync(TimeSpan.FromSeconds(120));
    }

    [OneTimeTearDown]
    public async Task StopAsync() => await App.DisposeAsync();
}
```

**Step 2: Build to check for compile errors**

```bash
dotnet build TelegramAggregator.IntegrationTests
```

Expected: `Build succeeded.` (no test run yet)

---

## Task 5: Write `ChannelsApiTests` — GET all channels

**Files:**
- Create: `TelegramAggregator.IntegrationTests/ChannelsApiTests.cs`

**Step 1: Create the file with the first test**

```csharp
using System.Net;
using System.Net.Http.Json;
using TelegramAggregator.Common.Data.Contracts;

namespace TelegramAggregator.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class ChannelsApiTests
{
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _client = AppHostFixture.App.CreateHttpClient("api");
    }

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    public async Task GetAll_ReturnsOkWithChannelList()
    {
        // Create a channel first so we have something to assert
        var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
        var create = new CreateChannelRequest(telegramId, $"@test_{telegramId}", $"Test {telegramId}");
        var post = await _client.PostAsJsonAsync("/api/channels", create);
        Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var response = await _client.GetAsync("/api/channels");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var channels = await response.Content.ReadFromJsonAsync<List<ChannelDto>>();
        Assert.That(channels, Is.Not.Null);
        Assert.That(channels!.Any(c => c.TelegramChannelId == telegramId), Is.True);
    }
}
```

**Step 2: Build to check for compile errors**

```bash
dotnet build TelegramAggregator.IntegrationTests
```

Expected: `Build succeeded.`

**Step 3: Run just this test (requires Docker)**

```bash
dotnet test TelegramAggregator.IntegrationTests --filter "Name=GetAll_ReturnsOkWithChannelList" -v normal
```

Expected: `Passed! - Failed: 0, Passed: 1`

If Docker is unavailable, skip and continue writing tests — run all at the end.

---

## Task 6: Write remaining CRUD tests

**Files:**
- Modify: `TelegramAggregator.IntegrationTests/ChannelsApiTests.cs`

**Step 1: Add POST tests**

Inside `ChannelsApiTests`, add:

```csharp
[Test]
public async Task Create_ValidRequest_Returns201WithBody()
{
    var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
    var request = new CreateChannelRequest(telegramId, $"@chan_{telegramId}", $"Channel {telegramId}");

    var response = await _client.PostAsJsonAsync("/api/channels", request);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    var dto = await response.Content.ReadFromJsonAsync<ChannelDto>();
    Assert.That(dto, Is.Not.Null);
    Assert.That(dto!.TelegramChannelId, Is.EqualTo(telegramId));
    Assert.That(dto.Username, Is.EqualTo(request.Username));
    Assert.That(dto.IsActive, Is.True);
}

[Test]
public async Task Create_DuplicateTelegramId_Returns400()
{
    var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
    var request = new CreateChannelRequest(telegramId, $"@dup_{telegramId}", $"Dup {telegramId}");

    // First create should succeed
    var first = await _client.PostAsJsonAsync("/api/channels", request);
    Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    // Second create with same TelegramChannelId should fail
    var second = await _client.PostAsJsonAsync("/api/channels", request);
    Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
}
```

**Step 2: Add GET by ID tests**

```csharp
[Test]
public async Task GetById_ExistingId_Returns200WithBody()
{
    var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
    var created = await CreateChannelAsync(telegramId);

    var response = await _client.GetAsync($"/api/channels/{created.Id}");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    var dto = await response.Content.ReadFromJsonAsync<ChannelDto>();
    Assert.That(dto!.Id, Is.EqualTo(created.Id));
    Assert.That(dto.TelegramChannelId, Is.EqualTo(telegramId));
}

[Test]
public async Task GetById_UnknownId_Returns404()
{
    var response = await _client.GetAsync("/api/channels/999999999");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
}
```

**Step 3: Add PUT test**

```csharp
[Test]
public async Task Update_ExistingId_Returns200WithUpdatedData()
{
    var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
    var created = await CreateChannelAsync(telegramId);

    var updateRequest = new UpdateChannelRequest(null, "Updated Title", false);
    var response = await _client.PutAsJsonAsync($"/api/channels/{created.Id}", updateRequest);

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    var dto = await response.Content.ReadFromJsonAsync<ChannelDto>();
    Assert.That(dto!.Title, Is.EqualTo("Updated Title"));
    Assert.That(dto.IsActive, Is.False);
}
```

**Step 4: Add DELETE tests**

```csharp
[Test]
public async Task Delete_ExistingId_Returns204()
{
    var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
    var created = await CreateChannelAsync(telegramId);

    var response = await _client.DeleteAsync($"/api/channels/{created.Id}");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
}

[Test]
public async Task Delete_ThenGet_Returns404()
{
    var telegramId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
    var created = await CreateChannelAsync(telegramId);

    await _client.DeleteAsync($"/api/channels/{created.Id}");
    var response = await _client.GetAsync($"/api/channels/{created.Id}");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
}
```

**Step 5: Add the `CreateChannelAsync` helper at the bottom of the class**

```csharp
private async Task<ChannelDto> CreateChannelAsync(long telegramId)
{
    var request = new CreateChannelRequest(telegramId, $"@h_{telegramId}", $"Helper {telegramId}");
    var response = await _client.PostAsJsonAsync("/api/channels", request);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<ChannelDto>())!;
}
```

**Step 6: Build**

```bash
dotnet build TelegramAggregator.IntegrationTests
```

Expected: `Build succeeded.`

---

## Task 7: Check DTO types match API surface

The tests use `CreateChannelRequest`, `UpdateChannelRequest`, and `ChannelDto` from `TelegramAggregator.Common.Data.Contracts`. Verify the constructors match what the tests use.

**Step 1: Confirm `CreateChannelRequest` record**

```bash
grep -n "CreateChannelRequest" TelegramAggregator.Common.Data/Contracts/*.cs
```

Expected: a record or class with `TelegramChannelId`, `Username`, `Title` parameters. If the constructor signature differs, adjust the test code accordingly.

**Step 2: Confirm `UpdateChannelRequest` record**

```bash
grep -n "UpdateChannelRequest" TelegramAggregator.Common.Data/Contracts/*.cs
```

Expected: parameters for `Username?`, `Title?`, `IsActive?`. Adjust if different.

---

## Task 8: Run all integration tests

**Step 1: Run with Docker available**

```bash
dotnet test TelegramAggregator.IntegrationTests --filter "TestCategory=Integration" -v normal
```

Expected:
```
Passed!  - Failed: 0, Passed: 8, Skipped: 0
```

AppHost startup takes 30–60 seconds on first run (Docker pull + postgres init + migrations).

**Step 2: Confirm unit tests still pass**

```bash
dotnet test TelegramAggregator.Tests
```

Expected: `Passed! - Failed: 0, Passed: 72`

**Step 3: Commit**

```bash
git add Directory.Packages.props TelegramAggregator.IntegrationTests/
git commit -m "feat: add integration test project with channel CRUD tests"
```

---

## Running Without Docker

To run only unit tests (e.g., in CI without Docker):

```bash
dotnet test --filter "TestCategory!=Integration"
```

This skips `TelegramAggregator.IntegrationTests` entirely since all its tests are tagged `[Category("Integration")]`.
