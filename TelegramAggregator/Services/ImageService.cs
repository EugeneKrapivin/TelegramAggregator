using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using TelegramAggregator.Data;
using TelegramAggregator.Data.Entities;

namespace TelegramAggregator.Services;

public class ImageService : IImageService
{
    private readonly ILogger<ImageService> _logger;
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _dbContext;

    public ImageService(ILogger<ImageService> logger, AppDbContext dbContext)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _dbContext = dbContext;
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

    public async Task<string> ComputeSha256HashAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(bytes);
                return Convert.ToHexString(hashedBytes);
            }
        }, cancellationToken);
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

            // Step 3: Image not found, create new record
            _logger.LogInformation("Creating new image record: {MimeType}, {Width}x{Height}", mimeType, width, height);

            var newImage = new Data.Entities.Image
            {
                Id = Guid.NewGuid(),
                ChecksumSha256 = checksumSha256,
                MimeType = mimeType,
                Width = width,
                Height = height,
                SizeBytes = bytes.Length,
                ContentBase64 = Convert.ToBase64String(bytes),
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
}
