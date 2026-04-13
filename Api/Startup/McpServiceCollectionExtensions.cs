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
                .AddMcpServer(ConfigureMealPrepMcpServerOptions)
                .WithHttpTransport(options => { options.Stateless = true; })
                .WithTools(MealPrepMcpToolsRegistration.CreateTools());
        }
    }

    private static void ConfigureMealPrepMcpServerOptions(McpServerOptions options) {
        options.ServerInstructions =
            "Meal Prep workspace assistant. Use these tools to manage recipes, meal plans, and shopping lists. "
            + "This server is scoped to one workspace by the MCP URL token, so do not pass workspaceId to tools. "
            + "For create/update tools, send JSON strings that match each tool's described request schema.";
    }
}

/// <summary>
///     Authorization policy names for MCP.
/// </summary>
public static class McpAuthorizationPolicies
{
    public const string McpPat = nameof(McpPat);
}
