namespace Api.Configuration;

/// <summary>
///     Stores the enabled application roles for runtime services.
/// </summary>
public class AppRolesOptions
{
    public HashSet<string> Roles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasRole(string role) {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return Roles.Contains(role);
    }
}
