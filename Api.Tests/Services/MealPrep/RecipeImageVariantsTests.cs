using Api.Services.MealPrep;

namespace Api.Tests.Services.MealPrep;

public class RecipeImageVariantsTests
{
    [Fact]
    public void KeyForWidth_ShouldInsertTheWidthBeforeTheExtension() {
        var key = RecipeImageVariants.KeyForWidth("abc-123_photo.webp", 400);

        Assert.Equal("abc-123_photo.w400.webp", key);
    }

    [Fact]
    public void KeyForWidth_ShouldDifferPerWidth() {
        var small = RecipeImageVariants.KeyForWidth("abc-123_photo.webp", 400);
        var large = RecipeImageVariants.KeyForWidth("abc-123_photo.webp", 800);

        Assert.NotEqual(small, large);
    }

    [Theory]
    [InlineData(1, 400)]
    [InlineData(400, 400)]
    [InlineData(401, 800)]
    [InlineData(800, 800)]
    public void ResolveWidth_ShouldSnapUpToTheSmallestRenditionThatCoversTheRequest(
        int requested,
        int expected
    ) {
        Assert.Equal(expected, RecipeImageVariants.ResolveWidth(requested));
    }

    [Theory]
    [InlineData(801)]
    [InlineData(1600)]
    public void ResolveWidth_ShouldFallBackToFullSizeWhenNoRenditionIsLargeEnough(int requested) {
        Assert.Null(RecipeImageVariants.ResolveWidth(requested));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-100)]
    public void ResolveWidth_ShouldFallBackToFullSizeWhenTheRequestIsAbsentOrNonsense(int? requested) {
        Assert.Null(RecipeImageVariants.ResolveWidth(requested));
    }

    [Fact]
    public void AllKeysForImage_ShouldCoverEveryStoredWidth() {
        var keys = RecipeImageVariants.AllKeysForImage("abc-123_photo.webp").ToArray();

        Assert.Equal(RecipeImageVariants.Widths.Count, keys.Length);
        Assert.All(RecipeImageVariants.Widths, width =>
            Assert.Contains(RecipeImageVariants.KeyForWidth("abc-123_photo.webp", width), keys)
        );
    }
}
