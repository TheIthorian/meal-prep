namespace Api.Startup;

/// <summary>
///     Logs relevant application configuration at startup.
/// </summary>
public static class ConfigurationLoggingExtensions
{
    private static readonly string[] SensitiveKeyMarkers = {
        "password", "pwd", "secret", "token", "apikey", "api_key", "accesskey", "access_key", "privatekey",
        "private_key", "clientsecret", "client_secret", "connectionstring", "connection_string", "key"
    };

    public static string MaskConfigurationValue(string key, string? value) {
        if (value is null)
            return "(null)";

        if (IsSensitiveKey(key))
            return ObfuscateSecret(value);

        if (LooksLikeConnectionString(value))
            return MaskConnectionString(value);

        return value;
    }

    private static bool IsSensitiveKey(string key) {
        return SensitiveKeyMarkers.Any(marker => key.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeConnectionString(string value) {
        return value.Contains("=", StringComparison.Ordinal) && value.Contains(";", StringComparison.Ordinal);
    }

    private static string MaskConnectionString(string value) {
        var segments = value.Split(';');
        var maskedSegments = new List<string>(segments.Length);

        foreach (var segment in segments) {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0) {
                maskedSegments.Add(segment);
                continue;
            }

            var segmentKey = segment[..separatorIndex].Trim();
            var segmentValue = segment[(separatorIndex + 1)..];

            maskedSegments.Add(
                IsSensitiveKey(segmentKey)
                    ? $"{segmentKey}={ObfuscateSecret(segmentValue)}"
                    : segment
            );
        }

        return string.Join(';', maskedSegments);
    }

    private static string ObfuscateSecret(string value) {
        if (string.IsNullOrWhiteSpace(value))
            return "(empty)";

        if (value.Length <= 4)
            return "****";

        if (value.Length <= 8)
            return $"{value[..1]}***{value[^1..]}";

        return $"{value[..2]}***{value[^2..]}";
    }

    private static string? GetUiUrl(IConfiguration configuration) {
        var origins = configuration["CORS_ORIGINS"];
        if (string.IsNullOrWhiteSpace(origins))
            return null;

        return origins
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(origin => !string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetApiUrl(IConfiguration configuration, string? uiUrl) {
        var httpPorts = configuration["ASPNETCORE_HTTP_PORTS"];
        if (!string.IsNullOrWhiteSpace(httpPorts)) {
            var firstHttpPort = httpPorts.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstHttpPort))
                return $"http://localhost:{firstHttpPort}";
        }

        var httpsPorts = configuration["ASPNETCORE_HTTPS_PORTS"];
        if (!string.IsNullOrWhiteSpace(httpsPorts)) {
            var firstHttpsPort = httpsPorts.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstHttpsPort))
                return $"https://localhost:{firstHttpsPort}";
        }

        if (!string.IsNullOrWhiteSpace(uiUrl))
            return $"{uiUrl.TrimEnd('/')}/api";

        return "http://localhost:5001";
    }

    extension(WebApplication app)
    {
        public void LogStartupConfiguration() {
            var lines = app.Configuration
                .AsEnumerable()
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => $"  {entry.Key} = {MaskConfigurationValue(entry.Key, entry.Value)}")
                .ToArray();

            app.Logger.LogDebug(
                "Startup configuration:{NewLine}{ConfigurationEntries}",
                Environment.NewLine,
                string.Join(Environment.NewLine, lines)
            );
        }

        public void LogStartupUrls() {
            var inferredUiUrl = GetUiUrl(app.Configuration);
            var uiUrl = inferredUiUrl ?? "(set CORS_ORIGINS)";
            var apiUrl = GetApiUrl(app.Configuration, inferredUiUrl);

            Console.WriteLine($"Startup URLs -> API: {apiUrl} | UI: {uiUrl}");
        }
    }
}
