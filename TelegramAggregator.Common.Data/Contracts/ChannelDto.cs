namespace TelegramAggregator.Common.Data.Contracts;

public record ChannelDto(long Id, long TelegramChannelId, string Username, string Title, bool IsActive, DateTime AddedAt);

public record CreateChannelRequest(long TelegramChannelId, string Username, string Title);

public record UpdateChannelRequest(string? Username, string? Title, bool? IsActive);
