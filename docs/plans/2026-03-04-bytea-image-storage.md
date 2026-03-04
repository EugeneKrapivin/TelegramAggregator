# Bytea Image Storage Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace base64 `text` image storage with raw `bytea`, and null content immediately after publish instead of waiting 7 days.

**Architecture:** The `Image` entity's `ContentBase64` (string, base64-encoded, stored as Postgres `text`) becomes `Content` (byte[], stored as Postgres `bytea`). All reads/writes of image content switch from base64 encode/decode to raw bytes. The `ImageCleanupBackgroundService` (not yet implemented) becomes a simple "null after publish" call inside the publish pipeline, eliminating the need for a separate background job.

**Tech Stack:** .NET 10, EF Core + Npgsql, xUnit, Moq, EF InMemory

---

### Task 1: Rename and retype the Image entity property

**Files:**
- Modify: `TelegramAggregator.Common.Data/Entities/Image.cs:12`

**Step 1: Update the property**

Change:
```csharp
public string? ContentBase64 { get; set; }
```
To:
```csharp
public byte[]? Content { get; set; }
```

**Step 2: Run build to see what breaks**

Run: `dotnet build`
Expected: Compilation errors in `ImageService.cs`, `AppDbContext.cs`, and `ImageServiceTests.cs` referencing `ContentBase64`.

**Step 3: Commit**

```bash
git add TelegramAggregator.Common.Data/Entities/Image.cs
git commit -m "refactor: rename Image.ContentBase64 to Image.Content (byte[])"
```

---

### Task 2: Update AppDbContext column mapping

**Files:**
- Modify: `TelegramAggregator.Common.Data/AppDbContext.cs:58`

**Step 1: Update the column configuration**

Change:
```csharp
entity.Property(e => e.ContentBase64).HasColumnType("text");
```
To:
```csharp
entity.Property(e => e.Content).HasColumnType("bytea");
```

**Step 2: Run build (still expect errors from ImageService/tests)**

Run: `dotnet build TelegramAggregator.Common.Data`
Expected: SUCCESS for this project alone.

**Step 3: Commit**

```bash
git add TelegramAggregator.Common.Data/AppDbContext.cs
git commit -m "refactor: map Image.Content to Postgres bytea column"
```

---

### Task 3: Update ImageService to store raw bytes

**Files:**
- Modify: `TelegramAggregator/Services/ImageService.cs:158-168`

**Step 1: Replace base64 encoding with raw byte storage**

In `FindOrCreateImageAsync`, change the `newImage` construction from:
```csharp
ContentBase64 = Convert.ToBase64String(bytes),
```
To:
```csharp
Content = bytes,
```

**Step 2: Build the solution**

Run: `dotnet build`
Expected: SUCCESS (all compile errors from task 1 should now be resolved). If test project still has errors, that's expected — handled in task 4.

**Step 3: Commit**

```bash
git add TelegramAggregator/Services/ImageService.cs
git commit -m "refactor: store raw image bytes instead of base64 in ImageService"
```

---

### Task 4: Update existing tests to use byte[] Content

**Files:**
- Modify: `TelegramAggregator.Tests/Services/ImageServiceTests.cs`

**Step 1: Update `FindOrCreateImageAsync_WithNewImage_CreatesNewRecord`**

Change line:
```csharp
Assert.NotNull(savedImage.ContentBase64);
```
To:
```csharp
Assert.NotNull(savedImage.Content);
```

**Step 2: Update `FindOrCreateImageAsync_StoresContentAsBase64`**

Rename test and update assertions. Replace the entire test method:
```csharp
[Fact]
public async Task FindOrCreateImageAsync_StoresRawContent()
{
    // Arrange
    var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
    var mimeType = "image/jpeg";

    // Act
    var imageId = await _service.FindOrCreateImageAsync(imageBytes, mimeType, 100, 100);

    // Assert
    var savedImage = await _dbContext.Images.FindAsync(imageId);
    Assert.NotNull(savedImage!.Content);
    Assert.Equal(imageBytes, savedImage.Content);
}
```

**Step 3: Run all tests**

Run: `dotnet test`
Expected: All tests PASS.

**Step 4: Commit**

```bash
git add TelegramAggregator.Tests/Services/ImageServiceTests.cs
git commit -m "test: update image tests for byte[] Content property"
```

---

### Task 5: Write test for clearing content after publish

**Files:**
- Modify: `TelegramAggregator.Tests/Services/ImageServiceTests.cs`

