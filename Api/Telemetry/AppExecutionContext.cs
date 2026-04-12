namespace Api.Telemetry;

/// <summary>
///     Stores request and correlation identifiers for the current async flow.
/// </summary>
public static class AppExecutionContext
{
    private static readonly AsyncLocal<ExecutionContextState?> CurrentState = new();

    public static string? RequestId => CurrentState.Value?.RequestId;
    public static string? CorrelationId => CurrentState.Value?.CorrelationId;

    public static IDisposable Push(string? requestId, string? correlationId) {
        var previous = CurrentState.Value;
        CurrentState.Value = new ExecutionContextState(requestId, correlationId);
        return new RestoreScope(previous);
    }

    private sealed record ExecutionContextState(string? RequestId, string? CorrelationId);

    private sealed class RestoreScope(ExecutionContextState? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose() {
            if (_disposed)
                return;

            _disposed = true;
            CurrentState.Value = previous;
        }
    }
}
