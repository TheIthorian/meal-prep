namespace Api.Authentication;

/// <summary>
///     Claim types issued when authenticating with an MCP personal access token (workspace-scoped).
/// </summary>
public static class McpPatClaims
{
    /// <summary>
    ///     The workspace this MCP token is limited to (<see cref="System.Guid" /> string).
    /// </summary>
    public const string WorkspaceId = "mcp_workspace_id";
}
