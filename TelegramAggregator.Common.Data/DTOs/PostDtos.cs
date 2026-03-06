namespace TelegramAggregator.Common.Data.DTOs;

public record PostListDto(
    long Id,
    string Text,
    DateTime PublishedAt,
    Guid[] ImageIds
);

public record PostPageDto(
    PostListDto[] Posts,
    int TotalCount,
    bool HasMore
);
