using Api.Models;
using Api.Services;
using Api.Services.MealPrep;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Services.MealPrep;

public class RecipeImageStoreTests
{
    [Fact]
    public async Task StoreAsync_ShouldWriteTheOriginalAndNothingElse() {
        var storage = new FakeS3StorageService();
        var queue = new RecordingDerivativeQueue();
        var store = BuildStore(storage, queue);

        await using var source = RecipeImageTestFactory.CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");

        Assert.Equal([key], storage.Keys);
    }

    /// <summary>
    ///     The point of the change: an upload or import returns as soon as the bytes are persisted,
    ///     leaving the CPU-bound resizing to the worker.
    /// </summary>
    [Fact]
    public async Task StoreAsync_ShouldQueueEveryRenditionRatherThanResizingInline() {
        var storage = new FakeS3StorageService();
        var queue = new RecordingDerivativeQueue();
        var store = BuildStore(storage, queue);

        await using var source = RecipeImageTestFactory.CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");

        Assert.Equal([key], queue.EnqueuedImages);
        Assert.Empty(queue.EnqueuedWidths);
    }

    [Fact]
    public async Task StoreAsync_ShouldStoreTheBytesExactlyAsUploaded() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage, new RecordingDerivativeQueue());

        await using var source = RecipeImageTestFactory.CreatePng(1200, 900);
        var original = source.ToArray();
        source.Position = 0;

        var key = await store.StoreAsync(source, "dinner.png");

        Assert.Equal(original, storage.Read(key));
    }

    [Fact]
    public async Task ResolveServedKeyAsync_ShouldServeTheRenditionCoveringTheRequestedWidth() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage, new RecordingDerivativeQueue());

        await using var source = RecipeImageTestFactory.CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");

        var renditionKey = RecipeImageVariants.KeyForWidth(key, 400);
        await storage.UploadFileAtKeyAsync(new MemoryStream([1, 2, 3]), renditionKey, "image/webp");

        Assert.Equal(renditionKey, await store.ResolveServedKeyAsync(key, 400));
    }

    [Fact]
    public async Task ResolveServedKeyAsync_ShouldServeTheOriginalWhileTheRenditionIsStillPending() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage, new RecordingDerivativeQueue());

        await using var source = RecipeImageTestFactory.CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");

        Assert.Equal(key, await store.ResolveServedKeyAsync(key, 400));
    }

    /// <summary>
    ///     Images uploaded before renditions existed have none, and neither does one whose queued
    ///     job was lost. A read re-queues it rather than resizing on the request thread.
    /// </summary>
    [Fact]
    public async Task ResolveServedKeyAsync_ShouldQueueAMissingRenditionRatherThanBuildingIt() {
        var storage = new FakeS3StorageService();
        var queue = new RecordingDerivativeQueue();
        var store = BuildStore(storage, queue);

        await using var legacy = RecipeImageTestFactory.CreatePng(1200, 900);
        var key = await storage.UploadFileAsync(legacy, "legacy.webp", "image/webp");
        var uploadsBefore = storage.UploadCount;

        var served = await store.ResolveServedKeyAsync(key, 400);

        Assert.Equal(key, served);
        Assert.Equal(uploadsBefore, storage.UploadCount);
        Assert.Contains((key, 400), queue.EnqueuedWidths);
    }

    [Fact]
    public async Task OpenAsync_ShouldReadTheRenditionOnceItExists() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage, new RecordingDerivativeQueue());

        await using var source = RecipeImageTestFactory.CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");
        var renditionKey = RecipeImageVariants.KeyForWidth(key, 400);
        await storage.UploadFileAtKeyAsync(new MemoryStream([1, 2, 3]), renditionKey, "image/webp");

        var image = await store.OpenAsync(key, 400);

        Assert.Equal(renditionKey, image.ObjectKey);
    }

    [Fact]
    public async Task ResolveServedKeyAsync_ShouldFallBackToTheOriginalWhenStorageCannotBeChecked() {
        var store = BuildStore(new ThrowingS3StorageService(), new RecordingDerivativeQueue());

        Assert.Equal("some-key.webp", await store.ResolveServedKeyAsync("some-key.webp", 400));
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveTheImageEveryRenditionAndAnyQueuedWork() {
        var storage = new FakeS3StorageService();
        var queue = new RecordingDerivativeQueue();
        var store = BuildStore(storage, queue);

        await using var source = RecipeImageTestFactory.CreatePng(1200, 900);
        var key = await store.StoreAsync(source, "dinner.png");
        foreach (var renditionKey in RecipeImageVariants.AllKeysForImage(key)) {
            await storage.UploadFileAtKeyAsync(new MemoryStream([1, 2, 3]), renditionKey, "image/webp");
        }

        await store.DeleteAsync(key);

        Assert.Empty(storage.Keys);
        Assert.Contains(key, queue.RemovedImages);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveAnImageThatHasNoRenditions() {
        var storage = new FakeS3StorageService();
        var store = BuildStore(storage, new RecordingDerivativeQueue());

        await using var legacy = RecipeImageTestFactory.CreatePng(400, 300);
        var key = await storage.UploadFileAsync(legacy, "legacy.webp", "image/webp");

        await store.DeleteAsync(key);

        Assert.Empty(storage.Keys);
    }

    private static RecipeImageStore BuildStore(IS3StorageService storage, IRecipeImageDerivativeQueue queue) {
        return new RecipeImageStore(storage, queue, NullLogger<RecipeImageStore>.Instance);
    }

    private sealed class RecordingDerivativeQueue : IRecipeImageDerivativeQueue
    {
        public List<string> EnqueuedImages { get; } = [];

        public List<(string Key, int Width)> EnqueuedWidths { get; } = [];

        public List<string> RemovedImages { get; } = [];

        public Task EnqueueAllWidthsAsync(string sourceObjectKey, CancellationToken cancellationToken = default) {
            EnqueuedImages.Add(sourceObjectKey);
            return Task.CompletedTask;
        }

        public Task EnqueueWidthAsync(
            string sourceObjectKey,
            int width,
            CancellationToken cancellationToken = default
        ) {
            EnqueuedWidths.Add((sourceObjectKey, width));
            return Task.CompletedTask;
        }

        public Task<RecipeImageDerivativeJob?> ClaimNextAsync(CancellationToken cancellationToken = default) {
            return Task.FromResult<RecipeImageDerivativeJob?>(null);
        }

        public Task CompleteAsync(RecipeImageDerivativeJob job, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task FailAsync(
            RecipeImageDerivativeJob job,
            Exception exception,
            CancellationToken cancellationToken = default
        ) {
            return Task.CompletedTask;
        }

        public Task RemoveForImageAsync(string sourceObjectKey, CancellationToken cancellationToken = default) {
            RemovedImages.Add(sourceObjectKey);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingS3StorageService : IS3StorageService
    {
        public Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType) {
            throw new InvalidOperationException("storage is down");
        }

        public Task UploadFileAtKeyAsync(Stream fileStream, string key, string contentType) {
            throw new InvalidOperationException("storage is down");
        }

        public Task<Stream> DownloadFileAsync(string s3Key) {
            throw new InvalidOperationException("storage is down");
        }

        public Task<Stream?> TryDownloadFileAsync(string s3Key) {
            throw new InvalidOperationException("storage is down");
        }

        public Task<bool> ObjectExistsAsync(string s3Key) {
            throw new InvalidOperationException("storage is down");
        }

        public Task DeleteFileAsync(string s3Key) {
            throw new InvalidOperationException("storage is down");
        }
    }
}
