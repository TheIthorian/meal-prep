using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Api.Services.UserSync;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Endpoints;

/// <summary>
///     Drives the real sign-in and signup routes through the real filter registration, with only the outbound
///     HTTP client replaced. A unit test of the filter would have to fake <c>UserManager&lt;AppUser&gt;</c> and
///     would not prove the filter is actually wired onto the auth group.
/// </summary>
public class UserSyncFilterTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "correct-horse-battery";

    public sealed record SyncCall(Guid UserId, string Email, string DisplayName, string Password);

    private sealed class RecordingUserSyncClient : IUserSyncClient
    {
        public ConcurrentBag<SyncCall> Calls { get; } = [];

        public Task SyncAsync(
            Guid userId,
            string email,
            string displayName,
            string password,
            CancellationToken ct = default
        ) {
            Calls.Add(new SyncCall(userId, email, displayName, password));
            return Task.CompletedTask;
        }
    }

    private readonly ApiWebApplicationFactory factory;

    public UserSyncFilterTests(ApiWebApplicationFactory factory) {
        this.factory = factory;
    }

    private (HttpClient Client, RecordingUserSyncClient Recorder) CreateClientWithRecorder() {
        var recorder = new RecordingUserSyncClient();

        var client = factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services => services.AddSingleton<IUserSyncClient>(recorder))
            )
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        return (client, recorder);
    }

    [Fact]
    public async Task SuccessfulLogin_SyncsTheUserWithTheirPassword() {
        var (userId, _, email) = await factory.SeedIdentityUserWithWorkspaceAsync("Sync Login Workspace", Password);
        var (client, recorder) = CreateClientWithRecorder();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var call = Assert.Single(recorder.Calls.Where(c => c.UserId == userId));
        Assert.Equal(email, call.Email);
        Assert.Equal(Password, call.Password);
    }

    [Fact]
    public async Task FailedLogin_SyncsNothing() {
        // The security-relevant case: a password this API rejected must never be forwarded to another
        // service. A wrong guess is exactly the thing an attacker would want replicated elsewhere.
        var (_, _, email) = await factory.SeedIdentityUserWithWorkspaceAsync("Sync Bad Login Workspace", Password);
        var (client, recorder) = CreateClientWithRecorder();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = "not-the-right-password" }
        );

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(recorder.Calls.Where(c => c.Email == email));
    }

    [Fact]
    public async Task SuccessfulSignup_SyncsTheNewUser() {
        var (client, recorder) = CreateClientWithRecorder();
        var email = $"signup-sync-{Guid.NewGuid():N}@tests.local";

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/signup",
            new { email, password = Password, displayName = "Signup Sync" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var call = Assert.Single(recorder.Calls.Where(c => c.Email == email));
        Assert.Equal(Password, call.Password);
        Assert.NotEqual(Guid.Empty, call.UserId);
    }

    [Fact]
    public async Task AnUnrelatedRouteInTheSameGroup_SyncsNothing() {
        var (client, recorder) = CreateClientWithRecorder();

        await client.PostAsync("/api/v1/auth/logout", content: null);

        Assert.Empty(recorder.Calls);
    }
}
