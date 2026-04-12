using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Api.Startup;

/// <summary>
///     Registers application rate limiting policies.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    public const string LoginRateLimitPolicyName = "login-browser";

    private static string GetClientIp(HttpContext context) {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();

        if (!string.IsNullOrWhiteSpace(forwardedFor)) {
            var firstForwardedIp = forwardedFor
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstForwardedIp)) return firstForwardedIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    extension(IServiceCollection services)
    {
        public void AddAppRateLimiting() {
            services.AddRateLimiter(options => {
                    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                    options.OnRejected = async (context, cancellationToken) => {
                        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        context.HttpContext.Response.ContentType = "application/problem+json";
                        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                                .ToString(CultureInfo.InvariantCulture);

                        await context.HttpContext.Response.WriteAsJsonAsync(
                            new ProblemDetails {
                                Title = "Too many requests", Status = StatusCodes.Status429TooManyRequests
                            },
                            cancellationToken
                        );
                    };

                    options.AddPolicy(
                        LoginRateLimitPolicyName,
                        httpContext => RateLimitPartition.GetFixedWindowLimiter(
                            GetClientIp(httpContext),
                            _ => new FixedWindowRateLimiterOptions {
                                PermitLimit = 5,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            }
                        )
                    );
                }
            );
        }
    }
}
