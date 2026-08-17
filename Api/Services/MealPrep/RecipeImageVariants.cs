namespace Api.Services.MealPrep;

/// <summary>
///     The rendition widths a recipe image is stored at, and how a rendition's object key is
///     derived from the key of the image as uploaded.
/// </summary>
/// <remarks>
///     Keys are derived rather than recorded against the recipe so a rendition either exists in
///     storage under its derived key or it does not, and the read path falls back to the original
///     object either way. That fallback is what lets renditions be generated after the upload
///     request has already returned.
/// </remarks>
public static class RecipeImageVariants
{
    /// <summary>
    ///     Widths chosen for how the UI actually lays images out: cards render at roughly 400 CSS
    ///     pixels (800 on a 2x display) and the recipe detail hero at roughly 900 (capped by
    ///     <see cref="RecipeImageUploadConstants.MaxPixelDimension" /> on a 2x display). The widest
    ///     entry doubles as the web-optimized copy of the original, which is what a request with no
    ///     width asks for.
    /// </summary>
    public static readonly IReadOnlyList<int> Widths =
        [400, 800, RecipeImageUploadConstants.MaxPixelDimension];

    /// <summary>The widest rendition, served when a request does not ask for a size.</summary>
    public static int FullWidth => Widths[^1];

    /// <summary>
    ///     Snaps a requested width to the smallest stored rendition that covers it. A request with
    ///     no width, or one wider than every rendition, resolves to <see cref="FullWidth" />: the
    ///     original itself is only ever served as a fallback while a rendition is still pending.
    /// </summary>
    public static int ResolveWidth(int? requestedWidth) {
        if (requestedWidth is null || requestedWidth <= 0) return FullWidth;

        foreach (var width in Widths) {
            if (requestedWidth <= width) return width;
        }

        return FullWidth;
    }

    /// <summary>
    ///     The object key a rendition is stored under, e.g. <c>abc_photo.png</c> at width 400
    ///     becomes <c>abc_photo.w400.webp</c>. Renditions are always WebP, whatever the original
    ///     was uploaded as.
    /// </summary>
    public static string KeyForWidth(string sourceObjectKey, int width) {
        var extension = Path.GetExtension(sourceObjectKey);
        var withoutExtension = sourceObjectKey[..^extension.Length];
        return $"{withoutExtension}.w{width}{RecipeImageUploadConstants.OptimizedExtension}";
    }

    /// <summary>
    ///     Every rendition key for an image, for deleting an image and all of its renditions.
    /// </summary>
    public static IEnumerable<string> AllKeysForImage(string sourceObjectKey) {
        return Widths.Select(width => KeyForWidth(sourceObjectKey, width));
    }
}
