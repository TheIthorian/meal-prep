namespace Api.Services.UserSync;

/// <summary>
///     Pushes a user this API has just authenticated into the Node app's Better Auth.
/// </summary>
public interface IUserSyncClient
{
    /// <summary>
    ///     Sends the user and the plaintext password that was just successfully validated.
    ///     <para>
    ///         Never throws. Every failure - unreachable host, timeout, non-success status, serialisation -
    ///         is logged at warning and swallowed. See <see cref="UserSyncClient" /> for why.
    ///     </para>
    /// </summary>
    Task SyncAsync(Guid userId, string email, string displayName, string password, CancellationToken ct = default);
}
