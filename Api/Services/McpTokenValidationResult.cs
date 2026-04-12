using Api.Models;

namespace Api.Services;

/// <summary>
///     Successful validation of an MCP personal access token (user + scoped workspace).
/// </summary>
public sealed record McpTokenValidationResult(AppUser User, Guid WorkspaceId);
