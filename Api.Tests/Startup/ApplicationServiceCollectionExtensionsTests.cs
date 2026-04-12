using Api.Configuration;
using Api.Services;
using Api.Startup;
using Hangfire;
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
    public void AddApplicationServices_DoesNotRegisterCategorisationWorkerServices_WhenRoleNotPresent() {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(("AppRoles", "api"));

        services.AddLogging();

        services.AddApplicationServices(configuration);

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService)
                          && descriptor.ImplementationType?.Namespace?.StartsWith("Hangfire") == true
        );
    }

    [Fact]
    public void GetHangfireQueues_ReturnsCleanupQueue_WhenCleanupRolePresent() {
        var configuration = BuildConfiguration(("AppRoles", AppRoles.WorkerCleanup));
        var queues = ApplicationServiceCollectionExtensions.GetHangfireQueues(configuration);

        Assert.Equal([BackgroundJobQueues.Cleanup], queues);
    }

    [Fact]
    public void GetHangfireQueues_ReturnsCronQueue_WhenCronRolePresent() {
        var configuration = BuildConfiguration(("AppRoles", AppRoles.WorkerCron));
        var queues = ApplicationServiceCollectionExtensions.GetHangfireQueues(configuration);

        Assert.Equal([BackgroundJobQueues.Cron], queues);
    }

    [Fact]
    public void AddApplicationServices_RegistersConfiguredRetentionDays() {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(("AppRoles", "api"), ("RetentionCleanup:RetentionDays", "14"));

        services.AddLogging();

        services.AddApplicationServices(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RetentionCleanupOptions>>();

        Assert.Equal(14, options.Value.RetentionDays);
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
    
    [Fact]
    public void CleanupBackgroundJob_UsesCleanupQueue() {
        var method = typeof(CleanupBackgroundJobs).GetMethod(nameof(CleanupBackgroundJobs.RunNightlyCleanup));

        var queueAttribute = Assert.Single(method!.GetCustomAttributes(typeof(QueueAttribute), inherit: false));

        Assert.Equal(BackgroundJobQueues.Cleanup, ((QueueAttribute)queueAttribute).Queue);
    }

    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] values) {
        var settings = new Dictionary<string, string?> { ["ConnectionStrings:Redis"] = "localhost:6379" };

        foreach (var (key, value) in values)
            settings[key] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
