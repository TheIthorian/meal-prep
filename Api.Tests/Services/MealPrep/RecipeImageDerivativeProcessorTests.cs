using Api.Models;
using Api.Services.MealPrep;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;

namespace Api.Tests.Services.MealPrep;

public class RecipeImageDerivativeProcessorTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessAsync_ShouldWriteTheRenditionAtTheJobsWidth() {
        var storage = new FakeS3StorageService();
        await using var source = RecipeImageTestFactory.CreatePng(1200, 900);
        var key = await storage.UploadFileAsync(source, "dinner.png", "image/png");

        await BuildProcessor(storage).ProcessAsync(RecipeImageDerivativeJob.CreateNew(key, 400, Now));

        var renditionKey = RecipeImageVariants.KeyForWidth(key, 400);
        Assert.Contains(renditionKey, storage.Keys);

        using var rendition = Image.Load(storage.Read(renditionKey));
        Assert.Equal(400, rendition.Width);
    }

    [Fact]
    public async Task ProcessAsync_ShouldMakeTheRenditionSmallerOnDiskThanTheOriginal() {
        var storage = new FakeS3StorageService();
        await using var source = RecipeImageTestFactory.CreatePng(1200, 900);
        var key = await storage.UploadFileAsync(source, "dinner.png", "image/png");

        await BuildProcessor(storage).ProcessAsync(RecipeImageDerivativeJob.CreateNew(key, 400, Now));

        var original = storage.Read(key).Length;
        var rendition = storage.Read(RecipeImageVariants.KeyForWidth(key, 400)).Length;

        Assert.True(rendition < original, $"rendition was {rendition} bytes, original was {original}");
    }

    [Fact]
    public async Task ProcessAsync_ShouldThrowWhenTheStoredObjectIsNotAnImage() {
        var storage = new FakeS3StorageService();
        var key = await storage.UploadFileAsync(
            new MemoryStream("not an image"u8.ToArray()),
            "broken.png",
            "image/png"
        );

        await Assert.ThrowsAnyAsync<Exception>(
            () => BuildProcessor(storage).ProcessAsync(RecipeImageDerivativeJob.CreateNew(key, 400, Now))
        );

        Assert.DoesNotContain(RecipeImageVariants.KeyForWidth(key, 400), storage.Keys);
    }

    private static RecipeImageDerivativeProcessor BuildProcessor(FakeS3StorageService storage) {
        return new RecipeImageDerivativeProcessor(
            storage,
            new RecipeImageProcessingService(),
            NullLogger<RecipeImageDerivativeProcessor>.Instance
        );
    }
}
