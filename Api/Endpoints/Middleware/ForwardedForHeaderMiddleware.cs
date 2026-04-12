using System.Net;

namespace Api.Endpoints.Middleware;

/// <summary>
///     Ensures requests have a remote IP and a corresponding <c>X-Forwarded-For</c> header for downstream consumers.
/// </summary>
public class ForwardedForHeaderMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context) {
        context.Connection.RemoteIpAddress ??= IPAddress.Loopback;

        if (!context.Request.Headers.ContainsKey("X-Forwarded-For")
            && context.Connection.RemoteIpAddress is { } remoteIp
           )
            context.Request.Headers["X-Forwarded-For"] = remoteIp.ToString();

        await next(context);
    }
}
