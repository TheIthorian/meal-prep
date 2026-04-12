namespace Api.Configuration;

public static class AppRoles
{
}

/// <summary>
///     Registers the application role definitions.
/// </summary>
public static class AppRoleConfiguration
{
    extension(IConfiguration configuration)
    {
        public bool HasAppRole(string role) {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentException.ThrowIfNullOrWhiteSpace(role);

            return configuration.GetAppRoles().Contains(role);
        }

        public void ValidateAppRolesConfiguration() {
            ArgumentNullException.ThrowIfNull(configuration);

            var rawValue = configuration["AppRoles"] ?? configuration["APP_ROLES"];
            if (string.IsNullOrWhiteSpace(rawValue))
                throw new InvalidOperationException(
                    "AppRoles is required. Set 'AppRoles' or environment variable 'APP_ROLES'."
                );
        }

        public HashSet<string> GetAppRoles() {
            configuration.ValidateAppRolesConfiguration();

            var rawValue = configuration["AppRoles"] ?? configuration["APP_ROLES"];

            return rawValue?
                       .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                       .ToHashSet(StringComparer.OrdinalIgnoreCase)
                   ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
