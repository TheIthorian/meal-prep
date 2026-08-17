using Api.Logging;

namespace Api.Services.MealPrep;

/// <summary>
///     Owns the storage side of a recipe image: writing the uploaded original, queueing its
///     renditions, reading back the rendition that fits a request, and deleting the whole set.
/// </summary>
/// <remarks>
///     Nothing here decodes or re-encodes an image. Resizing is CPU-bound work that used to run
///     inline on the upload and import paths, where a batch import could saturate the machine and
///     slow every unrelated request; it now happens on
///     <see cref="RecipeImageDerivativeWorker" />, which is bounded to a couple of threads. The
///     cost of that is a window after an upload where the renditions do not exist yet, which the
///     read path covers by serving the original.
/// </remarks>
public sealed class RecipeImageStore(
    IS3StorageService s3StorageService,
    IRecipeImageDerivativeQueue derivativeQueue,
    ILogger<RecipeImageStore> logger
)
{
    /// <summary>
    ///     Stores the image exactly as uploaded and queues a rendition at each of
    ///     <see cref="RecipeImageVariants.Widths" />. Returns the object key, which is what a recipe
    ///     records. Returns as soon as the bytes are persisted — no resizing happens first.
    /// </summary>
    public async Task<string> StoreAsync(
        Stream sourceStream,
        string originalFileName,
        string? contentType = null,
        CancellationToken cancellationToken = default
    ) {
        // The caller's content type wins when it has one (an upload's form part, or what the import
        // download resolved); the file name is only a fallback for inferring it.
        var resolvedContentType = RecipeImageUploadConstants.IsAllowedContentType(contentType)
            ? contentType!
            : RecipeImageUploadConstants.ContentTypeFromObjectKey(originalFileName)
              ?? "application/octet-stream";
        var fileName = RecipeImageUploadConstants.FileNameForUpload(originalFileName, resolvedContentType);

        var objectKey = await s3StorageService.UploadFileAsync(sourceStream, fileName, resolvedContentType);
        await derivativeQueue.EnqueueAllWidthsAsync(objectKey, cancellationToken);

        return objectKey;
    }

    /// <summary>
    ///     The key that should actually be served for a requested width: the rendition when it
    ///     exists, otherwise the original. A miss queues the rendition, which covers both an image
    ///     whose renditions have not been generated yet and one stored before renditions existed.
    /// </summary>
    /// <remarks>
    ///     Separate from <see cref="OpenKeyAsync" /> so a caller can build a cache tag from the key
    ///     it is about to serve and answer a conditional request without reading the object at all.
    ///     A tag naming a rendition that was not actually served would have clients cache the
    ///     original under it for as long as the response stays fresh.
    /// </remarks>
    public async Task<string> ResolveServedKeyAsync(
        string sourceObjectKey,
        int? requestedWidth,
        CancellationToken cancellationToken = default
    ) {
        var width = RecipeImageVariants.ResolveWidth(requestedWidth);
        var renditionKey = RecipeImageVariants.KeyForWidth(sourceObjectKey, width);

        try {
            if (await s3StorageService.ObjectExistsAsync(renditionKey)) return renditionKey;
        } catch (Exception exception) {
            using var scope = logger.BeginPropertyScope(("recipe.image.key", renditionKey));
            logger.LogWarning(exception, "Could not check for a recipe image rendition; serving the original");
            return sourceObjectKey;
        }

        await derivativeQueue.EnqueueWidthAsync(sourceObjectKey, width, cancellationToken);
        return sourceObjectKey;
    }

    /// <summary>Opens a specific object key, as returned by <see cref="ResolveServedKeyAsync" />.</summary>
    public async Task<RecipeImageContent> OpenKeyAsync(string objectKey) {
        var stream = await s3StorageService.DownloadFileAsync(objectKey);
        return new RecipeImageContent(stream, objectKey);
    }

    /// <summary>
    ///     Opens the rendition covering <paramref name="requestedWidth" />, falling back to the
    ///     original while the rendition is still pending.
    /// </summary>
    public async Task<RecipeImageContent> OpenAsync(
        string sourceObjectKey,
        int? requestedWidth,
        CancellationToken cancellationToken = default
    ) {
        var servedKey = await ResolveServedKeyAsync(sourceObjectKey, requestedWidth, cancellationToken);
        return await OpenKeyAsync(servedKey);
    }

    /// <summary>
    ///     Deletes the image, every rendition of it, and any rendition still queued. Renditions are
    ///     deleted best-effort: one may not exist yet, and a delete that fails would otherwise leave
    ///     the recipe pointing at an object that is already gone.
    /// </summary>
    public async Task DeleteAsync(string sourceObjectKey, CancellationToken cancellationToken = default) {
        await derivativeQueue.RemoveForImageAsync(sourceObjectKey, cancellationToken);

        foreach (var renditionKey in RecipeImageVariants.AllKeysForImage(sourceObjectKey)) {
            try {
                await s3StorageService.DeleteFileAsync(renditionKey);
            } catch (Exception exception) {
                using var scope = logger.BeginPropertyScope(("recipe.image.key", renditionKey));
                logger.LogWarning(exception, "Could not delete a recipe image rendition");
            }
        }

        await s3StorageService.DeleteFileAsync(sourceObjectKey);
    }
}

/// <summary>
///     An image's bytes together with the key they were actually served from, which the caller
///     needs in order to build an entity tag that distinguishes renditions from one another.
/// </summary>
public sealed record RecipeImageContent(Stream Content, string ObjectKey);
