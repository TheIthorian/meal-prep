using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;

namespace Api.Endpoints.Middleware;

/// <summary>
///     Middleware to set up OPTEL spans for the current user
/// </summary>
public class AppTelemetryMiddleware(RequestDelegate next, ILogger<AppTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context) {
        using var methodTiming = Activity.Current.BeginAppMethodEvent();

        var tags = new Dictionary<string, object>();

        var endpoint = context.GetEndpoint();
        if (endpoint != null) {
            var methodInfo = endpoint.Metadata.GetMetadata<MethodInfo>();
            var endpointNameMetadata = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>();

            if (methodInfo != null)
                tags["app.method_name"] = methodInfo.Name;
            else if (endpointNameMetadata != null && !string.IsNullOrEmpty(endpointNameMetadata.EndpointName))
                tags["app.method_name"] = endpointNameMetadata.EndpointName;
        }

        if (context.Request.RouteValues.TryGetValue("workspaceId", out var workspaceIdObj) && workspaceIdObj != null)
            tags["app.workspace_id"] = workspaceIdObj.ToString()!;

        if (context.User.Identity?.IsAuthenticated == true) {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId)) {
                tags["app.user_id"] = userId;

                // Also add userId to Current Activity for tracing correlation
                Activity.Current?.SetTag("app.user_id", userId);
            }
        }

        if (Activity.Current is { } activity) {
            tags["trace_id"] = activity.TraceId.ToHexString();
            tags["span_id"] = activity.SpanId.ToHexString();
            activity.SetTag("app.request_id", AppExecutionContext.RequestId);
            activity.SetTag("app.correlation_id", AppExecutionContext.CorrelationId);
        }

        if (!string.IsNullOrWhiteSpace(AppExecutionContext.RequestId))
            tags["request_id"] = AppExecutionContext.RequestId;

        if (!string.IsNullOrWhiteSpace(AppExecutionContext.CorrelationId))
            tags["correlation_id"] = AppExecutionContext.CorrelationId;

        if (tags.Count > 0) {
            // Add tags to Activity
            if (tags.TryGetValue("app.workspace_id", out var wId)) Activity.Current?.SetTag("app.workspace_id", wId);
            if (tags.TryGetValue("app.method_name", out var mName)) Activity.Current?.SetTag("app.method_name", mName);

            using (logger.BeginScope(tags)) {
                await next(context);
            }
        } else {
            await next(context);
        }
    }
}
