using Api.Services.MealPrep;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Api.Tests.Services.MealPrep;

public class RecipeImageCacheTests
{
    [Fact]
    public void ETagForObjectKey_ShouldBeStableForTheSameKey() {
        var first = RecipeImageCache.ETagForObjectKey("recipes/abc.webp");
        var second = RecipeImageCache.ETagForObjectKey("recipes/abc.webp");

        Assert.Equal(first.Tag.Value, second.Tag.Value);
    }

    [Fact]
    public void ETagForObjectKey_ShouldChangeWhenTheImageIsReplaced() {
        var before = RecipeImageCache.ETagForObjectKey("recipes/abc.webp");
        var after = RecipeImageCache.ETagForObjectKey("recipes/def.webp");

        Assert.NotEqual(before.Tag.Value, after.Tag.Value);
    }

    [Fact]
    public void ETagForObjectKey_ShouldBeAQuotedStrongTag() {
        var etag = RecipeImageCache.ETagForObjectKey("recipes/abc.webp");

        Assert.False(etag.IsWeak);
        Assert.Matches("^\"[0-9a-f]{32}\"$", etag.Tag.Value);
    }

    [Fact]
    public void IsNotModified_ShouldBeFalseWithoutIfNoneMatch() {
        var etag = RecipeImageCache.ETagForObjectKey("recipes/abc.webp");

        Assert.False(RecipeImageCache.IsNotModified(RequestHeadersWithIfNoneMatch(null), etag));
    }

    [Fact]
    public void IsNotModified_ShouldBeTrueWhenTheClientHoldsTheCurrentTag() {
        var etag = RecipeImageCache.ETagForObjectKey("recipes/abc.webp");

        Assert.True(RecipeImageCache.IsNotModified(RequestHeadersWithIfNoneMatch(etag.ToString()), etag));
    }

    [Fact]
    public void IsNotModified_ShouldBeTrueForAWildcard() {
        var etag = RecipeImageCache.ETagForObjectKey("recipes/abc.webp");

        Assert.True(RecipeImageCache.IsNotModified(RequestHeadersWithIfNoneMatch("*"), etag));
    }

    [Fact]
    public void IsNotModified_ShouldBeFalseWhenTheClientHoldsAnOlderImage() {
        var stale = RecipeImageCache.ETagForObjectKey("recipes/abc.webp");
        var current = RecipeImageCache.ETagForObjectKey("recipes/def.webp");

        Assert.False(RecipeImageCache.IsNotModified(RequestHeadersWithIfNoneMatch(stale.ToString()), current));
    }

    [Fact]
    public void ResponseCacheControl_ShouldKeepImagesOutOfSharedCaches() {
        var cacheControl = RecipeImageCache.ResponseCacheControl();

        Assert.True(cacheControl.Private);
        Assert.False(cacheControl.Public);
        Assert.True(cacheControl.MustRevalidate);
        Assert.Equal(RecipeImageCache.MaxAge, cacheControl.MaxAge);
    }

    private static Microsoft.AspNetCore.Http.Headers.RequestHeaders RequestHeadersWithIfNoneMatch(string? value) {
        var context = new DefaultHttpContext();
        if (value is not null) {
            context.Request.Headers.IfNoneMatch = value;
        }

        return context.Request.GetTypedHeaders();
    }
}