**Step 1: Add a new method to IImageService for clearing content**

First, add the interface method. Modify `TelegramAggregator/Services/IImageService.cs` — add:
```csharp
Task ClearContentAsync(Guid imageId, CancellationToken cancellationToken = default);
```

**Step 2: Write the failing test**

Add to `ImageServiceTests.cs`:
```csharp
[Fact]
public async Task ClearContentAsync_NullsContentAndPreservesMetadata()
{
    // Arrange — create an image with content
    var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
    var imageId = await _service.FindOrCreateImageAsync(imageBytes, "image/jpeg", 100, 100);

    // Verify content exists
    var before = await _dbContext.Images.FindAsync(imageId);
    Assert.NotNull(before!.Content);

    // Act
    await _service.ClearContentAsync(imageId);

    // Assert — content nulled, metadata preserved
    var after = await _dbContext.Images.FindAsync(imageId);
    Assert.Null(after!.Content);
    Assert.Equal("image/jpeg", after.MimeType);
    Assert.Equal(100, after.Width);
    Assert.Equal(100, after.Height);
    Assert.NotEmpty(after.ChecksumSha256);
}

[Fact]
public async Task ClearContentAsync_NonExistentImage_DoesNotThrow()
{
    // Act & Assert — should not throw
    await _service.ClearContentAsync(Guid.NewGuid());
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test --filter "ClearContentAsync"`
Expected: FAIL — `ClearContentAsync` not implemented yet.

**Step 4: Commit**

```bash
git add TelegramAggregator/Services/IImageService.cs TelegramAggregator.Tests/Services/ImageServiceTests.cs
git commit -m "test: add failing tests for ClearContentAsync"
```

---

### Task 6: Implement ClearContentAsync

**Files:**
- Modify: `TelegramAggregator/Services/ImageService.cs`

**Step 1: Add the implementation**

Add to `ImageService.cs`:
```csharp
public async Task ClearContentAsync(Guid imageId, CancellationToken cancellationToken = default)
{
    var image = await _dbContext.Images.FindAsync(new object[] { imageId }, cancellationToken);
    if (image is null)
    {
        _logger.LogWarning("ClearContentAsync: image {ImageId} not found", imageId);
        return;
    }

    image.Content = null;
    await _dbContext.SaveChangesAsync(cancellationToken);
    _logger.LogInformation("Cleared content for image {ImageId}, freed {Bytes} bytes", imageId, image.SizeBytes);
}
```

**Step 2: Run the tests**

Run: `dotnet test --filter "ClearContentAsync"`
Expected: Both PASS.

**Step 3: Run full test suite**

Run: `dotnet test`
Expected: All PASS.

**Step 4: Commit**

```bash
git add TelegramAggregator/Services/ImageService.cs
git commit -m "feat: implement ClearContentAsync to null image content after publish"
```

---

### Task 7: Write test for batch content clearing

**Files:**
- Modify: `TelegramAggregator/Services/IImageService.cs`
- Modify: `TelegramAggregator.Tests/Services/ImageServiceTests.cs`

**Step 1: Add batch interface method**

Add to `IImageService.cs`:
```csharp
Task ClearContentBatchAsync(IEnumerable<Guid> imageIds, CancellationToken cancellationToken = default);
```

**Step 2: Write the failing test**

Add to `ImageServiceTests.cs`:
```csharp
[Fact]
public async Task ClearContentBatchAsync_ClearsMultipleImages()
{
    // Arrange
    var id1 = await _service.FindOrCreateImageAsync(
        new byte[] { 1, 2, 3 }, "image/jpeg", 100, 100);
    var id2 = await _service.FindOrCreateImageAsync(
        new byte[] { 4, 5, 6 }, "image/png", 200, 200);
    var id3 = await _service.FindOrCreateImageAsync(
        new byte[] { 7, 8, 9 }, "image/gif", 300, 300);

    // Act — clear first two, leave third
    await _service.ClearContentBatchAsync(new[] { id1, id2 });

    // Assert
    var img1 = await _dbContext.Images.FindAsync(id1);
    var img2 = await _dbContext.Images.FindAsync(id2);
    var img3 = await _dbContext.Images.FindAsync(id3);

    Assert.Null(img1!.Content);
    Assert.Null(img2!.Content);
    Assert.NotNull(img3!.Content); // untouched
}
```

**Step 3: Run test to verify it fails**

