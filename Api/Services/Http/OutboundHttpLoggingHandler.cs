using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Api.Services.Http;

/// <summary>
///     Logs every outbound HTTP request this API makes, and its response.
///     <para>
///         A summary line per call at Information (method, host+path, status, duration), and the headers and
///         bodies at Debug. The Debug work is skipped entirely unless Debug is enabled, so a production
///         deployment pays only for the summary.
///     </para>
///     <para>
///         <b>Redaction is not optional here.</b> The user-sync call carries a plaintext password in its body
///         and a shared secret in its headers, so anything printed goes through <see cref="Redact" /> first.
///         Header names and JSON keys matching token/secret/password/key, plus authorization and cookie, are
///         replaced rather than logged.
///     </para>
///     <para>
///         Registered on the typed client rather than inside it, so it covers every outbound client this API
///         gains later without each one re-implementing logging.
///     </para>
/// </summary>
public partial class OutboundHttpLoggingHandler(ILogger<OutboundHttpLoggingHandler> logger) : DelegatingHandler
{
    /// <summary>Longest body written to the log. Anything larger is described by its size instead.</summary>
    private const int MaxLoggedBodyChars = 4096;

    private const string Redacted = "[REDACTED]";

    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase) {
        "authorization",
        "cookie",
        "set-cookie"
    };

    [GeneratedRegex("token|secret|password|key", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveNamePattern();

    /// <summary>Matches a JSON string property whose NAME looks sensitive, so its value can be replaced.</summary>
    [GeneratedRegex("(\"[^\"]*(?:token|secret|password|key)[^\"]*\"\\s*:\\s*)\"(?:[^\"\\\\]|\\\\.)*\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveJsonProperty();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    ) {
        var target = $"{request.Method} {request.RequestUri?.GetLeftPart(UriPartial.Path)}";
        var debug = logger.IsEnabled(LogLevel.Debug);

        if (debug)
            logger.LogDebug(
                "Outbound request {Target} headers {Headers} body {Body}",
                target,
                DescribeHeaders(request.Headers),
                await DescribeContentAsync(request.Content, cancellationToken)
            );

        var stopwatch = Stopwatch.StartNew();

        try {
            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            if ((int)response.StatusCode >= 400)
                logger.LogWarning(
                    "Outbound {Target} returned {StatusCode} in {ElapsedMs}ms",
                    target,
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds
                );
            else
                logger.LogInformation(
                    "Outbound {Target} returned {StatusCode} in {ElapsedMs}ms",
                    target,
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds
                );

            if (debug)
                logger.LogDebug(
                    "Outbound response {Target} {StatusCode} headers {Headers} body {Body}",
                    target,
                    (int)response.StatusCode,
                    DescribeHeaders(response.Headers),
                    await DescribeContentAsync(response.Content, cancellationToken)
                );

            return response;
        }
        catch (Exception ex) {
            stopwatch.Stop();
            // Logged and rethrown, not swallowed: whether a failure is fatal is the caller's decision.
            // UserSyncClient catches its own; a future client may not want to.
            logger.LogWarning(ex, "Outbound {Target} failed after {ElapsedMs}ms", target, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    /// <summary>Header names and values, with anything sensitive replaced.</summary>
    private static string DescribeHeaders(System.Net.Http.Headers.HttpHeaders headers) {
        var parts = headers.Select(header =>
            $"{header.Key}={(IsSensitiveName(header.Key) ? Redacted : string.Join(",", header.Value))}"
        );

        return string.Join("; ", parts);
    }

    private static bool IsSensitiveName(string name) {
        return SensitiveHeaders.Contains(name) || SensitiveNamePattern().IsMatch(name);
    }

    /// <summary>
    ///     The body, redacted and truncated. Non-textual or oversized content is described rather than printed.
    /// </summary>
    private static async Task<string> DescribeContentAsync(HttpContent? content, CancellationToken ct) {
        if (content is null) return "(none)";

        var mediaType = content.Headers.ContentType?.MediaType ?? string.Empty;
        var isTextual = mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
                        || mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                        || mediaType.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);

        if (!isTextual) return $"{(mediaType.Length == 0 ? "unknown content type" : mediaType)} of unlogged length";

        var body = await content.ReadAsStringAsync(ct);

        if (body.Length > MaxLoggedBodyChars) return $"{mediaType} of length {body.Length}";

        return Redact(body);
    }

    /// <summary>Replaces the value of any JSON property whose name looks sensitive.</summary>
    public static string Redact(string body) {
        return SensitiveJsonProperty().Replace(body, $"$1\"{Redacted}\"");
    }
}
