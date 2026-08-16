using Api.Services;
using Api.Services.MealPrep;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Api.Tests.Services.MealPrep;

public class RecipeImageStoreTests
{
    [Fact]
    public async Task StoreAsync_ShouldWriteTheFullSizeImageAndOneRenditionPerWidth() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage);

        await using var source = CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");

        Assert.Contains(key, storage.Keys);
        foreach (var width in RecipeImageVariants.Widths) {
            Assert.Contains(RecipeImageVariants.KeyForWidth(key, width), storage.Keys);
        }
    }

    [Fact]
    public async Task StoreAsync_ShouldMakeEachRenditionNarrowerThanTheLast() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage);

        await using var source = CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");

        foreach (var width in RecipeImageVariants.Widths) {
            using var rendition = Image.Load(storage.Read(RecipeImageVariants.KeyForWidth(key, width)));
            Assert.Equal(width, rendition.Width);
        }
    }

    [Fact]
    public async Task StoreAsync_ShouldMakeRenditionsSmallerOnDiskThanTheFullSizeImage() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage);

        await using var source = CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");

        var fullSize = storage.Read(key).Length;
        var smallest = storage.Read(RecipeImageVariants.KeyForWidth(key, RecipeImageVariants.Widths[0])).Length;

        Assert.True(smallest < fullSize, $"rendition was {smallest} bytes, full size was {fullSize}");
    }

    [Fact]
    public async Task OpenAsync_ShouldServeTheRenditionCoveringTheRequestedWidth() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage);

        await using var source = CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");

        var image = await store.OpenAsync(key, 400);

        Assert.Equal(RecipeImageVariants.KeyForWidth(key, 400), image.ObjectKey);
    }

    [Fact]
    public async Task OpenAsync_ShouldServeTheFullSizeImageWhenNoWidthIsRequested() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage);

        await using var source = CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");

        var image = await store.OpenAsync(key, null);

        Assert.Equal(key, image.ObjectKey);
    }

    /// <summary>
    ///     Images uploaded before renditions existed have none, which is the state the whole
    ///     library is in when this ships.
    /// </summary>
    [Fact]
    public async Task OpenAsync_ShouldBackfillARenditionForAnImageStoredWithoutOne() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage);

        await using var legacy = CreatePng(1200, 900);
        var key = await storage.UploadFileAsync(legacy, "legacy.webp", "image/webp");
        var renditionKey = RecipeImageVariants.KeyForWidth(key, 400);
        Assert.DoesNotContain(renditionKey, storage.Keys);

        var image = await store.OpenAsync(key, 400);

        Assert.Equal(renditionKey, image.ObjectKey);
        Assert.Contains(renditionKey, storage.Keys);
    }

    [Fact]
    public async Task OpenAsync_ShouldReadAnAlreadyBackfilledRenditionRatherThanRebuildingIt() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage);

        await using var legacy = CreatePng(1200, 900);
        var key = await storage.UploadFileAsync(legacy, "legacy.webp", "image/webp");

        await store.OpenAsync(key, 400);
        var uploadsAfterFirstRead = storage.UploadCount;
        await store.OpenAsync(key, 400);

        Assert.Equal(uploadsAfterFirstRead, storage.UploadCount);
    }

    [Fact]
    public async Task OpenAsync_ShouldFallBackToTheFullSizeImageWhenTheRenditionCannotBeBuilt() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage);

        var key = await storage.UploadFileAsync(
            new MemoryStream("not an image"u8.ToArray()),
            "broken.webp",
            "image/webp"
        );

        var image = await store.OpenAsync(key, 400);

        Assert.Equal(key, image.ObjectKey);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveTheImageAndEveryRendition() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage);

        await using var source = CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");

        await store.DeleteAsync(key);

        Assert.Empty(storage.Keys);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveAnImageThatHasNoRenditions() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage);

        await using var legacy = CreatePng(400, 300);
        var key = await storage.UploadFileAsync(legacy, "legacy.webp", "image/webp");

        await store.DeleteAsync(key);

        Assert.Empty(storage.Keys);
    }

    private static RecipeImageStore BuildStore(IS3StorageService storage) {
        return new RecipeImageStore(
            storage,
            new RecipeImageProcessingService(),
            NullLogger<RecipeImageStore>.Instance
        );
    }

    private static MemoryStream CreatePng(int width, int height) {
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

    private sealed class FakeS3StorageService : IS3StorageService
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

        public Task DeleteFileAsync(string s3Key) {
            files.Remove(s3Key);
            return Task.CompletedTask;
        }

        public byte[] Read(string s3Key) {
            return files[s3Key];
        }
    }
}
