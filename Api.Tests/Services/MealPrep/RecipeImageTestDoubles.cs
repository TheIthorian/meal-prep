using Api.Data;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Api.Tests.Services.MealPrep;

/// <summary>
///     In-memory object storage shared by the recipe image tests.
/// </summary>
internal sealed class FakeS3StorageService : IS3StorageService
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Keys => files.Keys;

    public int UploadCount { get; private set; }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType) {
        var key = $"{Guid.NewGuid():N}_{fileName}";
        await UploadFileAtKeyAsync(fileStream, key, contentType);
        return key;
    }

    public async Task UploadFileAtKeyAsync(Stream fileStream, string key, string contentType) {
        using var memory = new MemoryStream();
        await fileStream.CopyToAsync(memory);
        files[key] = memory.ToArray();
        UploadCount++;
    }

    public Task<Stream> DownloadFileAsync(string s3Key) {
        if (!files.TryGetValue(s3Key, out var payload))
            throw new InvalidOperationException($"No object at '{s3Key}'.");

        return Task.FromResult<Stream>(new MemoryStream(payload, false));
    }

    public Task<Stream?> TryDownloadFileAsync(string s3Key) {
        return Task.FromResult(files.TryGetValue(s3Key, out var payload)
            ? new MemoryStream(payload, false)
            : null as Stream
        );
    }

    public Task<bool> ObjectExistsAsync(string s3Key) {
        return Task.FromResult(files.ContainsKey(s3Key));
    }

    public Task DeleteFileAsync(string s3Key) {
        files.Remove(s3Key);
        return Task.CompletedTask;
    }

    public byte[] Read(string s3Key) {
        return files[s3Key];
    }
}

/// <summary>
///     A clock the tests move by hand, so retry backoff and stale claims can be exercised without
///     waiting for real time to pass.
/// </summary>
internal sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() {
        return Now;
    }

    public void Advance(TimeSpan amount) {
        Now += amount;
    }
}

internal static class RecipeImageTestFactory
{
    public static ApiDbContext CreateInMemoryDatabase() {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase($"recipe-images-{Guid.NewGuid():N}")
            .Options;

        return new ApiDbContext(options);
    }

    public static MemoryStream CreatePng(int width, int height) {
        using var image = new Image<Rgba32>(width, height);

        // A flat fill compresses to almost nothing, which would make the size comparisons
        // meaningless, so vary the pixels.
        image.ProcessPixelRows(accessor => {
            for (var y = 0; y < accessor.Height; y++) {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++) {
                    row[x] = new Rgba32((byte)(x % 256), (byte)(y % 256), (byte)((x * y) % 256));
                }
            }
        });

        var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        stream.Position = 0;
        return stream;
    }
}
