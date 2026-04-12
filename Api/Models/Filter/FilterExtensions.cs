using Microsoft.Extensions.Primitives;

namespace Api.Models.Filter;

/// <summary>
///     Provides helpers for applying configured query filters.
/// </summary>
public static class QueryableFilterExtensions
{
    extension<T>(IQueryable<T> query)
    {
        public IQueryable<T> ApplyFilters(
            IEnumerable<KeyValuePair<string, StringValues>> filterOptions,
            FilterConfiguration<T> config
        ) {
            foreach (var pair in filterOptions) {
                if (!config.Rules.TryGetValue(pair.Key, out var rule))
                    continue; // ignore unsupported filters

                if (!string.IsNullOrEmpty(pair.Value))
                    query = rule.ApplyToQuery(query, pair.Value);
            }

            return query;
        }
    }
}
