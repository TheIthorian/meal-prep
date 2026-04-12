namespace Api.Endpoints.Responses;

public record McpAccessTokenCreatedResponse(Guid Id, Guid WorkspaceId, string Token, string McpUrl);

public record McpAccessTokenListItemResponse(
    Guid Id,
    Guid WorkspaceId,
    string? Name,
    DateTime CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt
);
