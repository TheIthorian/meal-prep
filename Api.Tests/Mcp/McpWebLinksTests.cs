using System.Text.Json;
using Api.Mcp;
using Api.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.Mcp;

/// <summary>
///     MCP callers only ever see ids, so recipe payloads have to carry a usable web link.
/// </summary>
public sealed class McpWebLinksTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RecipeId = Guid.Parse("62183226-211a-454b-b437-8ebd46376d45");

    /// <summary>
    ///     Builds the links exactly as startup does, so configuration binding and validation are covered too.
    /// </summary>
    private static McpWebLinks BuildLinks(params (string Key, string Value)[] settings) {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();

        var services = new ServiceCollection();
        services.AddMealPrepMcpServer(configuration);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<McpWebLinks>();
    }

    [Fact]
    public void RecipeUrl_UsesConfiguredBaseUrl() {
        var links = BuildLinks(("WebApp:BaseUrl", "https://meal-prep-ui.pages.dev/"));

        Assert.Equal(
            $"https://meal-prep-ui.pages.dev/workspaces/{WorkspaceId}/recipe/{RecipeId}",
            links.RecipeUrl(WorkspaceId, RecipeId)
        );
    }

    [Fact]
    public void RecipeUrl_AcceptsTheFlatEnvironmentVariable() {
        var links = BuildLinks(("WEB_APP_BASE_URL", "https://flat.example"));

        Assert.StartsWith("https://flat.example/workspaces/", links.RecipeUrl(WorkspaceId, RecipeId));
    }

    [Fact]
    public void BuildingLinks_ThrowsWhenNothingIsConfigured() {
        var exception = Assert.Throws<OptionsValidationException>(() => BuildLinks());

        Assert.Contains("WebApp:BaseUrl is required", string.Join(" ", exception.Failures));
    }

    [Fact]
    public void BuildingLinks_ThrowsWhenTheBaseUrlIsNotAnAbsoluteHttpUrl() {
        var exception = Assert.Throws<OptionsValidationException>(() => BuildLinks(("WebApp:BaseUrl", "mealprep.example")));

        Assert.Contains("WebApp:BaseUrl is required", string.Join(" ", exception.Failures));
    }

    [Fact]
    public void WithRecipeLinks_AddsWebUrlToASingleRecipe() {
        var links = BuildLinks(("WEB_APP_BASE_URL", "https://app.example"));
        var json = $$"""{"id":"{{RecipeId}}","title":"Soup"}""";

        var result = JsonDocument.Parse(links.WithRecipeLinks(json, WorkspaceId)).RootElement;

        Assert.Equal($"https://app.example/workspaces/{WorkspaceId}/recipe/{RecipeId}", result.GetProperty("webUrl").GetString());
        Assert.Equal("Soup", result.GetProperty("title").GetString());
    }

    [Fact]
    public void WithRecipeLinks_AddsWebUrlToEveryPagedEntry() {
        var links = BuildLinks(("WEB_APP_BASE_URL", "https://app.example"));
        var other = Guid.NewGuid();
        var json = $$"""{"data":[{"id":"{{RecipeId}}"},{"id":"{{other}}"}],"page":1}""";

        var result = JsonDocument.Parse(links.WithRecipeLinks(json, WorkspaceId)).RootElement;

        var entries = result.GetProperty("data").EnumerateArray().ToArray();
        Assert.Equal($"https://app.example/workspaces/{WorkspaceId}/recipe/{RecipeId}", entries[0].GetProperty("webUrl").GetString());
        Assert.Equal($"https://app.example/workspaces/{WorkspaceId}/recipe/{other}", entries[1].GetProperty("webUrl").GetString());
        Assert.Equal(1, result.GetProperty("page").GetInt32());
    }

    [Fact]
    public void WithRecipeLinks_LeavesPayloadsWithoutAnIdAlone() {
        var links = BuildLinks(("WEB_APP_BASE_URL", "https://app.example"));
        var json = """{"title":"Request validation failed","status":400}""";

        var result = JsonDocument.Parse(links.WithRecipeLinks(json, WorkspaceId)).RootElement;

        Assert.False(result.TryGetProperty("webUrl", out _));
        Assert.Equal("Request validation failed", result.GetProperty("title").GetString());
    }

    [Fact]
    public void WithRecipeLinks_LeavesUnparsableJsonAlone() {
        var links = BuildLinks(("WEB_APP_BASE_URL", "https://app.example"));

        Assert.Equal("not json", links.WithRecipeLinks("not json", WorkspaceId));
    }
}
