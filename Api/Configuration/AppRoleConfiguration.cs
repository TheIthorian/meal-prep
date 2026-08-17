namespace Api.Configuration;

/// <summary>
///     Names of the roles an instance can be configured to run.
/// </summary>
public static class AppRoles
{
    /// <summary>Every role, for a single-instance deployment or local development.</summary>
    public const string All = "*";

    /// <summary>Generates recipe image renditions from the durable queue.</summary>
    public const string ImageDerivativeWorker = "worker:image-derivatives";
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

            var roles = configuration.GetAppRoles();

            // "*" is how a deployment says "run everything on this instance", which is what local
            // development and the single-container deployment both use.
            return roles.Contains(AppRoles.All) || roles.Contains(role);
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
