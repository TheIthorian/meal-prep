namespace DemoApi.Common;

/// <summary>
///     Represents an optional value.
/// </summary>
public class Option<T>
{
    private readonly T _content;
    private readonly bool _hasValue;

    private Option(T content, bool hasValue) {
        (_content, _hasValue) = (content, hasValue);
    }

    public static Option<T> Some(T content) {
        return new Option<T>(content, true);
    }

    public static Option<T> Maybe(T? content) {
        return content != null ? Some(content) : None();
    }

    public static Option<T> None() {
        return new Option<T>(default!, false);
    }

    public T Get(T defaultValue) {
        return _hasValue ? _content : defaultValue;
    }

    public T? GetOrNull() {
        return _hasValue ? _content : default;
    }

    public Option<R> Map<R>(Func<T, R> map) {
        return _hasValue ? Option<R>.Some(map(_content)) : Option<R>.None();
    }

    public Option<R> Bind<R>(Func<T, Option<R>> bind) {
        return _hasValue ? bind(_content) : Option<R>.None();
    }

    public override string ToString() {
        return _hasValue ? _content?.ToString() ?? string.Empty : string.Empty;
    }
}

/// <summary>
///     Provides helpers for working with optional values.
/// </summary>
public static class OptionExtensions
{
    extension<T>(IEnumerable<T> items)
    {
        public Option<T> FirstOrNone(Func<T, bool> predicate) {
            return items
                .Where(predicate)
                .Select(Option<T>.Some)
                .DefaultIfEmpty(Option<T>.None())
                .First();
        }
    }

    extension<T>(Option<T> obj)
    {
        public Option<R> Select<R>(Func<T, R> map) {
            return obj.Map(map);
        }

        public Option<T> Where(Func<T, bool> predicate) {
            return obj.Bind(content => predicate(content) ? obj : Option<T>.None());
        }

        public Option<TResult> SelectMany<R, TResult>(
            Func<T, Option<R>> bind,
            Func<T, R, TResult> map
        ) {
            return obj.Bind(original => bind(original).Map(result => map(original, result)));
        }
    }
}
