using System.Net.Http.Json;
using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Services.UserSync;

/// <summary>
///     Pushes a freshly-authenticated user into the Node app (server B) during the migration's dual-run window.
///     <para>
///         This exists for exactly one reason: ASP.NET Identity's PBKDF2 hash cannot be imported into Better Auth,
///         and a successful sign-in is the only moment the plaintext password exists. Everything else about a user
///         - id, email, display name, workspaces, recipes - is copied by the bulk migration. The password is the
///         sole exception.
///     </para>
///     <para>
///         <b>Fail-open, always.</b> Every failure is caught, logged at warning, and dropped. The Node app being
///         down, slow, misconfigured or entirely absent must be indistinguishable from it being healthy, as far as
///         anyone signing in to THIS app is concerned. There is no retry and no queue: users sign in often, so the
///         next successful sign-in is the retry.
///     </para>
///     <para>
///         <b>Never logs the password, the email, or the shared secret.</b> Log lines carry the user id and the
///         failure reason only.
///     </para>
/// </summary>
public class UserSyncClient(
    HttpClient httpClient,
    IOptions<UserSyncOptions> options,
    ILogger<UserSyncClient> logger
) : IUserSyncClient
{
    /// <summary>The header the Node app expects the shared secret in.</summary>
    private const string SecretHeader = "x-user-sync-secret";

    private const string SyncPath = "/api/internal/users/sync";

    private readonly UserSyncOptions _options = options.Value;

    public async Task SyncAsync(
        Guid userId,
        string email,
        string displayName,
        string password,
        CancellationToken ct = default
    ) {
        if (!_options.IsUsable()) return;

        try {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.BaseUrl.TrimEnd('/')}{SyncPath}"
            );

            request.Headers.Add(SecretHeader, _options.Secret);
            request.Content = JsonContent.Create(
                new { id = userId, email, displayName, password }
            );

            using var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning(
                    "User sync to the Node app returned {StatusCode} for user {UserId}",
                    (int)response.StatusCode,
                    userId
                );
        }
        // Catching Exception is correct here and is the whole point of this class: no failure of the
        // Node app may ever surface in this app's sign-in path. A narrower catch would let some
        // unanticipated exception type escape into a login request and turn a working sign-in into a
        // 500 because a service nobody is using yet was unreachable.
        catch (Exception ex) {
            logger.LogWarning(ex, "User sync to the Node app failed for user {UserId}", userId);
        }
    }
}
