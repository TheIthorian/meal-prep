using Microsoft.Extensions.Primitives;

namespace Api.Models.Filter;

public class FilterConfiguration<T>
{
    // Key: filter key used in request (e.g., "amountFrom")
    // Value: expression that extracts the model field as object
    public Dictionary<string, FilterRule<T>> Rules { get; } = new();
}

public class FilterRule<T>
{
    public required Func<IQueryable<T>, StringValues, IQueryable<T>> ApplyToQuery { get; init; }
    public required Type ValueType { get; init; }
}

public enum FilterComparisonType
{
    Equals,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Contains,
    In
}
