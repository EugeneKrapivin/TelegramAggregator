# Ingestion Issue - Root Cause Analysis

## Problem
Posts exist in database but have no images. Channels show post counts but clicking them shows posts with empty image arrays.

## Investigation Results

### What Works ✅
- ✅ Session file persistence (`data/wtelegram.session`)
- ✅ Auto-login logic (checks file exists before login)
- ✅ Client initialization
- ✅ OnUpdates event handler registered
- ✅ Message processing pipeline (ReceiveAndProcessPostAsync)

### Root Cause 🐛

**The ingestion is completely passive.**

Current flow:
1. `IngestionBackgroundService` starts
2. Calls `WTelegramClientAdapter.ConnectAsync()`
3. Auto-login succeeds (if session exists)
4. Registers `OnUpdates` event handler
5. **Then just waits forever** (`Task.Delay(Timeout.Infinite)`)

**The problem:** WTelegramClient `OnUpdates` only fires when:
- You're actively in a conversation
- Someone sends you a direct message
- OR you explicitly subscribe to channel updates

**Missing:** No code to:
- Fetch existing channels from database
- Subscribe to those channels
- Poll for new messages
- Actively retrieve message history

## How WTelegramClient Works

WTelegram has two modes:

### Mode 1: Passive (Current Implementation) ❌
```csharp
client.OnUpdates += HandleUpdateAsync;  // Waits for updates
await Task.Delay(Timeout.Infinite);     // Does nothing
```
- Only receives updates for active conversations
- Doesn't automatically monitor channels
- Channels must be "accessed" first to get updates

### Mode 2: Active Polling (What We Need) ✅
```csharp
// Get all dialogs (channels)
var dialogs = await client.Messages_GetAllDialogs();

// For each monitored channel, periodically fetch new messages
foreach (var channel in monitoredChannels)
{
    var messages = await client.Messages_GetHistory(
        peer: channelPeer,
        offset_id: lastMessageId,
        limit: 100
    );
    
    foreach (var msg in messages)
    {
        await ProcessMessageAsync(msg);
    }
}
```

## Solution Options

### Option A: Active Polling (Recommended)
Add polling logic to `IngestionBackgroundService`:

```csharp
protected override async Task ExecuteAsync(CancellationToken ct)
{
    await _adapter.ConnectAsync(ct);
    
    while (!ct.IsCancellationRequested)
    {
        try
        {
            await _adapter.PollMessagesAsync(ct);  // NEW METHOD
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Polling error");
        }
    }
}
```

In `WTelegramClientAdapter`, add:
```csharp
public async Task PollMessagesAsync(CancellationToken ct)
{
    if (Client.User == null) return;
    
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    var channels = await db.Channels.Where(c => c.IsActive).ToListAsync(ct);
    
    foreach (var channel in channels)
    {
        var peer = new InputPeerChannel(channel.TelegramChannelId, 0);
        
        // Get recent messages (last hour, or since last poll)
        var messages = await Client.Messages_GetHistory(peer, limit: 100);
        
        foreach (var msg in messages.Messages.OfType<Message>())
        {
            await ReceiveAndProcessPostAsync(msg, ct);
        }
    }
}
```

### Option B: Manual Trigger (Quick Fix)
Add an API endpoint to manually trigger ingestion:
```csharp
POST /api/telegram/ingest
```
This calls `PollMessagesAsync()` on demand.

### Option C: Historical Import (One-Time)
Add a command to import past N days of messages:
```csharp
await client.Messages_GetHistory(peer, offset_date: DateTime.UtcNow.AddDays(-7));
```

## Recommended Approach

1. **Short-term:** Add Option B (manual trigger endpoint) for testing
2. **Medium-term:** Implement Option A (active polling every 1-5 minutes)
3. **Long-term:** Keep passive `OnUpdates` as well for real-time updates

## Why Images Are Missing

Images are missing because **no messages have been ingested yet**. The ingestion pipeline (`ReceiveAndProcessPostAsync`) is correct and would download images if it received messages, but it never runs because:

1. No messages are flowing through `OnUpdates` (channels not subscribed)
2. No active polling to fetch messages
3. No initial historical import

## Next Steps

1. ✅ Confirm session file exists: `data/wtelegram.session`
2. ✅ Confirm client is logged in (check Aspire logs for "Connected as @username")
3. 🔧 Implement active polling in `IngestionBackgroundService`
4. 🔧 Add `PollMessagesAsync()` to `WTelegramClientAdapter`
5. ✅ Test: Add a channel, wait 1 minute, check posts appear with images

## Code Changes Required

**Files to modify:**
1. `TelegramAggregator.Api/Background/IngestionBackgroundService.cs` - Add polling loop
2. `TelegramAggregator.Api/Services/WTelegramClientAdapter.cs` - Add `PollMessagesAsync()` method
3. (Optional) `TelegramAggregator.Api/Endpoints/TelegramEndpoints.cs` - Add manual trigger endpoint

**Estimated effort:** 1-2 hours

---

**Status:** Ready to implement  
**Created:** 2026-03-06 09:45 UTC
