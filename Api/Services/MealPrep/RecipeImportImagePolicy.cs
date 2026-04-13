namespace Api.Services.MealPrep;

/// <summary>
///     Validates that a user-supplied import image URL is safe to fetch (same site as the recipe page).
/// </summary>
public static class RecipeImportImagePolicy
{
    public static bool AreHostsCompatibleForImportedImage(string sourcePageUrl, string imageUrl) {
        if (!Uri.TryCreate(sourcePageUrl, UriKind.Absolute, out var sourceUri)) return false;
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri)) return false;
        if (!IsHttpOrHttps(imageUri.Scheme) || !IsHttpOrHttps(sourceUri.Scheme)) return false;

        var sh = NormalizeHost(sourceUri.Host);
        var ih = NormalizeHost(imageUri.Host);
        if (string.Equals(sh, ih, StringComparison.OrdinalIgnoreCase)) return true;

        return ih.EndsWith("." + sh, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHost(string host) {
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    private static bool IsHttpOrHttps(string scheme) {
        return string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase)
               || string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase);
    }
}
