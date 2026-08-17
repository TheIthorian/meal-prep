using Api.Links;
using Api.Mcp;
using ModelContextProtocol.Server;

namespace Api.Startup;

/// <summary>
///     Registers the Model Context Protocol (MCP) HTTP server and meal-prep tools.
/// </summary>
public static class McpServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddMealPrepMcpServer() {
            services.AddTransient<MealPrepMcpTools>();
            services
                .AddMcpServer()
                .WithHttpTransport(options => { options.Stateless = true; })
                .WithTools(MealPrepMcpToolsRegistration.CreateTools());
            services.AddOptions<McpServerOptions>()
                .Configure<WebAppLinkGenerator>(ConfigureMealPrepMcpServerOptions);
        }
    }

    private static void ConfigureMealPrepMcpServerOptions(McpServerOptions options, WebAppLinkGenerator webApp) {
        options.ServerInstructions =
            "Meal Prep workspace assistant. Use these tools to manage recipes, meal plans, and shopping lists. "
            + "This server is scoped to one workspace by the MCP URL token, so do not pass workspaceId to tools. "
            + "For create/update tools, send JSON strings that match each tool's described request schema. "
            + "Recipe responses carry a webUrl for the recipe's page in the web app; link to that rather than "
            + $"quoting a bare id. The pattern is {webApp.BaseUrl}/workspaces/{{workspaceId}}/recipe/{{recipeId}}, "
            + "and list_workspaces returns the workspaceId this token is scoped to.";
    }
}

/// <summary>
///     Authorization policy names for MCP.
/// </summary>
public static class McpAuthorizationPolicies
{
    public const string McpPat = nameof(McpPat);
}