Run: `dotnet test --filter "ClearContentBatchAsync"`
Expected: FAIL.

**Step 4: Commit**

```bash
git add TelegramAggregator/Services/IImageService.cs TelegramAggregator.Tests/Services/ImageServiceTests.cs
git commit -m "test: add failing test for ClearContentBatchAsync"
```

---

### Task 8: Implement ClearContentBatchAsync

**Files:**
- Modify: `TelegramAggregator/Services/ImageService.cs`

**Step 1: Add the implementation**

Add to `ImageService.cs`:
```csharp
public async Task ClearContentBatchAsync(IEnumerable<Guid> imageIds, CancellationToken cancellationToken = default)
{
    var ids = imageIds.ToList();
    if (ids.Count == 0) return;

    var images = await _dbContext.Images
        .Where(i => ids.Contains(i.Id) && i.Content != null)
        .ToListAsync(cancellationToken);

    foreach (var image in images)
    {
        image.Content = null;
    }

    await _dbContext.SaveChangesAsync(cancellationToken);
    _logger.LogInformation("Cleared content for {Count} images", images.Count);
}
```

**Step 2: Run the tests**

Run: `dotnet test --filter "ClearContentBatch"`
Expected: PASS.

**Step 3: Run full suite**

Run: `dotnet test`
Expected: All PASS.

**Step 4: Commit**

```bash
git add TelegramAggregator/Services/ImageService.cs
git commit -m "feat: implement ClearContentBatchAsync for efficient post-publish cleanup"
```

---

### Task 9: Add EF Core migration for the schema change

**Files:**
- Creates new migration files under `TelegramAggregator/Migrations/`

**Step 1: Generate migration**

Run: `dotnet ef migrations add RenameContentBase64ToByteaContent -p TelegramAggregator.Common.Data -s TelegramAggregator`

Note: If `dotnet ef` is not installed, run `dotnet tool install --global dotnet-ef` first. The startup project (`-s`) must be able to create the DbContext. If the Aspire Npgsql registration causes issues at design time, you may need to use the AppHost as startup or add a design-time factory. The `Common.Data` project already has `Microsoft.EntityFrameworkCore.Design` — try:

Run: `dotnet ef migrations add RenameContentBase64ToByteaContent -p TelegramAggregator.Common.Data`

If this fails because there's no `IDesignTimeDbContextFactory`, create one:

Add `TelegramAggregator.Common.Data/DesignTimeDbContextFactory.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TelegramAggregator.Common.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=telegramaggregator;Username=postgres;Password=postgres");
        return new AppDbContext(optionsBuilder.Options);
    }
}
```

Then retry the migration command.

**Step 2: Inspect the generated migration**

Verify the `Up` method contains:
- Drop the `content_base64` column (or rename)
- Add a `content` column with `bytea` type

If EF generates a drop+add instead of rename, that's fine — there's no production data yet.

**Step 3: Commit**

```bash
git add TelegramAggregator.Common.Data/
git commit -m "migration: rename content_base64 (text) to content (bytea)"
```

---

### Task 10: Final verification

**Step 1: Clean build**

Run: `dotnet build --configuration Release`
Expected: SUCCESS, 0 warnings related to our changes.

**Step 2: Run full test suite**

Run: `dotnet test --configuration Release`
Expected: All PASS.

**Step 3: Verify no references to ContentBase64 remain**

Search the codebase for any leftover references:
- `ContentBase64` — should return zero results in `.cs` files
- `content_base64` — should only appear in migration files (if any prior migrations existed)
- `Convert.ToBase64String` and `Convert.FromBase64String` — should not appear in image-related code

**Step 4: Commit any remaining cleanup**

```bash
git add -A
git commit -m "chore: final cleanup for bytea image storage migration"
```

---

## Summary of changes

| File | Change |
|---|---|
| `Common.Data/Entities/Image.cs` | `ContentBase64` (string?) → `Content` (byte[]?) |
| `Common.Data/AppDbContext.cs` | Column type `text` → `bytea` |
| `Services/IImageService.cs` | Add `ClearContentAsync`, `ClearContentBatchAsync` |
| `Services/ImageService.cs` | Store raw bytes, implement content clearing |
| `Tests/Services/ImageServiceTests.cs` | Update assertions, add clearing tests |
| `Common.Data/Migrations/` | New migration for schema change |
| `Common.Data/DesignTimeDbContextFactory.cs` | New (if needed for EF tooling) |
