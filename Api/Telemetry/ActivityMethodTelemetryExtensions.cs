using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Api.Telemetry;

/// <summary>
///     Emits lightweight method timing spans onto the current OpenTelemetry trace.
/// </summary>
public static class ActivityMethodTelemetryExtensions
{
    public const string ActivitySourceName = "Api.AppMethods";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly AsyncLocal<int> MethodDepth = new();

    extension(Activity? activity)
    {
        public IDisposable? BeginAppMethodEvent(
            [CallerMemberName] string methodName = "",
            [CallerFilePath] string filePath = ""
        ) {
            if (activity is null)
                return null;

            var typeName = Path.GetFileNameWithoutExtension(filePath);
            return new AppMethodSpanScope(typeName, methodName);
        }
    }

    public static IDisposable? BeginRootAppMethodEvent(
        [CallerMemberName] string methodName = "",
        [CallerFilePath] string filePath = ""
    ) {
        var typeName = Path.GetFileNameWithoutExtension(filePath);
        return new AppMethodSpanScope(typeName, methodName);
    }

    private sealed class AppMethodSpanScope : IDisposable
    {
        private readonly Activity? _activity;
        private readonly int _depth = MethodDepth.Value;
        private bool _disposed;

        public AppMethodSpanScope(string typeName, string methodName) {
            MethodDepth.Value = _depth + 1;

            _activity = ActivitySource.StartActivity(
                $"{typeName}.{methodName}"
            );

            _activity?.SetTag("app.method.type", typeName);
            _activity?.SetTag("app.method.name", methodName);
            _activity?.SetTag("app.method.depth", _depth);
            _activity?.SetTag("app.request_id", AppExecutionContext.RequestId);
            _activity?.SetTag("app.correlation_id", AppExecutionContext.CorrelationId);
        }

        public void Dispose() {
            if (_disposed)
                return;

            _disposed = true;
            MethodDepth.Value = _depth;
            _activity?.Dispose();
        }
    }
}
