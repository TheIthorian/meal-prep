namespace Api.Services.MealPrep;

/// <summary>
///     The rendition widths a recipe image is stored at, and how a rendition's object key is
///     derived from the full-size key.
/// </summary>
/// <remarks>
///     Keys are derived rather than recorded against the recipe so that no schema change or
///     migration is needed: a rendition either exists in storage under its derived key or it does
///     not, and the read path falls back to the full-size object either way.
/// </remarks>
public static class RecipeImageVariants
{
    /// <summary>
    ///     Widths chosen for how the UI actually lays images out: cards render at roughly 400 CSS
    ///     pixels (800 on a 2x display) and the recipe detail hero at roughly 900 (capped by
    ///     <see cref="RecipeImageUploadConstants.MaxPixelDimension" /> on a 2x display).
    /// </summary>
    public static readonly IReadOnlyList<int> Widths = [400, 800];

    /// <summary>
    ///     Snaps a requested width to the smallest stored rendition that covers it. Returns null
    ///     when the request is absent or larger than every rendition, meaning the full-size object
    ///     should be served.
    /// </summary>
    public static int? ResolveWidth(int? requestedWidth) {
        if (requestedWidth is null || requestedWidth <= 0) return null;

        foreach (var width in Widths) {
            if (requestedWidth <= width) return width;
        }

        return null;
    }

    /// <summary>
    ///     The object key a rendition is stored under, e.g. <c>abc_photo.webp</c> at width 400
    ///     becomes <c>abc_photo.w400.webp</c>.
    /// </summary>
    public static string KeyForWidth(string fullSizeObjectKey, int width) {
        var extension = Path.GetExtension(fullSizeObjectKey);
        var withoutExtension = fullSizeObjectKey[..^extension.Length];
        return $"{withoutExtension}.w{width}{extension}";
    }

    /// <summary>
    ///     Every rendition key for a full-size key, for deleting an image and all of its renditions.
    /// </summary>
    public static IEnumerable<string> AllKeysForImage(string fullSizeObjectKey) {
        return Widths.Select(width => KeyForWidth(fullSizeObjectKey, width));
    }
}
