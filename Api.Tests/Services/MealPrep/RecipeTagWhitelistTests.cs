using Api.Services.MealPrep;

namespace Api.Tests.Services.MealPrep;

public class RecipeTagWhitelistTests
{
    [Fact]
    public void TryNormalize_AcceptsExactCanonicalValue() {
        Assert.True(RecipeTagWhitelist.TryNormalize("dinner", out var c));
        Assert.Equal("dinner", c);
    }

    [Fact]
    public void TryNormalize_MapsTitleCaseAndSpacesToKebab() {
        Assert.True(RecipeTagWhitelist.TryNormalize("Light Lunch", out var c));
        Assert.Equal("light-lunch", c);
    }

    [Fact]
    public void TryNormalize_RejectsUnknownTag() {
        Assert.False(RecipeTagWhitelist.TryNormalize("chicken-breast", out _));
    }

    [Fact]
    public void NormalizeToWhitelist_SplitsCommaSeparatedFragments() {
        var tags = RecipeTagWhitelist.NormalizeToWhitelist(["quick, dinner", "vegetarian"]);

        Assert.Equal(["dinner", "quick", "vegetarian"], tags);
    }
}
