using System.Globalization;
using System.Text.RegularExpressions;

namespace Api.Services.MealPrep;

/// <summary>
///     Provides unit normalization, serving scaling, and conversion helpers for recipes and shopping lists.
/// </summary>
public class MeasurementService
{
    private static readonly Dictionary<string, MeasurementUnitDefinition> Units =
        new(StringComparer.OrdinalIgnoreCase) {
            ["g"] = new("mass", "g", 1m, false),
            ["gram"] = new("mass", "g", 1m, false),
            ["grams"] = new("mass", "g", 1m, false),
            ["kg"] = new("mass", "g", 1000m, false),
            ["kilogram"] = new("mass", "g", 1000m, false),
            ["kilograms"] = new("mass", "g", 1000m, false),
            ["oz"] = new("mass", "g", 28.349523125m, false),
            ["ounce"] = new("mass", "g", 28.349523125m, false),
            ["ounces"] = new("mass", "g", 28.349523125m, false),
            ["lb"] = new("mass", "g", 453.59237m, false),
            ["lbs"] = new("mass", "g", 453.59237m, false),
            ["ml"] = new("volume", "ml", 1m, false),
            ["milliliter"] = new("volume", "ml", 1m, false),
            ["milliliters"] = new("volume", "ml", 1m, false),
            ["millilitre"] = new("volume", "ml", 1m, false),
            ["millilitres"] = new("volume", "ml", 1m, false),
            ["l"] = new("volume", "ml", 1000m, false),
            ["liter"] = new("volume", "ml", 1000m, false),
            ["liters"] = new("volume", "ml", 1000m, false),
            ["litre"] = new("volume", "ml", 1000m, false),
            ["litres"] = new("volume", "ml", 1000m, false),
            ["floz"] = new("volume", "ml", 29.5735295625m, false),
            ["fl oz"] = new("volume", "ml", 29.5735295625m, false),
            ["tsp"] = new("volume", "ml", 4.92892m, true),
            ["teaspoon"] = new("volume", "ml", 4.92892m, true),
            ["teaspoons"] = new("volume", "ml", 4.92892m, true),
            ["tbsp"] = new("volume", "ml", 14.7868m, true),
            ["tablespoon"] = new("volume", "ml", 14.7868m, true),
            ["tablespoons"] = new("volume", "ml", 14.7868m, true),
            ["cup"] = new("volume", "ml", 240m, true),
            ["cups"] = new("volume", "ml", 240m, true),
            ["item"] = new("count", "item", 1m, false),
            ["items"] = new("count", "item", 1m, false),
            ["unit"] = new("count", "item", 1m, false),
            ["units"] = new("count", "item", 1m, false)
        };

    public decimal? ScaleAmount(decimal? amount, decimal originalServings, decimal targetServings)
    {
        if (amount is null || originalServings <= 0m) return amount;
        var scaled = amount.Value * (targetServings / originalServings);
        return decimal.Round(scaled, 3, MidpointRounding.AwayFromZero);
    }

    public MeasurementParseResult Normalize(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return new MeasurementParseResult(null, null, null, false);

        var compact = Regex.Replace(unit.Trim().ToLowerInvariant(), "\\s+", " ");
        return Units.TryGetValue(compact, out var definition)
            ? new MeasurementParseResult(definition.Kind, definition.CanonicalUnit, definition.FactorToCanonical, definition.IsApproximate)
            : new MeasurementParseResult(null, compact, null, false);
    }

    public IngredientDisplayAmount ConvertForDisplay(decimal amount, string canonicalUnit, bool isApproximate)
    {
        if (canonicalUnit == "g" && amount >= 1000m)
            return new IngredientDisplayAmount(decimal.Round(amount / 1000m, 2, MidpointRounding.AwayFromZero), "kg", isApproximate);

        if (canonicalUnit == "ml" && amount >= 1000m)
            return new IngredientDisplayAmount(decimal.Round(amount / 1000m, 2, MidpointRounding.AwayFromZero), "l", isApproximate);

        return new IngredientDisplayAmount(decimal.Round(amount, 2, MidpointRounding.AwayFromZero), canonicalUnit, isApproximate);
    }

    public decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim()
            .Replace("½", ".5")
            .Replace("¼", ".25")
            .Replace("¾", ".75");

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        if (normalized.Contains('/'))
        {
            var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && decimal.TryParse(parts[0], CultureInfo.InvariantCulture, out var whole))
                return whole + ParseFraction(parts[1]);

            return ParseFraction(normalized);
        }

        var spaceParts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (spaceParts.Length == 2
            && decimal.TryParse(spaceParts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var wholePart)
            && decimal.TryParse(spaceParts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var fractionalPart)
            && fractionalPart is > 0 and < 1)
            return wholePart + fractionalPart;

        return null;
    }

    public string BuildDisplayText(decimal? amount, string? unit, string name, string? note)
    {
        var segments = new List<string>();
        if (amount is not null) segments.Add(FormatAmount(amount.Value));
        if (!string.IsNullOrWhiteSpace(unit)) segments.Add(unit.Trim());

        var baseText = $"{string.Join(" ", segments)} {name}".Trim();
        return string.IsNullOrWhiteSpace(note) ? baseText : $"{baseText}, {note}";
    }

    public string NormalizeIngredientName(string name)
    {
        var normalized = Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9\\s]", " ");
        normalized = Regex.Replace(normalized, "\\s+", " ").Trim();
        return normalized;
    }

    public string FormatAmount(decimal amount)
    {
        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
            .ToString("0.##", CultureInfo.InvariantCulture);
    }

    private decimal? ParseFraction(string value)
    {
        var fractionParts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fractionParts.Length != 2) return null;

        if (!decimal.TryParse(fractionParts[0], CultureInfo.InvariantCulture, out var numerator)) return null;
        if (!decimal.TryParse(fractionParts[1], CultureInfo.InvariantCulture, out var denominator)) return null;
        if (denominator == 0m) return null;

        return numerator / denominator;
    }
}

public sealed record MeasurementParseResult(string? Kind, string? CanonicalUnit, decimal? FactorToCanonical, bool IsApproximate);

public sealed record IngredientDisplayAmount(decimal Amount, string Unit, bool IsApproximate);

internal sealed record MeasurementUnitDefinition(string Kind, string CanonicalUnit, decimal FactorToCanonical, bool IsApproximate);
