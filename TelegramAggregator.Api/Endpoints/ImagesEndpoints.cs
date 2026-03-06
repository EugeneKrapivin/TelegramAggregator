using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelegramAggregator.Common.Data;

namespace TelegramAggregator.Api.Endpoints;

public static class ImagesEndpoints
{
    public static void MapImagesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/images")
            .WithTags("Images");

        group.MapGet("{id:guid}", GetImage)
            .WithName("GetImage")
            .WithDescription("Get image by ID");
    }

    private static async Task<IResult> GetImage(
        [FromRoute] Guid id,
        [FromServices] AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        var image = await db.Images
            .Where(i => i.Id == id)
            .Select(i => new { i.MimeType, i.Content })
            .FirstOrDefaultAsync(cancellationToken);

        if (image == null)
        {
            return Results.NotFound(new { error = "Image not found" });
        }

        if (image.Content == null || image.Content.Length == 0)
        {
            return Results.NotFound(new { error = "Image content has been cleared" });
        }

        return Results.File(
            image.Content,
            contentType: image.MimeType ?? "image/jpeg",
            enableRangeProcessing: false);
    }
}
