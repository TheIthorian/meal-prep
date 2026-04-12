using Microsoft.AspNetCore.Builder;

namespace Api.Startup;

/// <summary>
///     Maps MCP Streamable HTTP endpoints protected by personal-access-token URL authentication.
/// </summary>
public static class McpEndpointRouteBuilderExtensions
{
    extension(WebApplication app)
    {
        public void MapMealPrepMcpEndpoints() {
            app.MapMcp("/mcp/{mcpPat}").RequireAuthorization(McpAuthorizationPolicies.McpPat);
        }
    }
}
