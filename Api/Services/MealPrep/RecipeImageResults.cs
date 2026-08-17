namespace Api.Services.MealPrep;

/// <summary>
///     Serves a stored recipe image with its conditional-request handling.
///     Shared by the workspace image endpoint and the share-link image endpoint so that both apply the same
///     entity tag, cache-control and width-rendition rules; the two differ only in how they establish that the
///     caller may see the recipe at all.
/// </summary>
public static class RecipeImageResults
{
    public static async Task<IResult> ServeAsync(
        RecipeImageStore recipeImageStore,
        HttpContext httpContext,
        string imageObjectKey,
        int? requestedWidth,
        CancellationToken cancellationToken
    ) {
        // Images are immutable once written — a replacement upload stores a new object key — so the
        // key identifies the bytes. Clients that already hold them get a 304 and no body, and the
        // object is never read from S3 for such a request. The requested width is folded into the
        // key the tag is derived from so that two renditions of the same image never share a tag.
        var servedKey = RecipeImageVariants.ResolveWidth(requestedWidth) is { } width
            ? RecipeImageVariants.KeyForWidth(imageObjectKey, width)
            : imageObjectKey;

        var etag = RecipeImageCache.ETagForObjectKey(servedKey);
        httpContext.Response.GetTypedHeaders().CacheControl = RecipeImageCache.ResponseCacheControl();

        if (RecipeImageCache.IsNotModified(httpContext.Request.GetTypedHeaders(), etag)) {
            httpContext.Response.GetTypedHeaders().ETag = etag;
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        // A response varies by width, and the width is in the query string rather than a header, so
        // no Vary is needed — but a shared cache must not serve one client's rendition to another,
        // which the private cache-control above already prevents.
        var image = await recipeImageStore.OpenAsync(imageObjectKey, requestedWidth, cancellationToken);
        var contentType = RecipeImageUploadConstants.ContentTypeFromObjectKey(image.ObjectKey)
                          ?? "application/octet-stream";

        return TypedResults.File(image.Content, contentType, entityTag: etag);
    }
}
