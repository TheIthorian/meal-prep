namespace Api.Configuration;

/// <summary>
///     Configuration for pushing users this API has just authenticated into the Node app's Better Auth,
///     during the migration's dual-run window.
///     <para>
///         Disabled by default, deliberately. A deployment that has not configured the sync should do
///         nothing at all, rather than log a warning on every single login.
///     </para>
/// </summary>
public class UserSyncOptions
{
    public const string SectionName = "UserSync";

    /// <summary>
    ///     Whether to push users to the Node app at all. Off unless explicitly turned on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Origin of the Node API, without a trailing slash, e.g. https://meal-prep-node.example.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    ///     Shared secret sent in the x-user-sync-secret header. A dedicated secret, not the JWT key:
    ///     a leak of this must not also forge tokens for this API.
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    ///     How long to wait for the Node app before giving up. Short on purpose - this call sits inside a
    ///     user's login request, and the sync failing is always preferable to the login being slow.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    ///     True when the sync is switched on AND actually configured. Checked before every call, so that
    ///     turning Enabled on without a base URL or secret is inert rather than an exception per login.
    /// </summary>
    public bool IsUsable() {
        return Enabled && !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Secret);
    }
}
