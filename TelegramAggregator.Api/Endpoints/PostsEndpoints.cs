using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelegramAggregator.Common.Data;
using TelegramAggregator.Common.Data.DTOs;

namespace TelegramAggregator.Api.Endpoints;

public static class PostsEndpoints
{
    public static void MapPostsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/channels/{channelId:long}/posts")
            .WithTags("Posts");

        group.MapGet("", GetChannelPosts)
            .WithName("GetChannelPosts")
            .WithDescription("Get paginated posts for a channel");
    }

    private static async Task<IResult> GetChannelPosts(
        [FromRoute] long channelId,
        [FromServices] AppDbContext db,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var skip = (page - 1) * pageSize;

        var totalCount = await db.Posts
            .Where(p => p.ChannelId == channelId)
            .CountAsync(cancellationToken);

        var posts = await db.Posts
            .Where(p => p.ChannelId == channelId)
            .OrderByDescending(p => p.PublishedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(p => new PostListDto(
                p.Id,
                p.Text,
                p.PublishedAt,
                p.PostImages.Select(pi => pi.ImageId).ToArray()
            ))
            .ToArrayAsync(cancellationToken);

        var hasMore = skip + posts.Length < totalCount;

        return Results.Ok(new PostPageDto(posts, totalCount, hasMore));
    }
}
