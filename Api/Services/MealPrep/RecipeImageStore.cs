using Api.Logging;

namespace Api.Services.MealPrep;

/// <summary>
///     Owns the storage side of a recipe image: optimizing it, writing it and its renditions,
///     reading back the rendition that fits a request, and deleting the whole set.
/// </summary>
/// <remarks>
///     Before this existed, four call sites (photo upload, recipe import, recipe update and the MCP
///     tool) each repeated the optimize-upload-delete-old sequence inline, which is why renditions
///     are introduced here rather than at each of them.
/// </remarks>
public sealed class RecipeImageStore(
    IS3StorageService s3StorageService,
    RecipeImageProcessingService recipeImageProcessingService,
    ILogger<RecipeImageStore> logger
)
{
    /// <summary>
    ///     Optimizes the image, stores it, and stores a rendition at each of
    ///     <see cref="RecipeImageVariants.Widths" />. Returns the full-size object key, which is
    ///     what a recipe records.
    /// </summary>
    public async Task<string> StoreAsync(
        Stream sourceStream,
        string originalFileName,
        CancellationToken cancellationToken = default
    ) {
        var optimized = await recipeImageProcessingService.OptimizeForWebAsync(
            sourceStream,
            originalFileName,
            cancellationToken
        );

        await using var optimizedStream = new MemoryStream(optimized.Data);
        var fullSizeKey = await s3StorageService.UploadFileAsync(
            optimizedStream,
            optimized.FileName,
            optimized.ContentType
        );

        foreach (var width in RecipeImageVariants.Widths) {
            await using var sourceForWidth = new MemoryStream(optimized.Data);
            var renditionData = await recipeImageProcessingService.ResizeToWidthAsync(
                sourceForWidth,
                width,
                cancellationToken
            );

            await using var renditionStream = new MemoryStream(renditionData);
            await s3StorageService.UploadFileAtKeyAsync(
                renditionStream,
                RecipeImageVariants.KeyForWidth(fullSizeKey, width),
                RecipeImageUploadConstants.OptimizedContentType
            );
        }

        return fullSizeKey;
    }

    /// <summary>
    ///     Opens the rendition covering <paramref name="requestedWidth" />, falling back to the
    ///     full-size object.
    /// </summary>
    /// <remarks>
    ///     Images stored before renditions existed have none, so a miss generates the rendition and
    ///     writes it back before serving it: the first request for a given width pays the resize
    ///     and every later one is a plain read. That backfills the existing library through normal
    ///     traffic instead of needing a migration job. A failure to generate is not fatal — the
    ///     full-size object is served instead, exactly as before this change.
    /// </remarks>
    public async Task<RecipeImageContent> OpenAsync(
        string fullSizeObjectKey,
        int? requestedWidth,
        CancellationToken cancellationToken = default
    ) {
        var width = RecipeImageVariants.ResolveWidth(requestedWidth);
        if (width is null) return await OpenFullSizeAsync(fullSizeObjectKey);

        var renditionKey = RecipeImageVariants.KeyForWidth(fullSizeObjectKey, width.Value);

        var existing = await s3StorageService.TryDownloadFileAsync(renditionKey);
        if (existing is not null) return new RecipeImageContent(existing, renditionKey);

        try {
            return await GenerateRenditionAsync(fullSizeObjectKey, renditionKey, width.Value, cancellationToken);
        } catch (Exception exception) {
            using var scope = logger.BeginPropertyScope(
                ("recipe.image.key", fullSizeObjectKey),
                ("recipe.image.width", width.Value)
            );
            logger.LogWarning(exception, "Could not build a recipe image rendition; serving full size");
            return await OpenFullSizeAsync(fullSizeObjectKey);
        }
    }

    /// <summary>
    ///     Deletes the image and every rendition of it. Renditions are deleted best-effort: an
    ///     image predating this change has none, and a delete that fails would otherwise leave the
    ///     recipe pointing at an object that is already gone.
    /// </summary>
    public async Task DeleteAsync(string fullSizeObjectKey, CancellationToken cancellationToken = default) {
        foreach (var renditionKey in RecipeImageVariants.AllKeysForImage(fullSizeObjectKey)) {
            try {
                await s3StorageService.DeleteFileAsync(renditionKey);
            } catch (Exception exception) {
                using var scope = logger.BeginPropertyScope(("recipe.image.key", renditionKey));
                logger.LogWarning(exception, "Could not delete a recipe image rendition");
            }
        }

        await s3StorageService.DeleteFileAsync(fullSizeObjectKey);
    }

    private async Task<RecipeImageContent> GenerateRenditionAsync(
        string fullSizeObjectKey,
        string renditionKey,
        int width,
        CancellationToken cancellationToken
    ) {
        await using var fullSize = await s3StorageService.DownloadFileAsync(fullSizeObjectKey);
        await using var buffered = new MemoryStream();
        await fullSize.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        var renditionData = await recipeImageProcessingService.ResizeToWidthAsync(buffered, width, cancellationToken);

        await using (var uploadStream = new MemoryStream(renditionData)) {
            await s3StorageService.UploadFileAtKeyAsync(
                uploadStream,
                renditionKey,
                RecipeImageUploadConstants.OptimizedContentType
            );
        }

        return new RecipeImageContent(new MemoryStream(renditionData), renditionKey);
    }

    private async Task<RecipeImageContent> OpenFullSizeAsync(string fullSizeObjectKey) {
        var stream = await s3StorageService.DownloadFileAsync(fullSizeObjectKey);
        return new RecipeImageContent(stream, fullSizeObjectKey);
    }
}

/// <summary>
///     An image's bytes together with the key they were actually served from, which the caller
///     needs in order to build an entity tag that distinguishes renditions from one another.
/// </summary>
public sealed record RecipeImageContent(Stream Content, string ObjectKey);
