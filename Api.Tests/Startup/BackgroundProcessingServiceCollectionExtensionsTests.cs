using Api.Services.MealPrep;
using Api.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.Startup;

public class BackgroundProcessingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBackgroundProcessing_RegistersTheCollectionImportWorkerAndQueue() {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddBackgroundProcessing(BuildConfiguration());

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService)
                          && descriptor.ImplementationType == typeof(RecipeCollectionImportJobWorker)
        );
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RecipeCollectionImportJobQueue));
    }

    [Fact]
    public void AddBackgroundProcessing_ProcessesInBackgroundByDefault() {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddBackgroundProcessing(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RecipeCollectionImportJobOptions>>();

        Assert.True(options.Value.ProcessInBackground);
    }

    [Fact]
    public void AddBackgroundProcessing_HonoursConfiguredProcessingSwitch() {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddBackgroundProcessing(BuildConfiguration(("RecipeCollectionImport:ProcessInBackground", "false")));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RecipeCollectionImportJobOptions>>();

        Assert.False(options.Value.ProcessInBackground);
    }

    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] values) {
        var settings = new Dictionary<string, string?>();

        foreach (var (key, value) in values) settings[key] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
