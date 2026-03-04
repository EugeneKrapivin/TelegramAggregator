namespace TelegramAggregator.Services;

public interface IImageService
{
    Task<byte[]> DownloadImageAsync(string url, CancellationToken cancellationToken = default);
    string ComputeSha256Hash(byte[] bytes);
    Task<ulong> ComputePerceptualHashAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
    int ComputeHammingDistance(ulong hash1, ulong hash2);
    Task<Guid> FindOrCreateImageAsync(byte[] bytes, string mimeType, int width, int height, CancellationToken cancellationToken = default);
    Task ClearContentAsync(Guid imageId, CancellationToken cancellationToken = default);
    Task ClearContentBatchAsync(IEnumerable<Guid> imageIds, CancellationToken cancellationToken = default);
}
