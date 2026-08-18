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
    ///     Copies an existing image and its renditions to fresh keys, returning the new full-size
    ///     key, or null when the source image is gone. Used when a recipe is duplicated into another
    ///     workspace, where the bytes are already optimized and only need a second owner.
    /// </summary>
    /// <remarks>
    ///     The renditions are copied rather than left to be regenerated on demand. Without this an
    ///     imported recipe starts with none, so the first viewer of each width pays a download,
    ///     resize and write inside their own request — for a collection import that is two resizes
    ///     per recipe, charged to whoever happens to open them first. Copies run inside the storage
    ///     service, so none of it travels through the API.
    ///     A rendition that fails to copy is not fatal: images predating renditions have none, and
    ///     <see cref="OpenAsync" /> still backfills a miss.
    /// </remarks>
    public async Task<string?> CopyAsync(string sourceFullSizeKey, CancellationToken cancellationToken = default) {
        var destinationKey = BuildCopyKey(sourceFullSizeKey);

        if (!await s3StorageService.CopyFileAsync(sourceFullSizeKey, destinationKey)) {
            using var missingScope = logger.BeginPropertyScope(("recipe.image.key", sourceFullSizeKey));
            logger.LogWarning("Could not copy a recipe image because the source object is missing");
            return null;
        }

        foreach (var width in RecipeImageVariants.Widths) {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceRenditionKey = RecipeImageVariants.KeyForWidth(sourceFullSizeKey, width);
            var destinationRenditionKey = RecipeImageVariants.KeyForWidth(destinationKey, width);

            try {
                await s3StorageService.CopyFileAsync(sourceRenditionKey, destinationRenditionKey);
            } catch (Exception exception) {
                using var scope = logger.BeginPropertyScope(
                    ("recipe.image.key", sourceRenditionKey),
                    ("recipe.image.width", width)
                );
                logger.LogWarning(exception, "Could not copy a recipe image rendition");
            }
        }

        return destinationKey;
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

    /// <summary>
    ///     A fresh key for a copy, keeping the original file name — and so its extension, which is
    ///     what <see cref="RecipeImageVariants.KeyForWidth" /> derives rendition keys from — behind
    ///     a new unique prefix, matching the shape <see cref="IS3StorageService.UploadFileAsync" />
    ///     generates.
    /// </summary>
    private static string BuildCopyKey(string sourceFullSizeKey) {
        var separatorIndex = sourceFullSizeKey.IndexOf('_');
        var fileName = separatorIndex >= 0 ? sourceFullSizeKey[(separatorIndex + 1)..] : sourceFullSizeKey;
        return $"{Guid.NewGuid()}_{fileName}";
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
