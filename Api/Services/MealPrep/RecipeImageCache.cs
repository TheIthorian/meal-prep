using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.Net.Http.Headers;

namespace Api.Services.MealPrep;

/// <summary>
///     Cache metadata for the recipe image endpoint.
///     A recipe's image URL is stable, so freshness comes from an entity tag derived from the stored
///     object key: every upload writes a new key, so the tag changes whenever the image does.
/// </summary>
public static class RecipeImageCache
{
    /// <summary>
    ///     How long a client may reuse a recipe image without revalidating. Kept short because the
    ///     URL does not change when the image does; a client that has just uploaded appends a
    ///     <c>?v=</c> cache buster to see the new image immediately.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    public static EntityTagHeaderValue ETagForObjectKey(string objectKey) {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(objectKey));
        return new EntityTagHeaderValue($"\"{Convert.ToHexString(hash)[..32].ToLowerInvariant()}\"");
    }

    /// <summary>
    ///     True when the request's If-None-Match header already covers the current tag, so the
    ///     response body can be skipped and the object never read from storage.
    /// </summary>
    public static bool IsNotModified(RequestHeaders requestHeaders, EntityTagHeaderValue currentETag) {
        var ifNoneMatch = requestHeaders.IfNoneMatch;
        if (ifNoneMatch is null || ifNoneMatch.Count == 0) return false;

        return ifNoneMatch.Any(candidate =>
            candidate.Tag == "*" || candidate.Compare(currentETag, false)
        );
    }

    public static CacheControlHeaderValue ResponseCacheControl() {
        // Private: the image is behind the session cookie, so only the browser may store it, never
        // a shared proxy. MustRevalidate: once stale, a conditional request is required rather than
        // the image being served from cache indefinitely.
        return new CacheControlHeaderValue { Private = true, MaxAge = MaxAge, MustRevalidate = true };
    }
}
