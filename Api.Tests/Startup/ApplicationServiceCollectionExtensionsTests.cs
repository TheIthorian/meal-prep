using Api.Configuration;
using Api.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.Startup;

public class ApplicationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddApplicationServices_DoesNotRegisterHostedServices_WhenRoleNotPresent() {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(("AppRoles", "api"));

        services.AddLogging();

        services.AddApplicationServices(configuration);

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService)
        );
    }

    [Fact]
    public void AddApplicationServices_ConfiguresLoginRateLimiter() {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(("AppRoles", "api"));

        services.AddLogging();
        services.AddApplicationServices(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>();

        Assert.Equal(StatusCodes.Status429TooManyRequests, options.Value.RejectionStatusCode);
    }

    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] values) {
        var settings = new Dictionary<string, string?>();

        foreach (var (key, value) in values)
            settings[key] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
