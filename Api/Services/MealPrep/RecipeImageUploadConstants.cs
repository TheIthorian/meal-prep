namespace Api.Services.MealPrep;

/// <summary>
///     Allowed recipe image uploads and size limits.
/// </summary>
public static class RecipeImageUploadConstants
{
    public const long MaxBytes = 15 * 1024 * 1024;

    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
    };

    public static bool IsAllowedContentType(string? contentType) {
        return contentType is not null && AllowedContentTypes.Contains(contentType);
    }

    /// <summary>
    ///     Infers an image content type from a URL path (e.g. Azure Blob often returns application/octet-stream).
    /// </summary>
    public static string? TryInferImageContentTypeFromPath(string? path) {
        var ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
        return ext switch {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => null,
        };
    }

    /// <summary>
    ///     Resolves the effective image content type from response headers and URL path.
    /// </summary>
    public static string? ResolveImportedImageContentType(string? responseMediaType, string absolutePath) {
        if (IsAllowedContentType(responseMediaType)) return responseMediaType;

        if (string.IsNullOrWhiteSpace(responseMediaType)
            || string.Equals(responseMediaType, "application/octet-stream", StringComparison.OrdinalIgnoreCase)) {
            return TryInferImageContentTypeFromPath(absolutePath);
        }

        return null;
    }

    public static string FileNameForUpload(string originalFileName, string contentType) {
        var safeName = Path.GetFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(safeName)) {
            safeName = "image";
        }

        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext)) {
            ext = contentType switch {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".bin",
            };
            safeName += ext;
        }

        return safeName;
    }

    public static string? ContentTypeFromObjectKey(string? objectKey) {
        if (string.IsNullOrEmpty(objectKey)) return null;

        var ext = Path.GetExtension(objectKey).ToLowerInvariant();
        return ext switch {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };
    }
}
