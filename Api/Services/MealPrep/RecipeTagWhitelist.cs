using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Api.Services.MealPrep;

/// <summary>
///     Canonical, low-cardinality recipe tags (kebab-case). Imports and the tag suggestion model must stay within this list.
/// </summary>
public static class RecipeTagWhitelist
{
    private static readonly Regex NonSlugChars = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    /// <summary>Allowed tag values in stable product order (not necessarily alphabetical).</summary>
    public static readonly string[] All = [
        // Meal & course
        "breakfast",
        "brunch",
        "lunch",
        "light-lunch",
        "dinner",
        "snack",
        "appetizer",
        "side",
        "main",
        "soup",
        "salad",
        "sandwich",
        "bowl",
        "dessert",
        "baking",
        "drink",
        // Time & effort
        "quick",
        "under-30-minutes",
        "slow-cooker",
        "one-pot",
        "meal-prep",
        "make-ahead",
        "freezer-friendly",
        // Methods
        "grilled",
        "baked",
        "fried",
        "roasted",
        "steamed",
        "raw",
        "no-cook",
        "air-fryer",
        "instant-pot",
        "pressure-cooker",
        "stovetop",
        // Proteins & anchors
        "chicken",
        "beef",
        "pork",
        "lamb",
        "turkey",
        "fish",
        "seafood",
        "shrimp",
        "tofu",
        "tempeh",
        "eggs",
        "beans",
        "lentils",
        "pasta",
        "rice",
        "noodles",
        "potato",
        "bread",
        "pizza",
        // Dietary
        "vegetarian",
        "vegan",
        "gluten-free",
        "dairy-free",
        "nut-free",
        "low-carb",
        "high-protein",
        "keto",
        // Flavor & style
        "spicy",
        "mild",
        "sweet",
        "savory",
        "tangy",
        "smoky",
        "creamy",
        "fresh",
        "comfort-food",
        "light",
        "indulgent",
        // Cuisines (broad)
        "italian",
        "mexican",
        "indian",
        "thai",
        "japanese",
        "chinese",
        "korean",
        "vietnamese",
        "french",
        "greek",
        "mediterranean",
        "middle-eastern",
        "american",
        "british",
        "caribbean",
        "african",
        "spanish",
        "german",
        // Occasions
        "kid-friendly",
        "date-night",
        "party",
        "bbq",
        "holiday",
        "picnic"
    ];

    private static readonly FrozenSet<string> Set = FrozenSet.ToFrozenSet(All, StringComparer.Ordinal);

    /// <summary>Tags sorted alphabetically for API clients and UI pickers.</summary>
    public static string[] AllSorted { get; } = All.OrderBy(tag => tag, StringComparer.Ordinal).ToArray();

    public static string FormatForPrompt() {
        return string.Join(", ", All);
    }

    /// <summary>
    ///     Maps a single user or model-provided label to a canonical whitelist value, or returns false.
    /// </summary>
    public static bool TryNormalize(string? raw, out string canonical) {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var trimmed = raw.Trim();
        if (Set.Contains(trimmed)) {
            canonical = trimmed;

            return true;
        }

        var slug = ToKebabCase(trimmed);
        if (Set.Contains(slug)) {
            canonical = slug;

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Splits comma- or semicolon-separated fragments, normalizes each, deduplicates, and returns sorted canonical tags.
    /// </summary>
    public static string[] NormalizeToWhitelist(IEnumerable<string> rawTags) {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in rawTags) {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            foreach (var piece in SplitTagPieces(raw)) {
                if (TryNormalize(piece, out var canonical))
                    result.Add(canonical);
            }
        }

        return result.OrderBy(tag => tag, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> SplitTagPieces(string raw) {
        foreach (var piece in raw.Split(
                     [',', ';'],
                     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                 )) {
            if (!string.IsNullOrWhiteSpace(piece))
                yield return piece;
        }
    }

    private static string ToKebabCase(string value) {
        var lower = value.Trim().ToLowerInvariant();
        var slug = NonSlugChars.Replace(lower, "-").Trim('-');

        return slug;
    }
}
