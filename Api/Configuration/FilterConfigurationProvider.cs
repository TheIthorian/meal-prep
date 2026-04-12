using Api.Models.Filter;

namespace Api.Configuration;

/// <summary>
///     Finds the filter configuration for a given model
/// </summary>
public interface IFilterConfigurationProvider
{
    FilterConfiguration<T> GetConfiguration<T>();
}

/// <summary>
///     Builds the filter configuration used by query filtering.
/// </summary>
public class FilterConfigurationProvider : IFilterConfigurationProvider
{
    private readonly Dictionary<Type, object> _configs = new();

    public FilterConfiguration<T> GetConfiguration<T>() {
        if (_configs.TryGetValue(typeof(T), out var cfg))
            return (FilterConfiguration<T>)cfg;

        throw new InvalidOperationException($"No filter configuration registered for type {typeof(T).Name}");
    }

    public void Add<T>(FilterConfiguration<T> config) {
        _configs[typeof(T)] = config;
    }
}
