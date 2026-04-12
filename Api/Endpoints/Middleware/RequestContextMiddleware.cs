using System.Diagnostics;

namespace Api.Endpoints.Middleware;

/// <summary>
///     Ensures every request has request and correlation identifiers for logs and traces.
/// </summary>
public class RequestContextMiddleware(RequestDelegate next)
{
    private const string RequestIdHeaderName = "X-Request-Id";
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context) {
        var requestId = GetOrCreateHeaderValue(context, RequestIdHeaderName);
        var correlationId = GetOrCreateHeaderValue(context, CorrelationIdHeaderName);

        context.TraceIdentifier = requestId;
        context.Response.Headers[RequestIdHeaderName] = requestId;
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        using var executionContext = AppExecutionContext.Push(requestId, correlationId);

        Activity.Current?.SetTag("app.request_id", requestId);
        Activity.Current?.SetTag("app.correlation_id", correlationId);

        await next(context);
    }

    private static string GetOrCreateHeaderValue(HttpContext context, string headerName) {
        if (context.Request.Headers.TryGetValue(headerName, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue))
            return headerValue.ToString();

        var generatedValue = Guid.NewGuid().ToString("N");
        context.Request.Headers[headerName] = generatedValue;
        return generatedValue;
    }
}
