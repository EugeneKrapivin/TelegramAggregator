using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

using TelegramAggregator.Common.Data;
using TelegramAggregator.Common.Data.Entities;
using TelegramAggregator.Config;

namespace TelegramAggregator.Services;

public class ImageService : IImageService
{
    private readonly ILogger<ImageService> _logger;
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _dbContext;
    private readonly int _pHashThreshold;

    public ImageService(ILogger<ImageService> logger, AppDbContext dbContext, IOptions<WorkerOptions> workerOptions)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _dbContext = dbContext;
        _pHashThreshold = workerOptions.Value.PHashHammingThreshold;
    }

    public async Task<byte[]> DownloadImageAsync(string url, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Downloading image from {Url}", url);
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            _logger.LogDebug("Downloaded image, size: {Size} bytes", bytes.Length);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download image from {Url}", url);
            throw;
        }
    }

    public string ComputeSha256Hash(byte[] bytes)
    {
        var hashedBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashedBytes);
    }

    /// <summary>
    /// Computes the perceptual hash (aHash - average hash) of an image.
    /// Uses a 8x8 grid (64-bit hash) for fast comparison.
    /// </summary>
    public async Task<ulong> ComputePerceptualHashAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageBytes))
                {
                    // Resize to 8x8 for faster processing
                    image.Mutate(x => x.Resize(8, 8));

                    // Convert to grayscale and compute average brightness
                    float brightnessSum = 0;
                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 8; x++)
                        {
                            var pixel = image[x, y];
                            // Use standard luminosity formula
                            float brightness = (0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B) / 255f;
                            brightnessSum += brightness;
                        }
                    }

                    float averageBrightness = brightnessSum / 64f;

                    // Create hash by comparing each pixel to average
                    ulong hash = 0;
                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 8; x++)
                        {
                            var pixel = image[x, y];
                            float brightness = (0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B) / 255f;

                            int bitIndex = y * 8 + x;
                            if (brightness >= averageBrightness)
                            {
                                hash |= (1UL << bitIndex);
                            }
                        }
                    }

                    return hash;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compute perceptual hash");
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Computes the Hamming distance between two 64-bit hashes.
    /// Returns the count of differing bits (0-64).
    /// </summary>
    public int ComputeHammingDistance(ulong hash1, ulong hash2)
    {
        // XOR the hashes to get bits that differ
        ulong xorResult = hash1 ^ hash2;

        // Count the number of 1 bits in the XOR result
        int distance = 0;
        while (xorResult > 0)
        {
            if ((xorResult & 1) == 1)
            {
                distance++;
            }
            xorResult >>= 1;
        }

        return distance;
    }

    public async Task<Guid> FindOrCreateImageAsync(byte[] bytes, string mimeType, int width, int height, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing image: {MimeType}, {Width}x{Height}, {Size} bytes", mimeType, width, height, bytes.Length);

        try
        {
            // Step 1: Compute SHA256 hash for exact-match deduplication
            var checksumSha256 = ComputeSha256Hash(bytes);
            _logger.LogDebug("Computed SHA256 hash: {Hash}", checksumSha256);

            // Step 2: Check if image already exists by exact SHA256 match
            var existingImage = await _dbContext.Images
                .FirstOrDefaultAsync(i => i.ChecksumSha256 == checksumSha256, cancellationToken);

            if (existingImage != null)
            {
                _logger.LogInformation("Found existing image with SHA256: {ImageId}", existingImage.Id);
                // Update last used timestamp
                existingImage.UsedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return existingImage.Id;
            }

            // Step 3: pHash check — compute and scan existing images
            string? pHashHex = default;
            try
            {
                var pHash = await ComputePerceptualHashAsync(bytes, cancellationToken);
                pHashHex = pHash.ToString("X16");

                var candidates = await _dbContext.Images
                    .Where(i => i.PerceptualHash != null)
                    .ToListAsync(cancellationToken);

                foreach (var candidate in candidates)
                {
                    var candidateHash = Convert.ToUInt64(candidate.PerceptualHash, 16);
                    if (ComputeHammingDistance(pHash, candidateHash) <= _pHashThreshold)
                    {
                        _logger.LogInformation("pHash duplicate found: {ImageId} (Hamming ≤ {Threshold})", candidate.Id, _pHashThreshold);
                        candidate.UsedAt = DateTime.UtcNow;
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        return candidate.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "pHash computation skipped — bytes are not a decodable image");
            }

            // Step 4: No duplicate — create new record
            _logger.LogInformation("Creating new image record: {MimeType}, {Width}x{Height}", mimeType, width, height);

            var newImage = new Image
            {
                Id = Guid.NewGuid(),
                ChecksumSha256 = checksumSha256,
                PerceptualHash = pHashHex,
                MimeType = mimeType,
                Width = width,
                Height = height,
                SizeBytes = bytes.Length,
                Content = bytes,
                AddedAt = DateTime.UtcNow,
                UsedAt = DateTime.UtcNow
            };

            _dbContext.Images.Add(newImage);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("New image created: {ImageId}", newImage.Id);

            return newImage.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find or create image record");
            throw;
        }
    }

    public async Task ClearContentAsync(Guid imageId, CancellationToken cancellationToken = default)
    {
        var image = await _dbContext.Images.FindAsync([imageId], cancellationToken);
        if (image is null)
        {
            _logger.LogWarning("Image {ImageId} not found for content clearing", imageId);
            return;
        }

        image.Content = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cleared content for image {ImageId}", imageId);
    }

    public async Task ClearContentBatchAsync(IEnumerable<Guid> imageIds, CancellationToken cancellationToken = default)
    {
        var ids = imageIds.ToList();
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
}
