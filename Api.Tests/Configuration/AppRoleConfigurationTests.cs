using Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Configuration;

public class AppRoleConfigurationTests
{
    [Fact]
    public void GetAppRoles_ReadsPluralEnvironmentVariable() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> {
                    [
                            "APP_ROLES"
                        ] =
                        $"{AppRoles.WorkerCleanup},{AppRoles.WorkerCron}"
                }
            )
            .Build();

        var roles = configuration.GetAppRoles();

        Assert.Contains(AppRoles.WorkerCleanup, roles);
        Assert.Contains(AppRoles.WorkerCron, roles);
    }

    [Fact]
    public void GetAppRoles_ThrowsWhenMissing() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.GetAppRoles());

        Assert.Contains("AppRoles is required", exception.Message);
    }
}
