using System.Net;
using System.Net.Http.Json;
using System.Text;
using Api.Services.Http;
using Microsoft.Extensions.Logging;

namespace Api.Tests.Services.Http;

public class OutboundHttpLoggingHandlerTests
{
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NoopScope();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add((logLevel, formatter(state, exception)));

        private sealed class NoopScope : IDisposable
        {
            public void Dispose() { }
        }
    }

    private sealed class StubInnerHandler(HttpStatusCode status, string? body = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            var response = new HttpResponseMessage(status);
            if (body is not null)
                response.Content = new StringContent(body, Encoding.UTF8, "application/json");

            return Task.FromResult(response);
        }
    }

    private static (HttpClient Client, CapturingLogger<OutboundHttpLoggingHandler> Logger) Build(
        HttpStatusCode status = HttpStatusCode.OK,
        string? responseBody = null
    ) {
        var logger = new CapturingLogger<OutboundHttpLoggingHandler>();
        var handler = new OutboundHttpLoggingHandler(logger) { InnerHandler = new StubInnerHandler(status, responseBody) };

        return (new HttpClient(handler), logger);
    }

    [Fact]
    public async Task LogsASummaryLineWithStatusAtInformation() {
        var (client, logger) = Build();

        await client.PostAsJsonAsync("https://node.example/api/internal/users/sync", new { id = "abc" });

        var summary = Assert.Single(logger.Entries.Where(e => e.Message.Contains("returned 200")));
        Assert.Equal(LogLevel.Information, summary.Level);
        Assert.Contains("https://node.example/api/internal/users/sync", summary.Message);
    }

    [Fact]
    public async Task LogsAnErrorStatusAtWarning() {
        var (client, logger) = Build(HttpStatusCode.Unauthorized);

        await client.PostAsJsonAsync("https://node.example/api/internal/users/sync", new { id = "abc" });

        var summary = Assert.Single(logger.Entries.Where(e => e.Message.Contains("returned 401")));
        Assert.Equal(LogLevel.Warning, summary.Level);
    }

    [Fact]
    public async Task NeverLogsThePasswordFromARequestBody() {
        // The reason this handler exists at all: the user-sync body carries a plaintext password.
        var (client, logger) = Build();

        await client.PostAsJsonAsync(
            "https://node.example/api/internal/users/sync",
            new { id = "abc", email = "ada@example.com", displayName = "Ada", password = "never-log-me" }
        );

        var all = string.Join("\n", logger.Entries.Select(e => e.Message));
        Assert.DoesNotContain("never-log-me", all);
        Assert.Contains("[REDACTED]", all);
        // Redaction must be surgical, not a blanket drop of the body.
        Assert.Contains("ada@example.com", all);
    }

    [Fact]
    public async Task NeverLogsASensitiveHeader() {
        var (client, logger) = Build();

        var request = new HttpRequestMessage(HttpMethod.Post, "https://node.example/api/internal/users/sync");
        request.Headers.Add("x-user-sync-secret", "super-secret-value");
        request.Content = JsonContent.Create(new { id = "abc" });

        await client.SendAsync(request);

        var all = string.Join("\n", logger.Entries.Select(e => e.Message));
        Assert.DoesNotContain("super-secret-value", all);
        Assert.Contains("x-user-sync-secret=[REDACTED]", all);
    }

    [Fact]
    public void RedactReplacesOnlySensitivelyNamedProperties() {
        const string body = """{"email":"ada@example.com","password":"hunter2","apiKey":"k","displayName":"Ada"}""";

        var redacted = OutboundHttpLoggingHandler.Redact(body);

        Assert.DoesNotContain("hunter2", redacted);
        Assert.DoesNotContain("\"k\"", redacted);
        Assert.Contains("ada@example.com", redacted);
        Assert.Contains("Ada", redacted);
    }
}
