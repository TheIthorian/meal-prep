using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Api.Logging;

/// <summary>
///     Adds helpers for creating structured logging scopes from named properties.
/// </summary>
public static class LoggerScopeExtensions
{
    private static void AddTraceContext(IDictionary<string, object?> scope) {
        var activity = Activity.Current;
        if (activity is not null) {
            scope["trace_id"] = activity.TraceId.ToHexString();
            scope["span_id"] = activity.SpanId.ToHexString();

            AddActivityTag(scope, activity, "app.method_type");
            AddActivityTag(scope, activity, "app.method_name");
            AddActivityTag(scope, activity, "app.method_depth");
        }

        if (!string.IsNullOrWhiteSpace(AppExecutionContext.RequestId))
            scope["request_id"] = AppExecutionContext.RequestId;

        if (!string.IsNullOrWhiteSpace(AppExecutionContext.CorrelationId))
            scope["correlation_id"] = AppExecutionContext.CorrelationId;
    }

    private static void AddActivityTag(
        IDictionary<string, object?> scope,
        Activity activity,
        string tagName
    ) {
        var tagValue = activity.GetTagItem(tagName);
        if (tagValue is not null)
            scope[tagName] = tagValue;
    }

    extension(ILogger logger)
    {
        public IDisposable? BeginPropertyScope(params object[] properties) {
            var scope = new Dictionary<string, object?>(properties.Length);

            AddTraceContext(scope);

            foreach (var property in properties)
                if (property is ITuple tuple && tuple.Length == 2 && tuple[0] is string key)
                    scope[key] = tuple[1];
                else
                    throw new ArgumentException(
                        "BeginPropertyScope expects each property to be a (string Key, object? Value) tuple.",
                        nameof(properties)
                    );

            return logger.BeginScope(scope);
        }

        public IDisposable? BeginTraceScope() {
            var scope = new Dictionary<string, object?>();
            AddTraceContext(scope);

            return scope.Count == 0 ? null : logger.BeginScope(scope);
        }
    }
}
