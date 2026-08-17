using Api.Configuration;
using Api.Links;
using Api.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.Links;

/// <summary>
///     Clients only ever see ids, so recipe payloads have to carry a link to the page a user can open.
/// </summary>
public sealed class WebAppLinkGeneratorTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RecipeId = Guid.Parse("62183226-211a-454b-b437-8ebd46376d45");

    /// <summary>
    ///     Builds the generator the way startup does, so configuration binding and validation are covered too.
    /// </summary>
    private static WebAppLinkGenerator BuildGenerator(params (string Key, string Value)[] settings) {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value))
            )
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<WebAppOptions>()
            .Bind(configuration.GetSection(WebAppOptions.SectionName))
            .PostConfigure(options => options.Normalize(configuration["WEB_APP_BASE_URL"]))
            .Validate(options => options.IsValid(), WebAppOptions.RequiredMessage);
        services.AddSingleton<WebAppLinkGenerator>();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<WebAppLinkGenerator>();
    }

    [Fact]
    public void Recipe_UsesTheConfiguredBaseUrl() {
        var links = BuildGenerator(("WebApp:BaseUrl", "https://meal-prep-ui.pages.dev/"));

        Assert.Equal(
            $"https://meal-prep-ui.pages.dev/workspaces/{WorkspaceId}/recipe/{RecipeId}",
            links.Recipe(WorkspaceId, RecipeId)
        );
    }

    [Fact]
    public void Recipe_AcceptsTheFlatEnvironmentVariable() {
        var links = BuildGenerator(("WEB_APP_BASE_URL", "https://flat.example"));

        Assert.StartsWith("https://flat.example/workspaces/", links.Recipe(WorkspaceId, RecipeId));
    }

    [Fact]
    public void RecipeLibrary_PointsAtTheWorkspace() {
        var links = BuildGenerator(("WebApp:BaseUrl", "https://app.example"));

        Assert.Equal($"https://app.example/workspaces/{WorkspaceId}/", links.RecipeLibrary(WorkspaceId));
    }

    [Fact]
    public void BuildingLinks_ThrowsWhenNothingIsConfigured() {
        var exception = Assert.Throws<OptionsValidationException>(() => BuildGenerator());

        Assert.Contains("WebApp:BaseUrl is required", string.Join(" ", exception.Failures));
    }

    [Fact]
    public void BuildingLinks_ThrowsWhenTheBaseUrlIsNotAnAbsoluteHttpUrl() {
        var exception = Assert.Throws<OptionsValidationException>(
            () => BuildGenerator(("WebApp:BaseUrl", "mealprep.example"))
        );

        Assert.Contains("WebApp:BaseUrl is required", string.Join(" ", exception.Failures));
    }
}
