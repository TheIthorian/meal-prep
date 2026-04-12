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
            services
                .AddMcpServer()
                .WithHttpTransport(options => { options.Stateless = true; })
                .WithTools<MealPrepMcpTools>();
        }
    }
}

/// <summary>
///     Authorization policy names for MCP.
/// </summary>
public static class McpAuthorizationPolicies
{
    public const string McpPat = nameof(McpPat);
}
