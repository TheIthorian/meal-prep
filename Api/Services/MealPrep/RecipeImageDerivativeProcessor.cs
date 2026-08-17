using Api.Logging;
using Api.Models;

namespace Api.Services.MealPrep;

/// <summary>
///     Turns one queued job into the stored rendition it describes.
/// </summary>
public interface IRecipeImageDerivativeProcessor
{
    Task ProcessAsync(RecipeImageDerivativeJob job, CancellationToken cancellationToken = default);
}

/// <summary>
///     Reads the stored original, resizes it to the job's width and writes the rendition under the
///     derived key. Runs only on the worker, never on a request thread.
/// </summary>
public sealed class RecipeImageDerivativeProcessor(
    IS3StorageService s3StorageService,
    RecipeImageProcessingService recipeImageProcessingService,
    ILogger<RecipeImageDerivativeProcessor> logger
) : IRecipeImageDerivativeProcessor
{
    public async Task ProcessAsync(RecipeImageDerivativeJob job, CancellationToken cancellationToken = default) {
        using var scope = logger.BeginPropertyScope(
            ("recipe.image.key", job.SourceObjectKey),
            ("recipe.image.width", job.TargetWidth),
            ("recipe.image.attempts", job.Attempts)
        );

        logger.LogInformation("Generating a recipe image rendition");

        await using var source = await s3StorageService.DownloadFileAsync(job.SourceObjectKey);
        await using var buffered = new MemoryStream();
        await source.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        var renditionData = await recipeImageProcessingService.ResizeToWidthAsync(
            buffered,
            job.TargetWidth,
            cancellationToken
        );

        await using var renditionStream = new MemoryStream(renditionData);
        await s3StorageService.UploadFileAtKeyAsync(
            renditionStream,
            RecipeImageVariants.KeyForWidth(job.SourceObjectKey, job.TargetWidth),
            RecipeImageUploadConstants.OptimizedContentType
        );

        logger.LogInformation("Generated a recipe image rendition");
    }
}
