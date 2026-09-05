using System.Net;
using System.Text.Json;
using Api.Configuration;
using Api.Services.UserSync;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests.Services.UserSync;

/// <summary>
///     The fail-open contract is this class's whole reason for existing as its own unit rather than inline in the
///     endpoint filter: no failure of the Node app may reach a user signing in to this API.
/// </summary>
public class UserSyncClientTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) {
            CallCount++;
            LastRequest = request;
            if (request.Content is not null) LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static UserSyncClient BuildClient(StubHandler handler, UserSyncOptions? options = null) {
        var opts = options ?? new UserSyncOptions {
            Enabled = true,
            BaseUrl = "https://node.example",
            Secret = "shared-secret-value",
        };

        return new UserSyncClient(
            new HttpClient(handler),
            Options.Create(opts),
            NullLogger<UserSyncClient>.Instance
        );
    }

    [Fact]
    public async Task SyncAsync_PostsToTheSyncPathWithTheSecretHeader() {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = BuildClient(handler);

        await client.SyncAsync(UserId, "ada@example.com", "Ada Lovelace", "correct-horse");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://node.example/api/internal/users/sync", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("shared-secret-value", handler.LastRequest.Headers.GetValues("x-user-sync-secret").Single());
    }

    [Fact]
    public async Task SyncAsync_SendsTheIdEmailDisplayNameAndPassword() {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = BuildClient(handler);

        await client.SyncAsync(UserId, "ada@example.com", "Ada Lovelace", "correct-horse");

        using var body = JsonDocument.Parse(handler.LastBody!);
        var root = body.RootElement;
        Assert.Equal(UserId.ToString(), root.GetProperty("id").GetString());
        Assert.Equal("ada@example.com", root.GetProperty("email").GetString());
        Assert.Equal("Ada Lovelace", root.GetProperty("displayName").GetString());
        Assert.Equal("correct-horse", root.GetProperty("password").GetString());
    }

    [Fact]
    public async Task SyncAsync_DoesNotThrowOnAnErrorStatus() {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = BuildClient(handler);

        await client.SyncAsync(UserId, "ada@example.com", "Ada Lovelace", "correct-horse");
    }

    [Fact]
    public async Task SyncAsync_DoesNotThrowWhenTheHostIsUnreachable() {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));
        var client = BuildClient(handler);

        await client.SyncAsync(UserId, "ada@example.com", "Ada Lovelace", "correct-horse");
    }

    [Fact]
    public async Task SyncAsync_DoesNotThrowOnTimeout() {
        var handler = new StubHandler(_ => throw new TaskCanceledException("timed out"));
        var client = BuildClient(handler);

        await client.SyncAsync(UserId, "ada@example.com", "Ada Lovelace", "correct-horse");
    }

    [Fact]
    public async Task SyncAsync_MakesNoCallWhenDisabled() {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = BuildClient(handler, new UserSyncOptions {
            Enabled = false,
            BaseUrl = "https://node.example",
            Secret = "shared-secret-value",
        });

        await client.SyncAsync(UserId, "ada@example.com", "Ada Lovelace", "correct-horse");

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SyncAsync_MakesNoCallWhenEnabledButUnconfigured() {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = BuildClient(handler, new UserSyncOptions { Enabled = true });

        await client.SyncAsync(UserId, "ada@example.com", "Ada Lovelace", "correct-horse");

        Assert.Equal(0, handler.CallCount);
    }
}
