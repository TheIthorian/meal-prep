using System.Net;
using System.Net.Http.Json;

namespace Api.Tests.Endpoints;

/// <summary>
///     Drives the real sign-in and signup routes through the real filter registration, with only the outbound
///     HTTP client replaced. A unit test of the filter would have to fake <c>UserManager&lt;AppUser&gt;</c> and
///     would not prove the filter is actually wired onto the auth group.
/// </summary>
public class UserSyncFilterTests : IClassFixture<ApiWebApplicationFactory>
{
    private const string Password = "correct-horse-battery";

    private readonly ApiWebApplicationFactory factory;

    public UserSyncFilterTests(ApiWebApplicationFactory factory) {
        this.factory = factory;
        factory.RecordingUserSyncClient.Clear();
    }

    [Fact]
    public async Task SuccessfulLogin_SyncsTheUserWithTheirPassword() {
        var (userId, _, email) = await factory.SeedIdentityUserWithWorkspaceAsync("Sync Login Workspace", Password);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var call = Assert.Single(factory.RecordingUserSyncClient.Calls.Where(c => c.Email == email));
        Assert.Equal(userId, call.UserId);
        Assert.Equal(Password, call.Password);
    }

    [Fact]
    public async Task FailedLogin_SyncsNothing() {
        // The security-relevant case: a password this API rejected must never be forwarded to another
        // service. A wrong guess is exactly the thing an attacker would want replicated elsewhere.
        var (_, _, email) = await factory.SeedIdentityUserWithWorkspaceAsync("Sync Bad Login Workspace", Password);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = "not-the-right-password" }
        );

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(factory.RecordingUserSyncClient.Calls, c => c.Email == email);
    }

    [Fact]
    public async Task SuccessfulSignup_SyncsTheNewUser() {
        var client = factory.CreateClient();
        var email = $"signup-sync-{Guid.NewGuid():N}@tests.local";

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/signup",
            new { email, password = Password, displayName = "Signup Sync" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var call = Assert.Single(factory.RecordingUserSyncClient.Calls.Where(c => c.Email == email));
        Assert.Equal(Password, call.Password);
        Assert.NotEqual(Guid.Empty, call.UserId);
    }

    [Fact]
    public async Task AnUnrelatedRouteInTheSameGroup_SyncsNothing() {
        var client = factory.CreateClient();
        var before = factory.RecordingUserSyncClient.Calls.Count;

        await client.PostAsync("/api/v1/auth/logout", content: null);

        Assert.Equal(before, factory.RecordingUserSyncClient.Calls.Count);
    }
}
