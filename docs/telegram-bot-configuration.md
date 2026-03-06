# Telegram Bot Configuration Guide

## Summary Channel ID

The `SummaryChannelId` configuration needs to be set to the **correct Telegram channel ID format**.

### Finding Your Channel ID

**Method 1: Using a Bot**
1. Add `@raw_data_bot` or `@userinfobot` to your channel as admin
2. Send any message to the channel
3. The bot will reply with channel info including the ID
4. Note: Channel IDs for supergroups/channels are **negative numbers** in format: `-100XXXXXXXXXX`

**Method 2: From Channel URL**
- If your channel URL is `https://t.me/c/1234567890/1`
- The channel ID is: `-1001234567890` (add `-100` prefix)

**Method 3: Using Telegram Desktop**
- Right-click channel → Copy Link
- If link contains `/c/1234567890`, the ID is `-1001234567890`

### Configuring the Channel ID

**Option A: Via appsettings.json (Development)**
```json
{
  "Worker": {
    "SummaryChannelId": -1001234567890
  }
}
```

**Option B: Via Environment Variable (Production/Aspire)**
```
Worker__SummaryChannelId=-1001234567890
```

**Option C: Via Aspire Parameter (Recommended)**
In `apphostconfig.json` or Aspire dashboard:
```json
{
  "Parameters": {
    "worker-summary-channel-id": {
      "value": "-1001234567890"
    }
  }
}
```

### Important Notes

1. **Channel ID Format**:
   - ✅ Correct: `-1001234567890` (negative, starts with -100)
   - ❌ Wrong: `1234567890` (positive number)
   - ❌ Wrong: `-1234567890` (missing 100 prefix)

2. **Bot Permissions**:
   - Bot must be added to the channel as an **administrator**
   - Bot needs permission to **post messages**
   - For private channels, bot must be explicitly invited

3. **Testing**:
   ```bash
   # Test if bot can access the channel (using curl)
   curl "https://api.telegram.org/bot<YOUR_BOT_TOKEN>/getChat?chat_id=-1001234567890"
   ```
   Should return channel info, not "chat not found"

### Current Configuration

Default value in `WorkerOptions.cs`:
```csharp
public long SummaryChannelId { get; set; } = 3814174631;
```

**This is invalid!** Update it to your actual channel ID with `-100` prefix.

### Troubleshooting "Chat Not Found" Error

If you see `Telegram Bot API error 400: Bad Request: chat not found`:

1. **Verify Channel ID**: Make sure it's negative and starts with `-100`
2. **Check Bot is Admin**: Bot must be added to channel with admin rights
3. **Verify Bot Token**: Ensure `Telegram:BotToken` is correct
4. **Test Access**: Use `getChat` API to verify bot can see the channel
5. **Channel Type**: 
   - Regular channels: `-100XXXXXXXXXX`
   - Private groups (upgraded to supergroup): `-100XXXXXXXXXX`
   - Regular groups (not upgraded): `-XXXXXXXXXX` (without 100)

### Example Working Configuration

```json
{
  "Telegram": {
    "BotToken": "1234567890:ABCdefGHIjklMNOpqrsTUVwxyz",
    "ApiId": "12345678",
    "ApiHash": "abcdef1234567890abcdef1234567890",
    "UserPhoneNumber": "+1234567890"
  },
  "Worker": {
    "SummaryChannelId": -1001234567890,  
    "SummaryInterval": "00:10:00",
    "ImageCleanupInterval": "01:00:00",
    "ImageRetentionHours": "168:00:00",
    "PHashHammingThreshold": 8
  }
}
```
